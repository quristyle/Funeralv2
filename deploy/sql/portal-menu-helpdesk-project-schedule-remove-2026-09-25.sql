-- ============================================================
-- 헬프데스크 「프로젝트」·「일정」 묶음을 메뉴에서 걷어낸다 (scom 스키마 · 포털 DB)
-- ============================================================
--
-- 프로젝트·WBS·간트와 일정 달력은 헬프데스크에서 쓰지 않기로 했다.
-- 2026-09-25 에 화면과 그 화면들만 부르던 백엔드 경로까지 함께 지웠다.
--
-- 지우는 것은 묶음 둘과 그 아래 여덟이다.
--
--   HD_PRJ              프로젝트        (CATALOG, /helpdesk/project)
--   HD_PRJ_MNG          프로젝트 관리   (/helpdesk/project/manage)
--   HD_PRJ_WBS          WBS             (/helpdesk/project/wbs)
--   HD_PRJ_GANTT        WBS 간트        (/helpdesk/project/wbs-gantt)
--   HD_PRJ_GANTTVIEW    간트 보기       (/helpdesk/project/gantt)
--   HD_PRJ_READONLY     WBS 읽기전용    (/helpdesk/project/wbs-readonly)
--   HD_PRJ_INFO         프로젝트 정보   (/helpdesk/project/info)
--   HD_SCHEDULE         일정            (CATALOG, /helpdesk/schedule)
--   HD_SCHEDULE_ALL     전체 일정       (/helpdesk/schedule/all)
--   HD_SCHEDULE_MY      내 일정         (/helpdesk/schedule/my)
--
-- ⚠ **`HD_PRJ%` 로 훑지 않는다.** 그 앞자리를 쓰면서도 지우면 안 되는 줄이
--   하나 있다 — `HD_PRJ_DIAGRAM`(다이어그램, `/helpdesk/util/diagram`)이다.
--   이름만 프로젝트고 실제로는 「도구」 묶음에 달려 있어 이번에 남긴다.
--   그래서 아래는 전부 **열쇠를 하나씩 적는다.**
--
-- 화면 여덟(`ProjectManage`·`ProjectInfo`·`ProjectGantt`·`WbsList`·`WbsGantt`·
-- `WbsReadOnly`·`ScheduleAll`·`ScheduleMy`)을 함께 지웠으므로 메뉴만 남기면
-- 눌렀을 때 「준비 중」이 뜬다 — 그래서 지운다.
--
-- 백엔드도 같은 커밋에서 걷어냈다(`/api/project` · `/api/schedules` ·
-- `/api/wbslink` 묶음 전부, `/api/wbs` 의 평탄화·단건·등록·수정·삭제,
-- `dashboard/project-stats/{projectId}`). `GET /api/wbs`(트리)는 남겼다 —
-- 다이어그램 화면이 그림을 걸 항목을 여기서 고른다. `/api/teams` 는 통째로
-- 지웠다, 마지막 손님이던 프로젝트 관리 화면이 이번에 사라졌다.
--
-- 프로젝트·WBS·일정 **레코드**와 표는 건드리지 않았다.
--
-- 두 번 돌려도 안전하다.

-- ── 권한부터 ─────────────────────────────────────────────────
--
-- `role_menus.menu_id` 가 `system_menus.id` 를 참조한다(ON DELETE CASCADE 라
-- 메뉴만 지워도 따라 지워지지만, 무엇이 사라지는지 눈에 보이도록 먼저 지운다).
-- 2026-09-25 실측으로 열에 각각 다섯 줄, 모두 쉰 줄이 걸려 있었다.
DELETE FROM scom.role_menus
 WHERE menu_id IN ('HD_PRJ', 'HD_PRJ_MNG', 'HD_PRJ_WBS', 'HD_PRJ_GANTT',
                   'HD_PRJ_GANTTVIEW', 'HD_PRJ_READONLY', 'HD_PRJ_INFO',
                   'HD_SCHEDULE', 'HD_SCHEDULE_ALL', 'HD_SCHEDULE_MY');

-- 즐겨찾기도 같은 열쇠를 본다. 2026-09-25 에는 비어 있었지만 뒤늦게 눌러 둔
-- 사람이 있으면 여기서 걸린다.
DELETE FROM scom.menu_favorites
 WHERE menu_id IN ('HD_PRJ', 'HD_PRJ_MNG', 'HD_PRJ_WBS', 'HD_PRJ_GANTT',
                   'HD_PRJ_GANTTVIEW', 'HD_PRJ_READONLY', 'HD_PRJ_INFO',
                   'HD_SCHEDULE', 'HD_SCHEDULE_ALL', 'HD_SCHEDULE_MY');

-- ── 메뉴 ─────────────────────────────────────────────────────
--
-- 자식을 먼저 지운다. `pid` 는 외래키가 아니라 글자라 순서를 안 지켜도
-- 에러는 안 나지만, 중간에 멈췄을 때 부모 없는 자식이 남지 않게 한다.
DELETE FROM scom.system_menus
 WHERE id IN ('HD_PRJ_MNG', 'HD_PRJ_WBS', 'HD_PRJ_GANTT', 'HD_PRJ_GANTTVIEW',
              'HD_PRJ_READONLY', 'HD_PRJ_INFO', 'HD_SCHEDULE_ALL', 'HD_SCHEDULE_MY');

DELETE FROM scom.system_menus WHERE id IN ('HD_PRJ', 'HD_SCHEDULE');

-- 혹시 나중에 같은 자리에 다른 줄이 생겼다면 경로로도 한 번 훑는다.
-- (다이어그램은 `/helpdesk/util/diagram` 이라 여기에 안 걸린다.)
DELETE FROM scom.system_menus
 WHERE path LIKE '/helpdesk/project%' OR path LIKE '/helpdesk/schedule%';

-- ── 확인 ─────────────────────────────────────────────────────
-- 앞의 셋은 0, 마지막 `diagram` 은 1 이어야 한다.
--
--   SELECT (SELECT count(*) FROM scom.system_menus
--             WHERE path LIKE '/helpdesk/project%'
--                OR path LIKE '/helpdesk/schedule%')                        AS menu,
--          (SELECT count(*) FROM scom.role_menus
--             WHERE menu_id IN ('HD_PRJ','HD_PRJ_MNG','HD_PRJ_WBS','HD_PRJ_GANTT',
--                               'HD_PRJ_GANTTVIEW','HD_PRJ_READONLY','HD_PRJ_INFO',
--                               'HD_SCHEDULE','HD_SCHEDULE_ALL','HD_SCHEDULE_MY'))  AS role,
--          (SELECT count(*) FROM scom.menu_favorites
--             WHERE menu_id IN ('HD_PRJ','HD_PRJ_MNG','HD_PRJ_WBS','HD_PRJ_GANTT',
--                               'HD_PRJ_GANTTVIEW','HD_PRJ_READONLY','HD_PRJ_INFO',
--                               'HD_SCHEDULE','HD_SCHEDULE_ALL','HD_SCHEDULE_MY'))  AS fav,
--          (SELECT count(*) FROM scom.system_menus
--             WHERE id = 'HD_PRJ_DIAGRAM')                                  AS diagram;
