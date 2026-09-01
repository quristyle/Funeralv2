-- ============================================================
-- 옛 장례식장 시스템에서 이식한 화면들이 쓰는 표.
--
-- 대상 DB : funeralv2 (smfr)
-- 실행    : psql -h <host> -p <port> -U funeralv2 -d funeralv2 -f docs/sql/funeralv2_old_migration.sql
--
-- 여러 번 실행해도 안전하다 (IF NOT EXISTS).
--
-- ── 왜 ───────────────────────────────────────────────────────
-- 옛 시스템(funeralfr.jsini.co.kr:15432 / funeral / smfr)의 표 30개 중
-- 대부분은 현 funeralv2 에 대응하는 표가 이미 있다. 없는 것이 셋이라 여기서 만든다.
-- 무엇이 무엇에 대응하는지는 docs/analysis/40-old-funeral-migration.md 에 있다.
--
--   t_notification   → smfr.funeral_notices (+ funeral_notice_reads)
--   t_account_conf   → smfr.account_settings
--   t_music_build    → smfr.building_music
--
-- 옛 데이터(고인 10,261건 등)는 옮기지 않았다. 이식 대상은 화면이다 (D-F2).
--
-- ── 칸 이름 규칙 ─────────────────────────────────────────────
-- funeralv2Api 의 AppDbContext 는 칸 이름을 소문자로만 바꾸고 snake_case 로
-- 바꾸지는 않는다. 그래서 엔티티가 [Column("...")] 로 직접 적어야 snake_case 가 된다.
-- 아래 표는 그렇게 적힌 엔티티에 맞춘 것이다 (deceased_rooms 와 같은 방식).
-- ============================================================

BEGIN;

-- ── 알림 정보 ───────────────────────────────────────────────
-- 옛 t_notification 은 받는 사람과 읽음 여부를 한 행에 담아서, 같은 알림을
-- 여럿에게 보내려면 본문이 복제됐다. 여기서는 target_user_id 가 비면 전체 공지고
-- 읽음은 funeral_notice_reads 로 뺀다.
CREATE TABLE IF NOT EXISTS smfr.funeral_notices (
    id             varchar(50)  PRIMARY KEY,
    title          text         NOT NULL,
    content        text,
    notice_type    varchar(30)  NOT NULL DEFAULT 'NOTICE',
    is_important   boolean      NOT NULL DEFAULT false,
    target_user_id varchar(100),
    building_id    varchar(50),
    target_page    text,
    target_param   text,
    start_at       timestamptz,
    end_at         timestamptz,
    created_at     timestamptz  NOT NULL DEFAULT now(),
    created_by     varchar(100),
    updated_at     timestamptz,
    updated_by     varchar(100),
    is_deleted     boolean      NOT NULL DEFAULT false
);

COMMENT ON TABLE smfr.funeral_notices IS '장례식장 알림 정보 (옛 smfr.t_notification)';

CREATE INDEX IF NOT EXISTS ix_funeral_notices_target
    ON smfr.funeral_notices (target_user_id, is_deleted);

CREATE INDEX IF NOT EXISTS ix_funeral_notices_building
    ON smfr.funeral_notices (building_id, is_deleted);

CREATE TABLE IF NOT EXISTS smfr.funeral_notice_reads (
    id         varchar(50)  PRIMARY KEY,
    notice_id  varchar(50)  NOT NULL,
    user_id    varchar(100) NOT NULL,
    read_at    timestamptz  NOT NULL DEFAULT now(),
    created_at timestamptz  NOT NULL DEFAULT now(),
    created_by varchar(100),
    is_deleted boolean      NOT NULL DEFAULT false
);

COMMENT ON TABLE smfr.funeral_notice_reads IS '알림을 누가 읽었는지';

-- 같은 알림을 두 번 읽음 처리하지 않는다.
CREATE UNIQUE INDEX IF NOT EXISTS ix_funeral_notice_reads_notice_user
    ON smfr.funeral_notice_reads (notice_id, user_id);

-- ── 계정별 업무 설정 ────────────────────────────────────────
-- 옛 t_account_conf. 값은 옛 표기 그대로 'Y'/'N' 이다.
-- 어떤 코드가 있는지는 표가 아니라 코드(SettingCatalog.cs)에 적혀 있다 —
-- 넷뿐이고 늘 화면과 함께 바뀌기 때문이다.
CREATE TABLE IF NOT EXISTS smfr.account_settings (
    id            varchar(50)  PRIMARY KEY,
    user_id       varchar(100) NOT NULL,
    setting_code  varchar(100) NOT NULL,
    setting_value varchar(500),
    created_at    timestamptz  NOT NULL DEFAULT now(),
    created_by    varchar(100),
    updated_at    timestamptz,
    updated_by    varchar(100),
    is_deleted    boolean      NOT NULL DEFAULT false
);

COMMENT ON TABLE smfr.account_settings IS '계정별 장례식장 업무 설정 (옛 smfr.t_account_conf)';

-- 한 사람이 같은 설정을 두 줄 갖지 않게.
CREATE UNIQUE INDEX IF NOT EXISTS ix_account_settings_user_code
    ON smfr.account_settings (user_id, setting_code);

-- ── 건물별 음원 배정 ────────────────────────────────────────
-- 옛 t_music_build (ms_seq, b_key 두 칸뿐이었다).
CREATE TABLE IF NOT EXISTS smfr.building_music (
    id              varchar(50)  PRIMARY KEY,
    building_id     varchar(50)  NOT NULL,
    media_source_id varchar(50)  NOT NULL,
    sort_order      integer      NOT NULL DEFAULT 0,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    created_by      varchar(100),
    updated_at      timestamptz,
    updated_by      varchar(100),
    is_deleted      boolean      NOT NULL DEFAULT false
);

COMMENT ON TABLE smfr.building_music IS '건물별 음원 배정 (옛 smfr.t_music_build)';

-- 같은 건물에 같은 음원을 두 번 배정하지 않는다.
CREATE UNIQUE INDEX IF NOT EXISTS ix_building_music_building_media
    ON smfr.building_music (building_id, media_source_id);

CREATE INDEX IF NOT EXISTS ix_building_music_building
    ON smfr.building_music (building_id, is_deleted);

COMMIT;

-- 확인용
-- SELECT table_name FROM information_schema.tables
--  WHERE table_schema='smfr'
--    AND table_name IN ('funeral_notices','funeral_notice_reads','account_settings','building_music')
--  ORDER BY 1;
