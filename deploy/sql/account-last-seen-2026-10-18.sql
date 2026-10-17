-- ============================================================
-- 계정의 마지막 사용시각 — scom.accounts.last_seen_at (2026-10-18)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.Admin/Components/Pages/UserList.razor
--       (포털관리 › 시스템관리 › 계정 관리, /admin/system/account)
-- 쓰는 쪽: AuthServer 의 AccountActivityService.TouchAsync
--          ← LoginCompletion.CompleteAsync (들어온 순간)
--          ← MenuUsageService.RecordAsync  (화면을 옮길 때마다)
--
-- **EF 마이그레이션 `20261017145946_AddAccountLastSeenAt` 을 그대로 뽑은 것**에
-- 되채우기(backfill)를 덧붙였다. AuthServer 는 기동할 때 마이그레이션을 돌리지
-- 않으므로(그 일을 하는 곳은 FileServer 뿐이다) 운영 반영은 이 파일을 손으로
-- 한 번 돌린다. 맨 끝에서 scom."__EFMigrationsHistory" 에 줄을 넣는 것이 그래서
-- 꼭 필요하다 — 안 넣으면 다음에 누가 `dotnet ef database update` 를 돌릴 때
-- 같은 칸을 한 번 더 만들려다 멈춘다.
--
--   psql -h … -U funeralv2 -d jsiniportal -f account-last-seen-2026-10-18.sql
--
-- 두 번 돌려도 안전하다(각 단계가 이력을 먼저 보고, 되채우기는 GREATEST 라
-- 이미 더 늦은 값이 들어 있으면 건드리지 않는다).
--
-- ── 왜 칸이 하나 더 필요한가 ────────────────────────────────
--
-- 계정 관리 목록이 답해야 하는 물음이 둘인데 여태 값이 하나뿐이었다.
--
--   ① 「이 사람이 마지막으로 **들어온** 때는 언제인가」 → last_login_at (있었다)
--   ② 「이 사람이 마지막으로 포털을 **쓴** 때는 언제인가」 → 없었다
--
-- 둘을 last_login_at 하나로 뭉치면 틀린다. access token 이 7일이고 갱신 쿠키가
-- 30일이라 **브라우저를 안 닫는 사람은 로그인 화면을 한 달에 한 번도 안 거친다** —
-- 매일 쓰는 사람이 「한 달 전에 들어온 사람」으로 보인다. 안 쓰는 계정을
-- 가려내려고 목록을 여는 것인데 바로 그 물음에 못 답하는 값이다.
--
-- ── 왜 기록 표를 집계하지 않고 칸으로 두나 ──────────────────
--
-- 「마지막으로 쓴 때」는 scom.menu_usage_logs 를 user_id 로 묶어 max(occurred_at)
-- 하면 나온다. 그래도 칸으로 두는 까닭이 둘이다.
--
--   · 그 표는 **한 해가 지나면 스스로 걷어낸다**(MenuUsageService.RetentionDays
--     = 365). 집계로 셈하면 일 년 넘게 안 쓴 계정이 **한 번도 안 쓴 계정과
--     같은 그림**이 된다 — 가려내려는 것이 바로 그 계정들이다.
--   · 목록은 예순 줄을 한꺼번에 그린다. 줄마다 수만 줄짜리 기록 표를 훑게
--     하면 화면 하나를 여는 값이 너무 비싸진다.
--
-- ── 되채우기 — 지금 있는 자취에서 한 번 만들어 둔다 ─────────
--
-- 칸만 만들고 두면 **전원이 「한 번도 안 씀」**으로 보인다. 그것은 사실이
-- 아니라 「아직 안 적기 시작했다」이고, 둘은 화면에서 구별되지 않는다.
-- 그래서 이미 있는 자취 셋 중 가장 늦은 것으로 한 번 채운다.
--
--   · scom.accounts.last_login_at       — 마지막 로그인
--   · scom.account_login_logs           — 성공한 로그인 기록 (계정 키로 묶는다)
--   · scom.menu_usage_logs              — 화면 열람 기록 (로그인 아이디로 묶는다)
--
-- 로그인 기록을 last_login_at 과 **따로** 보는 까닭은 계정 결합(merge) 때문이다.
-- 결합은 기록 줄의 account_id 를 받는 계정으로 옮기지만 last_login_at 은 옮기지
-- 않아서, 소셜로만 들어오던 계정은 기록에 성공이 쌓여 있는데 last_login_at 이
-- 비어 있다(2026-10-18 운영에서 uspuni 한 건).
--
-- 같은 까닭으로 **last_login_at 도 함께 되채운다** — 그 값이 비어 있는데 성공한
-- 로그인 기록이 있는 계정은 기록 쪽이 맞다.
--
-- ============================================================

START TRANSACTION;

-- ── ① 칸 (EF 마이그레이션 그대로) ───────────────────────────

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM scom."__EFMigrationsHistory" WHERE "MigrationId" = '20261017145946_AddAccountLastSeenAt') THEN
    ALTER TABLE scom.accounts ADD last_seen_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM scom."__EFMigrationsHistory" WHERE "MigrationId" = '20261017145946_AddAccountLastSeenAt') THEN
    COMMENT ON COLUMN scom.accounts.last_seen_at IS '마지막으로 포털을 쓴 시각 (UTC). 아직 한 번도 안 썼으면 null 이다.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM scom."__EFMigrationsHistory" WHERE "MigrationId" = '20261017145946_AddAccountLastSeenAt') THEN
    INSERT INTO scom."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261017145946_AddAccountLastSeenAt', '10.0.11');
    END IF;
END $EF$;

-- ── ② last_login_at 되채우기 ────────────────────────────────
--
-- 비어 있는 계정만 건드린다. 들어 있는 값이 기록보다 이르더라도 덮지 않는다 —
-- 그 값을 적는 자리가 LoginCompletion 하나뿐이라 틀릴 일이 없고, 괜히 덮으면
-- 결합으로 옮겨 온 **남의 계정 기록**이 이 사람의 로그인 시각이 된다.

UPDATE scom.accounts a
   SET last_login_at = l.max_at
  FROM (SELECT account_id, max(created_at) AS max_at
          FROM scom.account_login_logs
         WHERE success AND account_id IS NOT NULL
         GROUP BY account_id) l
 WHERE l.account_id = a.id
   AND a.last_login_at IS NULL;

-- ── ③ last_seen_at 되채우기 ─────────────────────────────────
--
-- 자취 셋 중 가장 늦은 것. GREATEST 가 NULL 을 만나면 통째로 NULL 이라
-- '-infinity' 로 받쳐 두고, 셋 다 없는 계정은 NULLIF 로 다시 비운다.

UPDATE scom.accounts a
   SET last_seen_at = NULLIF(
         GREATEST(
           COALESCE(a.last_login_at,  '-infinity'::timestamptz),
           COALESCE(a.last_seen_at,   '-infinity'::timestamptz),
           COALESCE(m.max_at,         '-infinity'::timestamptz)),
         '-infinity'::timestamptz)
  FROM (SELECT a2.id,
               (SELECT max(u.occurred_at)
                  FROM scom.menu_usage_logs u
                 WHERE u.user_id = a2.user_id) AS max_at
          FROM scom.accounts a2) m
 WHERE m.id = a.id
   AND NULLIF(
         GREATEST(
           COALESCE(a.last_login_at,  '-infinity'::timestamptz),
           COALESCE(a.last_seen_at,   '-infinity'::timestamptz),
           COALESCE(m.max_at,         '-infinity'::timestamptz)),
         '-infinity'::timestamptz) IS DISTINCT FROM a.last_seen_at;

COMMIT;

-- ── 확인 ────────────────────────────────────────────────────
--
-- SELECT user_id,
--        last_login_at AT TIME ZONE 'Asia/Seoul' AS 로그인,
--        last_seen_at  AT TIME ZONE 'Asia/Seoul' AS 사용
--   FROM scom.accounts
--  WHERE is_deleted = false
--  ORDER BY last_seen_at DESC NULLS LAST;
