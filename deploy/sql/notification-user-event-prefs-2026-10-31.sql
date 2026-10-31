-- ============================================================
-- 내 알림 설정 — 사람 × 이벤트의 본인 설정 (2026-10-31)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.Funeral/Components/Pages/MyNotificationSettingPage.razor
-- API:  notification/notifications/preferences/me/events  (NotificationServer)
--
--   psql -h … -U funeralv2 -d jsiniportal -f notification-user-event-prefs-2026-10-31.sql
--
-- 두 번 돌려도 안전하다(IF NOT EXISTS).
--
-- 순서는 정책 → 본인 설정 → 구독이다. 이 표는 「본인 설정」을 이벤트 하나씩
-- 끌 수 있게 한다. **줄이 없으면 받는다** — 끈 것만 의미가 있고, 그래서 이 표가
-- 생긴 날 알림이 줄지 않는다. 정책이 안 걸리는 이벤트(governed = false)에는
-- 열쇠가 없다(PushSender · EmailEndpoints 가 정책이 풀어 준 코드로만 묻는다).
--
-- 댓글(HELPDESK_COMMENT)과 쪽지 메일은 notification_preferences 의 옛 스위치가
-- 정본이라 여기 적히지 않는다(MyNotificationEventService).
--
-- 되돌리려면: DROP TABLE scom.notification_user_event_prefs;
-- ============================================================

BEGIN;

CREATE TABLE IF NOT EXISTS scom.notification_user_event_prefs (
    id            text                     PRIMARY KEY,

    owner_type    text                     NOT NULL,
    owner_key     text                     NOT NULL,
    event_code    character varying(64)    NOT NULL,

    push_enabled  boolean                  NOT NULL DEFAULT true,
    email_enabled boolean                  NOT NULL DEFAULT true,

    created_at    timestamp with time zone NOT NULL DEFAULT now(),
    created_by    text,
    updated_at    timestamp with time zone,
    updated_by    text,
    is_deleted    boolean                  NOT NULL DEFAULT false
);

COMMENT ON TABLE scom.notification_user_event_prefs IS '내 알림 설정 — 이 사람은 이 이벤트를 이 길로 받지 않는다(끈 줄만 의미가 있다)';

CREATE UNIQUE INDEX IF NOT EXISTS "IX_notification_user_event_prefs_owner_event"
    ON scom.notification_user_event_prefs (owner_type, owner_key, event_code);

CREATE INDEX IF NOT EXISTS "IX_notification_user_event_prefs_event_code"
    ON scom.notification_user_event_prefs (event_code);

COMMIT;
