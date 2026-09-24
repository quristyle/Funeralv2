-- ============================================================
-- 위치를 마지막으로 「확인한 때」 — scom.notification_preferences 에 칸 하나
-- ============================================================
--
-- 포털이 열려 있는 동안 브라우저가 **저절로 위치를 다시 잰다**(권한을 이미
-- 허용해 둔 브라우저에서만, 세 시간에 한 번). 그 확인 시각을 여기 적는다.
--
-- ── 왜 weather_located_at 으로 대신하지 않나 ─────────────────
--
-- 그 칸은 **좌표가 실제로 달라진 때**다. 한자리에 사는 사람은 자리가 안
-- 바뀌므로 그 값이 몇 달 전 그대로인데, 그렇다고 위치가 묵은 것은 아니다 —
-- 그 사이에도 브라우저가 계속 확인해 왔다. 한 칸으로 뭉치면 둘을 가릴 수
-- 없고, 설정 화면은 「이 위치를 믿어도 되는가」에 답할 수 없다.
--
--   weather_located_at  자리가 바뀐 때      (이사·출장을 가린다)
--   weather_synced_at   확인한 때           (자동 재수집이 도는지 가린다)
--
-- ── 아무나 찍지 못한다 ───────────────────────────────────────
--
-- 설정 화면은 스위치 하나를 눌러도 설정 전체를 보내므로 위경도가 늘 함께
-- 온다. 좌표가 왔다고 이 칸을 찍으면 **푸시 스위치를 만질 때마다 방금
-- 확인한 것**이 된다 — 그래서 요청에 weather_located = true 가 실린
-- 경우에만 찍는다(브라우저가 방금 재어 보낸 것이라는 표시).
--
-- 멱등하다. 되돌리려면 맨 아래.

BEGIN;

ALTER TABLE scom.notification_preferences
    ADD COLUMN IF NOT EXISTS weather_synced_at timestamptz;

COMMENT ON COLUMN scom.notification_preferences.weather_synced_at
    IS '위치를 마지막으로 확인한 때. 좌표가 그대로여도 찍힌다 — 브라우저가 저절로 다시 잰 시각이다. 좌표가 달라진 때는 weather_located_at.';

COMMIT;

-- 확인
--   SELECT count(*) FILTER (WHERE weather_lat IS NOT NULL)   AS 위치있음,
--          count(*) FILTER (WHERE weather_synced_at IS NOT NULL) AS 확인됨,
--          max(weather_synced_at) AS 마지막확인
--     FROM scom.notification_preferences;
--
-- 되돌리기
--   ALTER TABLE scom.notification_preferences DROP COLUMN weather_synced_at;
