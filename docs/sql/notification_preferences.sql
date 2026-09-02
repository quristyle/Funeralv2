-- ============================================================
-- 사람별 알림 수신 설정 (내 알림 설정 화면)
-- ============================================================
-- DB: jsiniportal (scom)
--
-- `/system/push/setting` 을 **로그인한 본인의 알림 설정** 화면으로 바꾸면서 만든 표다.
-- 그 화면은 원래 헬프데스크의 구독 시험 화면을 옮겨 온 것이라 데이터도 헬프데스크
-- (`/api/helpdesk/push/*`)를 보고 있었다. 관리 주체를 포털로 옮겼다 —
-- 이제 NotificationServer(`/api/notification/notifications/*`)가 정본이다.
--
-- ── 구독 표와 무엇이 다른가 ────────────────────────────────
--
-- `scom.push_subscriptions` 는 **기기**다. 브라우저마다 한 행이고 브라우저를 지우면
-- 사라진다. 이 표는 **사람의 뜻**이다 — 기기를 다 지워도 남고, 새 기기로 구독하면
-- 그 뜻이 그대로 적용된다. 주인 키는 두 표가 같다(owner_type + owner_key).
--
--   owner_type = 'jsini'   owner_key = 'quristyle'   (포털 로그인 아이디)
--
-- 포털 계정의 owner_key 는 `scom.accounts.user_id` 다 — 게이트웨이가 넘기는
-- X-User-Id 가 그 값이기 때문이다(`accounts.id` 가 아니다).
--
-- ── 기본값을 켜짐으로 두는 이유 ────────────────────────────
--
-- **행이 없으면 켜짐이다.** 꺼짐을 기본으로 두면 설정 화면을 한 번도 열지 않은
-- 사람이 알림을 못 받게 되어, 이 표가 생기기 전과 동작이 달라진다.
-- 날씨만 예외로 꺼짐이 기본이다 — 업무 알림이 아니라 곁들이는 알림이다.
--
-- ── 날씨 알림은 아직 발송 경로가 없다 ──────────────────────
--
-- 판정(기상 임계치·특보)은 LifeEnvServer 가 이미 돌리고 있지만 발송은 이식하지
-- 않았다(결정 D-G1, docs/analysis/38-ghub-migration.md). 그 결정이 붙을 때
-- "누구에게" 의 답이 되도록 뜻만 먼저 받아 둔다.
--
-- 반복 실행해도 안전하다.

BEGIN;

CREATE TABLE IF NOT EXISTS scom.notification_preferences (
  id               text        NOT NULL,
  -- 주인. 구독 표와 같은 문자열 한 쌍 (위 설명 참고)
  owner_type       text        NOT NULL,
  owner_key        text        NOT NULL,
  -- 브라우저 푸시. 끄면 구독이 남아 있어도 보내지 않는다 (PushSender 가 발송 직전에 본다)
  push_enabled     boolean     NOT NULL DEFAULT true,
  -- 이메일. 역할로 보내는 메일(toRole)에만 걸린다 — 주소를 직접 적은 업무 메일은 막지 않는다
  email_enabled    boolean     NOT NULL DEFAULT true,
  -- 날씨(기상 특보·임계치). 원하는 사람만 받는다
  weather_enabled  boolean     NOT NULL DEFAULT false,
  created_at       timestamptz NOT NULL DEFAULT now(),
  created_by       text,
  updated_at       timestamptz,
  updated_by       text,
  is_deleted       boolean     NOT NULL DEFAULT false,
  CONSTRAINT "PK_notification_preferences" PRIMARY KEY (id)
);

COMMENT ON TABLE scom.notification_preferences IS
  '사람별 알림 수신 설정(푸시·이메일·날씨). 기기(push_subscriptions)와 달리 사람 하나에 한 행이고, 행이 없으면 푸시·이메일은 켜짐으로 본다.';

-- 사람 하나에 한 행이다. 두 행이 생기면 어느 쪽이 참인지 알 수 없다.
CREATE UNIQUE INDEX IF NOT EXISTS "UX_notification_preferences_owner"
  ON scom.notification_preferences (owner_type, owner_key);

COMMIT;

-- 확인
SELECT owner_type AS 주인종류,
       count(*)                                AS 설정수,
       count(*) FILTER (WHERE NOT push_enabled)    AS 푸시끔,
       count(*) FILTER (WHERE NOT email_enabled)   AS 이메일끔,
       count(*) FILTER (WHERE weather_enabled)     AS 날씨켬
FROM scom.notification_preferences
GROUP BY 1 ORDER BY 1;
