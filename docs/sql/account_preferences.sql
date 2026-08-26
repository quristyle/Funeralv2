-- ============================================================
-- 계정별 화면 환경설정
-- ============================================================
--
-- 지시: "헤더 톱니의 설정 화면에서 관리되는 모든 기능이 /setting/environment 에서
--        관리되기를 바란다" — 그중 **사용자별로 서버에 저장**하는 단계.
--
-- 지금까지 환경설정(테마 · 레이아웃 · 위젯 위치 · 단축키 …)은 브라우저
-- 로컬스토리지(`jsini-portal-web-...-preferences`)에만 있었다. 그래서
-- **사람이 아니라 브라우저에 붙었다.**
--
--   다른 PC 에서 로그인하면 기본값으로 돌아간다
--   브라우저 캐시를 지우면 사라진다
--   같은 사람이 크롬·엣지를 쓰면 설정이 둘로 갈린다
--
-- 계정에 붙여 두면 어디서 로그인해도 따라온다.
--
-- ── 왜 전체가 아니라 '차이' 만 저장하나 ────────────────────
--
-- `payload` 에는 기본값과 **다른 항목만** 담는다(프론트의 `diffPreference`).
-- 전체를 담으면 나중에 프레임워크 기본값이 바뀌어도 옛 값이 박혀 따라오지 않는다.
-- 실제로 그 사고를 이미 겪었다 — vben 상위 동기화가 `widget.logoutButtonPosition`
-- 기본값을 바꿨을 때, 로컬스토리지에 저장돼 있던 전체 값이 우선해서
-- 새 기본값이 반영되지 않았다(18-i18n-fallback-warning.md · 로그아웃 위치 건).
--
-- 차이만 담으면 사용자가 손대지 않은 항목은 늘 최신 기본값을 따른다.
--
-- ── 왜 jsonb 인가 ─────────────────────────────────────────
--
-- 설정 항목이 40개가 넘고 상위 동기화마다 늘어난다. 칸으로 만들면 항목이
-- 하나 생길 때마다 마이그레이션이 필요하다. 이 값은 서버가 해석하지 않고
-- 프론트에 그대로 돌려주기만 하므로 칸으로 쪼갤 이유가 없다.
--
-- 반복 실행해도 안전하다.

BEGIN;

CREATE TABLE IF NOT EXISTS scom.account_preferences (
  -- 이 저장소의 다른 표와 같은 모양을 지킨다(id + 감사 칸 = BaseEntity).
  -- account_id 만으로 키를 잡으면 EF 의 BaseEntity 를 특수 처리해야 하고,
  -- 그 예외를 나중에 읽는 사람이 다시 이해해야 한다.
  id          text        NOT NULL,
  -- 계정 키(accounts.id). 계정 하나에 한 행이라 아래에서 UNIQUE 를 건다.
  account_id  text        NOT NULL,
  -- 기본값과 다른 항목만 담은 JSON. 서버는 해석하지 않고 그대로 돌려준다.
  payload     jsonb       NOT NULL DEFAULT '{}'::jsonb,
  created_at  timestamptz NOT NULL DEFAULT now(),
  created_by  text,
  updated_at  timestamptz,
  updated_by  text,
  is_deleted  boolean     NOT NULL DEFAULT false,
  CONSTRAINT "PK_account_preferences" PRIMARY KEY (id)
);

-- 계정 하나에 한 행. 두 창에서 동시에 저장해도 행이 둘로 갈라지지 않게 DB 에서 막는다.
CREATE UNIQUE INDEX IF NOT EXISTS "UX_account_preferences_account"
  ON scom.account_preferences (account_id);

COMMENT ON TABLE scom.account_preferences IS
  '계정별 화면 환경설정. 기본값과 다른 항목만 담는다. /setting/environment 와 헤더 톱니가 같이 쓴다.';
COMMENT ON COLUMN scom.account_preferences.payload IS
  '프론트의 diffPreference — 기본값과 다른 항목만. 서버는 해석하지 않는다.';

-- ── 계정을 지우면 설정도 함께 지운다 ──────────────────────
--
-- 남겨 두면 아무 곳도 가리키지 않는 행이 쌓인다.
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'FK_account_preferences_accounts'
  ) THEN
    ALTER TABLE scom.account_preferences
      ADD CONSTRAINT "FK_account_preferences_accounts"
      FOREIGN KEY (account_id) REFERENCES scom.accounts (id) ON DELETE CASCADE;
  END IF;
END $$;

COMMIT;

-- 확인
SELECT count(*) AS 저장된계정수 FROM scom.account_preferences;
