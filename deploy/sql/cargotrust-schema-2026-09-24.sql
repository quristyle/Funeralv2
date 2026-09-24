-- CargoTrust(화물 거래처 신뢰정보) 스키마 — DB cargotrust / 스키마 cargotrust
--
-- 설계 원본: docs/cargotrust/01-service-overview.md (상위 설계안)
--
-- [이 파일이 스키마의 정본이다]
--
-- CargoTrustServer 는 EF Core 로 읽고 쓰지만 **스키마를 만들지 않는다**
-- (생활과환경 · ghub 와 같은 방식). 마이그레이션 파일이 유실돼 빈 DB 를 못
-- 세우는 일을 다른 서비스에서 이미 겪었다 — 여기서는 이 파일 하나로 빈 DB 에서
-- 끝까지 선다.
--
-- [안전한 성질]
--
--   · 멱등하다. `IF NOT EXISTS` 만 쓰므로 여러 번 돌려도 자료가 안 없어진다.
--   · 열을 바꿀 때는 이 파일 **아래에 ALTER 를 덧붙인다.** 위의 CREATE 를
--     고쳐도 이미 선 DB 에는 반영되지 않는다.
--
-- 앞선 일: deploy/sql/cargotrust-database-2026-09-24.sql (역할 · DB · 스키마)
-- 실행: cargotrust 계정으로 cargotrust DB 에 붙어 돌린다.

SET search_path TO cargotrust;

-- ── 거래처 ─────────────────────────────────────────────────────
--
-- 사업자등록번호가 **식별자**다. 회사명은 겹치고 바뀐다(설계안 33-3).
-- 번호는 숫자만 10자리로 넣는다 — 하이픈을 섞어 넣으면 같은 회사가 둘이 된다.

CREATE TABLE IF NOT EXISTS company (
    company_id          BIGSERIAL PRIMARY KEY,
    business_number     VARCHAR(20)  NOT NULL,
    company_name        VARCHAR(200) NOT NULL,
    ceo_name            VARCHAR(100),
    address             VARCHAR(500),
    region              VARCHAR(50),
    phone               VARCHAR(50),
    business_type       VARCHAR(100),
    -- ACTIVE 정상 · CLOSED 폐업 · HIDDEN 관리자 숨김
    status              VARCHAR(30)  NOT NULL DEFAULT 'ACTIVE',
    admin_memo          TEXT,
    created_by          BIGINT,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_company_business_number UNIQUE (business_number)
);

-- ── 사용자 ─────────────────────────────────────────────────────
--
-- 회원 체계를 따로 두지 않는다(설계안 20). external_user_id 가 포털 계정
-- (게이트웨이의 X-User-Id)이고, 처음 부를 때 서버가 줄을 만든다.

CREATE TABLE IF NOT EXISTS app_user (
    user_id             BIGSERIAL PRIMARY KEY,
    external_user_id    VARCHAR(100) NOT NULL,
    display_name        VARCHAR(100),
    -- DRIVER 차주 · CARRIER 운송사/주선사 · ADMIN 관리자
    user_type           VARCHAR(30)  NOT NULL DEFAULT 'DRIVER',
    -- 운송사 사용자가 대표하는 거래처. 이의제기는 이 회사의 거래에만 건다.
    company_id          BIGINT REFERENCES company(company_id),
    -- ACTIVE · BLOCKED (차단되면 등록·신고를 못 한다)
    status              VARCHAR(30)  NOT NULL DEFAULT 'ACTIVE',
    admin_memo          TEXT,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    last_seen_at        TIMESTAMPTZ,

    CONSTRAINT uq_app_user_external UNIQUE (external_user_id)
);

-- ── 거래 ───────────────────────────────────────────────────────
--
-- 운송과 결제는 별개의 사건이다(설계안 9). 거래 줄에는 **지금의 결제 상태**가
-- 있고, 결제를 등록할 때마다 payment_record 에 한 줄씩 쌓인다.
--
-- payment_status: SCHEDULED 예정 · PAID 정상 · DELAYED 지연 · PARTIAL 일부 ·
--                 UNPAID 미지급 · DISPUTE 분쟁
-- review_status : NORMAL · FLAGGED(비정상 패턴 의심) · VERIFIED(관리자 확인) ·
--                 HIDDEN(통계 제외)

CREATE TABLE IF NOT EXISTS cargo_transaction (
    transaction_id          BIGSERIAL PRIMARY KEY,
    company_id              BIGINT       NOT NULL REFERENCES company(company_id),
    user_id                 BIGINT       NOT NULL REFERENCES app_user(user_id),

    transport_date          DATE         NOT NULL,
    origin                  VARCHAR(300),
    destination             VARCHAR(300),
    transport_type          VARCHAR(50),
    dispatch_channel        VARCHAR(100),

    amount                  NUMERIC(15,2) NOT NULL,
    paid_amount             NUMERIC(15,2) NOT NULL DEFAULT 0,

    expected_payment_date   DATE,
    actual_payment_date     DATE,

    payment_status          VARCHAR(30)  NOT NULL DEFAULT 'SCHEDULED',
    memo                    TEXT,

    review_status           VARCHAR(30)  NOT NULL DEFAULT 'NORMAL',
    flag_reason             VARCHAR(300),
    admin_memo              TEXT,
    is_deleted              BOOLEAN      NOT NULL DEFAULT FALSE,

    created_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),

    CONSTRAINT ck_transaction_amount CHECK (amount > 0),
    CONSTRAINT ck_transaction_paid CHECK (paid_amount >= 0)
);

-- ── 결제 기록 ──────────────────────────────────────────────────
--
-- 결제 등록 한 번이 한 줄이다. 거래의 상태를 되짚을 수 있어야 분쟁 때 쓸모가
-- 있다(설계안 33-5).

CREATE TABLE IF NOT EXISTS payment_record (
    payment_id          BIGSERIAL PRIMARY KEY,
    transaction_id      BIGINT        NOT NULL REFERENCES cargo_transaction(transaction_id),
    user_id             BIGINT        NOT NULL REFERENCES app_user(user_id),
    paid_date           DATE,
    paid_amount         NUMERIC(15,2) NOT NULL DEFAULT 0,
    result_status       VARCHAR(30)   NOT NULL,
    delay_days          INTEGER,
    memo                TEXT,
    created_at          TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

-- ── 거래 후기 ──────────────────────────────────────────────────
--
-- 후기는 **거래에 딸린다**(설계안 11). 거래 하나에 후기 하나. 후기가 없어도
-- 통계는 선다(설계안 33-4).

CREATE TABLE IF NOT EXISTS transaction_review (
    review_id           BIGSERIAL PRIMARY KEY,
    transaction_id      BIGINT       NOT NULL REFERENCES cargo_transaction(transaction_id),
    user_id             BIGINT       NOT NULL REFERENCES app_user(user_id),
    content             TEXT         NOT NULL,
    -- VISIBLE · HIDDEN
    status              VARCHAR(30)  NOT NULL DEFAULT 'VISIBLE',
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_review_transaction UNIQUE (transaction_id)
);

-- ── 이의제기 ───────────────────────────────────────────────────
--
-- reason: NO_TRANSACTION 거래 사실 없음 · ALREADY_PAID 이미 지급 ·
--         WRONG_AMOUNT 금액 오류 · WRONG_COMPANY 다른 업체 · OTHER 기타
-- status: RECEIVED 접수 · REVIEWING 검토중 · ACCEPTED 인용 · REJECTED 기각

CREATE TABLE IF NOT EXISTS transaction_dispute (
    dispute_id          BIGSERIAL PRIMARY KEY,
    transaction_id      BIGINT       NOT NULL REFERENCES cargo_transaction(transaction_id),
    company_id          BIGINT       NOT NULL REFERENCES company(company_id),
    requester_user_id   BIGINT       NOT NULL REFERENCES app_user(user_id),
    reason              VARCHAR(50)  NOT NULL,
    content             TEXT,
    status              VARCHAR(30)  NOT NULL DEFAULT 'RECEIVED',
    resolution          TEXT,
    resolved_by         BIGINT,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    resolved_at         TIMESTAMPTZ
);

-- ── 신고 ───────────────────────────────────────────────────────
--
-- target_type: COMPANY · TRANSACTION · REVIEW
-- reason: FAKE 허위 거래 · DUPLICATE 중복 거래 · WRONG_COMPANY 사업자 오인 ·
--         ABUSE 욕설/비방 · PRIVACY 개인정보 노출 · SPAM 광고/스팸 · OTHER 기타
-- status: RECEIVED 접수 · REVIEWING 검토중 · REJECTED 반려 ·
--         REVISION 수정 요청 · DELETED 삭제 · DONE 처리 완료

CREATE TABLE IF NOT EXISTS report (
    report_id           BIGSERIAL PRIMARY KEY,
    target_type         VARCHAR(30)  NOT NULL,
    target_id           BIGINT       NOT NULL,
    reporter_user_id    BIGINT       NOT NULL REFERENCES app_user(user_id),
    reason              VARCHAR(50)  NOT NULL,
    content             TEXT,
    status              VARCHAR(30)  NOT NULL DEFAULT 'RECEIVED',
    resolution          TEXT,
    resolved_by         BIGINT,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    resolved_at         TIMESTAMPTZ
);

-- ── 최근 본 거래처 ─────────────────────────────────────────────
--
-- 홈의 「최근 검색」. 한 사람이 한 회사를 여러 번 봐도 한 줄이다.

CREATE TABLE IF NOT EXISTS company_view (
    user_id             BIGINT       NOT NULL REFERENCES app_user(user_id),
    company_id          BIGINT       NOT NULL REFERENCES company(company_id),
    viewed_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    PRIMARY KEY (user_id, company_id)
);

-- ── 감사 기록 ──────────────────────────────────────────────────
--
-- 관리자가 바꾼 것은 전부 남긴다(설계안 30). 사용자의 거래 수정도 남긴다 —
-- 분쟁에서 「처음엔 얼마라고 적었나」를 물을 수 있어야 한다.

CREATE TABLE IF NOT EXISTS audit_log (
    audit_id        BIGSERIAL PRIMARY KEY,
    actor_user_id   BIGINT,
    actor_external  VARCHAR(100),
    action          VARCHAR(100) NOT NULL,
    target_type     VARCHAR(50),
    target_id       BIGINT,
    before_data     JSONB,
    after_data      JSONB,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- ── 인덱스 (설계안 28) ─────────────────────────────────────────

CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX IF NOT EXISTS ix_company_name           ON company (company_name);
CREATE INDEX IF NOT EXISTS ix_company_name_trgm      ON company USING gin (company_name gin_trgm_ops);
CREATE INDEX IF NOT EXISTS ix_company_ceo            ON company (ceo_name);
CREATE INDEX IF NOT EXISTS ix_company_phone          ON company (phone);

CREATE INDEX IF NOT EXISTS ix_transaction_company          ON cargo_transaction (company_id);
CREATE INDEX IF NOT EXISTS ix_transaction_user             ON cargo_transaction (user_id);
CREATE INDEX IF NOT EXISTS ix_transaction_payment_status   ON cargo_transaction (payment_status);
CREATE INDEX IF NOT EXISTS ix_transaction_expected_payment ON cargo_transaction (expected_payment_date);
CREATE INDEX IF NOT EXISTS ix_transaction_transport_date   ON cargo_transaction (transport_date);

CREATE INDEX IF NOT EXISTS ix_payment_transaction   ON payment_record (transaction_id);
CREATE INDEX IF NOT EXISTS ix_dispute_transaction   ON transaction_dispute (transaction_id);
CREATE INDEX IF NOT EXISTS ix_dispute_status        ON transaction_dispute (status);
CREATE INDEX IF NOT EXISTS ix_report_status         ON report (status);
CREATE INDEX IF NOT EXISTS ix_report_target         ON report (target_type, target_id);
CREATE INDEX IF NOT EXISTS ix_audit_target          ON audit_log (target_type, target_id);
CREATE INDEX IF NOT EXISTS ix_audit_created         ON audit_log (created_at);
