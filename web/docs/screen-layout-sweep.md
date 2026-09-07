# 화면 구성 통일 진행표 (CommSch · CommCont · CommGrd)

자율 진행용 작업표다. 한 화면을 고치면 바로 커밋·푸시한다(main).
검증(브라우저·서버 재기동)은 하지 않는다 — 전부 끝낸 뒤 빌드·테스트를 한 번에 돈다.

`대시보드` 표시가 붙은 것은 **CommCont 로 감싸지 않는다.** 키 큰 조각이 여럿
쌓인 화면을 감싸면 남은 높이를 그 판이 다 먹어 아래가 잘린다(web/CLAUDE.md).

| # | 상태 | 메뉴 URL | 파일 | 비고 |
|---|---|---|---|---|
| 1 | ☑ | `/admin/auth/role` | RoleList.razor | 관리칸1 |
| 2 | △ | `/admin/auth/user-role` | UserRoleMap.razor | 도구줄1 |
| 3 | ☑ | `/admin/company/user` | CompanyUserList.razor | 도구줄3 · 팝업2 |
| 4 | △ | `/admin/profile` | Profile.razor | 대시보드 · 도구줄4 · 관리칸2 · 팝업1 |
| 5 | ☑ | `/admin/push/dashboard` | PushDashboard.razor | 대시보드 · 도구줄1 |
| 6 | ☑ | `/admin/push/history` | NotificationHistory.razor | 도구줄1 |
| 7 | ☑ | `/admin/push/logs` | PushLogs.razor | 도구줄1 |
| 8 | △ | `/admin/push/setting` | NotificationSetting.razor | 도구줄1 |
| 9 | ☑ | `/admin/release` | ReleaseNotes.razor | 도구줄1 · 팝업1 |
| 10 | ☑ | `/admin/status/deploy` | DeployStatus.razor | 대시보드 · 도구줄1 |
| 11 | △ | `/admin/status/server` | ServerStatus.razor | 대시보드 · 도구줄2 · 팝업1 |
| 12 | △ | `/admin/system/menu` | MenuList.razor | 도구줄2 · 관리칸1 · 팝업1 |
| 13 | △ | `/admin/system/signup` | SignupList.razor | 팝업1 |
| 14 | ☑ | `/funeral/building/device` | DeviceList.razor | 도구줄1 · 관리칸2 |
| 15 | ☑ | `/funeral/building/music-build` | MusicBuildingMapping.razor | 도구줄2 |
| 16 | ☑ | `/funeral/deceased` | DeceasedList.razor | 도구줄1 · 관리칸2 |
| 17 | ☑ | `/funeral/help/archive` | ArchiveListPage.razor | 도구줄2 · 관리칸1 · 팝업1 |
| 18 | ☑ | `/funeral/info/my-info` | MyInfoPage.razor | 대시보드 · 도구줄1 |
| 19 | ☑ | `/funeral/stat/billing` | BillingStat.razor | 대시보드 |
| 20 | ☑ | `/funeral/status/deceased-status` | DeceasedStatus.razor | 도구줄1 · 관리칸1 · 팝업1 |
| 21 | ☑ | `/helpdesk/dashboard` | HelpDeskDashboard.razor | 대시보드 · 도구줄2 |
| 22 | ☑ | `/helpdesk/monitor/maintenance` | MaintenanceMonitor.razor | 대시보드 · 도구줄1 |
| 23 | ☑ | `/helpdesk/monitor/sm` | SmMonitor.razor | 대시보드 · 도구줄1 |
| 24 | ☑ | `/helpdesk/project/manage` | ProjectManage.razor | 도구줄3 · 관리칸1 · 팝업1 |
| 25 | ☑ | `/helpdesk/project/wbs` | WbsList.razor | 도구줄3 · 관리칸1 · 팝업1 |
| 26 | ☑ | `/helpdesk/request/list` | RequestList.razor | 도구줄2 |
| 27 | ☑ | `/helpdesk/request/manage` | RequestManage.razor | 도구줄1 |
| 28 | ☑ | `/helpdesk/schedule/all` | ScheduleAll.razor | 도구줄3 · 관리칸1 · 팝업1 |
| 29 | ☑ | `/helpdesk/schedule/my` | ScheduleMy.razor | 도구줄3 · 관리칸1 · 팝업1 |
| 30 | ☑ | `/helpdesk/system/account-link` | AccountLink.razor | 도구줄3 · 팝업2 |
| 31 | ☑ | `/helpdesk/system/checklist` | ChecklistList.razor | 도구줄3 · 관리칸1 · 팝업1 |
| 32 | ☑ | `/helpdesk/system/user-properties` | UserProperties.razor | 도구줄2 |
| 33 | ☑ | `/helpdesk/util/mc-model` | McModelList.razor | 도구줄5 · 관리칸2 · 팝업2 |
| 34 | ☑ | `/life/birthday/list` | BirthdayList.razor | 도구줄1 |
| 35 | ☑ | `/life/weather/events` | WeatherEvents.razor | 도구줄1 |
| 36 | ☑ | `/life/weather/forecast` | WeatherForecast.razor | 대시보드 |
| 37 | ☑ | `/life/weather/history` | WeatherHistory.razor | 도구줄2 |
| 38 | ☑ | `/site/inquiries` | InquiryList.razor | 도구줄1 |
| 39 | △ | `/diagnostics` | Diagnostics.razor | 대시보드 |


## 2차 — 표(`CommGrd`)가 없어 1차 스캔이 놓친 화면

나무·카드·달력으로 그리는 화면들이다. 표가 없으니 관리 칸은 없고,
**손으로 적은 조건줄**만 조회 판으로 옮긴다.

| # | 상태 | 메뉴 URL | 파일 |
|---|---|---|---|
| 1 | ☑ | `/admin/auth/menu-role` | MenuRoleMap.razor |
| 2 | ☑ | `/admin/company/dept` | DeptList.razor |
| 3 | ☑ | `/funeral/help/faq` | FaqListPage.razor |
| 4 | ☑ | `/funeral/help/qna` | QnaList.razor |
| 5 | ☑ | `/funeral/room-status` | RoomStatus.razor |
| 6 | ☑ | `/helpdesk/hanju/procedure-result` | ProcedureResult.razor |
| 7 | ☑ | `/helpdesk/util/binary-parser` | BinaryParser.razor |
| 8 | ☑ | `/life/weather/warning` | WeatherWarningPage.razor |

## 다 돌고 난 뒤 — 검사 결과

빌드 오류 0, 아키텍처 테스트 162/162 통과, 컴파일러 경고 0.
아래는 빌드·테스트가 잡아 주지 못하는 것들을 따로 훑은 결과다.

| 검사 | 결과 |
|---|---|
| 조건이 담긴 `jsini-toolbar` 가 남은 화면 | 0 |
| 조회 판이 둘로 갈린 화면 | 0 |
| 안내 줄(`PageNotice Text="@Notice"`)이 두 번 든 화면 | 0 |
| 자료 판 안에 손으로 적은 제목(`<h2>`) | 0 |
| 자료 판 안에 손으로 적은 관리·처리 칸 | 1 (MenuList — 아래 참고) |
| 조건 칸이 빈 조회 판 | 0 |
| 죽은 private 멤버 | 0 |
| 권한 없을 때 빈 관리 칸이 뜨는 화면 | 0 (ArchiveListPage 를 고쳤다) |

남아 있는 `jsini-toolbar` 24건은 **조건줄이 아니다** — 폼 저장 띠, 구역
동작 단추, 도장 줄이다. 조건이 담긴 것은 하나도 없다.

### 자료 판을 쓰면서 위에 통계 타일이 있는 화면 여섯

메뉴 관리 · 정산 통계 · 기기 관리 · 장례 현황판 · 내 정보 · 호실 이용률.
타일이 **한 줄**이라 남은 높이를 자료 판이 받는 정상 배치다. web/CLAUDE.md
가 경고하는 것은 키 큰 조각이 **여럿** 쌓인 경우다.

### 구조를 고치느라 CSS 를 세 곳 더했다

- `app.css` — `.commcont__body > .dxbl-grid` 에 남은 높이를 준다. 나무
  (`DxTreeList`)와 맨 `DxGrid` 는 `CommGrd` 로 감싸이지 않아 표 규칙이
  안 걸리는데, 높이 제한을 푸는 쪽은 걸려서 **아래 띠가 잘려 누를 수
  없었다.**
- `admin.css` — `.ad-split` 이 본문 높이를 채운다(공통코드 · 메뉴롤).
- `helpdesk.css` — `.hd-two` 가 본문 높이를 채운다(MC 모델).

뒤의 둘은 판이 `div` 안에 들어가 있어 공통 규칙(`셸 본문의 직속 자식일 때`)이
안 걸리는 자리다. 셋 다 **넘친 부분이 잘리는** 사고를 막는 것이다.

## 다음에 손댈 것 (이번에 하지 않은 판단)

- **`AutoGrid` 를 `CommGrd` 위에 다시 얹는 일** — 헬프데스크 보고서·로그
  화면 열셋(`AutoGrid` 를 쓰는 것 전부)이 맨 `DxGrid` 라서 아래 띠가 없다.
  다시 읽기·칸별 검색·엑셀이 그 화면들에만 없고, 조회 단추를 화면 위에
  따로 둬야 하는 이유도 그것이다. 부품 하나를 바꾸면 열세 화면의 겉모습이
  한꺼번에 달라지므로(순번 칸·가운데 정렬) **눈으로 보고 결정할 일**이다.
- **MenuList 의 내장 편집** — 표와 나무가 편집 폼 한 벌을 같이 써야 해서
  공통코드 화면과 같은 손질이 필요하다. 공통코드에서 한 방식
  (`CodeFields` 한 벌을 두 팝업이 공유)을 그대로 옮기면 된다.
- **`ad-two-pane`(사람롤)** — 오른쪽 판에 역할 칩·안내·표가 쌓여 자료 판을
  못 씌웠다. 판을 셋으로 가르거나 칩을 접는 편이 맞는지 결정이 필요하다.

## 멈춘 자리 (결정이 필요해 넘긴 것)

- `/admin/auth/user-role` — UserRoleMap.razor: 오른쪽 판에 역할 칩·안내·표가 쌓여 CommCont 로 감싸면 아래가 잘린다 — 조회 판만 적용

- `/admin/profile` — Profile.razor: 목록 화면이 아니라 탭·폼 화면이다. 도구줄은 폼 저장 띠, jsini-actions 는 고정 메뉴 조작이라 조건·관리 칸이 아니다. CommCont 를 쓰지 않는 사유도 이미 주석에 적혀 있다
- `/admin/push/setting` — NotificationSetting.razor: 조건이 하나도 없는 설정 화면이다. 도구줄에 있는 것은 시험 발송·조회로 조건이 아니고, 설정 폼과 기기 표가 쌓여 있어 CommCont 로 감싸면 아래가 잘린다
- `/admin/status/server` — ServerStatus.razor: 구역이 넷인 대시보드라 CommCont 로 감싸지 않는다. 「쌓인 이미지 정리」 동작 줄은 그 구역의 것이라 그대로 뒀다
- `/admin/system/menu` — MenuList.razor: 표·나무 두 그림이 편집 폼 한 벌을 같이 써야 해서 CommGrd 의 내장 편집 흐름은 아직 안 옮겼다. 조회 판·자료 판·관리 칸 아이콘만 맞췄다
- `/admin/system/signup` — SignupList.razor: 이미 CommCont·CommGrd·RowActions·Reload 를 쓴다. 남은 팝업은 편집 폼이 아니라 거절 사유를 묻는 창이라 그대로 둔다
- `/diagnostics` — Diagnostics.razor: 메뉴가 아니라 셸의 합성 진단 화면이다. 조건이 없고 표 아래에 사실 목록이 붙는 구성이라 조회 판·자료 판을 세울 것이 없다
