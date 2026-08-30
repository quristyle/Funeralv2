-- ============================================================
-- 포털 계정(scom.accounts)에 생년월일을 더한다.
--
-- 대상 DB : jsiniportal (scom)
-- 실행    : psql -h <host> -p <port> -U funeralv2 -d jsiniportal -f docs/sql/account_birthday.sql
--
-- 여러 번 실행해도 안전하다 (IF NOT EXISTS).
--
-- ── 왜 ───────────────────────────────────────────────────────
-- 생활과환경(LifeEnvServer)의 생일 화면이 처음에는 GHUB 에서 이관한 별도 명단
-- (ghub.birthday_profiles)을 봤는데, 생일은 포털 사용자의 속성으로 관리하기로
-- 했다 (2026-08-30). 정본은 여기(scom.accounts)고, 관리(입력·수정)는 포털의
-- 계정 관리 화면이 한다. LifeEnvServer 는 읽기만 한다.
-- ghub.birthday_profiles 는 자료를 이 컬럼으로 옮긴 뒤 지웠다
-- (scripts/ghub-db-migration/birthday_to_accounts.py).
-- ============================================================

ALTER TABLE scom.accounts ADD COLUMN IF NOT EXISTS birth_date date;
COMMENT ON COLUMN scom.accounts.birth_date IS '생년월일. birth_date_is_lunar 가 참이면 음력 날짜다';

ALTER TABLE scom.accounts ADD COLUMN IF NOT EXISTS birth_date_is_lunar boolean NOT NULL DEFAULT false;
COMMENT ON COLUMN scom.accounts.birth_date_is_lunar IS '생년월일이 음력인지';

ALTER TABLE scom.accounts ADD COLUMN IF NOT EXISTS birthday_celebrated boolean NOT NULL DEFAULT true;
COMMENT ON COLUMN scom.accounts.birthday_celebrated IS '생일 축하(생일 화면 노출·메시지) 대상인지. 본인이 원치 않으면 끈다';

-- ── 확인 ─────────────────────────────────────────────────────
SELECT column_name, data_type, column_default
FROM information_schema.columns
WHERE table_schema = 'scom' AND table_name = 'accounts'
  AND column_name IN ('birth_date', 'birth_date_is_lunar', 'birthday_celebrated');
