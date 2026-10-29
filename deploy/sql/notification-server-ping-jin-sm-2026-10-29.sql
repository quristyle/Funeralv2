-- ============================================================
-- SERVER_PING 이벤트 설명에 jin sm 장비(192.168.219.244) 추가 (2026-10-29)
-- ============================================================
--
-- AuthServer 의 ServerPingWatcher 가 그리드 중계 장비(192.168.219.229)와
-- jin sm 장비(192.168.219.244) 두 장비를 함께 감시하도록 확장되었다.
-- 알림 이벤트 명칭과 설명을 갱신한다.
-- 대상 DB: jsiniportal (스키마 scom). 두 번 돌려도 같다.
--
--   psql -h 127.0.0.1 -p 31015 -U funeralv2 -d jsiniportal -f deploy/sql/notification-server-ping-jin-sm-2026-10-29.sql
-- ============================================================

UPDATE scom.notification_events
   SET name = '내부 서버 Ping (그리드 중계 · jin sm)',
       description = '내부 장비(그리드 중계 192.168.219.229 · jin sm 192.168.219.244) 통신 두절(3회 연속 실패) 시 알림 · 두절이 이어지면 60분마다 다시 · 통신 정상화 시 복구 알림',
       updated_at  = now(),
       updated_by  = 'admin'
 WHERE id = 'SERVER_PING';
