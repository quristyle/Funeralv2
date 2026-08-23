-- 메뉴 아이콘 백필
--   scom.system_menus 중 icon 이 비어 있던 80건에 어울리는 Iconify 아이콘을 채운다.
--   기존 데이터와 동일하게 lucide 세트를 기본으로 쓰고, 데모/예제 메뉴 일부는
--   형제 메뉴가 쓰던 세트(ic, logos)를 그대로 따른다.
--   이미 아이콘이 있는 메뉴는 건드리지 않도록 WHERE 절로 막아둔다.

UPDATE scom.system_menus m
SET icon = v.icon
FROM (VALUES
  -- 최상위 / 카탈로그
  ('dbb58d38-5b53-4703-9e3c-e61cf3024cf3', 'lucide:code'),                 -- 개발영역
  ('7068a867-99bd-47c2-8b5f-4b2b0574a431', 'lucide:church'),               -- 장례식장 관리시스템
  ('SETTING',                              'lucide:settings'),             -- 설정
  ('HELP',                                 'lucide:help-circle'),          -- 도움말
  ('fef18dc3-9fdf-4e7a-bb0a-1afba9bd97b5', 'lucide:radio-tower'),          -- 상태관리
  ('AUTH',                                 'lucide:shield'),               -- 권한

  -- 장례식장 업무
  ('c4ea307e-75a1-4317-af58-e13499cfbc12', 'lucide:layout-dashboard'),     -- 빈소현황
  ('INFO',                                 'lucide:info'),                 -- 정보
  ('STAT',                                 'lucide:pie-chart'),            -- 통계
  ('STATUS',                               'lucide:monitor-check'),        -- 현황관리
  ('NOTICE',                               'lucide:bell-ring'),            -- 알림정보
  ('ROOM_HISTORY',                         'lucide:history'),              -- 호실히스토리
  ('DECEASED_SEARCH',                      'lucide:user-search'),          -- 고인정보조회
  ('MY_INFO',                              'lucide:contact'),              -- 나의정보
  ('PREVIEW',                              'lucide:eye'),                  -- 미리보기
  ('BILLING',                              'lucide:receipt'),              -- 과금내역
  ('ROOM_USAGE',                           'lucide:calendar-clock'),       -- 빈소 사용내역
  ('FUNERAL_INFO',                         'lucide:clipboard-list'),       -- 빈소 정보
  ('FUNERAL_STATUS',                       'lucide:tv'),                   -- 빈소 현황
  ('DECEASED_STATUS',                      'lucide:contact-round'),        -- 고인 현황
  ('FUNERAL_SIMPLE',                       'lucide:layout-template'),      -- 빈소현황-심플
  ('FUNERAL_MOBILE',                       'lucide:smartphone'),           -- 빈소 현황-모바일

  -- 건물 / 자원
  ('BUILDING_INFO',                        'lucide:building'),             -- 건물
  ('FLOOR',                                'lucide:layers'),               -- 층
  ('ROOM',                                 'lucide:door-open'),            -- 호실
  ('DEVICE',                               'lucide:monitor'),              -- 장비
  ('DECEASED',                             'lucide:user-round'),           -- 고인
  ('52e04f50-dd9f-4c4b-807d-c818628c38b0', 'lucide:network'),              -- 조직도
  ('7b88d600-cd29-4e15-80c4-f329e7a2e18f', 'lucide:image'),                -- 장비배경이미지
  ('VIDEO',                                'lucide:video'),                -- 영상
  ('AUDIO',                                'lucide:music'),                -- 음원
  ('158bec21-c00b-4f3b-b03d-4c59f2a70eb8', 'lucide:flower-2'),             -- 장식관리

  -- 시스템 / 권한 / 설정
  ('23c99477-64a5-4ef0-bae5-dcb039076ea7', 'lucide:tags'),                 -- 공통코드
  ('HELP_MNG',                             'lucide:file-cog'),             -- 메타데이터 관리
  ('65cbdd16-0760-4c2f-9005-728f10e6a6ee', 'lucide:server'),               -- MSA상태정보
  ('ENV_SETTING',                          'lucide:sliders-horizontal'),   -- 환경설정
  ('ROLE_USER',                            'lucide:user-check'),           -- 롤사람
  ('USER_ROLE',                            'lucide:user-cog'),             -- 사람롤
  ('ROLE_MENU',                            'lucide:shield-check'),         -- 롤메뉴
  ('MENU_ROLE',                            'lucide:menu'),                 -- 메뉴롤

  -- 도움말
  ('INQUIRY',                              'lucide:message-circle'),       -- 문의
  ('QNA',                                  'lucide:messages-square'),      -- Q & A
  ('FAQ',                                  'lucide:badge-help'),           -- F.A.Q
  ('ARCHIVE',                              'lucide:archive'),              -- 자료실

  -- 헬프데스크 / 프로젝트 (숨김 상세 화면 포함)
  ('HD_REQ_DETAIL',                        'lucide:file-text'),            -- 요청 상세
  ('HD_REQ_EDIT',                          'lucide:file-pen'),             -- 요청 수정
  ('HD_PROFILE',                           'lucide:id-card'),              -- 내 프로필
  ('HD_PRJ_READONLY',                      'lucide:book-open'),            -- WBS 열람
  ('HD_PRJ_INFO',                          'lucide:folder-open'),          -- 프로젝트 정보
  ('PM_PROJ_APPT',                         'lucide:calendar-plus'),        -- 일정 편집
  ('PM_TOOL_COMPONENT',                    'lucide:puzzle'),               -- 부품 모음
  ('2cf9af85-53b3-4bd3-b193-418b12b405bc', 'lucide:bot'),                  -- AI쳇

  -- 데모 / 예제 (비활성 메뉴지만 형제 메뉴와 톤을 맞춰 채운다)
  ('5bc0366c-75f6-47ea-b209-381c4750301e', 'lucide:navigation-2'),         -- breadcrumb levelDetail
  ('49d34ead-6cdd-4728-881b-c7b89b7aa0e5', 'lucide:navigation-2'),         -- breadcrumb lateralDetail
  ('a0d70166-4197-44c1-bd34-0f2ff4bf5097', 'ic:round-menu'),               -- hideChildrenInMenu
  ('81ec2880-55fc-45e9-a461-97fb0952059c', 'lucide:app-window'),           -- tabDetail
  ('c2999078-6900-4ed7-9954-77b7360f0a7d', 'logos:ant-design'),            -- antdv-next
  ('245a3e2c-784e-4103-abf8-6f7fa9988368', 'logos:ant-design'),            -- antdv
  ('d76737c9-9f03-479c-b318-984a0718916e', 'lucide:move-horizontal'),      -- slider captcha
  ('451b3ed5-a45f-42b3-9080-0812304e21ac', 'lucide:mouse-pointer-click'),  -- point selection
  ('226c435e-5efc-44f0-b7ff-9c493da5e79a', 'lucide:move-right'),           -- slider translate
  ('3d3ff90f-1a9c-4434-95f2-d4d4da8d9065', 'lucide:rotate-cw'),            -- slider rotate
  ('c0511db1-5c67-476e-9306-fef5062ea9ba', 'lucide:combine'),              -- form merge
  ('51a7ec5b-7e90-4c37-ba28-ed5bdd1f9b34', 'lucide:alert-circle'),         -- form scrollToError
  ('5d52b0f6-6b1c-4917-a985-8ac0a6ba63c8', 'lucide:plug'),                 -- form api
  ('dfcdd960-4d30-4031-ab74-0e31fbadc701', 'lucide:pencil-ruler'),         -- form custom
  ('a4878f79-212c-4f07-9806-3a489f4f14f2', 'lucide:file-check'),           -- form rules
  ('ab34ba8e-6910-42b8-8d50-c01cfc4f13c4', 'lucide:layout-grid'),          -- form layout
  ('6070eb70-19ef-43f4-9ad6-e66cad8f1f06', 'lucide:file-input'),           -- form basic
  ('5edf4d0c-0051-4106-9f87-c455082118eb', 'lucide:shuffle'),              -- form dynamic
  ('70581c05-d172-43d5-b1c6-6f962aef6b0d', 'lucide:search'),               -- form query
  ('dfec6b4b-7bc9-46e2-ab5e-182fafa94c57', 'lucide:scroll'),               -- vxe virtual
  ('17cdb5c9-2014-4ccf-a7ed-3f337ff5c021', 'lucide:table'),                -- vxe basic
  ('bf94cd35-02d7-4a7c-8ee3-e8d7b2593189', 'lucide:cloud-download'),       -- vxe remote
  ('94acae48-db71-48bc-befd-177a7150ecbd', 'lucide:list-tree'),            -- vxe tree
  ('8edddcd2-3b2e-4978-9f82-510e67caf273', 'lucide:pin'),                  -- vxe fixed
  ('d937d123-1855-47b2-b9d8-8451ee873523', 'lucide:grid-2x2'),             -- vxe custom-cell
  ('59f3c000-3e7f-4356-bfaa-e2ae337e4323', 'lucide:file-spreadsheet'),     -- vxe form
  ('8782f795-e40a-434b-bf60-2337f6150896', 'lucide:pencil'),               -- vxe editCell
  ('b052aadb-e4e8-4b3c-9762-3c7b5a26ed4c', 'lucide:pencil-line')           -- vxe editRow
) AS v(id, icon)
WHERE m.id = v.id
  AND (m.icon IS NULL OR btrim(m.icon) = '');
