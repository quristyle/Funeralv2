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
| 36 | ☐ | `/life/weather/forecast` | WeatherForecast.razor | 대시보드 |
| 37 | ☐ | `/life/weather/history` | WeatherHistory.razor | 도구줄2 |
| 38 | ☐ | `/site/inquiries` | InquiryList.razor | 도구줄1 |
| 39 | ☐ | `/diagnostics` | Diagnostics.razor | 대시보드 |

## 멈춘 자리 (결정이 필요해 넘긴 것)

- `/admin/auth/user-role` — UserRoleMap.razor: 오른쪽 판에 역할 칩·안내·표가 쌓여 CommCont 로 감싸면 아래가 잘린다 — 조회 판만 적용

- `/admin/profile` — Profile.razor: 목록 화면이 아니라 탭·폼 화면이다. 도구줄은 폼 저장 띠, jsini-actions 는 고정 메뉴 조작이라 조건·관리 칸이 아니다. CommCont 를 쓰지 않는 사유도 이미 주석에 적혀 있다
- `/admin/push/setting` — NotificationSetting.razor: 조건이 하나도 없는 설정 화면이다. 도구줄에 있는 것은 시험 발송·조회로 조건이 아니고, 설정 폼과 기기 표가 쌓여 있어 CommCont 로 감싸면 아래가 잘린다
- `/admin/status/server` — ServerStatus.razor: 구역이 넷인 대시보드라 CommCont 로 감싸지 않는다. 「쌓인 이미지 정리」 동작 줄은 그 구역의 것이라 그대로 뒀다
- `/admin/system/menu` — MenuList.razor: 표·나무 두 그림이 편집 폼 한 벌을 같이 써야 해서 CommGrd 의 내장 편집 흐름은 아직 안 옮겼다. 조회 판·자료 판·관리 칸 아이콘만 맞췄다
- `/admin/system/signup` — SignupList.razor: 이미 CommCont·CommGrd·RowActions·Reload 를 쓴다. 남은 팝업은 편집 폼이 아니라 거절 사유를 묻는 창이라 그대로 둔다
