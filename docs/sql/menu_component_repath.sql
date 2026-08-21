-- ============================================================
-- 메뉴 component 경로 재배치
-- ============================================================
--
-- 프론트엔드 화면 폴더를 시스템 경계에 맞춰 3분할했다.
--   views/portal/   — JSini 포털 공통 (시스템 관리, 역할·권한, 대시보드, AI)
--   views/funeral/  — 장례식장 MSA
--   views/helpdesk/ — 헬프데스크 MSA
--
-- 메뉴는 백엔드가 내려주므로 scom.system_menus.component 도 같이 옮긴다.
-- 반복 실행해도 안전하다(이미 옮겨진 경로에는 걸리지 않는다).

BEGIN;

-- 포털 공통
UPDATE scom.system_menus SET component = replace(component, '#/views/ai/',        '#/views/portal/ai/')        WHERE component LIKE '#/views/ai/%';
UPDATE scom.system_menus SET component = replace(component, '#/views/auth/',      '#/views/portal/auth/')      WHERE component LIKE '#/views/auth/%';
UPDATE scom.system_menus SET component = replace(component, '#/views/dashboard/', '#/views/portal/dashboard/') WHERE component LIKE '#/views/dashboard/%';
UPDATE scom.system_menus SET component = replace(component, '#/views/system/',    '#/views/portal/system/')    WHERE component LIKE '#/views/system/%';

-- 장례식장 MSA
UPDATE scom.system_menus SET component = replace(component, '#/views/building/', '#/views/funeral/building/') WHERE component LIKE '#/views/building/%';
UPDATE scom.system_menus SET component = replace(component, '#/views/help/',     '#/views/funeral/help/')     WHERE component LIKE '#/views/help/%';
UPDATE scom.system_menus SET component = replace(component, '#/views/info/',     '#/views/funeral/info/')     WHERE component LIKE '#/views/info/%';
UPDATE scom.system_menus SET component = replace(component, '#/views/setting/',  '#/views/funeral/setting/')  WHERE component LIKE '#/views/setting/%';
UPDATE scom.system_menus SET component = replace(component, '#/views/stat/',     '#/views/funeral/stat/')     WHERE component LIKE '#/views/stat/%';
UPDATE scom.system_menus SET component = replace(component, '#/views/status/',   '#/views/funeral/status/')   WHERE component LIKE '#/views/status/%';

-- 플레이어 다운로드는 포털 설정이 아니라 장례식장 전용 화면이라 함께 옮겼다.
UPDATE scom.system_menus
SET component = '#/views/funeral/player-download/index.vue'
WHERE component = '#/views/portal/system/player-download/index.vue';

COMMIT;

-- 확인: 옮겨지지 않고 남은 최상위 폴더가 있는지
SELECT split_part(replace(component, '#/views/', ''), '/', 1) AS 최상위폴더,
       count(*)
FROM scom.system_menus
WHERE component LIKE '#/views/%' AND is_deleted = false
GROUP BY 1
ORDER BY 2 DESC;
