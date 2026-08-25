-- ============================================================
-- 계정 접속 기록과 비밀번호 사용 기간
-- ============================================================
--
-- /profile 화면에서 '가입일 · 최근 로그인 시간 · 접속 아이피' 를 보여주고,
-- 90일마다 비밀번호 변경을 요구하기 위해 필요한 칸을 만든다.
--
--   가입일          → 이미 있는 created_at 을 그대로 쓴다 (새 칸 없음)
--   최근 로그인     → last_login_at
--   접속 아이피     → last_login_ip
--   비밀번호 나이   → password_changed_at
--
-- 반복 실행해도 안전하다.

BEGIN;

ALTER TABLE scom.accounts ADD COLUMN IF NOT EXISTS last_login_at        timestamptz;
ALTER TABLE scom.accounts ADD COLUMN IF NOT EXISTS last_login_ip        text;
ALTER TABLE scom.accounts ADD COLUMN IF NOT EXISTS password_changed_at  timestamptz;

COMMENT ON COLUMN scom.accounts.last_login_at       IS '최근 로그인 성공 시각 (UTC)';
COMMENT ON COLUMN scom.accounts.last_login_ip       IS '최근 로그인 시 접속 IP. 게이트웨이 뒤이므로 X-Forwarded-For 의 첫 값을 쓴다.';
COMMENT ON COLUMN scom.accounts.password_changed_at IS '비밀번호를 마지막으로 바꾼 시각 (UTC). 90일 만료 계산의 기준이다.';

-- ── 기존 계정의 기준 시각 ────────────────────────────────
--
-- **일부러 created_at 이 아니라 now() 로 채운다.**
--
-- created_at 으로 채우면 오래전에 만든 계정 대부분이 이미 90일을 넘겨서,
-- 이 스크립트를 실행한 순간 265개 계정이 한꺼번에 비밀번호 변경 화면에 갇힌다.
-- now() 로 채우면 모두가 오늘부터 90일을 새로 받는다.
--
-- 이관 계정 42개는 비밀번호가 로그인 아이디와 같은 상태다(15-jsini-user-unification.md D13).
-- 그 문제는 이 만료 정책으로 해결되지 않는다 — D13 을 따로 처리해야 한다.
UPDATE scom.accounts
SET password_changed_at = now()
WHERE password_changed_at IS NULL;

COMMIT;

-- 확인
SELECT count(*)                                            AS 전체,
       count(last_login_at)                                AS 로그인기록있음,
       count(password_changed_at)                          AS 비밀번호기준시각있음,
       count(*) FILTER (WHERE password_changed_at < now() - interval '90 days') AS 이미만료
FROM scom.accounts
WHERE is_deleted = false;
