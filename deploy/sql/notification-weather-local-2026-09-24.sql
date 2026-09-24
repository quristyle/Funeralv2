-- ============================================================
-- 「내 위치 날씨」 알림을 들인다 — scom.notification_preferences 에 칸 여섯
-- ============================================================
--
-- PWA 가 브라우저에서 받아 온 위경도를 사람마다 하나 저장해 두고,
-- 생활과환경(LifeEnvServer)이 정해진 시각에 그 지점의 **현재 날씨와
-- 예보**를 기상청에서 받아 앱 푸시로 보낸다.
--
-- ── 왜 weather_enabled 를 그대로 쓰지 않았나 ─────────────────
--
-- 그 칸은 **기상 특보**다 — 기상청이 발표한 특보가 우리 관리 지역
-- (ghub.weather_locations)에 걸렸을 때 켠 사람 전원에게 나간다. 회사가
-- 등록해 둔 지점이고, 사건이 있을 때만 울린다.
--
-- 이쪽은 **내가 지금 서 있는 곳**이고 **사건이 없어도 시각마다** 온다.
-- 받는 이유도 빈도도 다르므로 스위치를 갈라 둔다 — 한 칸에 얹으면
-- 특보만 받고 싶은 사람이 매일 아침 알림을 함께 받게 된다.
--
-- ── 위치를 기기가 아니라 사람에 붙이는 까닭 ──────────────────
--
-- 구독표(scom.push_subscriptions)는 브라우저 하나가 한 줄이라 거기에
-- 붙이면 기기를 바꿀 때마다 위치를 다시 잡아야 한다. 이 표는 사람의
-- 뜻이라 기기를 다 지워도 남는다 — 스위치들과 같은 자리가 맞다.
--
-- 멱등하다. 되돌리려면 맨 아래.

BEGIN;

ALTER TABLE scom.notification_preferences
    ADD COLUMN IF NOT EXISTS weather_local_enabled boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS weather_lat           double precision,
    ADD COLUMN IF NOT EXISTS weather_lon           double precision,
    ADD COLUMN IF NOT EXISTS weather_place         text,
    ADD COLUMN IF NOT EXISTS weather_hours         text,
    ADD COLUMN IF NOT EXISTS weather_located_at    timestamptz,
    ADD COLUMN IF NOT EXISTS weather_local_sent_at timestamptz;

COMMENT ON COLUMN scom.notification_preferences.weather_local_enabled
    IS '내 위치 날씨 알림을 받을지. 기본 꺼짐 — 위치를 한 번 잡아야 켤 수 있다.';
COMMENT ON COLUMN scom.notification_preferences.weather_lat
    IS '브라우저 Geolocation 이 준 위도(10진 도). 없으면 보낼 곳이 없다.';
COMMENT ON COLUMN scom.notification_preferences.weather_lon
    IS '브라우저 Geolocation 이 준 경도(10진 도).';
COMMENT ON COLUMN scom.notification_preferences.weather_place
    IS '보여 줄 지역 이름(예: 울산광역시 남구 삼산동). ghub.grid_coordinates 에서 가장 가까운 행정구역을 찾아 채운다. 표시 전용이다.';
COMMENT ON COLUMN scom.notification_preferences.weather_hours
    IS '받을 시각들(KST, 쉼표로 나눈 0~23). 비면 07,18 로 본다.';
COMMENT ON COLUMN scom.notification_preferences.weather_located_at
    IS '위치를 마지막으로 잡은 때. 오래되면 화면이 다시 잡기를 권한다.';
COMMENT ON COLUMN scom.notification_preferences.weather_local_sent_at
    IS '마지막으로 보낸 때. 같은 시각 칸에 두 번 보내지 않으려고 본다 — 발송기가 5분마다 도는데 이것이 없으면 한 시간에 열두 번 간다.';

COMMIT;

-- 확인
--   SELECT count(*) FILTER (WHERE weather_local_enabled) AS 켠사람,
--          count(*) FILTER (WHERE weather_lat IS NOT NULL) AS 위치있음,
--          count(*) AS 전체
--     FROM scom.notification_preferences;
--
-- 되돌리기
--   ALTER TABLE scom.notification_preferences
--     DROP COLUMN weather_local_enabled, DROP COLUMN weather_lat,
--     DROP COLUMN weather_lon, DROP COLUMN weather_place,
--     DROP COLUMN weather_hours, DROP COLUMN weather_located_at,
--     DROP COLUMN weather_local_sent_at;
