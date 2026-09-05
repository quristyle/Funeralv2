# 결정이 필요한 것들

> 이 문서는 **작업을 멈추지 않기 위해** 있다. 판단이 갈리는 자리를 만나면
> 여기 적어 두고 다른 일을 계속했다. 각 항목에는 결정을 내리는 데 필요한
> 사실과, 확인할 수 있는 파일 링크가 붙어 있다.
>
> 작성 시작: 2026-09-06 · 마지막 갱신: 2026-09-06 (D13 결정 반영)
>
> **결정 방법** — 각 항목의 `결정:` 줄에 고른 것을 적어 주십시오.
> 그대로 구현하겠습니다.

---

## 요약표

| # | 무엇을 정해야 하나 | 지금 상태 | 급한가 |
|---|---|---|---|
| [D1](#d1-운영-db-메뉴-경로-컷오버-sql-을-돌릴까) | 운영 DB 메뉴 경로 일괄 변경 | 안 돌림(코드가 흡수 중) | 낮음 |
| [D2](#d2-되돌리기-어려운-동작을-화면에-붙일까) | 답장 발송·릴리스·재배정 | 화면에서 뺌 | 중간 |
| [D3](#d3-차트를-쓸까-쓴다면-무엇으로) | 대시보드·보고서의 차트 | 표로만 보여 줌 | 중간 |
| [D4](#d4-간트-차트를-어떻게-할까) | 헬프데스크 간트 2화면 | 표로 대체 | 중간 |
| [D5](#d5-첨부-업로드를-어디까지-옮길까) | 요청 등록·자료실 첨부 | 미구현 | 높음 |
| [D6](#d6-헬프데스크-35화면의-칸을-손으로-적을까) | AutoGrid 대 손수 컬럼 | AutoGrid | 중간 |
| [D7](#d7-잠금화면을-되살릴까) | vben 잠금화면 | 없앰 | 낮음 |
| [D8](#d8-사용자-환경설정을-서버에-저장할까) | 테마·탭 상태 저장 위치 | 브라우저만 | 낮음 |
| [D9](#d9-엑셀-내보내기-범위) | 어느 화면에 붙일까 | ProjMng 만 있음 | 중간 |
| [D10](#d10-알림-실시간-수신을-붙일까) | SignalR·웹푸시 | 없음 | 중간 |
| [D11](#d11-ai-대화-화면을-어디까지-살릴까) | AI 에이전트 | 화면만 있음 | 낮음 |
| [D12](#d12-대시보드-첫-화면을-무엇으로-채울까) | `/` · `/workspace` | 진단 화면 | 높음 |
| ~~[D13](#d13-bootstrap-테마-묶음을-넣을까)~~ | 테마 창의 Bootstrap 묶음 | **정해짐 — 넣었다** | — |

---

## D1. 운영 DB 메뉴 경로 컷오버 SQL 을 돌릴까

**무엇** — `scom.system_menus.path` 69건이 아직 Vue 시절 경로다
(`/room_status` · `/portal/notice`). Blazor 라우트는 업무 접두사가 붙은
새 경로(`/funeral/room-status`)다.

**지금** — 코드가 흡수한다. `RouteAliases` 가 옛 경로를 새 경로로 옮겨 주므로
DB 를 안 바꿔도 메뉴가 열린다. SQL 을 돌려도 멱등이라 그대로 동작한다.

**정할 것** — 돌릴지 말지.

| 돌리면 | 안 돌리면 |
|---|---|
| 별칭표 69줄을 지울 수 있다 | 별칭표를 계속 들고 간다 |
| DB 의 `path` 가 실제 주소와 같아져 헷갈릴 일이 없다 | DB 와 화면 경로가 계속 다르다 |
| **되돌리는 동안 메뉴가 깨진다** | 위험이 없다 |

**주의** — 로컬 개발 DB 가 곧 운영 DB(portal.jsini.co.kr)다. 이 SQL 은 운영에
바로 반영된다. 롤백 스크립트는 같은 파일 하단에 있다.

- 정본: [`web/docs/menu-path-cutover.sql`](menu-path-cutover.sql)
- 대조표: [`web/docs/menu-route-map.md`](menu-route-map.md)
- 흡수 코드: [`RouteAliases.cs`](../src/Shared/JSini.Web.Components/Menu/RouteAliases.cs)

**결정:**

---

## D2. 되돌리기 어려운 동작을 화면에 붙일까

**무엇** — 옛 화면에 있었지만 **누르면 되돌릴 수 없는** 동작들이다.
이식하면서 일부러 뺐다.

| 동작 | 무슨 일이 벌어지나 | 화면 |
|---|---|---|
| 사이트 문의 **답장 발송** | 고객에게 메일이 나간다 | `/site/inquiries` |
| 플레이어 **릴리스 걸기** | 태그를 만들고 GitHub 워크플로가 돈다 | `/admin/status/player-release` |
| 요청 **담당자 재배정** | 새 담당자에게 알림이 나간다 | `/helpdesk/request/edit/{id}` |
| 푸시 **시험 발송** | 실제로 알림이 간다 (이건 붙여 두었다) | `/admin/push/setting` |

**정할 것** — 붙일지, 붙인다면 확인 절차를 어떻게 할지.

- 그냥 붙인다 (옛 화면과 같다)
- 확인 창을 붙인다 (`DxPopup` 으로 "정말 보냅니까")
- 미리보기 + 확인 (답장은 보낼 내용을 먼저 보여 준다)
- 계속 빼 둔다

**결정:**

---

## D3. 차트를 쓸까, 쓴다면 무엇으로

**무엇** — Vue 는 `echarts` 로 대시보드·보고서에 차트를 그렸다.
이식본은 표로만 보여 준다.

**지금** — DevExpress Blazor 에 차트가 **이미 들어 있다**(`DxChart` ·
`DxPieChart` · `DxSparkline` · `DxBarGauge`). 라이브러리를 새로 얹을 필요가
없고 테마도 함께 따라온다.

**정할 것** — 어디에 붙일지.

차트가 있던 화면(옛 Vue 기준):

| 화면 | 무슨 차트였나 |
|---|---|
| `/admin/push/dashboard` | 성공률 추이(선) · 실패 사유(막대) |
| `/helpdesk/dashboard` | 상태별 건수(도넛) · 월별 추이(선) |
| `/helpdesk/report/*` | 8화면이 저마다 다르다 |
| `/life/weather/dashboard` | 기온·습도 추이(선) |
| `/projmng/proj/monitoring` | 진척(막대) |

**참고** — 표만으로도 읽히는 화면이 많다. "어제보다 나빠졌나" 를 보는 화면만
차트가 값어치를 한다. 전부 붙이면 화면이 무거워지고 회로마다 그리기 비용이 든다.

**결정:**

---

## D4. 간트 차트를 어떻게 할까

**무엇** — 헬프데스크 `/helpdesk/project/wbs-gantt` · `/helpdesk/project/gantt`
두 화면이 간트였다. 지금은 **같은 자료를 기간이 보이는 표로** 보여 준다.

**정할 것**

- DevExpress `DxScheduler` 의 타임라인 뷰로 흉내낸다 (부품이 이미 있다)
- `DxGrid` 안에 막대를 CSS 로 그린다 (가볍지만 손이 간다)
- 간트 라이브러리를 하나 얹는다
- 표로 둔다

**참고** — 프로젝트관리(ProjMng)에도 WBS 가 있는데 그쪽은 애초에 간트가
없었다. 헬프데스크만의 요구다.

- 화면: [`WbsGantt.razor`](../src/Apps/JSini.Web.HelpDesk/Components/Pages/WbsGantt.razor)

**결정:**

---

## D5. 첨부 업로드를 어디까지 옮길까

**무엇** — 옛 화면 여럿이 파일을 함께 올렸다. 이식본은 **본문만** 올린다.

| 화면 | 첨부가 하던 일 |
|---|---|
| `/helpdesk/request/new` | 증상 화면 캡처 |
| `/funeral/help/archive` | 자료 파일 |
| `/funeral/building/*` | 건물·빈소 사진, 영상·음원 원본 |
| `/funeral/status/deceased-status` | 영정 사진 |

**지금** — 배관은 있다. `FileUploadClient`(장례식장) · `HelpDeskApi.PostMultipartAsync`
가 멀티파트를 보낼 수 있다. 없는 것은 **화면 쪽**이다 — DevExpress `DxUpload`
를 붙이고 진행률·취소·용량 제한을 다뤄야 한다.

**정할 것** — 어느 화면부터 붙일지. 넷을 한꺼번에 할지, 요청 등록만 먼저 할지.

**주의** — 개발 장비에서 올린 파일은 **운영 서버에 실제 바이트가 없다**.
로컬 저장소와 운영 DB 가 분리돼 있다(루트 CLAUDE.md 참고). 시험할 때 유의.

**결정:**

---

## D6. 헬프데스크 35화면의 칸을 손으로 적을까

**무엇** — 헬프데스크 화면 대부분이 `AutoGrid` 다. 서버가 준 JSON 을 그대로
표로 만들고, 칸 이름표만 붙였다.

**왜 그렇게 했나** — 엔드포인트가 마흔 개 넘게 다르다. 화면마다 DTO 를 만들면
클래스가 마흔 개 늘고, 백엔드가 칸을 하나 더할 때마다 화면이 아니라 DTO 를
고치러 가야 한다.

**대가** — 칸 순서·너비·서식(금액 천 단위, 날짜 형식)이 서버가 준 대로다.
옛 Vue 화면은 그것을 손으로 다듬어 두었다.

**정할 것** — 어느 화면을 손으로 다듬을지. 후보(옛 화면이 크고 매일 보는 것):

| 화면 | 옛 Vue 줄 수 |
|---|---|
| `/helpdesk/request/manage` 요청 처리 | 563 |
| `/helpdesk/request/monitor` 요청 모니터 | 503 |
| `/helpdesk/schedule/all` 전체 일정 | 846 |
| `/helpdesk/monitor/sm` SM 모니터링 | 480 |
| `/helpdesk/util/mc-model` MC 모델 관리 | 874 |

**결정:**

---

## D7. 잠금화면을 되살릴까

**무엇** — vben 에 `LockScreen` 이 있었다. 자리를 비울 때 화면을 덮고,
돌아와서 비밀번호를 넣어야 풀린다.

**지금** — 없다.

**만들려면** — 비밀번호를 확인하는 엔드포인트가 필요하다. AuthServer 에
`/user/change-password` 는 있지만 **확인만 하는** 것은 없다. 로그인 API 를
다시 부르는 방법도 있는데, 그러면 새 토큰이 발급되고 기존 세션과 섞인다.

**정할 것** — 되살릴지, 되살린다면 확인을 어떻게 할지.

- 백엔드에 확인 전용 엔드포인트를 하나 만든다 (권장, 작다)
- 실제로 잠그지 않고 화면만 덮는다 (보안이 아니라 예의 수준)
- 만들지 않는다

**결정:**

---

## D8. 사용자 환경설정을 서버에 저장할까

**무엇** — 지금 테마는 브라우저(localStorage)에만 남는다. 기기를 옮기면 초기화된다.

**지금** — 백엔드에 계정 환경설정 API 가 **그대로 살아 있다**
(`GET/PUT /auth/user/preferences`, jsonb 페이로드, 최대 몇 KB).
vben 이 이 API 에 레이아웃·테마·글자 크기를 저장했다.

**정할 것** — 무엇을 서버에 저장할지.

- 테마만
- 테마 + 사이드바 접힘 + 고정한 탭
- 저장하지 않는다 (기기별이 낫다)

**주의** — 로그인 **전** 화면(로그인·오류)은 계정 설정을 알 수 없다.
서버 저장을 켜도 브라우저 값이 먼저고 계정 값이 그 위에 덮는 순서여야 한다.

- 백엔드: `microservices/AuthServer/Endpoints/UserEndpoints.cs` (`/preferences`)

**결정:**

---

## D9. 엑셀 내보내기 범위

**무엇** — 옛 화면 다수에 엑셀 내보내기가 있었다. 지금은 프로젝트관리의
`DynamicGrid` 에만 있다.

**지금** — DevExpress `DxGrid` 에 `ExportToXlsxAsync` 가 들어 있다.
화면마다 단추 하나와 세 줄이면 붙는다.

**정할 것** — 전 화면에 붙일지, 권한(`use_excel`)이 켜진 화면만 붙일지.

**참고** — DB 메뉴 표에 `use_excel` 플래그가 있고 권한표가 그것을 내려준다.
`PermissionView Action="MenuAction.Excel"` 로 감싸면 **권한이 있는 화면에만**
나온다. 그 편이 옛 동작과 같다.

**결정:**

---

## D10. 알림 실시간 수신을 붙일까

**무엇** — 지금 알림은 화면을 열었을 때만 읽는다. 옛 포털은 웹푸시로 받았다.

**남아 있는 것** — NotificationServer(:5460)와 구독 표, VAPID 키가 그대로 있다.
`/admin/push/setting` 이 등록된 기기를 보여 주고 시험 발송도 된다.
**없는 것은 구독 등록**이다 — 브라우저 권한을 받아 서비스 워커를 등록하는 JS.

**정할 것**

- 웹푸시를 되살린다 (서비스 워커 + VAPID 교환 JS 가 필요하다)
- SignalR 로 회로에 밀어 넣는다 (포털을 열어 둔 동안만 온다, 훨씬 간단)
- 둘 다
- 지금처럼 둔다

**참고** — 장례식장 `DeviceHub`(SignalR)도 아직 안 이었다. 붙인다면 한 번에
설계하는 편이 낫다 — 회로마다 연결하지 말고 공용 연결 + 팬아웃.

**결정:**

---

## D11. AI 대화 화면을 어디까지 살릴까

**무엇** — `/site/ai/chat` 이 있고 `AiChatClient` 도 있다. 옛 포털은 헤더에
AI 단추가 있어 본문 오른쪽에 대화창이 열렸다.

**정할 것** — 화면 하나로 둘지, 헤더에서 여는 대화창까지 되살릴지.

**결정:**

---

## D12. 대시보드 첫 화면을 무엇으로 채울까

**무엇** — `/` · `/workspace` · `/analytics` 가 지금 **진단 화면**이다
(어느 모듈이 붙었는지 보여 준다). 로그인하면 가장 먼저 보는 자리다.

**옛 화면** — vben 템플릿의 데모 대시보드였다. 우리 업무 자료가 아니었다.

**정할 것** — 무엇을 넣을지. 재료는 이미 있다.

| 후보 | 재료 |
|---|---|
| 오늘의 생일자 | `auth/birthday/today` |
| 내 요청 현황 | `helpdesk/dashboard/requests/status-count` |
| 빈소 사용 현황 | `funeral/status/room-board` |
| 기상 특보 | `life/weather/warning` |
| 최근 공지 | `auth/notices` |
| 즐겨찾기 바로가기 | `auth/menu/favorites` |

**참고** — 진단 표는 어디든 남겨 두는 편이 낫다(예: `/admin` 아래).
모듈 하나가 안 붙었을 때 가장 먼저 볼 곳이다.

**결정:**

---

## ~~D13. Bootstrap 테마 묶음을 넣을까~~ — 정해짐

**무엇** — DevExpress 데모의 테마 창에는 묶음이 셋이다.

| 묶음 | 항목 | 우리 상태 |
|---|---|---|
| DevExpress Fluent | Light/Dark × 강조색 11 + 사용자 지정 | 넣었다 |
| DevExpress Classic | Blazing Berry · Blazing Dark · Purple · Office White | 넣었다 |
| Bootstrap | Default · Default Dark · Cerulean · Flatly · Journal · Lumen | **넣었다** |

**결정: 넣는다.** — 2026-09-06, 사용자 지시("해당 부분도 함께 적용하라").

**한 일**

- Bootstrap 5.3.3 과 Bootswatch 넷(Cerulean · Flatly · Journal · Lumen)을
  [`wwwroot/bootstrap/`](../src/Shared/JSini.Web.Components/wwwroot/bootstrap/)
  에 커밋했다. 합쳐 1.2MB, MIT. **CDN 을 부르지 않는다** — 그 이유는 그 폴더의
  [README](../src/Shared/JSini.Web.Components/wwwroot/bootstrap/README.md) 에 적었다.
- Default 와 Default Dark 는 **같은 파일**이다. Bootstrap 5.3 부터 어두운 쪽이
  별도 파일이 아니라 `<html data-bs-theme="dark">` 로 켜진다.
- 고르면 스타일시트가 두 장 실린다 — Bootstrap 본체와, 그 색을 DevExpress
  부품으로 옮겨 주는 `bootstrap-external.bs5.min.css`. **순서가 뒤집히면
  DevExpress 부품만 옛 색으로 남는다.**
  [`theme.js`](../src/Shared/JSini.Web.Components/wwwroot/theme.js) 의
  `priorityOf` 가 그 순서를 지킨다.

**곁가지로 고친 것** — 이 일을 하다 붙박이 테마 `<link>` 넷이
[`JSiniHead.razor`](../src/Shared/JSini.Web.Components/Layout/JSiniHead.razor)
에 남아 있는 것을 발견했다. theme.js 는 **자기가 만든 링크만** 껐다 켜므로
붙박이 office-white 는 끌 방법이 없어, 어떤 테마를 골라도 늘 함께 켜져 있었다.
걷어냈다.

---

## 결정이 필요 없는 것 — 그냥 하고 있는 일

아래는 판단이 갈리지 않아 그대로 진행 중이다. 참고용.

- 관리 화면에 등록·수정·삭제 붙이기 (`DxGrid` 편집 + 권한 플래그)
- 조회 조건 채우기 (옛 화면에 있던 것 기준)
- 표 칸 서식 (금액 천 단위, 날짜 형식, 상태 배지)
- 화면마다 남은 주석의 "아직 안 옮김" 표시 정리
