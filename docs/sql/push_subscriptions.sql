-- ============================================================
-- 알림 서비스의 푸시 구독 표 (결정 D8-A)
-- ============================================================
--
-- 푸시·이메일이 헬프데스크 안에만 있어서 포털도 장례식장도 알림을 보내려면
-- 헬프데스크를 거쳐야 했다. NotificationServer 를 만들어 셋이 공유한다.
--
-- ── 헬프데스크 구독 표와 무엇이 다른가 ─────────────────────
--
-- 헬프데스크의 `jsini.pushsubscription` 은 주인을 (int UserId, string UserType) 으로
-- 잡고 Admin·Customer 테이블에 외래키로 묶어 두었다. **그 구조는 헬프데스크 밖에서
-- 쓸 수 없다** — 포털 계정은 아이디가 문자열이고 장례식장은 또 다른 신원 체계를 쓴다.
--
-- 그래서 주인을 문자열 한 쌍으로 둔다.
--
--   owner_type = 'jsini'             owner_key = 'quristyle'   (포털 로그인 아이디)
--   owner_type = 'helpdesk-admin'    owner_key = '5'           (헬프데스크 Admin.Id)
--   owner_type = 'helpdesk-customer' owner_key = '12'
--
-- 누구에게 보낼지는 **부르는 쪽이 정한다.** 헬프데스크가 "이 팀의 관리자" 를 알고
-- 싶으면 자기 DB 에서 골라 주인 키 목록을 넘긴다. 이 서비스는 팀도 회사도 모른다.
--
-- ── 기존 구독은 아직 옮기지 않았다 ─────────────────────────
--
-- 헬프데스크의 구독은 다른 DB(jinrecept)에 있고, 옮기면 헬프데스크의 발송 경로도
-- 함께 바꿔야 한다. 지금은 헬프데스크가 예전 방식으로 계속 돌고, 이 표는 포털이
-- 새로 만드는 구독을 받는다. 순서는 29-notification-server.md 에 적었다.
--
-- 반복 실행해도 안전하다.

BEGIN;

CREATE TABLE IF NOT EXISTS scom.push_subscriptions (
  id             text        NOT NULL,
  -- 푸시 서비스가 준 발송 주소. 브라우저·기기마다 다르고 이것이 실질적인 신원이다.
  endpoint       text        NOT NULL,
  -- 브라우저가 준 암호화 키와 인증 비밀
  p256dh         text        NOT NULL,
  auth           text        NOT NULL,
  -- 주인. 종류 + 식별자 (위 설명 참고)
  owner_type     text        NOT NULL,
  owner_key      text        NOT NULL,
  -- 어느 시스템에서 구독했나 (참고용)
  source         text,
  -- 구독을 만든 브라우저 (문제를 쫓을 때 쓴다)
  user_agent     text,
  -- 마지막으로 발송에 성공한 시각
  last_sent_at   timestamptz,
  -- 연달아 실패한 횟수. 404/410 은 즉시 지우고 그 밖의 실패만 센다.
  failure_count  integer     NOT NULL DEFAULT 0,
  created_at     timestamptz NOT NULL DEFAULT now(),
  created_by     text,
  updated_at     timestamptz,
  updated_by     text,
  is_deleted     boolean     NOT NULL DEFAULT false,
  CONSTRAINT "PK_push_subscriptions" PRIMARY KEY (id)
);

COMMENT ON TABLE scom.push_subscriptions IS
  '웹푸시 구독. 주인을 (owner_type, owner_key) 문자열 쌍으로 두어 포털·헬프데스크·장례식장이 함께 쓴다.';

-- 같은 브라우저가 다시 구독하면 같은 endpoint 가 온다. 새 행을 만들면 같은 기기에
-- 두 번 보내게 되므로 유일해야 한다.
CREATE UNIQUE INDEX IF NOT EXISTS "UX_push_subscriptions_endpoint"
  ON scom.push_subscriptions (endpoint);

-- 발송은 "이 주인들에게" 로만 훑는다.
CREATE INDEX IF NOT EXISTS "IX_push_subscriptions_owner"
  ON scom.push_subscriptions (owner_type, owner_key);

COMMIT;

-- 확인
SELECT owner_type AS 주인종류, count(*) AS 구독수
FROM scom.push_subscriptions
GROUP BY 1 ORDER BY 1;
