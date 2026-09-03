# 옛 장례식장 시스템 이식

2026-09-01 시작. 옛 JSP 시스템(`C:\down\funeralfr_oldsrc`)과 그 DB 를 읽어
현 funeralv2 에 없는 화면을 채운다.

## 근거 자료

| 무엇 | 어디 |
|---|---|
| 옛 소스 | `C:\down\funeralfr_oldsrc` (JSP 142 · JS 425, 316MB) |
| 옛 메뉴 | `C:\Users\jjstyle\t_menu_202609012220.csv` (54행, UTF-8) |
| 옛 DB | `funeralfr.jsini.co.kr:15432 / funeral / smfr` — **조회만 한다** |

옛 DB 비밀번호는 소스 안에 평문으로 있다(`sess_chk.jsp`). 사용자가 알려 준
`funeral/funeral` 로는 인증이 되지 않았고 소스의 값으로 붙었다.

## 옛 시스템이 만들어진 방식

화면은 **저장 프로시저 호출 한 줄**로 끝난다. JSP 는 UI 만 있고,
`fr_base.jsp` 가 `information_schema.parameters` 를 뒤져 인자를 자동 바인딩한 뒤
`CALL sm<스키마>.p_<패키지>_<프로시저>(...)` 를 부른다.

그래서 화면 정의가 `tblInit` 문자열 하나에 다 들어 있다 — 이식할 때 이것만 읽으면
컬럼·제목·너비·편집기·정렬이 그대로 나온다.

```
page/build/room.jsp
  'f:b_key|e:select|epcode:build|t:건물|w:180'
  +',f:nm|w:150|e:input|a:l|t:호실명'
  ...
  '|proc:fr.room.list|proc_save:fr.room.save|key:r_key'
```

`f:`필드 `t:`제목 `w:`너비 `e:`편집기 `a:`정렬 `epcode:`코드셀렉트
`epu:`프로시저셀렉트 `fmt:actionView`버튼칸 `hide`숨김.

## 표 대응

옛 `smfr.t_*` 30개 중 현 시스템이 이미 덮은 것과 아닌 것.

| 옛 표 | 행수 | 현 funeralv2 | 판정 |
|---|---|---|---|
| `t_company` | 3 | `scom.companies` | 있음 |
| `t_account` | 27 | `scom.accounts` | 있음 |
| `t_menu` · `t_rol` · `t_rol_menu` · `t_rol_account` | 54·6·55·64 | `scom.system_menus` · 역할 | 있음 |
| `t_code` | 86 | `scom.common_codes` | 있음 |
| `t_build` · `t_floor` · `t_room` | 19·37·126 | `smfr.buildings/floors/rooms` | 있음 |
| `t_machine` · `t_machine_info` | 107·789 | `smfr.devices/device_attributes` | 있음 |
| `t_movie` · `t_music` | 18·16 | `smfr.media_sources` | 있음 |
| `t_goin` | 10,261 | `smfr.deceaseds` | 있음 |
| `t_sangju` | 4 | `smfr.deceased_mourners` | 있음 |
| `t_manager` | 4 | `smfr.deceased_managers` | 있음 |
| `t_room_goin` | 9,930 | `smfr.deceased_rooms` | 표는 있고 **화면이 없다** |
| `t_goin_pay` | 29,190 | `smfr.deceased_facilities` | 표는 있고 **화면이 없다** |
| `t_account_conf` | 140 | — | **없다** |
| `t_music_build` | 2 | — | **없다** |
| `t_notification` | 0 | — | **없다** |
| `t_account_favorites` | 35 | — | 없다(포털에 즐겨찾기 있음) |
| `t_qna` | 26 | `scom` 도움말 Q&A | 있음 |
| `t_file` · `t_filegrp` | 11,549·9,714 | FileServer | 있음 |

### 옛 데이터에서 읽은 것

- **`t_goin_pay` 는 고인 한 명당 세 줄**로 고정이다 — 기본료 120,000 ·
  환경부담금 50,000 · 시설관리비 30,000. `gp_day_apply` 가 켜져 있으면 사용일수를
  곱한다. 금액이 실제로 채워진 행은 한 명뿐이고 나머지는 비어 있다(기본값을
  프로시저가 채우는 구조로 보인다).
- **`t_room_goin` 의 날짜 칸은 전부 비어 있다.** `use_days` 도 10,385행이 모두 1 이다.
  즉 옛 시스템의 호실 이력은 사실상 **고인↔호실 연결표**였고 기간 관리는 하지 않았다.
  현 `deceased_rooms` 는 `start_time`/`end_time` 이 있어 더 낫다.
- **옛 `t_goin` 의 확장 칸은 전부 비어 있다.** 표에는 봉안 · 종교 · 축문 · 부고 ·
  주민번호 · 혼인 · 국가유공자 · 기초수급 · 주소 · 사망장소(4단계) · 사망종류 ·
  의료/검안 여부 · 지방 같은 칸이 선언돼 있는데, **10,384행 중 채워진 것이 하나도 없다.**
  실제로 쓰인 것은 이것뿐이다.

  | 칸 | 채워진 행 |
  |---|---|
  | `nm` 성명 | 10,384 (**거의 전부 "자동생성"**) |
  | `layout_corpse_dt`/`tm` 입실 | 10,384 |
  | `gi_img` 영정 사진 | 10,145 |
  | `crop_img` 잘라 낸 사진 | 8,344 |
  | `gi_video` 영상 | 5,162 |
  | `gi_audio` 음악 | 1,754 |
  | `chulsang` 출상 | 10,338 |
  | `sex` 성별 | **1** |
  | `age` 나이 · `jangji` 장지 | 0 · 1 |

  상주(`t_sangju`)도 4행뿐이다. 즉 **옛 시스템은 실제로는 고인 관리가 아니라
  "빈소 장비에 사진·영상·음악을 거는 일"** 이었고, 고인 행은 호실을 쓸 때마다
  자동으로 하나 만들어지는 자리표시자였다.

  현 `smfr.deceaseds` 는 계약자 · 담당자 · 시설 이용 · 상주 · 영정 편집까지 갖춰
  이미 옛 표보다 낫다. **그래서 확장 칸을 옮기지 않았고 옛 데이터도 옮기지 않았다.**
  옮길 값이 없다.

- **`t_account_conf` 는 계정별 UI 토글 8개**다. 이름은 `t_code` 에 있다.

  | 코드 | 뜻 |
  |---|---|
  | `page_tab_view` | 페이지 구분 탭 숨기기 |
  | `side_bar_open` | 최초 사이드 메뉴 닫기 |
  | `side_menu_expend` | 최초 사이드 하위 메뉴 모두 열기 |
  | `side_bar_autohide` | 사이드 메뉴 자동 닫힘 |
  | `multy_room_use` | 기존고객연결 기능 활성화 |
  | `auto_goin_name` | 호실 — 자동생성 대신 고인 명칭 사용 |
  | `machine_create_auto_conf` | 장비추가시 자동세팅(영정1, 좌:140, 우:170, 16:10) |
  | `hide_company` | 회사 숨기기 |

  앞의 넷은 포털(vben)이 이미 개인 환경설정으로 갖고 있다. **뒤의 넷이 장례식장
  업무 규칙**이라 이식 대상이다.

## 메뉴 대응

옛 메뉴 54개 중 현 시스템에 없는 것만 추린다. 나머지는 포털 공통(시스템·권한·회사)
으로 이미 대체됐다.

| 옛 메뉴 | 옛 경로 | 판정 |
|---|---|---|
| 건물 관리자 관리(고객) | `acct/build_acct` | 포털 `회사 사용자관리` 로 대체 |
| 롤메뉴 | `auth/rol_menu` | 포털 `메뉴롤` 로 대체 |
| 코드관리 | `comm/code` | 포털 `공통코드` 로 대체 |
| 문의 | `help/cont_us` | 헬프데스크 문의로 대체 |
| **건물별 음원** | `rsrc/music_build` | **신규** |
| 호실 장비 | `room/room_machine` | 건물관리 > 장비 로 대체 |
| 다음 고인 | `room/next_goin` | 만들지 않음 — 옛 `reservation_yn` 이 10,384행 모두 꺼져 있다(쓰지 않던 기능) |
| 빈소 프로필 | `room/room_profile` | 고인관리 + 장비관리 + 빈소현황 으로 나뉘어 이미 있다 |

`room_profile.jsp`(640줄)는 옛 시스템에서 가장 많이 쓰던 작업 화면이다.
하는 일은 고인 저장 · 출상 처리 · 호실 장비 목록/전원 · 장비 이동 · 새 장비 등록 ·
여백 설정이었는데, 현 시스템에서는 이렇게 흩어져 있고 각각 더 낫다.

| 옛 기능 | 지금 어디 |
|---|---|
| 고인 저장 · 출상/출상취소 | 고인관리 (`cancel-departure` 까지 있다) |
| 호실 장비 목록 · 전원 | 건물관리 > 장비 |
| 장비 이동 | 장비 수정에서 호실을 바꾼다 |
| 여백 · 화면비 설정 | 장비 속성 탭 (옛 `MG_LEFT`·`MG_RIGHT`·`MSIZE` 에 대응) |
| 영정/리본/자막 | 장비 리본 탭 · 텍스트 오버레이 탭 (옛 시스템에 없던 것) |

## 할 일

현 시스템에 **메뉴는 있는데 화면이 안내문(19줄)뿐**인 것이 여덟이다.
프론트에 API 정의만 있고 **백엔드는 통째로 없다**.

| 화면 | 경로 | 옛 대응 | 데이터 출처 |
|---|---|---|---|
| ~~알림정보~~ | ~~`/info/notice`~~ | ~~`t_notification`~~ | **2026-09-03 걷어냄** (아래) |
| 호실히스토리 | `/info/room-history` | `room/room_goin_hist.jsp` | `deceased_rooms` |
| 고인정보조회 | `/info/deceased-search` | `room/goin4room.jsp` | `deceaseds` |
| 나의정보 | `/info/my-info` | `page/ui_config.jsp` 왼쪽 | 게이트웨이 헤더 + 새 표 |
| 미리보기 | `/info/preview` | `client_machine/*` | `devices` |
| 과금내역 | `/stat/billing` | `goin_profile_useinfo2.jsp` | `deceased_facilities` |
| 빈소현황-심플 | `/status/simple` | `monitor/room_status_simple.jsp` | `rooms`+`deceased_rooms` |
| 환경설정 | `/setting/environment` | `page/ui_config.jsp` 오른쪽 | 새 표 |

### 프론트 API 경로가 틀려 있었다

`src/api/funeral/{info,stat,status,setting}/index.ts` 가 `/info/...` 처럼 부르는데,
게이트웨이는 **`/api/funeral/**` 만** funeral 서비스로 보낸다(`funeral-service-route`).
`/api/info/...` 는 어디로도 가지 않는다. `/funeral/info/...` 로 고쳐야 한다
(`getMediaSources` 가 `/funeral/building/source/list` 를 쓰는 것과 같은 규칙).

## 새로 만드는 표

**EF 마이그레이션이 정본이다** — `20260901140733_AddOldFuneralMigrationTables`.
`docs/sql` 에 손으로 쓴 것을 잠깐 두었다가 지웠다. funeralv2Api 는 마이그레이션을
쓰는데(`Migrations/` · `smfr.__EFMigrationsHistory`) SQL 파일을 따로 두면
정본이 둘이 되고, 인덱스 이름부터 어긋난다(손으로 쓴 `ix_*` vs EF 의 `IX_*`).

| 표 | 무엇 |
|---|---|
| ~~`smfr.funeral_notices`~~ | ~~알림정보~~ — **2026-09-03 지움** (아래) |
| ~~`smfr.funeral_notice_reads`~~ | ~~알림 읽음 표시~~ — 함께 지움 |
| `smfr.account_settings` | 계정별 업무 설정 (옛 `t_account_conf`) |
| `smfr.building_music` | 건물별 음원 배정 (옛 `t_music_build`) |

### 알림정보는 걷어냈다 (2026-09-03)

> 지시: "`/info/notice` 는 장례식장관리에서 알림정보로 쓰려던 화면이다. 쓰지 않고
> **완전히 제거되어도 되는 화면**이다. 화면 관련 소스와 메뉴 데이터도 모두 제거하라."

그 자리는 이미 둘이 채우고 있었다 — 포털 공지(`/portal/notice`, AuthServer
`scom.notices`)와 내 알림 설정(`/system/push/setting`, NotificationServer,
[29번 문서 8절](29-notification-server.md)). 옛 `t_notification` 을 그대로 옮겨 온
세 번째 알림 체계를 남겨 둘 이유가 없었다.

지운 것:

| 어디 | 무엇 |
|---|---|
| 프론트 화면 | `views/funeral/info/notice/index.vue` |
| 프론트 API | `api/funeral/info` 의 알림 함수 일곱 · `InfoApi.Notice`·`NoticeSave` |
| 나의정보 화면 | '안 읽은 알림' 칸 (누를 곳이 없어졌다) |
| 백엔드 | `InfoEndpoints` 알림 라우트 일곱 · `IInfoService`/`InfoService` 메서드 일곱 · `NoticeDto` 셋 · `MyInfoDto.UnreadNoticeCount` |
| 엔티티 | `FuneralNotice` · `FuneralNoticeRead` · DbSet 둘 · 읽음 유일 인덱스 |
| 표 | `smfr.funeral_notices` · `smfr.funeral_notice_reads` (EF `RemoveFuneralNotices`) |
| 메뉴 | `scom.system_menus` 의 `NOTICE` + 역할 권한 5건 ([funeral_notice_menu_drop.sql](../sql/funeral_notice_menu_drop.sql)) |

**표를 지운 근거**: 두 표 모두 **행이 0 이었다.** 지우기 전에 세어 보고 확인했다.

**`PORTAL_NOTICE` 는 남겼다.** 메뉴 id 가 `NOTICE`(장례식장 알림정보)와
`PORTAL_NOTICE`(포털 공지 관리)로 비슷해 헷갈리기 쉽다 — 지울 때 id 로 정확히 짚었다.

#### 걸린 것 — 빈 마이그레이션이 먼저 나왔다

`dotnet ef migrations add` 를 `--no-build` 로 돌렸는데 **`Up()` 이 빈 마이그레이션**이
나왔다. funeralv2Api 가 떠 있어 Debug 빌드가 exe 잠금으로 실패했고, EF 가 **낡은
어셈블리**(엔티티가 아직 있는)를 읽은 것이다. CLAUDE.md 가 경고하는 바로 그 함정이다.

`dev.bat stop funeral` → `dotnet build -c Debug` → 다시 `migrations add` 로 해결했다.
**그리고 한 번 더 빌드해야 한다** — `migrations add` 는 소스만 만들므로, 바로
`database update --no-build` 를 돌리면 "already up to date" 로 아무 일도 하지 않는다
(새 마이그레이션 클래스가 아직 어셈블리에 없다).

### 호실 히스토리를 손봤다 (2026-09-03)

> 지시: "`/info/room-history` 는 호실별 입관한 고인의 히스토리를 보는 화면이다.
> **사진 컬럼의 사진을 조금 더 크게** 보이도록 개선하고, 호실별 입관한 고인의 정보를
> **조금 더 편리하게 찾을 수 있도록** 화면을 개선하라."

**사진.** 48×60 → **72×90** 으로 키우고 행 높이를 104px 로 맞췄다(안 올리면 사진이
칸에 잘린다). 영정 사진은 얼굴을 확인하는 것이 목적인데 48px 로는 누구인지 가려지지
않았다. `fit="cover"` 로 칸을 꽉 채운다. 눌러서 원본을 크게 보는 것은 전부터 되던
것이다(`ImagePreview` 의 미리보기).

**찾는 길.** 전에는 건물·호실·기간 셋뿐이라, **이름만 아는 고인**이 어느 호실에
있었는지 찾으려면 호실을 하나씩 골라 훑어야 했다. 넷을 더했다.

| 더한 것 | 어디서 걸러지나 |
|---|---|
| 고인 성명으로 찾기 | 백엔드 `keyword` — 이름에 맞는 고인 키를 먼저 구해 배정 질의에 넣는다 |
| 사용 중 / 출상 가리기 | 백엔드 `inUse` — `end_time` 유무로 판정(DTO 의 `InUse` 와 같은 규칙) |
| 기간 프리셋 1·3·6개월 · 1년 · 전체 | 화면 |
| 결과 요약 (총 n건 · 사용 중 n · 출상 n) | 화면 |

성명을 백엔드에 둔 이유: 이름은 배정(`deceased_rooms`)이 아니라 고인(`deceaseds`)에
있어서, 화면에서 거르려면 기간 안의 배정을 전부 받아와야 한다.

**호실 칸을 합쳤다.** 건물·층 칸 둘을 없애고 호실 칸 아래 작은 글씨로 적는다.
폭이 220px 줄었고, `params.filterText` 로 셋을 이어 붙여 **'본관' 이나 '2층' 으로도**
그리드 필터에 걸린다. 성별·상태는 `filterOptions` 를 줘서 고르는 칸이 된다 —
정렬·필터를 화면에 적지 않는 준수사항 6 그대로다.

조회 조건이 두 줄이 되어 `content-class="page-fill-last"` 를 줬다(준수사항 4).

확인한 것 — 필터 조합 여덟 가지를 API 로 직접 재고(기간만 3 · 사용중 1 · 출상 2 ·
성명 1 · 성명+출상 0 · 없는 이름 0), 화면에서도 성명 검색 → 상태 겹치기 → 초기화 →
프리셋까지 눌러 요약 숫자가 따라오는 것을 봤다. 1280×720 에서 넘침 0.

> **헛디딘 것.** 검증 중 서비스가 세 번 죽어 `inUse` 가 프로세스를 죽이는 줄 알았는데,
> Bash 도구에서 `dotnet run &` 로 띄운 것이 **도구 호출이 끝날 때 함께 죽은** 것이었다.
> 로그를 보니 죽기 직전 요청이 아예 도착하지도 않았다. 개발 서버는
> `dev.bat`(분리 실행)으로 띄워야 한다.

#### 알아 둘 것 — `Migrations/` 는 git 에 없다

`.gitignore:8` 의 `Migrations/` 규칙에 걸려 **funeralv2Api 의 마이그레이션 폴더 전체가
추적되지 않는다**(`git ls-files` 0건). 이 문서와 CLAUDE.md 는 "EF 마이그레이션이
정본" 이라고 적었지만, 그 정본이 **이 장비에만 있다** — 다른 환경에는 옮겨지지 않는다.

그래서 이번 삭제는 SQL 파일에도 적어 두었다
([funeral_notice_menu_drop.sql](../sql/funeral_notice_menu_drop.sql) 머리말의 DROP 두 줄).
이 어긋남을 어떻게 할지는 판단이 필요하다 — 이 작업 범위를 넘으므로 손대지 않았다.

**funeralv2Api 는 기동 때 마이그레이션을 스스로 적용하지 않는다**
(`Database.Migrate()` 를 쓰는 것은 FileServer 뿐이다). 사람이 돌려야 한다.

```bash
cd microservices/funeralv2Api && dotnet ef database update
```

`--no-build` 로 돌릴 때는 **Debug 빌드가 최신인지 먼저 확인한다.** 낡은 어셈블리를
보면 EF 가 변경을 못 알아채고 빈 마이그레이션을 만든다 (처음에 그렇게 한 번 헛돌았다).

메뉴 등록은 `jsiniportal` 쪽이라 SQL 로 남긴다 —
`docs/sql/funeral_menu_old_migration.sql`.

## 판단 대기

- **D-F1 과금 단가를 어디에 둘 것인가.** 옛 시스템은 프로시저 안에 박아 두었다
  (기본료 120,000 · 환경부담금 50,000 · 시설관리비 30,000). 지금은
  `deceased_facilities.unit_price` 에 행마다 적히므로 단가표가 없다.
  건물별 단가표를 만들지, 공통코드로 둘지 정해야 한다. **지금은 공통코드로
  읽되 없으면 옛 기본값을 쓴다.**
- **D-F2 옛 데이터 10,261건을 옮길 것인가.** 이식 대상은 화면이지 데이터가 아니라고
  보고 **옮기지 않았다.** 필요하면 별도 작업이다.
- **D-F3 `t_account_conf` 의 앞 네 개**(탭·사이드바)는 vben 개인 설정과 겹친다.
  겹치는 것을 지우고 vben 것을 쓸지, 장례식장 화면에서 따로 둘지.
- **D-F4 장비 미리보기 주소를 어디에 둘 것인가.** 옛 시스템은
  `/client_machine/{번호}/index.jsp` 를 새 창으로 띄웠다. 지금 재생 장비는
  **설치형(.deb 등)이라 그에 해당하는 웹 주소가 없다.** `funeralv2.jsini.co.kr` 은
  응답하지 않는다.
  그래서 백엔드가 설정 `Device:PreviewUrlTemplate`(`{code}` 자리에 장비 코드가 들어간다)를
  보고 주소를 만들고, **설정이 없으면 빈 값**을 준다. 화면은 그때 버튼을 잠그고
  장비 코드 복사만 내준다. 주소가 정해지면 `appsettings.Local.json` 에 한 줄 적으면 된다.
