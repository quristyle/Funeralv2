-- ============================================================
-- 알림정보 화면(/info/notice) 을 걷어낸다
-- ============================================================
-- DB: jsiniportal (scom)  ※ 표 삭제는 아래 별도 안내 참고
--
-- 장례식장 알림정보 화면은 쓰지 않는다(지시, 2026-09-03). 그 자리는 이미 둘이 채운다 —
-- 포털 공지(`/portal/notice`, AuthServer `scom.notices`)와
-- 내 알림 설정(`/system/push/setting`, NotificationServer).
--
-- ── 무엇을 지우나 ──────────────────────────────────────────
--
-- 메뉴 id 는 `NOTICE` 이고 `INFO`(정보 묶음) 아래에 있었다.
-- **`PORTAL_NOTICE` 와 다른 것이다** — 그쪽은 포털 공지 관리 화면이고 남긴다.
-- 이름이 비슷해 헷갈리기 쉬우므로 id 로 정확히 지운다.
--
-- 지우는 순서가 있다. `role_menus` 에 외래키가 걸려 있어 메뉴보다 먼저 지운다
-- (`demo_menu_drop.sql` 과 같은 순서다).
--
-- ── 장례식장 표는 여기서 지우지 않는다 ─────────────────────
--
-- `smfr.funeral_notices` · `smfr.funeral_notice_reads` 는 **EF 마이그레이션**으로
-- 지웠다(`RemoveFuneralNotices`). funeralv2 스키마는 EF 가 정본이라 손으로 쓰면
-- 정본이 둘이 된다(CLAUDE.md). 두 표 모두 **행이 0 이었다** — 확인하고 지웠다.
--
-- 다른 환경(운영 등)에 반영할 때:
--   cd microservices/funeralv2Api && dotnet build -c Debug && dotnet ef database update
--
-- 마이그레이션을 돌릴 수 없는 환경이라면 아래 두 줄을 직접 돌린다(행이 없을 때만 안전하다).
--   DROP TABLE IF EXISTS smfr.funeral_notice_reads;
--   DROP TABLE IF EXISTS smfr.funeral_notices;
--
-- 반복 실행해도 안전하다.

BEGIN;

-- 1) 역할↔메뉴 권한. 외래키 때문에 메뉴보다 먼저 지운다.
DELETE FROM scom.role_menus WHERE menu_id = 'NOTICE';

-- 2) 즐겨찾기. 지금은 해당분이 없지만 나중에 생겨도 막히지 않게 함께 지운다.
DELETE FROM scom.menu_favorites WHERE menu_id = 'NOTICE';

-- 3) 메뉴 자체.
DELETE FROM scom.system_menus WHERE id = 'NOTICE';

COMMIT;

-- 확인 — 아무 행도 나오지 않아야 한다.
SELECT id AS 남은메뉴, path AS 경로
FROM scom.system_menus
WHERE id = 'NOTICE' OR path = '/info/notice';

-- 포털 공지는 그대로 있어야 한다 (한 행이 나와야 정상).
SELECT id AS 포털공지, path AS 경로, title AS 이름
FROM scom.system_menus
WHERE id = 'PORTAL_NOTICE';
