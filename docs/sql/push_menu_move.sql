-- DB: jsiniportal (scom)
-- 푸시 화면(알림 묶음)을 헬프데스크에서 시스템 영역으로 옮긴다. 반복 실행 안전.
--
-- 푸시는 NotificationServer(포털 공통)가 담당하는 기능이라 화면 소속도
-- 포털(시스템)이 맞다 — 헬프데스크 이식 때 딸려 온 위치를 바로잡는 것.
-- 메뉴 id 는 그대로 둔다: role_menus·menu_favorites 연결이 id 기준이라 보존된다.
-- 화면 파일: fronts/apps/jsini-portal/src/views/portal/system/push/*
-- (데이터 출처는 아직 HelpDeskServer 의 /helpdesk/push API 다 —
--  NotificationServer 로의 API 이관은 별도 결정.)

UPDATE scom.system_menus SET
    pid = 'b24f9105-0dbf-4d30-a13d-b7997def1d5d',  -- System CATALOG
    title = '알림 관리',
    path = '/system/push',
    updated_at = now()
WHERE id = 'HD_PUSH';

UPDATE scom.system_menus SET
    path = '/system/push/dashboard',
    component = '#/views/portal/system/push/dashboard.vue',
    updated_at = now()
WHERE id = 'HD_PUSH_DASHBOARD';

UPDATE scom.system_menus SET
    path = '/system/push/logs',
    component = '#/views/portal/system/push/logs.vue',
    updated_at = now()
WHERE id = 'HD_PUSH_LOGS';

UPDATE scom.system_menus SET
    path = '/system/push/history',
    component = '#/views/portal/system/push/history.vue',
    updated_at = now()
WHERE id = 'HD_PUSH_HISTORY';

UPDATE scom.system_menus SET
    path = '/system/push/setting',
    component = '#/views/portal/system/push/setting.vue',
    updated_at = now()
WHERE id = 'HD_PUSH_SETTING';
