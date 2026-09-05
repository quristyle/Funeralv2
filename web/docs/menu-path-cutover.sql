-- ============================================================================
-- Blazor 포털 컷오버 — scom.system_menus.path 일괄 변경
-- ============================================================================
-- 대상 DB   : jsiniportal, 스키마 scom (운영, 포트 31015)
-- 실행 시점 : 운영 Blazor 포털(:5557) 컷오버 배포와 **동시에** 실행한다.
--             ⚠ 이 스크립트를 실행하는 순간 옛 Vue 포털(:5555)의 메뉴가 깨진다.
--             (Vue 라우트는 DB path 기준으로 생성되므로 되돌리려면 하단 롤백 실행)
-- 정본 문서 : web/docs/menu-route-map.md (2026-09-05 운영 179건 실측 기준)
-- 작성일    : 2026-09-05
--
-- 조사 결과 (path 를 참조하는 다른 테이블 없음 — 이 파일 하나로 충분):
--   * role_menus.menu_id      → system_menus.id FK. 권한은 id 매핑이라 path 변경 무관.
--   * menu_favorites.menu_id  → system_menus.id FK. 즐겨찾기도 id 매핑.
--   * /auth/menu/permissions (AuthServer MenuService.GetMenuPermissionsAsync) 는
--     role_menus 를 menu id 로 묶은 뒤 system_menus.path 를 그때그때 읽어 내려준다.
--     즉 path 를 바꾸면 권한 응답의 path 도 즉시 새 값으로 나간다. 별도 작업 없음.
--   * account_preferences.payload(jsonb) 에 옛 path 포함 0건 실측 (2026-09-05).
--   * system_menus.redirect 는 전 행 비어 있음. component 는 Blazor 가 읽지 않음.
--   * path 에 유니크 인덱스 없음(PK 는 id) — 그래도 중복 path 는 만들지 않는다.
--
-- 변경 내용: 경로 변경 69건 (Funeral 28+그룹9, Admin 23+그룹6, Site 2+그룹1)
--            + 대시보드 3건 hide_in_menu = true
-- HelpDesk(54)·ProjMng(38)·LifeEnv(15) 는 접두사가 이미 일치 — 변경 없음.
--
-- 파라미터 라우트 2건은 그대로 둔다 (Blazor 는 @page "/request/detail/{id}" 로
-- 선언하지만 DB 는 메뉴 노출·권한용이므로 vben 표기 :id 를 유지):
--   HD_REQ_DETAIL /helpdesk/request/detail/:id
--   HD_REQ_EDIT   /helpdesk/request/edit/:id
-- ============================================================================

BEGIN;

-- 사전 안전장치: 대상 69건이 옛 path 그대로 있어야 진행한다.
-- (이미 실행했거나 누가 손댔으면 여기서 멈춘다)
DO $$
DECLARE cnt int;
BEGIN
  SELECT count(*) INTO cnt FROM scom.system_menus WHERE path IN (
    '/building/info','/building/music-build','/building/floor','/building/room',
    '/building/device','/building/video','/building/audio','/device/background',
    '/decoration','/room_status','/building/deceased','/info/room-history',
    '/info/deceased-search','/info/my-info','/info/preview','/stat/billing',
    '/stat/room-usage','/status/funeral-info','/status/funeral-status',
    '/status/deceased-status','/status/simple','/status/mobile','/help/qna',
    '/help/faq','/help/archive','/setting/environment','/setting/work-options',
    '/system/player-download',
    '/funerals','/building','/building/source','/deceased','/info','/stat',
    '/status','/help','/setting',
    '/system/common-code','/system/metadata_manager','/system/i18n',
    '/system/account','/system/menu','/portal/notice','/portal/release',
    '/system/role-map','/auth/user-role','/auth/menu-role','/company/org-chart',
    '/system/company','/system/dept','/company/user','/system/push/dashboard',
    '/system/push/logs','/system/push/history','/system/push/setting',
    '/system/server-status','/system/server-status/jin114','/system/deploy-status',
    '/system/player-release','/profile',
    '/system','/common','/auth','/company','/system/push','/system/status',
    '/ai/chat','/company/site-inquiries','/devs');
  IF cnt <> 69 THEN
    RAISE EXCEPTION '대상 옛 path 가 69건이 아니라 %건 — 이미 실행했거나 메뉴가 바뀌었다. 중단.', cnt;
  END IF;
END $$;

-- ---------------------------------------------------------------------------
-- Funeral 리프 28건 (/funeral 앱, :5561)
-- ---------------------------------------------------------------------------
UPDATE scom.system_menus SET path='/funeral/building/info',        updated_at=now(), updated_by='menu-path-cutover' WHERE id='BUILDING_INFO'   AND path='/building/info';
UPDATE scom.system_menus SET path='/funeral/building/music-build', updated_at=now(), updated_by='menu-path-cutover' WHERE id='MUSIC_BUILD'     AND path='/building/music-build';
UPDATE scom.system_menus SET path='/funeral/building/floor',       updated_at=now(), updated_by='menu-path-cutover' WHERE id='FLOOR'           AND path='/building/floor';
UPDATE scom.system_menus SET path='/funeral/building/room',        updated_at=now(), updated_by='menu-path-cutover' WHERE id='ROOM'            AND path='/building/room';
UPDATE scom.system_menus SET path='/funeral/building/device',      updated_at=now(), updated_by='menu-path-cutover' WHERE id='DEVICE'          AND path='/building/device';
UPDATE scom.system_menus SET path='/funeral/building/video',       updated_at=now(), updated_by='menu-path-cutover' WHERE id='VIDEO'           AND path='/building/video';
UPDATE scom.system_menus SET path='/funeral/building/audio',       updated_at=now(), updated_by='menu-path-cutover' WHERE id='AUDIO'           AND path='/building/audio';
UPDATE scom.system_menus SET path='/funeral/building/background',  updated_at=now(), updated_by='menu-path-cutover' WHERE id='7b88d600-cd29-4e15-80c4-f329e7a2e18f' AND path='/device/background';
UPDATE scom.system_menus SET path='/funeral/building/decoration',  updated_at=now(), updated_by='menu-path-cutover' WHERE id='158bec21-c00b-4f3b-b03d-4c59f2a70eb8' AND path='/decoration';
UPDATE scom.system_menus SET path='/funeral/room-status',          updated_at=now(), updated_by='menu-path-cutover' WHERE id='c4ea307e-75a1-4317-af58-e13499cfbc12' AND path='/room_status';
UPDATE scom.system_menus SET path='/funeral/deceased',             updated_at=now(), updated_by='menu-path-cutover' WHERE id='DECEASED'        AND path='/building/deceased';
UPDATE scom.system_menus SET path='/funeral/info/room-history',    updated_at=now(), updated_by='menu-path-cutover' WHERE id='ROOM_HISTORY'    AND path='/info/room-history';
UPDATE scom.system_menus SET path='/funeral/info/deceased-search', updated_at=now(), updated_by='menu-path-cutover' WHERE id='DECEASED_SEARCH' AND path='/info/deceased-search';
UPDATE scom.system_menus SET path='/funeral/info/my-info',         updated_at=now(), updated_by='menu-path-cutover' WHERE id='MY_INFO'         AND path='/info/my-info';
UPDATE scom.system_menus SET path='/funeral/info/preview',         updated_at=now(), updated_by='menu-path-cutover' WHERE id='PREVIEW'         AND path='/info/preview';
UPDATE scom.system_menus SET path='/funeral/stat/billing',         updated_at=now(), updated_by='menu-path-cutover' WHERE id='BILLING'         AND path='/stat/billing';
UPDATE scom.system_menus SET path='/funeral/stat/room-usage',      updated_at=now(), updated_by='menu-path-cutover' WHERE id='ROOM_USAGE'      AND path='/stat/room-usage';
UPDATE scom.system_menus SET path='/funeral/status/funeral-info',    updated_at=now(), updated_by='menu-path-cutover' WHERE id='FUNERAL_INFO'    AND path='/status/funeral-info';
UPDATE scom.system_menus SET path='/funeral/status/funeral-status',  updated_at=now(), updated_by='menu-path-cutover' WHERE id='FUNERAL_STATUS'  AND path='/status/funeral-status';
UPDATE scom.system_menus SET path='/funeral/status/deceased-status', updated_at=now(), updated_by='menu-path-cutover' WHERE id='DECEASED_STATUS' AND path='/status/deceased-status';
UPDATE scom.system_menus SET path='/funeral/status/simple',        updated_at=now(), updated_by='menu-path-cutover' WHERE id='FUNERAL_SIMPLE'  AND path='/status/simple';
UPDATE scom.system_menus SET path='/funeral/status/mobile',        updated_at=now(), updated_by='menu-path-cutover' WHERE id='FUNERAL_MOBILE'  AND path='/status/mobile';
UPDATE scom.system_menus SET path='/funeral/help/qna',             updated_at=now(), updated_by='menu-path-cutover' WHERE id='QNA'             AND path='/help/qna';
UPDATE scom.system_menus SET path='/funeral/help/faq',             updated_at=now(), updated_by='menu-path-cutover' WHERE id='FAQ'             AND path='/help/faq';
UPDATE scom.system_menus SET path='/funeral/help/archive',         updated_at=now(), updated_by='menu-path-cutover' WHERE id='ARCHIVE'         AND path='/help/archive';
UPDATE scom.system_menus SET path='/funeral/setting/environment',  updated_at=now(), updated_by='menu-path-cutover' WHERE id='ENV_SETTING'     AND path='/setting/environment';
UPDATE scom.system_menus SET path='/funeral/setting/work-options', updated_at=now(), updated_by='menu-path-cutover' WHERE id='WORK_OPTIONS'    AND path='/setting/work-options';
UPDATE scom.system_menus SET path='/funeral/player-download',      updated_at=now(), updated_by='menu-path-cutover' WHERE id='c5ab90a4-4355-4404-9ea0-51c94ef2634c' AND path='/system/player-download';

-- ---------------------------------------------------------------------------
-- Funeral 그룹 9건 — 링크 없는 트리 노드. 자식과 같은 /funeral 접두사로 정규화.
-- ---------------------------------------------------------------------------
UPDATE scom.system_menus SET path='/funeral',                 updated_at=now(), updated_by='menu-path-cutover' WHERE id='7068a867-99bd-47c2-8b5f-4b2b0574a431' AND path='/funerals';
UPDATE scom.system_menus SET path='/funeral/building',        updated_at=now(), updated_by='menu-path-cutover' WHERE id='BUILDING' AND path='/building';
UPDATE scom.system_menus SET path='/funeral/building/source', updated_at=now(), updated_by='menu-path-cutover' WHERE id='SOURCE'   AND path='/building/source';
-- ⚠ 그룹 /deceased 를 규칙대로 /funeral/deceased 로 하면 위 리프(/building/deceased
--    → /funeral/deceased)와 path 가 중복된다. path 에 유니크 제약은 없지만 권한 조회
--    (/auth/menu/permissions 의 path 매칭)가 흔들리므로 그룹만 -group 을 붙여 비켜 둔다.
--    (이 그룹은 자식이 리프 하나뿐 — 컷오버 뒤 트리 정리 때 흡수 검토)
UPDATE scom.system_menus SET path='/funeral/deceased-group',  updated_at=now(), updated_by='menu-path-cutover' WHERE id='6726e39d-7ecf-4744-8213-fc967d984c78' AND path='/deceased';
UPDATE scom.system_menus SET path='/funeral/info',            updated_at=now(), updated_by='menu-path-cutover' WHERE id='INFO'     AND path='/info';
UPDATE scom.system_menus SET path='/funeral/stat',            updated_at=now(), updated_by='menu-path-cutover' WHERE id='STAT'     AND path='/stat';
UPDATE scom.system_menus SET path='/funeral/status',          updated_at=now(), updated_by='menu-path-cutover' WHERE id='STATUS'   AND path='/status';
UPDATE scom.system_menus SET path='/funeral/help',            updated_at=now(), updated_by='menu-path-cutover' WHERE id='HELP'     AND path='/help';
-- SETTING 그룹 밑에 admin 소속 리프 둘(/profile, /system/push/setting)이 매달려 있다.
-- path 만 바꾸므로 트리(pid)는 그대로 — 사이드바 소속은 컷오버 뒤 pid 정리로 해결한다.
UPDATE scom.system_menus SET path='/funeral/setting',         updated_at=now(), updated_by='menu-path-cutover' WHERE id='SETTING'  AND path='/setting';

-- ---------------------------------------------------------------------------
-- Admin 리프 23건 (/admin 앱, :5563)
-- ---------------------------------------------------------------------------
UPDATE scom.system_menus SET path='/admin/system/common-code', updated_at=now(), updated_by='menu-path-cutover' WHERE id='23c99477-64a5-4ef0-bae5-dcb039076ea7' AND path='/system/common-code';
UPDATE scom.system_menus SET path='/admin/system/metadata',    updated_at=now(), updated_by='menu-path-cutover' WHERE id='HELP_MNG' AND path='/system/metadata_manager';
UPDATE scom.system_menus SET path='/admin/system/i18n',        updated_at=now(), updated_by='menu-path-cutover' WHERE id='be491180-1998-4364-85a6-dcac04ac5959' AND path='/system/i18n';
UPDATE scom.system_menus SET path='/admin/system/account',     updated_at=now(), updated_by='menu-path-cutover' WHERE id='ACCOUNT' AND path='/system/account';
UPDATE scom.system_menus SET path='/admin/system/menu',        updated_at=now(), updated_by='menu-path-cutover' WHERE id='153225e9-82c9-4403-a6ce-d428a052020b' AND path='/system/menu';
UPDATE scom.system_menus SET path='/admin/notice',             updated_at=now(), updated_by='menu-path-cutover' WHERE id='PORTAL_NOTICE'  AND path='/portal/notice';
UPDATE scom.system_menus SET path='/admin/release',            updated_at=now(), updated_by='menu-path-cutover' WHERE id='PORTAL_RELEASE' AND path='/portal/release';
UPDATE scom.system_menus SET path='/admin/auth/role',          updated_at=now(), updated_by='menu-path-cutover' WHERE id='f4e8c3d5-e2ac-453a-8b44-2d47ad5703d0' AND path='/system/role-map';
UPDATE scom.system_menus SET path='/admin/auth/user-role',     updated_at=now(), updated_by='menu-path-cutover' WHERE id='USER_ROLE' AND path='/auth/user-role';
UPDATE scom.system_menus SET path='/admin/auth/menu-role',     updated_at=now(), updated_by='menu-path-cutover' WHERE id='MENU_ROLE' AND path='/auth/menu-role';
UPDATE scom.system_menus SET path='/admin/company/org-chart',  updated_at=now(), updated_by='menu-path-cutover' WHERE id='52e04f50-dd9f-4c4b-807d-c818628c38b0' AND path='/company/org-chart';
UPDATE scom.system_menus SET path='/admin/company/list',       updated_at=now(), updated_by='menu-path-cutover' WHERE id='c1a2b3c4-d5e6-4f7g-8h9i-j0k1l2m3n4o5' AND path='/system/company';
UPDATE scom.system_menus SET path='/admin/company/dept',       updated_at=now(), updated_by='menu-path-cutover' WHERE id='5b62b971-55b5-401d-aa6d-afb8f4bc9249' AND path='/system/dept';
UPDATE scom.system_menus SET path='/admin/company/user',       updated_at=now(), updated_by='menu-path-cutover' WHERE id='4a4a34a0-5718-4375-9d62-dbdcafc9028e' AND path='/company/user';
UPDATE scom.system_menus SET path='/admin/push/dashboard',     updated_at=now(), updated_by='menu-path-cutover' WHERE id='HD_PUSH_DASHBOARD' AND path='/system/push/dashboard';
UPDATE scom.system_menus SET path='/admin/push/logs',          updated_at=now(), updated_by='menu-path-cutover' WHERE id='HD_PUSH_LOGS'      AND path='/system/push/logs';
UPDATE scom.system_menus SET path='/admin/push/history',       updated_at=now(), updated_by='menu-path-cutover' WHERE id='HD_PUSH_HISTORY'   AND path='/system/push/history';
UPDATE scom.system_menus SET path='/admin/push/setting',       updated_at=now(), updated_by='menu-path-cutover' WHERE id='HD_PUSH_SETTING'   AND path='/system/push/setting';
UPDATE scom.system_menus SET path='/admin/status/server',      updated_at=now(), updated_by='menu-path-cutover' WHERE id='65cbdd16-0760-4c2f-9005-728f10e6a6ee' AND path='/system/server-status';
UPDATE scom.system_menus SET path='/admin/status/jin114',      updated_at=now(), updated_by='menu-path-cutover' WHERE id='11f3d315-0a10-48c3-825c-027358d61b4c' AND path='/system/server-status/jin114';
UPDATE scom.system_menus SET path='/admin/status/deploy',      updated_at=now(), updated_by='menu-path-cutover' WHERE id='SYS_DEPLOY_STATUS' AND path='/system/deploy-status';
-- menu-player-release 는 pid 가 장례식장 그룹(/funerals)이지만 정본 대응표는 Admin 소속.
UPDATE scom.system_menus SET path='/admin/status/player-release', updated_at=now(), updated_by='menu-path-cutover' WHERE id='menu-player-release' AND path='/system/player-release';
UPDATE scom.system_menus SET path='/admin/profile',            updated_at=now(), updated_by='menu-path-cutover' WHERE id='e56930f6-c25d-4898-aaee-56644016e9ee' AND path='/profile';

-- ---------------------------------------------------------------------------
-- Admin 그룹 6건 — 자식의 새 경로와 일관되게 /admin 아래로.
-- ---------------------------------------------------------------------------
UPDATE scom.system_menus SET path='/admin/system',        updated_at=now(), updated_by='menu-path-cutover' WHERE id='b24f9105-0dbf-4d30-a13d-b7997def1d5d' AND path='/system';
-- /common 그룹의 자식(common-code·i18n·metadata)은 전부 /admin/system/* 로 갔다.
-- 부모 그룹(/system→/admin/system) 아래 서브그룹이므로 /admin/system/common 으로.
UPDATE scom.system_menus SET path='/admin/system/common', updated_at=now(), updated_by='menu-path-cutover' WHERE id='COMMON' AND path='/common';
UPDATE scom.system_menus SET path='/admin/auth',          updated_at=now(), updated_by='menu-path-cutover' WHERE id='AUTH' AND path='/auth';
UPDATE scom.system_menus SET path='/admin/company',       updated_at=now(), updated_by='menu-path-cutover' WHERE id='0f2843e6-4def-4cd4-9742-f814ce230269' AND path='/company';
UPDATE scom.system_menus SET path='/admin/push',          updated_at=now(), updated_by='menu-path-cutover' WHERE id='HD_PUSH' AND path='/system/push';
-- /system/status 그룹 밑에는 projmng 소속 EMBEDDED 2건도 매달려 있다(pid 그대로 둠).
UPDATE scom.system_menus SET path='/admin/status',        updated_at=now(), updated_by='menu-path-cutover' WHERE id='fef18dc3-9fdf-4e7a-bb0a-1afba9bd97b5' AND path='/system/status';

-- ---------------------------------------------------------------------------
-- Site 리프 2건 + 그룹 1건 (/site 앱, :5564)
-- ---------------------------------------------------------------------------
UPDATE scom.system_menus SET path='/site/ai/chat',   updated_at=now(), updated_by='menu-path-cutover' WHERE id='2cf9af85-53b3-4bd3-b193-418b12b405bc' AND path='/ai/chat';
-- site-inquiries 의 pid 는 admin 의 /company 그룹 — path 만 site 소속으로 바꾼다.
UPDATE scom.system_menus SET path='/site/inquiries', updated_at=now(), updated_by='menu-path-cutover' WHERE id='SITE_INQUIRY' AND path='/company/site-inquiries';
-- /devs 그룹(정리 대상, 자식은 /site/ai/chat 하나) — 앱 접두사로 정규화.
UPDATE scom.system_menus SET path='/site',           updated_at=now(), updated_by='menu-path-cutover' WHERE id='dbb58d38-5b53-4703-9e3c-e61cf3024cf3' AND path='/devs';

-- ---------------------------------------------------------------------------
-- vben 대시보드 3건 — path 는 두고 메뉴에서만 숨긴다.
--   /workspace : 셸(:5557) 홈 화면으로 흡수됨 — 별도 메뉴 불필요
--   /analytics : vben 템플릿 데모 화면 — Blazor 로 이관하지 않음
--   /dashboard : 위 둘을 담던 그룹 — 자식이 모두 숨으므로 함께 숨김
-- 행을 지우지 않는 이유: role_menus 가 id 로 물려 있고, 되돌리기(롤백)가 쉬워야 한다.
-- ---------------------------------------------------------------------------
UPDATE scom.system_menus SET hide_in_menu=true, updated_at=now(), updated_by='menu-path-cutover' WHERE id='f4473942-6c5b-4b11-a37f-90a385f07788' AND path='/dashboard';
UPDATE scom.system_menus SET hide_in_menu=true, updated_at=now(), updated_by='menu-path-cutover' WHERE id='1ccaa136-d133-4e81-8fa9-d8e4abd9b5fc' AND path='/analytics';
UPDATE scom.system_menus SET hide_in_menu=true, updated_at=now(), updated_by='menu-path-cutover' WHERE id='03af6880-9f77-4149-97c8-6fff51414de8' AND path='/workspace';

COMMIT;

-- ============================================================================
-- [실행 전 검증] — 아래가 69 | 3 이어야 실행 대상이 맞다 (위 DO 블록도 같은 검사)
-- ============================================================================
-- select
--   (select count(*) from scom.system_menus where path in (
--     '/building/info','/building/music-build','/building/floor','/building/room',
--     '/building/device','/building/video','/building/audio','/device/background',
--     '/decoration','/room_status','/building/deceased','/info/room-history',
--     '/info/deceased-search','/info/my-info','/info/preview','/stat/billing',
--     '/stat/room-usage','/status/funeral-info','/status/funeral-status',
--     '/status/deceased-status','/status/simple','/status/mobile','/help/qna',
--     '/help/faq','/help/archive','/setting/environment','/setting/work-options',
--     '/system/player-download',
--     '/funerals','/building','/building/source','/deceased','/info','/stat',
--     '/status','/help','/setting',
--     '/system/common-code','/system/metadata_manager','/system/i18n',
--     '/system/account','/system/menu','/portal/notice','/portal/release',
--     '/system/role-map','/auth/user-role','/auth/menu-role','/company/org-chart',
--     '/system/company','/system/dept','/company/user','/system/push/dashboard',
--     '/system/push/logs','/system/push/history','/system/push/setting',
--     '/system/server-status','/system/server-status/jin114','/system/deploy-status',
--     '/system/player-release','/profile',
--     '/system','/common','/auth','/company','/system/push','/system/status',
--     '/ai/chat','/company/site-inquiries','/devs')) as "옛path(69여야)",
--   (select count(*) from scom.system_menus
--     where path in ('/dashboard','/analytics','/workspace')
--       and hide_in_menu=false) as "대시보드(3이어야)";

-- ============================================================================
-- [실행 후 검증]
-- ============================================================================
-- ① 옛 path 잔존 0건 확인 (위 [실행 전 검증]의 첫 select 를 다시 실행 → 0 이어야 한다)
--
-- ② 새 접두사 집계 — /funeral 38(리프28+그룹10... 리프28+그룹9=37 +room-status 포함)
--    정확히는 /funeral·/funeral/% 37, /admin·/admin/% 29, /site·/site/% 3:
-- select
--   count(*) filter (where path='/funeral' or path like '/funeral/%') as funeral_37,
--   count(*) filter (where path='/admin'   or path like '/admin/%')   as admin_29,
--   count(*) filter (where path='/site'    or path like '/site/%')    as site_3
-- from scom.system_menus;
--
-- ③ path 중복 0건 확인 (권한 조회가 path 매칭이므로 중복이 있으면 안 된다):
-- select path, count(*) from scom.system_menus group by path having count(*) > 1;
--
-- ④ 대시보드 3건 숨김 확인:
-- select id, path, hide_in_menu from scom.system_menus
--  where path in ('/dashboard','/analytics','/workspace');  -- 전부 true

-- ============================================================================
-- [롤백] — Vue 포털로 되돌릴 때 아래 블록 전체를 실행 (역방향 UPDATE 72건)
-- ============================================================================
-- BEGIN;
-- UPDATE scom.system_menus SET path='/building/info'        WHERE id='BUILDING_INFO'   AND path='/funeral/building/info';
-- UPDATE scom.system_menus SET path='/building/music-build' WHERE id='MUSIC_BUILD'     AND path='/funeral/building/music-build';
-- UPDATE scom.system_menus SET path='/building/floor'       WHERE id='FLOOR'           AND path='/funeral/building/floor';
-- UPDATE scom.system_menus SET path='/building/room'        WHERE id='ROOM'            AND path='/funeral/building/room';
-- UPDATE scom.system_menus SET path='/building/device'      WHERE id='DEVICE'          AND path='/funeral/building/device';
-- UPDATE scom.system_menus SET path='/building/video'       WHERE id='VIDEO'           AND path='/funeral/building/video';
-- UPDATE scom.system_menus SET path='/building/audio'       WHERE id='AUDIO'           AND path='/funeral/building/audio';
-- UPDATE scom.system_menus SET path='/device/background'    WHERE id='7b88d600-cd29-4e15-80c4-f329e7a2e18f' AND path='/funeral/building/background';
-- UPDATE scom.system_menus SET path='/decoration'           WHERE id='158bec21-c00b-4f3b-b03d-4c59f2a70eb8' AND path='/funeral/building/decoration';
-- UPDATE scom.system_menus SET path='/room_status'          WHERE id='c4ea307e-75a1-4317-af58-e13499cfbc12' AND path='/funeral/room-status';
-- UPDATE scom.system_menus SET path='/building/deceased'    WHERE id='DECEASED'        AND path='/funeral/deceased';
-- UPDATE scom.system_menus SET path='/info/room-history'    WHERE id='ROOM_HISTORY'    AND path='/funeral/info/room-history';
-- UPDATE scom.system_menus SET path='/info/deceased-search' WHERE id='DECEASED_SEARCH' AND path='/funeral/info/deceased-search';
-- UPDATE scom.system_menus SET path='/info/my-info'         WHERE id='MY_INFO'         AND path='/funeral/info/my-info';
-- UPDATE scom.system_menus SET path='/info/preview'         WHERE id='PREVIEW'         AND path='/funeral/info/preview';
-- UPDATE scom.system_menus SET path='/stat/billing'         WHERE id='BILLING'         AND path='/funeral/stat/billing';
-- UPDATE scom.system_menus SET path='/stat/room-usage'      WHERE id='ROOM_USAGE'      AND path='/funeral/stat/room-usage';
-- UPDATE scom.system_menus SET path='/status/funeral-info'    WHERE id='FUNERAL_INFO'    AND path='/funeral/status/funeral-info';
-- UPDATE scom.system_menus SET path='/status/funeral-status'  WHERE id='FUNERAL_STATUS'  AND path='/funeral/status/funeral-status';
-- UPDATE scom.system_menus SET path='/status/deceased-status' WHERE id='DECEASED_STATUS' AND path='/funeral/status/deceased-status';
-- UPDATE scom.system_menus SET path='/status/simple'        WHERE id='FUNERAL_SIMPLE'  AND path='/funeral/status/simple';
-- UPDATE scom.system_menus SET path='/status/mobile'        WHERE id='FUNERAL_MOBILE'  AND path='/funeral/status/mobile';
-- UPDATE scom.system_menus SET path='/help/qna'             WHERE id='QNA'             AND path='/funeral/help/qna';
-- UPDATE scom.system_menus SET path='/help/faq'             WHERE id='FAQ'             AND path='/funeral/help/faq';
-- UPDATE scom.system_menus SET path='/help/archive'         WHERE id='ARCHIVE'         AND path='/funeral/help/archive';
-- UPDATE scom.system_menus SET path='/setting/environment'  WHERE id='ENV_SETTING'     AND path='/funeral/setting/environment';
-- UPDATE scom.system_menus SET path='/setting/work-options' WHERE id='WORK_OPTIONS'    AND path='/funeral/setting/work-options';
-- UPDATE scom.system_menus SET path='/system/player-download' WHERE id='c5ab90a4-4355-4404-9ea0-51c94ef2634c' AND path='/funeral/player-download';
-- UPDATE scom.system_menus SET path='/funerals'             WHERE id='7068a867-99bd-47c2-8b5f-4b2b0574a431' AND path='/funeral';
-- UPDATE scom.system_menus SET path='/building'             WHERE id='BUILDING' AND path='/funeral/building';
-- UPDATE scom.system_menus SET path='/building/source'      WHERE id='SOURCE'   AND path='/funeral/building/source';
-- UPDATE scom.system_menus SET path='/deceased'             WHERE id='6726e39d-7ecf-4744-8213-fc967d984c78' AND path='/funeral/deceased-group';
-- UPDATE scom.system_menus SET path='/info'                 WHERE id='INFO'     AND path='/funeral/info';
-- UPDATE scom.system_menus SET path='/stat'                 WHERE id='STAT'     AND path='/funeral/stat';
-- UPDATE scom.system_menus SET path='/status'               WHERE id='STATUS'   AND path='/funeral/status';
-- UPDATE scom.system_menus SET path='/help'                 WHERE id='HELP'     AND path='/funeral/help';
-- UPDATE scom.system_menus SET path='/setting'              WHERE id='SETTING'  AND path='/funeral/setting';
-- UPDATE scom.system_menus SET path='/system/common-code'   WHERE id='23c99477-64a5-4ef0-bae5-dcb039076ea7' AND path='/admin/system/common-code';
-- UPDATE scom.system_menus SET path='/system/metadata_manager' WHERE id='HELP_MNG' AND path='/admin/system/metadata';
-- UPDATE scom.system_menus SET path='/system/i18n'          WHERE id='be491180-1998-4364-85a6-dcac04ac5959' AND path='/admin/system/i18n';
-- UPDATE scom.system_menus SET path='/system/account'       WHERE id='ACCOUNT' AND path='/admin/system/account';
-- UPDATE scom.system_menus SET path='/system/menu'          WHERE id='153225e9-82c9-4403-a6ce-d428a052020b' AND path='/admin/system/menu';
-- UPDATE scom.system_menus SET path='/portal/notice'        WHERE id='PORTAL_NOTICE'  AND path='/admin/notice';
-- UPDATE scom.system_menus SET path='/portal/release'       WHERE id='PORTAL_RELEASE' AND path='/admin/release';
-- UPDATE scom.system_menus SET path='/system/role-map'      WHERE id='f4e8c3d5-e2ac-453a-8b44-2d47ad5703d0' AND path='/admin/auth/role';
-- UPDATE scom.system_menus SET path='/auth/user-role'       WHERE id='USER_ROLE' AND path='/admin/auth/user-role';
-- UPDATE scom.system_menus SET path='/auth/menu-role'       WHERE id='MENU_ROLE' AND path='/admin/auth/menu-role';
-- UPDATE scom.system_menus SET path='/company/org-chart'    WHERE id='52e04f50-dd9f-4c4b-807d-c818628c38b0' AND path='/admin/company/org-chart';
-- UPDATE scom.system_menus SET path='/system/company'       WHERE id='c1a2b3c4-d5e6-4f7g-8h9i-j0k1l2m3n4o5' AND path='/admin/company/list';
-- UPDATE scom.system_menus SET path='/system/dept'          WHERE id='5b62b971-55b5-401d-aa6d-afb8f4bc9249' AND path='/admin/company/dept';
-- UPDATE scom.system_menus SET path='/company/user'         WHERE id='4a4a34a0-5718-4375-9d62-dbdcafc9028e' AND path='/admin/company/user';
-- UPDATE scom.system_menus SET path='/system/push/dashboard' WHERE id='HD_PUSH_DASHBOARD' AND path='/admin/push/dashboard';
-- UPDATE scom.system_menus SET path='/system/push/logs'     WHERE id='HD_PUSH_LOGS'    AND path='/admin/push/logs';
-- UPDATE scom.system_menus SET path='/system/push/history'  WHERE id='HD_PUSH_HISTORY' AND path='/admin/push/history';
-- UPDATE scom.system_menus SET path='/system/push/setting'  WHERE id='HD_PUSH_SETTING' AND path='/admin/push/setting';
-- UPDATE scom.system_menus SET path='/system/server-status' WHERE id='65cbdd16-0760-4c2f-9005-728f10e6a6ee' AND path='/admin/status/server';
-- UPDATE scom.system_menus SET path='/system/server-status/jin114' WHERE id='11f3d315-0a10-48c3-825c-027358d61b4c' AND path='/admin/status/jin114';
-- UPDATE scom.system_menus SET path='/system/deploy-status' WHERE id='SYS_DEPLOY_STATUS' AND path='/admin/status/deploy';
-- UPDATE scom.system_menus SET path='/system/player-release' WHERE id='menu-player-release' AND path='/admin/status/player-release';
-- UPDATE scom.system_menus SET path='/profile'              WHERE id='e56930f6-c25d-4898-aaee-56644016e9ee' AND path='/admin/profile';
-- UPDATE scom.system_menus SET path='/system'               WHERE id='b24f9105-0dbf-4d30-a13d-b7997def1d5d' AND path='/admin/system';
-- UPDATE scom.system_menus SET path='/common'               WHERE id='COMMON' AND path='/admin/system/common';
-- UPDATE scom.system_menus SET path='/auth'                 WHERE id='AUTH' AND path='/admin/auth';
-- UPDATE scom.system_menus SET path='/company'              WHERE id='0f2843e6-4def-4cd4-9742-f814ce230269' AND path='/admin/company';
-- UPDATE scom.system_menus SET path='/system/push'          WHERE id='HD_PUSH' AND path='/admin/push';
-- UPDATE scom.system_menus SET path='/system/status'        WHERE id='fef18dc3-9fdf-4e7a-bb0a-1afba9bd97b5' AND path='/admin/status';
-- UPDATE scom.system_menus SET path='/ai/chat'              WHERE id='2cf9af85-53b3-4bd3-b193-418b12b405bc' AND path='/site/ai/chat';
-- UPDATE scom.system_menus SET path='/company/site-inquiries' WHERE id='SITE_INQUIRY' AND path='/site/inquiries';
-- UPDATE scom.system_menus SET path='/devs'                 WHERE id='dbb58d38-5b53-4703-9e3c-e61cf3024cf3' AND path='/site';
-- UPDATE scom.system_menus SET hide_in_menu=false WHERE id='f4473942-6c5b-4b11-a37f-90a385f07788' AND path='/dashboard';
-- UPDATE scom.system_menus SET hide_in_menu=false WHERE id='1ccaa136-d133-4e81-8fa9-d8e4abd9b5fc' AND path='/analytics';
-- UPDATE scom.system_menus SET hide_in_menu=false WHERE id='03af6880-9f77-4149-97c8-6fff51414de8' AND path='/workspace';
-- COMMIT;
