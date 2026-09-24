-- ============================================================
-- 쪽지 메일받기 스위치를 들인다 — scom.notification_preferences.note_email_enabled
-- ============================================================
--
-- 쪽지를 받을 때 메일도 한 통 받을지. **기본은 꺼짐이다.**
--
-- ── 왜 email_enabled 를 그대로 쓰지 않았나 ───────────────────
--
-- 그 칸은 **업무 알림**이다 — 역할로 보내는 메일(문의 접수 따위)에 걸리고
-- 기본이 켜짐이다. 쪽지는 사람이 나에게 쓴 글이라 갈래가 다르고, 이미
-- 쪽지함에 남고 앱 푸시로도 두드린다. 그 칸에 얹으면 업무 메일을 받는
-- 사람 전원에게 쪽지 메일이 함께 나가고, 끄려면 업무 메일까지 끊긴다.
--
-- ── 기본이 꺼짐인 까닭 ───────────────────────────────────────
--
-- 메일 한 통은 받는 쪽에 **지워야 할 것이 하나 늘어나는 일**이다. 쪽지는
-- 두드림 없이도 쪽지함에 남으므로 원하는 사람만 켜면 된다. 그래서 이 칸은
-- 다른 스위치들과 달리 "행이 없으면 꺼짐" 으로 읽는다
-- (NotificationPreferenceService.GetNoteEmailEnabledLoginIdsAsync).
--
-- 멱등하다. 되돌리려면 맨 아래 한 줄.

BEGIN;

ALTER TABLE scom.notification_preferences
    ADD COLUMN IF NOT EXISTS note_email_enabled boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN scom.notification_preferences.note_email_enabled
    IS '쪽지를 메일로도 받을지. 기본 꺼짐 — 받는 사람이 개인설정에서 켠다.';

COMMIT;

-- 확인
--   SELECT count(*) FILTER (WHERE note_email_enabled) AS 켠사람, count(*) AS 전체
--     FROM scom.notification_preferences;
--
-- 되돌리기
--   ALTER TABLE scom.notification_preferences DROP COLUMN note_email_enabled;
