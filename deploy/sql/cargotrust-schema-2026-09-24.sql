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


-- ============================================================
-- 덧붙임 2026-10-06 — 톨게이트 심야할인
-- ============================================================
--
-- 설계: docs/cargotrust/06-toll-night-discount.md
--
-- 이 묶음이 푸는 일은 둘이다.
--
--   ① 진입·진출 시각을 받아 **심야할인율**을 알려 준다
--   ② 한쪽 시각과 목표 할인율을 받아 **나머지 한쪽 시각**을 추천한다
--
-- 둘은 같은 식을 양쪽으로 푸는 것이다. 식의 알맹이는
-- 「전체 이용시간 중 야간시간대에 머문 **비율**」이고, 할인율은 그 비율로만
-- 정해진다 — 차종은 금액과 대상 자격을 가를 뿐 비율표를 바꾸지 않는다.

-- ── 영업소(톨게이트) ───────────────────────────────────────────
--
-- 할인율 계산 자체에는 영업소가 필요 없다. 그런데 **구간 유형**은 필요하다 —
-- 폐쇄식(진입·진출 영업소가 나뉨)과 개방식은 야간창도 다르고 할인 방식도
-- 다르기 때문이다. 그래서 이 표의 핵심 칸은 좌표가 아니라 `section_type` 이다.
--
-- 바깥(한국도로공사 공개 데이터)에서 받아 와 **여기 보관**한다. 실시간으로
-- 부르지 않는다 — 영업소는 거의 변하지 않고, 바깥이 죽었을 때 우리 화면이
-- 함께 멈출 이유가 없다.

CREATE TABLE IF NOT EXISTS toll_plaza (
    plaza_id        BIGSERIAL PRIMARY KEY,
    -- 도로공사 영업소코드. 바깥 자료와 맞추는 열쇠라 유일하다.
    unit_code       VARCHAR(20)  NOT NULL,
    unit_name       VARCHAR(100) NOT NULL,
    route_no        VARCHAR(20),
    route_name      VARCHAR(100),
    -- CLOSED 폐쇄식 · OPEN 개방식. **할인 규칙이 여기서 갈린다.**
    section_type    VARCHAR(20)  NOT NULL DEFAULT 'CLOSED',
    -- 재정(도로공사) / 민자. 민자는 요금·할인 체계가 따로라 1차 범위 밖이다.
    is_private      BOOLEAN      NOT NULL DEFAULT false,
    lat             NUMERIC(10,7),
    lon             NUMERIC(10,7),
    is_active       BOOLEAN      NOT NULL DEFAULT true,
    -- 어디서 받은 줄인가 — EX_API(공개 API) · MANUAL(손으로 넣음)
    source          VARCHAR(30),
    synced_at       TIMESTAMPTZ,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_toll_plaza_unit ON toll_plaza (unit_code);
CREATE INDEX IF NOT EXISTS ix_toll_plaza_name ON toll_plaza (unit_name);

-- ── 심야할인 규칙 — 코드가 아니라 **자료**다 ───────────────────
--
-- 심야할인은 한시 제도다. 일몰이 연장되고, 비율 구간이 손질되고, 유가 대응으로
-- 한시 면제가 끼어든 적도 있다. 비율표를 코드에 상수로 박으면 제도가 바뀔 때마다
-- 배포해야 하고 — 더 나쁜 것은 — **지난달 계산을 재현할 수 없다.**
--
-- 줄로 두면 관리자가 고치고, 옛 계산은 그때 쓰던 묶음으로 되살아난다.
--
-- 야간창을 묶음에 둔 이유: 창은 구간 유형마다 하나인데 띠는 넷이다.
-- 띠마다 적으면 같은 값을 네 번 적고, 한 줄만 어긋나면 알 길이 없다.

CREATE TABLE IF NOT EXISTS toll_rule_set (
    rule_set_id         BIGSERIAL PRIMARY KEY,
    code                VARCHAR(50)  NOT NULL,
    name                VARCHAR(200) NOT NULL,
    -- 야간창은 **KST 벽시계**다. 저장·계산은 UTC 지만 창은 사람의 밤이다.
    closed_night_start  TIME         NOT NULL DEFAULT '21:00',
    closed_night_end    TIME         NOT NULL DEFAULT '06:00',
    open_night_start    TIME         NOT NULL DEFAULT '23:00',
    open_night_end      TIME         NOT NULL DEFAULT '05:00',
    effective_from      DATE         NOT NULL,
    effective_to        DATE,
    -- 이 값을 어디서 가져왔는지. 「확인한 적 없는 숫자」가 되지 않게 적어 둔다.
    source_note         TEXT,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_toll_rule_set_code ON toll_rule_set (code);

-- 비율의 띠. `min_ratio` **이상**이면 이 띠다 — 띠의 위 끝은 다음 줄이 정한다.
--
-- 위 끝을 따로 적지 않는 이유: 두 칸으로 두면 경계에서 틈이나 겹침이 생긴다
-- (「80 이상」과 「80 미만」을 둘 다 적다가 80 이 어느 쪽인지 갈리는 일).
-- 한 칸이면 틈이 생길 수 없다.

CREATE TABLE IF NOT EXISTS toll_discount_band (
    band_id          BIGSERIAL PRIMARY KEY,
    rule_set_id      BIGINT       NOT NULL REFERENCES toll_rule_set(rule_set_id) ON DELETE CASCADE,
    section_type     VARCHAR(20)  NOT NULL,
    min_ratio        NUMERIC(5,2) NOT NULL,
    discount_percent NUMERIC(5,2) NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_toll_band
    ON toll_discount_band (rule_set_id, section_type, min_ratio);

-- ── 차량 ───────────────────────────────────────────────────────
--
-- **차량번호만으로 차종을 알아 오는 공개 API 는 없다.** 국토교통부
-- 자동차종합정보 API 도 차량번호·소유자명·소유자 동의가 전제다. 그래서
-- 차종과 축수는 사람이 적는다 — 기사는 자기 차의 축수를 안다.
--
-- 차량은 **계정에 딸린다.** 계정 자체(scom.accounts)는 전사 공용이라
-- 거기에 화물차 칸을 붙이면 운송관리를 안 쓰는 계정에도 차량 개념이 생긴다.
-- 여기 두고 `app_user` 를 거쳐 계정을 가리킨다 — 거래·신고가 쓰는 길 그대로다.
--
-- 차량번호는 개인정보에 준해 다룬다. **다른 사용자에게 내보내지 않는다.**

CREATE TABLE IF NOT EXISTS vehicle (
    vehicle_id      BIGSERIAL PRIMARY KEY,
    user_id         BIGINT       NOT NULL REFERENCES app_user(user_id),
    plate_no        VARCHAR(20)  NOT NULL,
    nickname        VARCHAR(50),
    -- 고속도로 통행료 차종 — LIGHT 경차 · C1~C5 (1종~5종)
    vehicle_class   VARCHAR(10)  NOT NULL DEFAULT 'C4',
    axle_count      SMALLINT,
    tonnage         NUMERIC(6,2),
    -- 심야할인은 **사업용** 화물차 제도다. 자가용은 대상이 아니다.
    is_business     BOOLEAN      NOT NULL DEFAULT true,
    -- 3축 미만은 서약서와 하이패스 단말이 조건이다.
    has_hipass      BOOLEAN      NOT NULL DEFAULT true,
    is_default      BOOLEAN      NOT NULL DEFAULT false,
    memo            TEXT,
    is_deleted      BOOLEAN      NOT NULL DEFAULT false,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- 같은 사람이 같은 번호판을 두 줄 가질 수 없다. 지운 줄은 센다 치지 않는다.
CREATE UNIQUE INDEX IF NOT EXISTS uq_vehicle_plate
    ON vehicle (user_id, plate_no) WHERE is_deleted = false;
CREATE INDEX IF NOT EXISTS ix_vehicle_user ON vehicle (user_id);

-- ── 계산 이력 ──────────────────────────────────────────────────
--
-- 「지난번 그 계산」을 다시 보려고, 그리고 **제도가 바뀐 뒤에도 그때 값을
-- 설명할 수 있게** 쓴 규칙 묶음을 함께 남긴다.

CREATE TABLE IF NOT EXISTS toll_calc_log (
    calc_id          BIGSERIAL PRIMARY KEY,
    user_id          BIGINT       NOT NULL REFERENCES app_user(user_id),
    vehicle_id       BIGINT       REFERENCES vehicle(vehicle_id),
    -- CALC 정방향(시각 둘 → 할인율) · SUGGEST 역방향(한쪽 시각 + 목표 → 나머지 시각)
    mode             VARCHAR(20)  NOT NULL,
    section_type     VARCHAR(20)  NOT NULL,
    entry_at         TIMESTAMPTZ,
    exit_at          TIMESTAMPTZ,
    entry_plaza_id   BIGINT       REFERENCES toll_plaza(plaza_id),
    exit_plaza_id    BIGINT       REFERENCES toll_plaza(plaza_id),
    total_minutes    INT,
    night_minutes    INT,
    night_ratio      NUMERIC(5,2),
    discount_percent NUMERIC(5,2),
    target_discount  NUMERIC(5,2),
    rule_set_code    VARCHAR(50),
    created_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_toll_calc_user ON toll_calc_log (user_id, created_at DESC);

-- ── 규칙 첫 줄 ─────────────────────────────────────────────────
--
-- 출처: 국토교통부 정책브리핑 「화물차 고속도로 통행료 심야할인」
--       https://www.korea.kr/briefing/policyBriefingView.do?newsId=148738657
--
--   폐쇄식 — 야간창 21:00~06:00, 야간 이용비율로 네 띠
--            80% 이상 50% · 50% 이상 30% · 20% 이상 20% · 그 아래 0%
--   개방식 — 야간창 23:00~05:00, 그 안을 통과하면 일괄 50%
--
-- 개방식은 진·출입 요금소가 나뉘지 않아 **통과 시각 한 점**으로 본다.
-- 그래서 비율이 0 아니면 100 이고, 띠도 둘이면 족하다.
--
-- **운영에 올리기 전에 한국도로공사 고시 정본으로 한 번 더 맞춰 본다.**
-- 2차 출처들이 서로 다른 구간값을 말한다 — 여기 적힌 것은 정부 브리핑 기준이다.

INSERT INTO toll_rule_set (code, name, effective_from, source_note)
VALUES ('KEC-BASE', '화물차 심야할인 (한국도로공사)', DATE '2000-01-10',
        '국토교통부 정책브리핑 148738657. 운영 반영 전 도로공사 고시로 재확인 필요.')
ON CONFLICT (code) DO NOTHING;

INSERT INTO toll_discount_band (rule_set_id, section_type, min_ratio, discount_percent)
SELECT rs.rule_set_id, b.section_type, b.min_ratio, b.discount_percent
  FROM toll_rule_set rs
 CROSS JOIN (VALUES
        ('CLOSED',   0.00,  0.00),
        ('CLOSED',  20.00, 20.00),
        ('CLOSED',  50.00, 30.00),
        ('CLOSED',  80.00, 50.00),
        ('OPEN',     0.00,  0.00),
        ('OPEN',   100.00, 50.00)
 ) AS b(section_type, min_ratio, discount_percent)
 WHERE rs.code = 'KEC-BASE'
ON CONFLICT (rule_set_id, section_type, min_ratio) DO NOTHING;


-- ── 덧붙임 2026-10-06 (2) — 번호판이 말한 것 ───────────────────
--
-- 차량번호로 차종·축수를 주는 **무료 공개 API 가 없다.** 국토교통부
-- 자동차종합정보 첨부형API 는 소유자의 휴대폰 본인인증을 거친 제3자 제공 동의와
-- 기관 승인·행정서류·유료 본인인증 서비스 가입이 전제이고, 민간은 전부 유료다.
--
-- 그런데 심야할인 판정에 필요한 셋 중 둘이 **번호판에 이미 적혀 있다** —
-- 앞자리 숫자가 화물차인지를, 한글 한 자가 사업용인지를 말한다. 그래서 바깥을
-- 부르는 대신 번호판을 읽고, 읽은 것을 여기 남긴다.
--
-- [왜 따로 남기는가 — 사람이 고른 값과 견주려고]
--
-- 읽은 것은 제안이지 판정이 아니라 사람이 덮어쓸 수 있다. 번호판이 말한 것과
-- 사람이 고른 것을 나란히 두면 **덮어썼는지가 견주기만 해도 드러난다** —
-- 「덮어썼음」 표시 칸을 따로 둘 필요가 없고, 그 칸이 실제와 어긋날 일도 없다.

ALTER TABLE vehicle ADD COLUMN IF NOT EXISTS plate_region VARCHAR(20);
-- UNKNOWN · PASSENGER 승용 · VAN 승합 · FREIGHT 화물 · SPECIAL 특수
ALTER TABLE vehicle ADD COLUMN IF NOT EXISTS plate_kind   VARCHAR(20) NOT NULL DEFAULT 'UNKNOWN';
-- UNKNOWN · PRIVATE 자가용 · BUSINESS 사업용 · DELIVERY 택배 · RENTAL 렌터카
ALTER TABLE vehicle ADD COLUMN IF NOT EXISTS plate_usage  VARCHAR(20) NOT NULL DEFAULT 'UNKNOWN';

-- 축수는 **선택이다.** 모르는 것을 모른다고 두는 편이, 등록 자체를 막아
-- 할인율 계산까지 못 하게 하는 것보다 낫다. (axle_count 는 처음부터 NULL 허용)
