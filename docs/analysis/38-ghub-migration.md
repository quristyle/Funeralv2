# 38. GHUB(SK가스 지허브) 이식 — 생활과환경 (기상 · 생일)

2026-08-29. GHUB(C:\GHUB\GHUB — SK gas 지허브 시스템)의 업무 중 **기상(WEATHER)** 과
**생일(BIRTHDAY)** 을 포털 MSA 로 이식했다. **GHUB 원본 소스와 ASIS DB 는 일절 손대지
않았다** (ASIS 는 읽기 전용 세션으로만 열었다).

## 1. 무엇이 생겼나

| 층 | 것 | 위치 |
|---|---|---|
| DB | `ghub` DB · `ghub` 스키마 · `ghub` 롤 (jin114.co.kr:31015) | 스키마: [docs/sql/ghub_schema.sql](../sql/ghub_schema.sql) |
| 자료 이관 | ASIS(jin114.co.kr:45750) → TOBE, 17개 표 181,236행 | [scripts/ghub-db-migration/](../../scripts/ghub-db-migration/) |
| 백엔드 | **GhubServer** (:5490, 루프백 고정) | microservices/GhubServer |
| 게이트웨이 | `/api/ghub/{**}` → 5490, 접두사 제거, 전 경로 인증 필수 | ApiGateway/appsettings.json |
| 메뉴 | `생활과환경`(LIFEENV) 아래 카탈로그 4 + 화면 11 | [docs/sql/ghub_menu_seed.sql](../sql/ghub_menu_seed.sql) |
| 프론트 | views/life/weather 8화면 · views/life/birthday 3화면 + API 레이어 | fronts/apps/jsini-portal/src/{views,api}/life |
| 기동 | `dev.bat ghub` · backend_run_{ubuntu,mac}.sh · smoke-test 포트 목록 | 각 스크립트 |

메뉴 ID 대응(ASIS 숫자 id → 포털 텍스트 id)은 ghub_menu_seed.sql 머리말에 있다.
역할 권한은 우선 ADMINISTRATOR · SYSTEM_ADMINISTRATOR 에만 주었다 — ASIS 는 전 직원
공개였으므로, 어느 역할까지 열지는 역할 권한 화면에서 정하면 된다.

## 2. 백엔드에서 원본과 다르게 한 것

- **인증**: 원본의 JWT 미들웨어 · AuthFilter · ApiAuditFilter 를 걷어내고 포털 규약
  (게이트웨이가 검증, 서비스는 X-User-* 헤더 신뢰 + 루프백 바인딩)을 따른다.
- **응답 봉투**: JSini.Shared 의 ApiResponse(`code: S000`) — 프론트 requestClient 와 맞다.
- **기상청 인증키**: 원본은 소스 13곳에 하드코딩돼 있었다. `Weather:ServiceKey` 설정으로
  뺐고 실제 값은 appsettings.Local.json 에만 있다. **그 키는 이미 GHUB 저장소에 노출된
  것이라 재발급을 권한다** (재발급하면 Local 만 바꾸면 된다). 키가 없으면 수집은 쉬고
  실시간 조회는 조용히 빈 값을 준다 — 기동은 막지 않는다.
- **시간대**: 원본의 `DateTime.Now` / `AddHours(±9)` 혼용을 정리했다. 저장은 전부
  UTC(DateTimeOffset), 기상청 요청 문자열을 만들 때만 KST(Utilities/Kst.cs).
- **알림 발송 절단**: 기준 초과 판정과 weather_event_records 기록은 그대로 두고
  발송(웹푸시 · 이메일 · 카카오 알림톡)만 잘랐다. 아래 4절 참조.
- **영구 빈 테이블 5개 미이식**: weather_warning_details · weather_warning_codes ·
  weather_info_reports · weather_breaking_news · weather_preliminary_warnings 는 원본의
  수집 코드가 죽어 있어 항상 비어 있었다. 대응 엔드포인트와 함께 뺐다.
  debug-fetch(응답에 인증키 노출) · POST /weather(호출자 없음)도 뺐다.
- **원본 버그 수정**: 특보 요약 그룹키가 호출마다 바뀌던 것(Guid), 이벤트 조회의
  endDate 미적용, warnings4location 의 N+1, 이름 기반 캐시 키. 동작 결과는 원본과 같다.

## 3. 생일 — 명단을 어디에 두었나

ASIS 는 생일을 자체 사용자 표(user_profiles)에 두었지만 포털의 사용자 정본
(scom.accounts)에는 생년월일이 없다. 서비스 경계를 넘는 FK 도 둘 수 없으므로
**ghub.birthday_profiles 를 신설**해 ASIS user_profiles 의 생일 관련 필드만
(user_id · 이름 · 부서 · 소속 · 썸네일 · 생일 · 음력 · 축하대상 · 활성) 이관했다(67명).

- 메시지의 보낸 이는 X-User-Id 로 기록한다. **포털 계정과 이 명단은 user_id 문자열로만
  느슨하게 이어진다** — msa_user_import 로 들어온 지허브 사용자라면 자연히 맞는다.
- 명단에 없는 포털 사용자가 메시지를 보내면 이름은 user_id 로 폴백한다.
- 원본의 위험 동작(수정 API 가 로그인 ID 를 덮어쓰는 것)은 제거했고, 등록은 upsert 로
  바꿔 새 명단 행을 만들 수 있다.

## 4. 결정 대기 — 알림 연동 (D-G1)

기상 기준 초과와 생일 메시지 도착 때 원본은 웹푸시 · 이메일 · 카카오 알림톡
(비즈뿌리오)을 쐈다. 포털에는 NotificationServer 가 있으므로 **그쪽으로 연동하는 것이
맞다고 보고 이식하지 않았다.** 남은 결정:

- D-G1a. 기상 이벤트 알림을 NotificationServer 로 보낼지, 보낸다면 수신 대상(역할? 구독?)
- D-G1b. 카카오 알림톡(비즈뿌리오) 채널을 NotificationServer 에 추가할지 —
  GHUB 의 KakaoUtil · 비즈뿌리오 계정 · 템플릿 코드는 원본에 그대로 있다.
- 판정 쪽은 준비돼 있다: weather_event_records.is_notified 가 원본 규칙(발송 전 true)
  그대로라, 연동을 붙일 때 이 값의 의미를 "발송 성공" 으로 바꿀지도 함께 정한다.

## 5. 이식하지 않은 GHUB 기능 (이번 범위 밖)

안전(SHE) 전반 · 안전교육 · 협력사/프로젝트 서류 · 교대근무 · 식단 · 커넥트허브 ·
GHUB 자체 시스템 관리. 필요해지면 GhubServer 에 모듈을 더하거나 별도 서비스로 뗀다.

## 6. 운영 메모

- 수집은 GhubServer 안의 HostedService 가 30분 주기로 돈다(`Weather:CollectMinutes`).
  **인스턴스를 여러 개 띄우면 수집이 중복된다** — 지금은 단일 인스턴스 전제.
- ASIS 가 아직 살아 있으므로 당분간 양쪽 다 수집한다. 지허브 쪽 운영을 끄는 시점은
  SK gas 와의 계약/운영 문제라 여기서 정하지 않는다.
- 프론트의 vxe 그리드는 **배열만 반환하면 0행이 된다** — `response: { list: 'result' }`
  + `{ result, page: { total } }` 로 감싼다 (portal/system/menu/list.vue 의 선례,
  이번에 life 화면 전부 같은 방식으로 맞췄다).
- requestListClient(dataField `data.result`)는 점 경로를 못 읽는다 — life API 레이어는
  requestClient + toList/toOne 헬퍼를 쓴다 (menu-favorite.ts 의 선례).
