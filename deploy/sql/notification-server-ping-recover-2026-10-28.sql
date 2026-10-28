-- ============================================================
-- SERVER_PING 이벤트 설명에 복구 알림을 적는다 (2026-10-28)
-- ============================================================
--
-- AuthServer 의 ServerPingWatcher 가 두절·두절 지속(60분마다)·복구를 같은
-- 이벤트로 보낸다. 알림관리 화면의 설명이 「다운 시」만 말하고 있어 고친다.
-- 대상 DB: jsiniportal (스키마 scom). 두 번 돌려도 같다.
--
--   psql -h jin114.co.kr -p 31015 -U funeralv2 -d jsiniportal -f deploy/sql/notification-server-ping-recover-2026-10-28.sql
-- ============================================================

UPDATE scom.notification_events
   SET description = '192.168.219.229 장비 통신 두절(3회 연속 실패) 시 알림 · 두절이 이어지면 60분마다 다시 · 통신 정상화 시 복구 알림',
       updated_at  = now(),
       updated_by  = 'admin'
 WHERE id = 'SERVER_PING';
