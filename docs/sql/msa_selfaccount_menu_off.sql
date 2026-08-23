-- 이식 시스템의 자체 계정관리 화면 정리 (결정 Q4)
--
-- 계정·권한은 JSini 관리 포털이 단독으로 맡는다. 그에 맞춰 이식본에 남아 있던
-- 자체 계정관리 화면을 걷어냈고, 그 메뉴를 비활성으로 내린다.
--
--   PM_COMM_USERGRP  프로젝트 사용자 그룹  — 자체 사용자 그룹 + 그룹별 화면 권한 관리 화면
--                    화면 파일(views/projmng/comm/user-group.vue)도 함께 삭제했다.
--
-- 지우지 않고 비활성(status=0)으로 두는 이유는 되돌리기 쉽게 하기 위해서다.
-- 되돌리려면 아래 주석의 SQL 을 실행하고 화면 파일을 git 에서 복원하면 된다.
--
-- 반복 실행해도 안전하다.

UPDATE scom.system_menus
   SET status = 0,
       updated_at = now(),
       updated_by = 'q4-selfaccount-removal'
 WHERE id = 'PM_COMM_USERGRP'
   AND status <> 0;

-- 확인
--   SELECT id, title, status FROM scom.system_menus WHERE id = 'PM_COMM_USERGRP';

-- ── 되돌리기 ────────────────────────────────────────────
-- UPDATE scom.system_menus SET status = 1 WHERE updated_by = 'q4-selfaccount-removal';
