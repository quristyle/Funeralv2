-- PWA 푸시 구독에 브라우저가 제공하는 장비 식별 정보를 더한다.
--
-- 수집 정보는 구독을 만든 시점의 브라우저·OS·장비·화면·입력·지역·네트워크
-- 환경이다. User-Agent 하나만 저장하면 아이폰·아이패드·PC와 브라우저를
-- 구분하기 어려우므로, 가능한 값은 구조화해 저장하고 없는 값은 NULL로 둔다.
--
-- NotificationServer에는 전용 EF Migration을 두지 않는다. 기존
-- scom.push_subscriptions를 손대는 운영 SQL이며 여러 번 실행해도 안전해야 한다.

BEGIN;

ALTER TABLE scom.push_subscriptions
    ADD COLUMN IF NOT EXISTS device_type text,
    ADD COLUMN IF NOT EXISTS platform text,
    ADD COLUMN IF NOT EXISTS platform_version text,
    ADD COLUMN IF NOT EXISTS device_vendor text,
    ADD COLUMN IF NOT EXISTS device_model text,
    ADD COLUMN IF NOT EXISTS browser text,
    ADD COLUMN IF NOT EXISTS browser_version text,
    ADD COLUMN IF NOT EXISTS browser_engine text,
    ADD COLUMN IF NOT EXISTS is_mobile boolean,
    ADD COLUMN IF NOT EXISTS is_standalone boolean,
    ADD COLUMN IF NOT EXISTS display_mode text,
    ADD COLUMN IF NOT EXISTS screen_width integer,
    ADD COLUMN IF NOT EXISTS screen_height integer,
    ADD COLUMN IF NOT EXISTS viewport_width integer,
    ADD COLUMN IF NOT EXISTS viewport_height integer,
    ADD COLUMN IF NOT EXISTS device_pixel_ratio double precision,
    ADD COLUMN IF NOT EXISTS color_depth integer,
    ADD COLUMN IF NOT EXISTS hardware_concurrency integer,
    ADD COLUMN IF NOT EXISTS device_memory_gb double precision,
    ADD COLUMN IF NOT EXISTS max_touch_points integer,
    ADD COLUMN IF NOT EXISTS language text,
    ADD COLUMN IF NOT EXISTS languages text,
    ADD COLUMN IF NOT EXISTS time_zone text,
    ADD COLUMN IF NOT EXISTS connection_type text,
    ADD COLUMN IF NOT EXISTS effective_connection_type text,
    ADD COLUMN IF NOT EXISTS user_agent_data_json text;

COMMIT;

-- 확인
-- SELECT endpoint, device_type, platform, device_vendor, device_model, browser
--   FROM scom.push_subscriptions
--  ORDER BY created_at DESC
--  LIMIT 20;
