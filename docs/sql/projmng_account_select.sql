-- ============================================================
-- 담당자 셀렉트를 포털 계정으로 바꾼다 (프로젝트관리 2단계)
-- ============================================================
--
-- 배경: [36-projmng-tobe-feature-cleanup.md] 4.2
--   프로젝트관리 [할일]·[할일 정산 현황]의 담당자 드롭다운은
--   `sp_projCommon` 의 `user` 코드를 읽고, 그 코드는 `projmng.dev_user` 를 본다.
--   `dev_user` 를 걷어내면 두 화면의 드롭다운이 빈다.
--
--   사용자의 정본은 포털 계정 한 곳이므로, 드롭다운도 거기서 읽게 한다.
--
-- 값은 `loginId` 를 쓴다. 이게 중요하다 —
--   `projmng.home_todo.target_user` 에 쌓여 있는 값이 `hsstyle` · `jjstyle` 처럼
--   **로그인 아이디**다. 계정의 UUID(`id`)를 값으로 쓰면 기존 자료와 어긋난다.
--
-- ASIS 와 달라지는 점: 목록이 9명에서 43명이 된다.
--   ASIS `dev_user` 에 있던 9명은 전원 포털 계정에 같은 아이디로 존재하므로
--   **기존 자료의 값이 선택 불가능해지는 일은 없다**(상위 집합이다).
--   대신 목록이 길어져 화면에서 검색을 켠다(`show-search`).
--
-- 반복 실행해도 안전하다.
-- ============================================================

BEGIN;

INSERT INTO scom.biz_select_configs (
  id, biz_type, service_code, api_url, http_method,
  result_path, label_field, value_field,
  remark, created_at, created_by, is_deleted
)
SELECT
  'portal-account', 'portal_account', 'auth', '/system/account/list', 'GET',
  'result', 'userName', 'loginId',
  '포털 계정 목록 (값은 로그인 아이디 — 이식 시스템의 담당자 컬럼이 아이디를 쓴다)',
  now(), 'projmng-step2', false
WHERE NOT EXISTS (
  SELECT 1 FROM scom.biz_select_configs WHERE biz_type = 'portal_account'
);

-- 이미 있으면 값이 맞는지 맞춰 둔다(지워진 상태면 되살린다).
UPDATE scom.biz_select_configs
   SET service_code = 'auth',
       api_url      = '/system/account/list',
       http_method  = 'GET',
       result_path  = 'result',
       label_field  = 'userName',
       value_field  = 'loginId',
       is_deleted   = false,
       updated_at   = now(),
       updated_by   = 'projmng-step2'
 WHERE biz_type = 'portal_account';

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
SELECT id, biz_type, service_code, api_url, result_path, label_field, value_field
  FROM scom.biz_select_configs
 WHERE biz_type = 'portal_account' AND NOT is_deleted;
