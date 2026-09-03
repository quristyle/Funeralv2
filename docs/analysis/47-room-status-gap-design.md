# 47. 빈소현황 — ASIS/TOBE 갭분석과 재설계안

작성일: 2026-09-03. 개발 착수 전 **설계 검토용** 문서다.
**2026-09-03 설계 검토 완료** — D-RS1~D-RS8 을 6장의 권고안대로 확정했고,
**0~4단계를 같은 날 모두 구현했다** (7장 진행 기록).
분석 대상은 옛 시스템(`C:\down\funeralfr_oldsrc`)의 `/main.jsp?m=/monitor/room_status` 와
현 시스템의 `/room_status`(`fronts/apps/jsini-portal/src/views/funeral/building/status/`)이며,
옛 DB(`funeralfr.jsini.co.kr:15432`, 조회 전용)의 프로시저 원문까지 확인했다.

요구조건 일곱을 기준으로 한다.

1. ASIS `/monitor/room_status` 의 관리 항목·동작·처리 방식 분석
2. TOBE `/room_status` 의 관리 항목·동작·처리 방식 분석
3. **ASIS 관리 항목은 모두 TOBE 에서도 관리**되어야 한다
4. **ASIS 의 변경 처리는 모두 TOBE 에서도 처리**할 수 있어야 한다
5. **현재 TOBE 에 구현된 기능은 모두 유지**한다
6. 데스크탑·모바일·태블릿 **모든 화면에서 관리**할 수 있어야 한다
7. 갭분석 → 설계 → 설계 검토 후 개발

---

## 1. ASIS 분석 — `/monitor/room_status`

### 1.1 구조

- 라우팅: `main.jsp:13-31` 이 `m` 파라미터로 `/page/monitor/room_status.jsp`(648줄) 를 include.
  실행은 `index.jsp` 의 iframe 탭 안이다.
- 자매 화면: `room_status_simple.jsp`(736줄, 심플판) · `mobile/room_status.jsp`(255줄, 모바일판).
- 서버 호출은 전부 `POST /fr3.jsp?p=<프로시저명>` 하나다. `fr_base.jsp:66-78` 이
  `fr.room.roomstatus2` → `CALL smfr.p_room_roomstatus2(...)` 로 변환하고,
  인자는 `information_schema.parameters` 로 자동 바인딩한다.
  **SQL 원문은 소스에 한 줄도 없고 전부 DB 프로시저다** (옛 DB에서 원문 확인함).

### 1.2 메인 조회 — `smfr.p_room_roomstatus2` (DB 원문 확인)

호실마다 한 행. 부수 효과로 **하트비트 30초 초과 장비의 CPU 값을 `-` 로 밀어 버리는
UPDATE 를 조회 때마다 수행**한다(조회가 곧 상태 정리다).

| 반환 필드 | 뜻 | 산출 |
|---|---|---|
| `b_nm` `f_nm` `r_nm` `rs_nm` | 건물·층·호실명·호실닉네임 | `t_build`·`t_floor`·`t_room` |
| `b_key` `r_key` `gi_key` | 키 | |
| `g_nm` `sex` | 현재 입실 고인명·성별 | `t_room_goin(expired=false)`×`t_goin(reservation_yn=false, chulsang=false)` |
| `layout_corpse_dt/tm` | 입실(입관준비)일시 | `t_goin` |
| `borne_out_dt/tm` | 발인일시 | `t_goin` (데스크톱 화면에선 주석 처리) |
| `jangji` | 장지 | `t_goin` (모바일판만 표시) |
| `preview` `crop_img` | 영정 썸네일 경로·크롭 이미지 | `t_file` |
| `gi_video` `video_nm` / `gi_audio` `audio_nm` | **고인 단위** 영상·음악 | `t_movie`·`t_music` |
| `chulsang` | 출상 여부 | `t_goin` |
| `reservation_cnt` `next_dt` | 대기(예약)자수·다음 입관준비일 | 함수 (화면에선 주석 처리) |
| `machine_cnt/names/authkeys` | 연결 장비 수·이름·인증키 | `t_machine_info(info_gb='ROOM')` |
| `machine_powers` | 프로세스 생존: 하트비트 30초 이내 `on` | `t_machine.last_power_dt` |
| `machine_shutdowns` | 접속 생존: 50초 이내 `on` | `t_machine.last_conn_dt` |
| `machine_temps` | CPU 온도 (플레이어가 하트비트로 보고) | `t_machine_info(info_gb='CPU')` |
| `last_lever_dttm` | **마지막 출상(퇴실) 일시** — 빈 호실에 표시 | `t_room.last_feave_dt` |

### 1.3 상태 모델 — 상태 코드 컬럼이 없다

화면 판정은 오직 `gi_key` 유무 하나. 진짜 상태는 세 값의 조합으로 유도된다.

| 판정 | 조건 |
|---|---|
| 빈 호실 | 활성 `t_room_goin` 없음 → 헤더 연회색, `퇴실 {last_lever_dttm}` 표시 |
| 사용중 | `t_room_goin.expired=false` × `t_goin.reservation_yn=false, chulsang=false` → 헤더 진회색 |
| 예약중 | `reservation_yn=true` (goin_profile 에 배지만. **실데이터 0건 — 40번 문서**) |
| 출상 | `chulsang=true` |

### 1.4 변경 처리(액션) 전량 — 14가지

| # | 액션 | 노출 조건 | 처리(프로시저) | DB 부수 효과 |
|---|---|---|---|---|
| A1 | 검색: 회사·건물 | 관리자만 편집 | `p_room_roomstatus2` | 조회마다 오프라인 장비 CPU `-` 정리 |
| A2 | **고인명 인라인 편집** | 고인 있음 | `p_goin_save(U)` — Enter/blur 즉시 저장 | |
| A3 | **영정사진 클릭 → 업로드/편집 모달** | 고인 있음 | 파일 모듈 `/module/fup` iframe | 닫으면 재조회 |
| A4 | **영상 변경** 드롭다운 | 고인 있음 | `p_goin_save(U)` `gi_video` | **고인 단위** — 호실 전 장비에 반영 |
| A5 | **음악 변경** 드롭다운 | 고인 있음 | `p_goin_save(U)` `gi_audio` | 건물별 노출 필터(`t_music_build.mb_seqs`) + 하드코딩 예외(`cd_cd=='15'` 는 특정 건물만) |
| A6 | **호실관리** 탭 이동 | 항상 | — | `/room/room_profile` |
| A7 | **고인관리** 탭 이동 | 고인 있음 | — | `/user/goin_profile` |
| A8 | **고인등록** | 공실 | `p_goin_save(I)` `nm='자동생성'` **폼 없이 즉시 INSERT** 후 고인관리 탭 오픈 | `t_goin.nm` 10,384행이 거의 다 "자동생성"인 원인 |
| A9 | **기존고인연결** | 공실 + 계정설정 `multy_room_use='Y'` | 목록 `p_room_get_other_active_goin` → 연결 `p_room_goin_save(I)` | **한 고인을 여러 호실에** 배정 |
| A10 | **호실변경** | 고인 있음 | 목록 `p_room_get_other_room`(같은 건물의 **빈** 호실만) → `p_room_goin_save(U)` | |
| A11 | **출상** | 고인 있음 | `p_goin_chulsang` | ①`chulsang=true, chulsang_dt=now` ②`t_room.last_feave_dt=now` ③**대기(예약)자 있으면 자동 입실 승격**(`p_goin_next_reservation`) ④호실 장비에 `SHUTDOWN='reboot'` 지시 |
| A12 | **장비 표현방식 변경** | SUPER_ADMIN·PARTER_ADMIN | `p_machine_view_type_change` | `VIEW_TYPE`+`DISP` 저장, 유형 102/103 이면 기본 영상 자동 지정 |
| A13 | **미리보기 / 미리보기(세로)** | 전원 | — | `/client_machine/?key={auth_key}` 새 창 (플레이어 웹 화면 그대로) |
| A14 | **모니터 켜기/끄기 · 장비 재시작/끄기** | SUPER_ADMIN·PARTER_ADMIN | `p_machine_info_authsave` | `SHUTDOWN` = `displayon/displayoff/reboot/shutdown` — 플레이어가 하트비트 응답으로 수신 |

출상 취소는 이 화면이 아니라 고인관리(goin_profile)에 있다 — `p_goin_chulsang_cancel`
(같은 호실에 다른 입실자가 있으면 예외로 거부).

### 1.5 갱신

| 방식 | 주기 |
|---|---|
| 자동 폴링 | **180초** (`setInterval(f_search, 1000*180)`; 심플판 300초) |
| 탭 재활성화 | 즉시 재조회 (`f_onActive`) |
| 검색조건 변경·액션 후 | 즉시 재조회 |
| 실시간 푸시 | 없음. 장비 생존은 하트비트(30/50초)를 폴링 때 계산 |

### 1.6 레이아웃

건물별 블록 → 호실 카드 그리드. 미디어쿼리로 1/2/3/4/5열(760/1110/1450/1570px).
빈 호실 연회색, 사용중 진회색. 장비 버튼 3색(녹색=프로세스·접속 모두 on /
회색 반투명=접속만 on / 진회색 25%=끊김). 모바일 전용 JSP 가 따로 있다.

---

## 2. TOBE 분석 — 현재 구현

### 2.1 빈소현황 계열은 화면이 다섯이다

| | 이름 | 라우트 | 소스 | 데이터 |
|---|---|---|---|---|
| A | **빈소현황** (본 화면) | `/room_status` | `views/funeral/building/status/` | 건물·호실·장비·고인 4 API 를 **브라우저에서 조인** |
| B | 빈소 정보 | `/status/funeral-info` | `views/funeral/status/funeral-info/` | `StatusApi` |
| C | 빈소 현황(그리드) | `/status/funeral-status` | `views/funeral/status/funeral-status/` | `StatusApi` |
| D | 빈소현황-심플(전광판) | `/status/simple` | `views/funeral/status/simple/` | `StatusApi` — 30초 자동 갱신 |
| E | 빈소 현황-모바일 | `/status/mobile` | `views/funeral/status/mobile/` | `StatusApi` |

B~E 는 백엔드 `StatusEndpoints`/`StatusService`(6쿼리 서버 조인, `EMPTY/USING` 파생)를 쓰고,
A 만 따로 4개 목록 API 를 받아 클라이언트에서 조인한다.

### 2.2 화면 A 의 표시 항목

호실 카드: 호실명(shortName) · 영정 썸네일(편집본→원본 4단계 fallback) · 고인명 ·
성별/나이 · 상태 태그(장례 진행중/발인 완료/정산 완료/**출상**) · 공실 표시 ·
장비 타일(이름·코드 툴팁·유형 뱃지 5종·온라인 뱃지·현재 영상·음원).
건물 아코디언 헤더에 8타일 요약(전체/사용/공실/유형별 장비수),
층 그룹 헤더, 건물 공용 장비 패널.

### 2.3 화면 A 의 액션

- 필터 6종: 회사·건물·층(연동 BizSelect, 변경 즉시 재조회)·고인명·입실기간·발인기간 + 검색/초기화
- **출상 처리**: 상태 태그 클릭 → 확인 다이얼로그 → `PUT /building/deceased/{id}`
  (`status='FUNERAL_DEPARTURE_COMPLETED'`, `roomId=''`) → 배정 soft-delete + SignalR 로
  호실 장비에 `DeviceChanged` 전파(플레이어 즉시 갱신)
- **영상·음원 즉시 변경**: 장비 타일 드롭다운 → `device-attribute` GET→PUT (**장비 단위**),
  `isVideoEnabled/isMusicEnabled` 자동 토글, 화면은 낙관적 부분 갱신
- **장비 상세 모달**(읽기 전용 4탭: 기본·속성·리본·텍스트 오버레이)
- SignalR `DeviceStatusChanged` 로 장비 온라인/오프라인 실시간 반영

### 2.4 상태 모델과 알려진 결함

- `rooms.status`(`ACTIVE/INACTIVE`) 는 화면 A 가 읽지 않는다(플레이어 안내 쿼리만 사용).
- 점유 판정 술어가 **세 곳에서 서로 다르다**:

| 소비자 | 술어 |
|---|---|
| 화면 A | `d.roomId === room.id && status ∉ {COMPLETED, FUNERAL_DEPARTURE_COMPLETED}` |
| StatusService(B~E) | 배정 `EndTime==null && !IsDeleted` — **고인 status 무시** |
| 플레이어 입구안내 | `!dr.IsDeleted && !d.IsDeleted && d.Status != "COMPLETED"` |

- 고인 상태 코드가 어긋나 있다: 고인 폼은 `FUNERAL_IN_PROGRESS` 를 쓰는데
  카드 태그 분기는 `IN_HOSPITAL` 을 검사한다. 그래서 진행중 고인이 어느 분기에도
  안 걸려 **else 가지인 빨간 `출상` 태그가 '우연히' 노출**되는 것이 현재의 출상 진입점이다.
  `SETTLEMENT_COMPLETED` 는 아무 데서도 쓰지 않는 죽은 코드, `RESERVED` 는 선언만 있고
  생산되지 않는다.
- 출상 취소 API(`PUT /building/deceased/{id}/cancel-departure`)는 있으나 화면 A 에 UI 가 없다
  (고인 폼 드로어에만 있음).
- 백엔드 검증 부재: 대상 호실 ACTIVE 여부·중복 배정 검사 없음(중복 시 화면 A 는 첫 고인만
  표시), 상태 문자열 자유 입력, `UpdatedBy="System"` 고정.
- 기타: 리본·오버레이 탭 그리드 컬럼이 DTO 와 안 맞아 빈 칸으로 렌더(모달),
  composable 의 이중 봉투 벗기기(준수사항 7 위반 잔재), `console.log` 잔존,
  SignalR 에 `accessTokenFactory`·재접속 후 재동기화 없음, i18n 0%.

### 2.5 반응형 — 요구 6 의 현재 상태

요약 배너와 필터 바만 반응형이고 **호실 카드 행은 `flex` 고정(줄바꿈 없음)** 이라
좁은 화면에서 가로로 넘친다. 준수사항 점검(16번 문서 R2)에 **477px 초과** 로 기록된
바로 그 화면이다. 모바일은 사실상 별도 화면 E 로 우회 중이나, E 는 **조회 전용**이라
"모든 화면에서 관리"(요구 6)를 충족하지 못한다.

---

## 3. 갭분석

### 3.1 관리 항목 (요구 3) — ASIS 항목의 TOBE 커버리지

| ASIS 항목 | TOBE 대응 | 판정 |
|---|---|---|
| 건물·층·호실명 | 있음 (아코디언·층 그룹·카드) | ✅ |
| 고인명·성별 | 있음 (+나이 추가) | ✅ |
| 영정 썸네일·크롭본 | 있음 (편집본 우선 4단계) | ✅ |
| 입실(입관)일시 | **카드에 없음.** DB(`funeral_date`)와 화면 D·E 엔 있음 | 🔺 카드 표시만 추가 |
| 발인일시 | 상동 (`burial_date`, D·E 만) — ASIS 데스크톱도 주석이었음 | 🔺 |
| 장지 | 상동 (`burial_plot`, E 만) — ASIS 모바일만 표시였음 | 🔺 |
| 빈 호실의 **마지막 퇴실 일시** | **없음.** 전용 컬럼도 없으나 `deceased_rooms.EndTime` 최대값으로 파생 가능 | ❌ 갭 |
| 현재 영상·음악 이름 | 있음 (장비 타일) | ✅ |
| 장비 수·이름 | 있음 | ✅ |
| 장비 생존(프로세스/접속 2단계) | 온라인/오프라인 1단계 (SignalR, 30초 유예) — 실시간성은 TOBE 우세 | ✅ (2단계 구분은 사실상 동일 목적) |
| **장비 CPU 온도** | **없음.** 플레이어가 온도를 보고하지 않음 | ❌ 갭 (플레이어 프로토콜 확장 필요) |
| 대기자수·다음 입관일 | 없음 — ASIS 화면에서도 주석, **예약 실데이터 0건**이라 이식 제외 결정 완료(40번 문서) | ➖ 제외 유지 |
| 상주 | ASIS 빈소현황에도 없음 (TOBE 는 D·E 에 표시) | ✅ TOBE 우세 |

### 3.2 변경 처리 (요구 4) — ASIS 액션의 TOBE 커버리지

| # | ASIS 액션 | TOBE 화면 A | 판정 |
|---|---|---|---|
| A1 | 검색 (회사·건물) | 6종 필터 | ✅ 우세 |
| A2 | 고인명 인라인 편집 | 없음 | ❌ 갭 |
| A3 | 영정사진 업로드·편집 진입 | 없음 (표시만; 기능 자체는 고인관리에 있음) | ❌ 갭 (진입 동선) |
| A4·A5 | 영상·음악 변경 | 있음 — 단 ASIS 는 **고인(=호실 일괄)** 단위, TOBE 는 **장비** 단위 | 🔺 호실 일괄 변경 편의 갭 |
| A5' | 음악 건물별 노출 필터 | 없음 (`t_music_build` 상당 개념 없음) | ❌ 갭 (실데이터 2행 — 이식 가치 판단 필요) |
| A6 | 호실관리 바로가기 | 없음 (건물관리 화면은 별도 존재) | ❌ 갭 (동선) |
| A7 | 고인관리 바로가기 | 없음 (고인관리 화면은 별도 존재) | ❌ 갭 (동선) |
| A8 | 공실에서 고인등록 | 없음 — 고인관리에서 폼 등록 | ❌ 갭 (단 ASIS 의 '폼 없이 자동생성 INSERT' 는 나쁜 데이터의 원인 — 그대로 이식하면 안 됨) |
| A9 | 기존고인연결 (다중 호실 배정) | 없음 — 스키마(`deceased_rooms`)는 수용 가능하나 서비스가 단일 배정 가정 | ❌ 갭 (결정 필요) |
| A10 | 호실변경 | 화면 A 에 없음 (고인 폼에서 `roomId` 변경으로 가능) | ❌ 갭 (동선) |
| A11 | 출상 | 있음 (확인 다이얼로그) | ✅ — 부수 효과 차이: ASIS 의 '예약자 자동 승격'은 예약 미이식으로 해당 없음, '장비 reboot' 는 TOBE 의 SignalR `DeviceChanged` 재조회가 대체 |
| A11' | 출상 취소 | 화면 A 에 없음 (API·고인 폼엔 있음) — ASIS 도 화면엔 없었음 | ✅ 동등 (카드 노출은 개선 후보) |
| A12 | 장비 표현방식 변경 | 화면 A 에 없음 (장비관리의 속성 탭에 있음) | 🔺 갭 (동선) |
| A13 | 장비 미리보기 | **없음.** TOBE 플레이어는 Flutter 장비 앱이라 웹 미리보기 자체가 없음 | ❌ 갭 (결정 필요) |
| A14 | 모니터 켜기/끄기 | 화면 A 에 없음 — API(`screen-power`)와 플레이어 수신부는 **이미 있음**(장비관리에서 사용) | ❌ 갭 (노출만 하면 됨) |
| A14' | 장비 재시작·끄기 | **없음.** 명령 자체가 TOBE 에 없음 (ScreenPower ON/OFF 만) | ❌ 갭 (플레이어 명령 추가 필요) |
| — | 자동 폴링(180초)·탭 복귀 재조회 | 없음 (SignalR 은 장비 상태만; 고인·배정 변화는 수동 검색) | ❌ 갭 |
| — | 장비 제어 권한 게이트 | 없음 (메뉴 권한뿐) | ❌ 갭 (결정 필요) |

### 3.3 유지해야 하는 TOBE 기능 (요구 5)

필터 6종과 연동 검색 · 건물 아코디언 + 요약 배너 · 층 그룹 · 건물 공용 장비 패널 ·
장비 타일(유형 뱃지·온라인 뱃지) · 장비 상세 모달 4탭 · 장비 단위 영상/음원 즉시 변경
(낙관적 갱신) · 출상 확인 다이얼로그 · SignalR 실시간 장비 상태 · 화면 B~E 전부 ·
`StatusApi` 계약 · 출상/배정 변경 시 플레이어 SignalR 전파(**절대 깨지면 안 되는 결합점**).

---

## 4. 설계안

### 4.1 원칙

1. **화면 A 하나가 반응형으로 세 폼팩터를 모두 감당한다** (요구 6).
   화면 E(모바일 조회 전용)는 유지하되(요구 5), 관리는 화면 A 로 일원화한다.
2. **점유 판정을 서버 한 곳으로 모은다.** 술어 3곳 분산이 현재 결함의 뿌리다.
3. ASIS 를 "그대로" 가 아니라 "관리 항목과 처리 능력 기준으로" 이식한다.
   폼 없는 '자동생성' INSERT 같은 나쁜 패턴은 같은 능력의 올바른 형태로 바꾼다.
4. 플레이어 결합점(SignalR 전파·익명 라우트)은 계약을 바꾸지 않고 확장만 한다.

### 4.2 0단계 — 기반 정리 (기능 추가 전에 끝낸다)

| 항목 | 내용 |
|---|---|
| 고인 상태 코드 통일 | 정본을 `FUNERAL_IN_PROGRESS`(장례 진행중) · `FUNERAL_DEPARTURE_COMPLETED`(출상) · `COMPLETED`(종료) 로 확정. `IN_HOSPITAL` 은 entity 기본값·통계 화면에서 제거/치환, `SETTLEMENT_COMPLETED`·`DISCHARGED`·`RESERVED` 는 사용처 정리 후 제거. **백엔드에 상태 enum 검증 추가** (자유 문자열 차단) |
| 출상 태그를 명시적 버튼으로 | else-가지 우연 노출을 없애고, 상태 태그(진행중/출상완료)와 **출상 버튼**을 분리해 항상 의도적으로 노출 |
| 점유 술어 통일 | `StatusService` 의 판정을 정본으로 삼고 "배정 활성 && 고인 status ≠ 출상/종료" 로 보강. 화면 A·플레이어 쿼리도 같은 기준을 쓰도록 정리 |
| 화면 A 의 데이터 소스 전환 | 클라이언트 4-API 조인을 버리고 **서버 조인 API 1개**로 전환(4.4). 이중 봉투 벗기기·`console.log`·`catch(eee)`·미사용 함수 제거 (준수사항 7) |
| 배정 검증 추가 | `UpdateDeceasedAsync` 에 대상 호실 `ACTIVE` 검사 + 중복 배정 검사(옵션 D-RS6 과 연동) + `UpdatedBy` 에 실제 사용자 기록 |
| 모달 그리드 수정 | 장비 상세 모달의 리본·오버레이 탭 컬럼을 DTO 필드에 맞춤 |

### 4.3 화면 설계 — 반응형 카드 대시보드

```
필터 바 (모바일: 접이식, 회사/건물/층 + 검색어; 기간은 '상세 필터' 아코디언)
건물 아코디언
 ├ 요약 배너: 2열(모바일) / 4열(태블릿) / 8열(데스크탑)  ← 현행 유지
 ├ 건물 공용 장비 패널 ← 현행 유지
 └ 층 그룹
    └ 호실 카드 그리드: grid-cols-1 / sm:2 / lg:3 / xl:4 / 2xl:5   ← flex 고정폭 폐지
```

- 카드 안 장비 타일은 `flex-wrap`, 카드 높이 고정 해제. 목록 영역에 자체 스크롤
  (16번 문서 R2 제안 A 채택 — 준수사항 4).
- **호실 카드 구성(안)**

```
┌──────────────────────────────────────┐
│ [301호]  [장례 진행중]        [⋮ 메뉴] │  ← 카드 메뉴: 모든 관리 액션의 단일 진입점
│ ┌영정┐ 故 홍길동  남 / 82세            │     (데스크탑=드롭다운, 모바일=바텀시트)
│ │사진│ 입실 09-01 14:00 · 발인 09-04   │  ← 갭 보강: 입실·발인 표시
│ └────┘ 장지 ○○추모공원                │  ← 갭 보강(옵션 표시)
│ [영정 DID ●] [미디어 ●] [안내 ○]      │  ← 현행 장비 타일 유지 + 전원 토글
│ 🎬 추모영상A   🎵 음악3               │
└──────────────────────────────────────┘
  공실 카드: "공실 · 마지막 퇴실 09-01 10:30"  + [고인 등록] [기존 고인 배정*]
```

- **카드 메뉴(⋮) 항목** — ASIS A2~A14 를 여기로 수렴:
  고인 정보 수정(이름 포함 — 고인 폼 드로어 재사용, A2·A7) ·
  영정사진 관리(고인 폼의 사진 탭으로 진입, A3) ·
  호실 변경(빈 호실 목록 팝오버, A10) · 출상 / 출상 취소(A11) ·
  호실 미디어 일괄 변경(호실 내 전 장비의 영상/음원 동시 지정, A4·A5) ·
  장비 제어(전원 ON/OFF·재시작 — 권한자만, A14) · 호실 관리로 이동(A6).
- 팝업류는 전부 헤더 드래그 가능해야 한다(준수사항 3). 표가 필요한 곳은
  `useVbenVxeGrid`(준수사항 6).

### 4.4 백엔드 설계

**① 화면 A 전용 서버 조인 API (신규)**

`GET /status/room-board` — `StatusService.BuildAsync` 를 확장해 한 번에 내려준다.
기존 `/status/funeral-status/*` 계약은 그대로 두고(요구 5, B~E 보호) DTO 를 상속·확장.

```
RoomBoardDto = FuneralStatusDto
  + LastVacatedAt        // 공실의 마지막 퇴실: max(deceased_rooms.end_time) — 스키마 변경 불필요
  + Devices[]            // 장비: id, name, code, type, status, videoId/Name, musicId/Name
  + Deceased.MemorialEditedPhotoFileId 등 사진 4종   // 카드 fallback 유지
```

필터 인자: `companyId, buildingId, floorId, name, roomEnterStart/End, funeralStart/End`
(현행 화면 A 필터 전부 수용). 기존 `DeceasedSearchDto` 의
"파라미터 이름과 실제 필터 컬럼 불일치"(roomEnter→funeral_date)는 이 API 에서 바로잡는다.

**② 배정·상태 변경 API 정리**

- 호실 변경: `PUT /building/deceased/{id}` 재사용 + **이동 가능 호실 목록**
  `GET /building/room/available?buildingId=&excludeRoomId=` 신설
  (ASIS `p_room_get_other_room` 대응: ACTIVE + 미점유만).
- 출상·출상 취소: 기존 API 유지, 화면 A 에 취소 노출. 출상 시 SignalR
  `DeviceChanged` 전파는 현행 유지(ASIS 의 reboot 지시를 대체).
- 고인 등록: 신규 API 불요 — 고인 폼 드로어를 `roomId` 프리필로 여는 프론트 동선만 추가.

**③ 장비 명령 확장 (플레이어 협업 필요)**

- 현행 `ScreenPower ON/OFF` 에 `RESTART`(앱 재시작) 추가 —
  `POST /building/device/command/{code}?cmd=` 로 일반화하거나 기존 라우트에 상태 추가.
  플레이어(Flutter)에 SignalR 수신 핸들러 추가. OS 종료(`shutdown`)는 플랫폼 제약이
  크므로 **앱 재시작까지만** 1차 범위로 한다(D-RS3).
- CPU 온도(1.2 의 `machine_temps`): 플레이어 하트비트에 온도 보고를 추가하고
  `devices` 에 `last_telemetry`(jsonb 또는 컬럼) 저장 — **이식 여부 자체가 결정 대상**(D-RS2).

**④ 갱신 체계**

- 폴링: 화면 D 와 같은 방식으로 화면 A 에 60초 자동 갱신 + 탭 복귀(visibilitychange) 시
  즉시 갱신 (ASIS 180초보다 촘촘하게, 서버 조인 API 1회라 부담 적음).
- SignalR: 현행 `DeviceStatusChanged` 유지 + **`RoomAssignmentChanged`(호실 단위) 이벤트
  신설**해 출상·배정 변경이 열려 있는 모든 빈소현황 화면에 즉시 반영되게 한다.
  이때 `accessTokenFactory`·재접속 재동기화·허브 URL 상수화(frontend-analysis 5.1)도 함께 수리.

**⑤ 스키마 변경 — 없음이 기본**

퇴실 일시는 파생으로 충분하다. 온도(D-RS2)를 채택할 때만 EF 마이그레이션 1건
(`devices` 텔레메트리 컬럼)이 생긴다. funeralv2 는 EF 마이그레이션으로만 스키마를 바꾼다.

### 4.5 권한 설계

ASIS 는 장비 제어를 `SUPER_ADMIN/PARTER_ADMIN` 문자열 검사로 막았다. TOBE 는
메뉴 단위 권한뿐이므로, 장비 제어(전원·재시작)와 출상 취소에 **역할 기반 노출 제어**를
넣는다. 기준 역할은 결정 필요(D-RS4) — 후보: `SUPER_ADMINISTRATOR`·`PARTNER_ADMINISTRATOR`
(도움말 D-H1 과 같은 판단 축).

### 4.6 이식하지 않는 것 (근거 포함)

| 항목 | 근거 |
|---|---|
| 예약(대기자수·다음 입관일·자동 승격) | 옛 실데이터 0건, 40번 문서에서 이미 제외 결정. `RESERVED` 죽은 코드도 이번에 제거 |
| '자동생성' 즉시 INSERT | 옛 데이터 오염의 원인. 같은 능력(공실에서 바로 등록)은 폼 프리필로 제공 |
| 음악 하드코딩 노출 규칙(`cd_cd=='15'`→특정 건물) | 데이터로 옮길 가치가 있는지부터 결정(D-RS5). 실데이터 `t_music_build` 2행 |
| 온도 표시 이외의 조회-시-UPDATE 패턴 | 조회가 상태를 바꾸는 ASIS 패턴은 채택하지 않음. 생존 판정은 SignalR + LastSeenAt |

---

## 5. 개발 단계 제안

| 단계 | 내용 | 갭 해소 | 비고 |
|---|---|---|---|
| **0** | 기반 정리: 상태 코드 통일·출상 버튼 명시화·점유 술어 통일·서버 조인 API·검증 추가·모달 그리드 수정 | 3.2 의 결함 전부 | 화면 변화 최소, 회귀 위험 관리 구간. B~E·플레이어 회귀 테스트 필수 |
| **1** | 반응형 개편: 카드 그리드·장비 타일 wrap·모바일 필터·바텀시트 메뉴 | 요구 6, R2(477px) | 화면 E 는 그대로 |
| **2** | 관리 액션 이식: 카드 메뉴(고인 수정/사진/호실 변경/출상 취소/호실 미디어 일괄/바로가기), 공실 고인 등록(프리필), available-room API | A2·A3·A4/5(일괄)·A6·A7·A8·A10·A11' | |
| **3** | 장비 제어: 전원 토글 노출(기존 API)·재시작 명령(플레이어 수정)·권한 게이트 | A12(동선)·A14·A14' | 플레이어 배포 일정과 연동 |
| **4** | 갱신 체계: 60초 폴링 + `RoomAssignmentChanged` + SignalR 수리 | 폴링 갭 | |
| 별도 | D-RS2(온도)·D-RS5(음악 건물 필터)·D-RS6(다중 호실)·D-RS7(미리보기) 채택 시 각각 추가 | | 결정 후 착수 |

---

## 6. 결정 사항 (2026-09-03 확정 — 전부 권고안 채택)

| ID | 질문 | 기본안(권고) |
|---|---|---|
| **D-RS1** | 고인 상태 코드 정본을 `FUNERAL_IN_PROGRESS/FUNERAL_DEPARTURE_COMPLETED/COMPLETED` 3개로 줄이는 데 동의하나? `DISCHARGED`(발인 완료)를 별도 상태로 남길지? | 3개로 축소, 발인 완료는 `burial_date` 경과로 표현 |
| **D-RS2** | 장비 CPU 온도를 이식할까? (플레이어 하트비트 확장 + devices 텔레메트리 컬럼 + EF 마이그레이션 필요) | **보류** — 온라인/오프라인으로 운영 가능하면 생략, 필요 시 별도 건 |
| **D-RS3** | 장비 원격 명령 범위: 화면 전원 ON/OFF + 앱 재시작까지? OS 재부팅/종료까지? | 앱 재시작까지 (OS 제어는 플랫폼 제약·위험 큼) |
| **D-RS4** | 장비 제어·출상 취소의 권한 기준 역할은? | `SUPER_ADMINISTRATOR` + `PARTNER_ADMINISTRATOR` (D-H1 판단과 정합) |
| **D-RS5** | 음악(미디어)의 건물별 노출 제한을 이식할까? (media_sources 에 건물 매핑 추가) | **생략** — 옛 실데이터 2행. 필요해지면 미디어 관리에 건물 태그로 추가 |
| **D-RS6** | 한 고인의 다중 호실 배정(기존고인연결)을 지원할까? 스키마는 가능하나 서비스·화면·플레이어 전반의 단일 배정 가정을 걷어야 한다 | **생략(단일 배정 유지 + 중복 배정 차단 검증 추가)** — 옛 계정설정 옵션이었고 사용 흔적 미미 |
| **D-RS7** | 장비 화면 미리보기: 웹 미리보기 화면을 새로 만들까, 장비 스크린샷 요청 명령으로 할까, 보류할까 | **보류** — 3단계의 원격 제어가 들어오면 스크린샷 명령이 자연스러운 후속 |
| **D-RS8** | 화면 B~E 를 장기적으로 통폐합할까? (A 가 반응형이 되면 E 의 존재 이유가 약해진다) | 이번 범위에선 전부 유지(요구 5), 통폐합은 운영 후 별도 결정 |

---

## 7. 진행 기록

### 2026-09-03 — 0단계(기반 정리) 구현 완료

**상태 코드 통일 (D-RS1).** 정본 셋을 `Entities/DeceasedStatus.cs` 로 뒀다 —
`FUNERAL_IN_PROGRESS`(진행중) · `FUNERAL_DEPARTURE_COMPLETED`(출상) · `COMPLETED`(종료).
엔티티·DTO 기본값을 진행중으로 바꾸고, 저장 경로(등록·수정·상세저장)에
`ValidateStatus` 검증을 넣었다 — 옛 값은 `Normalize` 가 정본으로 고쳐 받고,
그 밖의 자유 문자열은 400 으로 거부한다. 기존 데이터는 EF 마이그레이션
`20260903123820_UnifyDeceasedStatusCodes` 가 치환했다(개발 DB 적용 완료 —
**운영 배포 때 `dotnet ef database update` 를 잊지 말 것**).
화면 쪽은 고인 그리드·고인현황 통계·고인 정보 조회의 옛 코드
(IN_HOSPITAL·DISCHARGED·SETTLEMENT_COMPLETED) 사용처를 전부 치환했다.

**점유 술어 통일.** 정본은 "배정이 살아 있고(`end_time` 없음, 삭제 아님) +
고인이 장례 진행중" 하나다. `StatusService`(화면 B~E) · 화면 A ·
플레이어 쿼리 셋(영정·입구안내·키오스크)이 모두 이 기준을 쓴다.
검증 중에 플레이어 쿼리가 `end_time` 을 안 보고 있던 것을 발견했다 —
끝났지만 삭제되지 않은 배정이 있으면 공실에 고인이 계속 표출되던 실결함이었고
(개발 DB 의 JS VIP 1호가 실제로 그랬다), 함께 고쳤다.

**서버 조인 API.** `GET /status/room-board` (`RoomBoardQueryDto` →
`RoomBoardDto { rooms, commonDevices, summary }`). 화면 A 의 4-API 클라이언트
조인을 이것 하나로 바꿨고, 이중 봉투 벗기기·`console.log`·`catch(eee)`·
미사용 함수도 걷어냈다. 빈 호실의 `lastVacatedAt`(마지막 퇴실)은
배정 이력의 `max(end_time)` 으로 유도한다 — 스키마 변경 없음.
기간 파라미터 이름은 거르는 컬럼을 그대로 말한다(`coffin*`=입관, `burial*`=발인).
장비 필터가 전부 비면 `GetByFilterAsync` 가 빈 목록을 주므로(가드)
'회사 전체' 조회는 `GetAllAsync` 로 받는다.

**출상 전용 API.** `PUT /building/deceased/{id}/depart` 신설. 예전에는 화면이
전체 PUT 을 재구성해 보내서 목록 DTO 에 없는 칸(비고·주민번호·사망원인·장지)이
**지워지고 있었다.** 출상 취소에는 옛 시스템의 규칙(되돌아갈 호실에 다른 고인이
있으면 거부)을 넣었다. 화면 A 의 출상은 이제 상태 태그와 분리된 명시적 버튼이다 —
상태 코드 어긋남 탓에 else 가지가 우연히 노출되던 구조를 제거했다.

**배정 검증 (D-RS6).** 등록·수정의 호실 배정에 `EnsureRoomAssignableAsync` —
호실 존재·`ACTIVE`·타 고인 점유 여부를 검사하고 400 으로 사유를 돌려준다.
`CreatedBy/UpdatedBy` 는 게이트웨이 `X-User-Id` 를 기록한다("System" 고정 제거).

**그 밖의 결함 수리.** `api/funeral/status` 모듈이 봉투를 안 벗기고 있어
(준수사항 7 위반) `unwrapOne/unwrapList` 로 고쳤다 — 심플·모바일 등 B~E 화면이
이 덕에 실데이터를 받는다. 장비 상세 모달의 리본·오버레이 그리드 컬럼이
DTO 에 없는 필드(text·ribbonType·overlayKey 등)를 그려 빈 칸으로 나오던 것을
실제 필드(mediaSourceName·positionLeft/Top·textContent·fontColor 등)로 맞췄다.

**검증.** funeralv2Api Debug 빌드·`pnpm vite build` 통과. 브라우저 실측 —
화면 A 서버 조인 렌더(요약 배너·공용 장비 패널·장비 타일·미디어명),
출상 → 공실 전환 + 방금 퇴실 시각 표시 → 출상 취소 복귀 왕복,
화면 C(그리드)·D(심플) 정상, 플레이어 입구안내 익명 라우트의 점유 판정 교정 확인.

**이번에 하지 않은 것.** `DeceasedSearchDto` 의 `RoomEnter*` 파라미터 이름
혼동(입관일을 거른다)은 기존 고인 목록 API 호환 때문에 그대로 뒀고,
상세저장(detail)의 호실 이력 병합 경로에는 배정 검증을 넣지 않았다
(이력 편집 화면이 과거 행을 다루기 때문 — 1단계 이후 별도 판단).

### 2026-09-03 — 1단계(반응형) 구현 완료

호실 카드 행의 고정 flex 를 **반응형 그리드**(1/2/3/4/5열 — sm·xl·2xl·2200px)로
바꾸고 카드 고정 높이(175px)를 걷어냈다. 장비 타일은 `flex-wrap`, 건물 요약
배너는 모바일에서 제목 아래 4열로 내려온다. 카드에 **입관·발인·장지**를 표시한다
(3.1 의 표시 갭 보강). 실측 — 모바일(375px)·태블릿(768px)·데스크탑(1280px)에서
가로 넘침 0px (16번 문서 R2 의 477px 넘침 해소), 각각 1·2·3열.

### 2026-09-03 — 2단계(관리 액션) 구현 완료

카드 헤더의 **⋮ 메뉴**가 모든 관리 액션의 단일 진입점이다.

| 액션 | 구현 | ASIS 대응 |
|---|---|---|
| 고인 정보 관리 | 고인관리의 종합 드로어를 그대로 재사용 (사진·상주 포함) | A2·A3·A7 |
| 호실 변경 | `GET /building/room/available`(ACTIVE+미점유, 옛 `p_room_get_other_room`) + `PUT /deceased/{id}/room`(배정만 바꾸는 전용 API, `MoveRoomAsync`) + `useVbenModal` 팝업 | A10 |
| 호실 영상·음원 일괄 변경 | 하위 메뉴 — 호실의 전 장비 속성에 순차 적용 | A4·A5 (고인 단위=호실 일괄) |
| 출상 처리 | 기존 버튼 유지 + 메뉴에도 | A11 |
| 고인 등록 (공실) | 카드 버튼+메뉴 → 드로어 신규 모드에 **호실 프리필** ('자동생성' 즉시 INSERT 대체) | A8 |
| 출상 취소 (공실) | room-board 가 내려주는 `lastDepartedDeceased*`(마지막 배정의 고인이 출상 완료일 때) 로 진입 | A11' |
| 바로가기 | `/building/deceased` · `/building/room` 라우터 이동 | A6·A7 |

다중 호실 배정(A9)은 D-RS6 확정대로 이식하지 않는다(중복 배정 차단이 대신 들어감).

### 2026-09-03 — 3단계(장비 제어) 구현 완료

- **화면 전원 ON/OFF** — 기존 `screen-power` API 를 카드·공용 장비 타일의
  전원 아이콘 드롭다운으로 노출.
- **앱 재시작 (D-RS3)** — `POST /building/device/app-restart/{code}` 신설
  (`SendAppRestartAsync` → SignalR `AppRestart`). 플레이어는 수신하면
  UnregisterDevice 후 `exit(0)` — 리눅스는 systemd(`Restart=always, RestartSec=3`)가
  되살린다(48번 문서). 안드로이드는 키오스크 런처가 되살릴 때만 유효.
  옛 `SHUTDOWN='reboot'`(하트비트 응답)를 푸시로 옮긴 것. OS 종료는 D-RS3 대로 제외.
- **권한 게이트 (D-RS4)** — `UserContext.CanControlDevices`
  (역할: `ADMINISTRATOR`·`SYSTEM_ADMINISTRATOR`·`PARTNER_ADMINISTRATOR` — 확정문의
  SUPER_ADMINISTRATOR 는 실제 역할 코드 기준으로 이렇게 옮겼다). 서버는
  screen-power·app-restart·**출상 취소**에 403, 화면은 같은 역할 목록으로 숨긴다.
  게이트웨이 `X-User-Roles`(전체 역할, 쉼표)를 UserContext 가 새로 읽는다.
  실측 — 역할 없는 계정 403 + 사유 표시, ADMINISTRATOR 200.
  ⚠ **개발 계정 syl 에는 역할이 없어 제어가 숨겨진다** — 역할 배정 필요
  (현재 ADMINISTRATOR 는 vben, SYSTEM_ADMINISTRATOR 는 quristyle 뿐).

### 2026-09-03 — 4단계(갱신 체계) 구현 완료

- **60초 폴링**(탭이 보일 때만) + **탭 복귀(visibilitychange) 즉시 재조회** —
  옛 화면의 180초 폴링·탭 복귀 재조회 대응. 스피너 없는 `reloadSilently`.
- **`RoomAssignmentChanged` 푸시** — `SendDeviceChangedByRoomIdAsync` 가 배정이
  바뀌는 자리(등록·수정·이동·출상·취소)에서만 불리는 점을 이용해 거기서 함께
  브로드캐스트한다. 화면은 0.8초 디바운스로 재조회. 실측 — 화면을 열어 둔 채
  API 로 출상/취소를 쏘면 **수동 새로고침 없이** 카드가 바뀐다.
- **SignalR 수리**(frontend-analysis 5.1) — `accessTokenFactory`(익명 해제 D-M1 대비),
  `onreconnected` 재동기화. 허브 URL 은 게이트웨이 익명 라우트 유지라 상수 그대로.

**배포 주의.** ① EF 마이그레이션(상태 코드 치환) 실행 필요. ② 플레이어(AppRestart
수신)는 앱 배포가 함께 나가야 재시작 명령이 유효하다 — 나가기 전엔 명령이 무시될
뿐 해롭지 않다. ③ 출상 취소가 관리자 역할 전용이 되었으므로 운영 계정 역할을
먼저 점검할 것.

## 8. 근거 위치

- ASIS 화면: `C:\down\funeralfr_oldsrc\page\monitor\room_status.jsp` (648줄),
  `js/page.js` 1998~2583 (탭·출상·미디어·장비 제어), `js/monitor.js` 4~49 (호실변경·기존고인연결),
  `fr_base.jsp` 66~78 (프로시저 디스패치)
- ASIS DB: `smfr.p_room_roomstatus2`·`p_goin_chulsang`(+cancel)·`p_goin_next_reservation`·
  `p_room_goin_save`·`p_room_get_other_room`·`p_machine_view_type_change`·
  `p_fr_frinfo_shutdown`·`machine_powers/shutdowns/temps` — 옛 DB `pg_get_functiondef` 로 원문 확인
- TOBE 프론트: `views/funeral/building/status/index.vue`(SignalR·미디어 변경),
  `composables/use-status-data.ts:141-154`(클라이언트 조인·점유 술어),
  `modules/room-card.vue:24-55,108-111`(출상·상태 태그), `building-section.vue:285`(비반응형 flex)
- TOBE 백엔드: `Services/StatusService.cs:62-205`, `Services/DeceasedService.cs:249-333`(배정 변경
  + SignalR 전파), `:1252-1303`(출상 취소), `:907-1006`(플레이어 입구안내),
  `Endpoints/DeviceEndpoints.cs:104-140`(screen-power), `Hubs/DeviceHub.cs`
- 관련 문서: 40(옛 시스템 이식)·44(팝업→드로어)·45(응답 봉투)·46(플레이어 익명 접근)·
  16(준수사항 점검 R2)·frontend-analysis(SignalR·봉투 결함)
