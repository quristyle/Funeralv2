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
| **호실 장비** | `room/room_machine` | **신규** — 호실에서 장비를 본다 |
| **다음 고인** | `room/next_goin` | **신규** — 호실의 다음 예약 |

## 할 일

현 시스템에 **메뉴는 있는데 화면이 안내문(19줄)뿐**인 것이 여덟이다.
프론트에 API 정의만 있고 **백엔드는 통째로 없다**.

| 화면 | 경로 | 옛 대응 | 데이터 출처 |
|---|---|---|---|
| 알림정보 | `/info/notice` | `t_notification` | 새 표 |
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

`docs/sql/funeralv2_old_migration.sql` 에 있다. 전부 `IF NOT EXISTS` 다.

| 표 | 무엇 |
|---|---|
| `smfr.funeral_notices` | 알림정보 (옛 `t_notification`) |
| `smfr.account_settings` | 계정별 업무 설정 (옛 `t_account_conf`) |
| `smfr.building_music` | 건물별 음원 배정 (옛 `t_music_build`) |

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
