-- ============================================================
-- 계정 접속 기록 (로그인 이력)
-- ============================================================
--
-- 지시: "account-info.vue 은 사용자의 계정이 활동한 정보가 보여지는 곳이다.
--        사용자 계정이 활동한 정보를 추가 할수 있는것이 있다면 더 추가하라.
--        필요 하다면 관리항목을 더 늘려서 관리하라."
--
-- 지금까지는 `accounts` 에 **마지막 한 번**만 남겼다(`last_login_at` · `last_login_ip`).
-- 그래서 화면이 "지금 이 접속" 밖에 보여 줄 수 없었다.
--
--   내가 지난번에 언제·어디서 들어왔는지     ← 알 수 없음
--   누가 내 아이디로 로그인을 시도했는지      ← 알 수 없음
--   내가 이 계정을 얼마나 써 왔는지          ← 알 수 없음
--
-- 이 셋이 계정 화면에서 사람이 실제로 궁금해하는 것이고, 남의 접근을 알아채는 단서다.
-- 그래서 한 줄씩 쌓는 표를 둔다. 마지막 값도 계속 `accounts` 에 남긴다 —
-- 로그인 화면과 게이트웨이가 이미 그 값을 쓰고 있고, 표를 매번 훑는 것보다 싸다.
--
-- **실패도 남긴다.** 성공만 남기면 "누가 내 아이디를 두드리고 있다" 를 볼 수 없다.
--
-- 반복 실행해도 안전하다.

BEGIN;

CREATE TABLE IF NOT EXISTS scom.account_login_logs (
  id             text        NOT NULL,
  -- 계정 키(accounts.id). 실패한 로그인은 계정을 못 찾은 경우가 있어 비어 있을 수 있다.
  account_id     text,
  -- 입력된 로그인 아이디. 계정을 못 찾은 실패도 무엇을 시도했는지 남는다.
  login_id       text        NOT NULL,
  -- 성공 여부
  success        boolean     NOT NULL,
  -- 실패 이유 (NOT_FOUND · BAD_PASSWORD …). 성공이면 NULL
  fail_reason    text,
  -- 접속 IP. 게이트웨이 뒤이므로 X-Forwarded-For 의 첫 값을 쓴다.
  ip             text,
  -- 브라우저·기기. 낯선 접속을 알아보는 단서다.
  user_agent     text,
  created_at     timestamptz NOT NULL DEFAULT now(),
  created_by     text,
  updated_at     timestamptz,
  updated_by     text,
  is_deleted     boolean     NOT NULL DEFAULT false,
  CONSTRAINT "PK_account_login_logs" PRIMARY KEY (id)
);

COMMENT ON TABLE scom.account_login_logs IS
  '로그인 시도 기록. 성공·실패를 모두 남긴다. 계정 화면의 접속 기록이 이 표를 읽는다.';

-- 조회는 늘 "내 계정의 최근 것부터" 다.
CREATE INDEX IF NOT EXISTS "IX_account_login_logs_account"
  ON scom.account_login_logs (account_id, created_at DESC);

-- 계정을 못 찾은 실패를 아이디로 훑을 때 쓴다.
CREATE INDEX IF NOT EXISTS "IX_account_login_logs_login_id"
  ON scom.account_login_logs (login_id, created_at DESC);

-- ── 계정을 지우면 기록도 함께 지운다 ──────────────────────
--
-- 남겨 두면 아무 곳도 가리키지 않는 행이 쌓인다.
-- 계정을 못 찾은 실패(account_id IS NULL)는 이 제약과 무관하게 남는다.
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'FK_account_login_logs_accounts'
  ) THEN
    ALTER TABLE scom.account_login_logs
      ADD CONSTRAINT "FK_account_login_logs_accounts"
      FOREIGN KEY (account_id) REFERENCES scom.accounts (id) ON DELETE CASCADE;
  END IF;
END $$;

-- ── 이미 있는 마지막 접속을 첫 줄로 옮겨 둔다 ─────────────
--
-- 그러지 않으면 이 표가 생긴 뒤 처음 로그인할 때까지 화면이 "기록 없음" 으로 보인다.
-- 이미 아는 사실(accounts.last_login_at)이 있는데 비어 보이는 것은 이상하다.
-- 계정마다 한 줄만 넣고, 다시 실행해도 늘어나지 않는다.
INSERT INTO scom.account_login_logs (
  id, account_id, login_id, success, ip, user_agent, created_at, created_by
)
SELECT
  'seed-' || a.id, a.id, a.user_id, true, a.last_login_ip, NULL,
  a.last_login_at, 'login-log-seed'
FROM scom.accounts a
WHERE a.is_deleted = false
  AND a.last_login_at IS NOT NULL
ON CONFLICT (id) DO NOTHING;

COMMIT;

-- 확인
SELECT count(*) AS 기록수,
       count(*) FILTER (WHERE success)     AS 성공,
       count(*) FILTER (WHERE NOT success) AS 실패,
       count(DISTINCT account_id)          AS 계정수
FROM scom.account_login_logs;
