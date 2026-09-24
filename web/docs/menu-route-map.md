# 메뉴 라우트 정본 (scom.system_menus 실측, 2026-09-05)

운영 DB `jsiniportal.scom.system_menus` 179건 전수 조회 결과.
**개발 환경(appsettings.Local.json)이 운영 DB 를 직접 바라본다** — 이 파일 기준으로
작업하고, DB UPDATE 는 컷오버 SQL([menu-path-cutover.sql](menu-path-cutover.sql))로만 한다.

- 전 행 status=1, is_deleted=false, hide_in_menu=false (모두 사용 중)
- 권한 컬럼(authority·auth_code)은 전부 비어 있음 — 권한은 역할-메뉴 매핑(path 기준)
- 버튼 노출 정본은 use_view/search/create/update/delete/print/excel 플래그
- type=MENU 인데 자식을 가진 그룹이 다수(/info /stat /status /auth /setting /help) — type 값 불신
- 파라미터 라우트 2건: `:id` → Blazor `{id}` 로 변환

## 접두사 일치 현황

| 앱 | DB 일치 | 처리 |
|---|---|---|
| HelpDesk 46 · ProjMng 38 · LifeEnv 15 | 접두사 일치 | DB path 그대로, `@page` 는 접두사 제거분 |
| Funeral 37 · Admin 29 · Site 3 | **전면 불일치** | Blazor 는 새 정규 경로로 선언, 옛→새 별칭표 운용, 컷오버 때 DB 일괄 UPDATE |
| 목적지 불명 3 (/dashboard /analytics /workspace) | vben 대시보드 | 인벤토리 확인 후 결정 |

## HelpDesk (:5562, /helpdesk) — DB path 그대로

/helpdesk/dashboard, /helpdesk/dashboard/customer, /helpdesk/request/{list,manage,new,monitor,my-comments},
/helpdesk/request/detail/{id}, /helpdesk/request/edit/{id},
/helpdesk/monitor/{sm,maintenance}, /helpdesk/util/{ascii-parser,binary-parser,mc-model,diagram},
/helpdesk/hanju/{health-check,collection-status,equipment-log,fms-log,procedure-result},
/helpdesk/report/{monitoring,weekly,monthly,prediction,io-deep-dive,availability,capacity-planning,root-cause},
/helpdesk/system/{account-link,user-properties}, /helpdesk/contact-us
(그룹 노드: /helpdesk, /helpdesk/{request,monitor,util,hanju,report,system})
※ 조직 관리(`/helpdesk/org/*`)는 2026-09-25 에 통째로 걷어냈다 — 조직과 계정은
  JSini 관리 포털(AuthServer)이 단독으로 맡는다.
※ 프로젝트(`/helpdesk/project/*`)와 일정(`/helpdesk/schedule/*`)도 같은 날
  걷어냈다. `/helpdesk/util/diagram`(다이어그램)은 이름만 WBS 고 「도구」
  묶음이라 남는다 — 그 화면만 `GET /api/wbs` 를 계속 쓴다.

## ProjMng (:5566, /projmng) — DB path 그대로

/projmng/proj/{manage,wbs,scheduler,appointment,user,source,monitoring},
/projmng/design/{erd,flow,use-case,mind-map}, /projmng/db/{list,code,tools,tester,table},
/projmng/source/{trace,glue,scaner}, /projmng/comm/common-code,
/projmng/sys/{db-logic,db-logic-item}, /projmng/todo/{list,monitor},
/projmng/tool/{sheet,fast-test,com-test,component},
/projmng/external/{funeral-monitor,jsini} (EMBEDDED iframe 2건)

## LifeEnv (:5565, /life) — DB path 그대로

/life/weather/{dashboard,forecast,warning,responses,history,events},
/life/weather/manage/{locations,standards}, /life/birthday/{list,calendar,messages}

## Funeral (:5561, /funeral) — 옛 path → 새 정규 경로

| 옛 path (DB) | 새 정규 경로 | Vue component |
|---|---|---|
| /building/info | /funeral/building/info | funeral/building/info/index |
| /building/music-build | /funeral/building/music-build | funeral/building/music-build/index |
| /building/floor | /funeral/building/floor | funeral/building/floor/index |
| /building/room | /funeral/building/room | funeral/building/room/index |
| /building/device | /funeral/building/device | funeral/building/device/index |
| /building/video | /funeral/building/video | funeral/building/video/index |
| /building/audio | /funeral/building/audio | funeral/building/audio/index |
| /device/background | /funeral/building/background | funeral/building/background/index |
| /decoration | /funeral/building/decoration | funeral/building/decoration/index |
| /room_status | /funeral/room-status | funeral/building/status/index |
| /building/deceased | /funeral/deceased | funeral/building/deceased/index |
| /info/room-history | /funeral/info/room-history | funeral/info/room-history/index |
| /info/deceased-search | /funeral/info/deceased-search | funeral/info/deceased-search/index |
| /info/my-info | /funeral/info/my-info | funeral/info/my-info/index |
| /info/preview | /funeral/info/preview | funeral/info/preview/index |
| /stat/billing | /funeral/stat/billing | funeral/stat/billing/index |
| /stat/room-usage | /funeral/stat/room-usage | funeral/stat/room-usage/index |
| /status/funeral-info | /funeral/status/funeral-info | funeral/status/funeral-info/index |
| /status/funeral-status | /funeral/status/funeral-status | funeral/status/funeral-status/index |
| /status/deceased-status | /funeral/status/deceased-status | funeral/status/deceased-status/index |
| /status/simple | /funeral/status/simple | funeral/status/simple/index |
| /status/mobile | /funeral/status/mobile | funeral/status/mobile/index |
| /help/qna | /funeral/help/qna | funeral/help/qna/index |
| /help/faq | /funeral/help/faq | funeral/help/faq/index |
| /help/archive | /funeral/help/archive | funeral/help/archive/index |
| /setting/environment | /funeral/setting/environment | funeral/setting/environment/index |
| /setting/work-options | /funeral/setting/work-options | funeral/setting/work-options/index |
| /system/player-download | /funeral/player-download | funeral/player-download/index |

(그룹 노드 9건: /funerals /building /building/source /deceased /info /stat /status /help /setting →
새 경로에서도 그룹은 링크 없음, path 만 /funeral 접두사로 정규화)

## Admin (:5563, /admin) — 옛 path → 새 정규 경로

| 옛 path (DB) | 새 정규 경로 | Vue component |
|---|---|---|
| /system/common-code | /admin/system/common-code | portal/system/common-code/list |
| /system/metadata_manager | /admin/system/metadata | portal/system/biz-select-config/list |
| /system/i18n | /admin/system/i18n | portal/system/i18n/list |
| /system/account | /admin/system/account | portal/system/account/index |
| /system/menu | /admin/system/menu | portal/system/menu/list |
| /portal/notice | /admin/notice | portal/notice/list |
| /portal/release | /admin/release | portal/release/index |
| /system/role-map | /admin/auth/role | portal/system/role/index |
| /auth/user-role | /admin/auth/user-role | portal/auth/user-role/index |
| /auth/menu-role | /admin/auth/menu-role | portal/auth/menu-role/index |
| /company/org-chart | /admin/company/org-chart | portal/system/company-user/org-chart |
| /system/company | /admin/company/list | portal/system/company/list |
| /system/dept | /admin/company/dept | portal/system/dept/list |
| /company/user | /admin/company/user | portal/system/company-user/index |
| /system/push/dashboard | /admin/push/dashboard | portal/system/push/dashboard |
| /system/push/logs | /admin/push/logs | portal/system/push/logs |
| /system/push/history | /admin/push/history | portal/system/push/history |
| ~~/system/push/setting~~ | (지움 — `/funeral/setting/environment` 로 합침) | portal/system/push/setting |
| /system/server-status | /admin/status/server | portal/system/server-status/index |
| /system/server-status/jin114 | /admin/status/jin114 | EMBEDDED iframe https://sec.jin114.co.kr/ |
| /system/deploy-status | /admin/status/deploy | portal/system/deploy-status/index |
| /system/player-release | /admin/status/player-release | portal/system/player-release/index |
| /profile | /admin/profile | portal/_core/profile/index |

(그룹 노드: /system /common /auth /company /system/push /system/status → /admin 아래로 정규화)

> **이 뒤에 들인 메뉴는 이 표에 없다.** 위 목록은 2026-09-05 에 실측한 **Vue 시절
> 메뉴**의 옛→새 대조표라, 옮길 옛 경로가 없는 새 메뉴는 적을 칸이 없다. 새 메뉴는
> `path` 를 처음부터 새 정규 경로로 넣는다 — 「메시지 발송」(`/admin/push/send`)이
> 그 첫 줄이고, 「쪽지 쓰기」(`/admin/note/write`) · 「쪽지함」(`/admin/note/box`)도
> 그렇다. 그래야 `RouteAliases` 를 타지 않고 권한표 열쇠와 화면 주소가 같아진다.

## Site (:5564, /site) — 옛 path → 새 정규 경로

| 옛 path (DB) | 새 정규 경로 | Vue component |
|---|---|---|
| /ai/chat | /site/ai/chat | portal/ai/chat/index |
| /company/site-inquiries | /site/inquiries | portal/site/inquiries |

(그룹 노드 /devs → 정리 대상)

## 목적지 불명 3건

/dashboard(그룹), /analytics(portal/dashboard/analytics), /workspace(portal/dashboard/workspace)
— vben 템플릿 대시보드일 가능성. 인벤토리 결과로 이식/폐기 결정.
