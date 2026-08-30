# 38. GHUB(SK가스 지허브) 이식 — 생활과환경 (기상 · 생일)

2026-08-29. GHUB(C:\GHUB\GHUB — SK gas 지허브 시스템)의 업무 중 **기상(WEATHER)** 과
**생일(BIRTHDAY)** 을 포털 MSA 로 이식했다. **GHUB 원본 소스와 ASIS DB 는 일절 손대지
않았다** (ASIS 는 읽기 전용 세션으로만 열었다).

## 0. 이름 — LifeEnvServer

처음에 서비스 이름을 GhubServer 로 지었다가 **LifeEnvServer 로 바꿨다** (2026-08-30).
GHUB 는 원천 시스템(SK가스 지허브)의 이름이지 이 서비스의 업무가 아니다 — 업무 이름은
생활과환경이고, 프론트 모듈(views/life) · 메뉴(LIFEENV) · 게이트웨이 경로(/api/life) ·
기동 이름(dev.bat life)이 모두 그에 맞춰져 있다. **DB 이름(`ghub`)만 그대로다** —
접속 정보가 그렇게 발급됐고, 자료의 출처를 이름이 말해 주는 것도 나쁘지 않다.

## 1. 무엇이 생겼나

| 층 | 것 | 위치 |
|---|---|---|
| DB | `ghub` DB · `ghub` 스키마 · `ghub` 롤 (jin114.co.kr:31015) | 스키마: [docs/sql/ghub_schema.sql](../sql/ghub_schema.sql) |
| 자료 이관 | ASIS(jin114.co.kr:45750) → TOBE, 17개 표 181,236행 | [scripts/ghub-db-migration/](../../scripts/ghub-db-migration/) |
| 백엔드 | **LifeEnvServer** (:5490, 루프백 고정) | microservices/LifeEnvServer |
| 게이트웨이 | `/api/life/{**}` → 5490, 접두사 제거, 전 경로 인증 필수 | ApiGateway/appsettings.json |
| 메뉴 | `생활과환경`(LIFEENV) 아래 카탈로그 4 + 화면 11 | [docs/sql/ghub_menu_seed.sql](../sql/ghub_menu_seed.sql) |
| 프론트 | views/life/weather 8화면 · views/life/birthday 3화면 + API 레이어 | fronts/apps/jsini-portal/src/{views,api}/life |
| 기동 | `dev.bat life` · backend_run_{ubuntu,mac}.sh · smoke-test 포트 목록 | 각 스크립트 |

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

## 3. 생일 — 포털이 관리한다 (A안, 2026-08-30)

두 번에 걸쳐 자리를 옮겼다.

1. 처음에는 ASIS user_profiles 를 이관한 **ghub.birthday_profiles** 를 두고
   LifeEnvServer 가 읽었다.
2. "생일은 포털 사용자의 속성" 이라는 결정(A안)으로 **정본과 API 를 모두 포털로**
   옮겼다. birthday_profiles · ghub.birthday_messages 는 지웠다.

지금 구조:

| 것 | 자리 |
|---|---|
| 생년월일 · 음력 · 축하표시 | `scom.accounts` (docs/sql/account_birthday.sql) |
| 축하 메시지 | `scom.birthday_messages` (docs/sql/birthday_messages.sql) |
| API | **AuthServer** `/birthday/*` (게이트웨이 `/api/auth/birthday/*`) |
| 입력 · 수정 | 포털 [계정 관리] 화면 (생년월일 · 음력 · 생일 축하 표시) |
| 화면 | 생활과환경 메뉴 그대로 (views/life/birthday — 조회 · 메시지만) |
| 소속 필터 | 회사(companyId) · 부서(departmentId) — scom companies/departments 기준 |

- LifeEnvServer 는 기상 전담이 됐다. 생일 코드는 남아 있지 않다.
- ASIS 명단 46명 중 포털 계정과 user_id 가 맞은 것은 1명뿐이었다 — 지허브 임직원
  대부분이 포털 사용자가 아니다. 나머지 45명의 생일은 버렸다(지시 확인 받음).
  필요해지면 ASIS(user_profiles)가 살아 있으므로 계정을 만든 뒤 다시 읽어 올 수 있다.
- 계정 수정 화면의 기존 버그를 함께 고쳤다 — 부서 목록을 로그인한 사람의 회사로만
  받아 와서, 다른 회사 계정을 열면 소속 회사 프리필이 비고 저장이 조용히 막혔다
  (getDeptList(undefined, true) 로 전 회사 조회).

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
GHUB 자체 시스템 관리. 필요해지면 LifeEnvServer 에 모듈을 더하거나 별도 서비스로 뗀다.

## 6. 운영 메모

- 수집은 LifeEnvServer 안의 HostedService 가 30분 주기로 돈다(`Weather:CollectMinutes`).
  **인스턴스를 여러 개 띄우면 수집이 중복된다** — 지금은 단일 인스턴스 전제.
- ASIS 가 아직 살아 있으므로 당분간 양쪽 다 수집한다. 지허브 쪽 운영을 끄는 시점은
  SK gas 와의 계약/운영 문제라 여기서 정하지 않는다.
- 프론트의 vxe 그리드는 **배열만 반환하면 0행이 된다** — `response: { list: 'result' }`
  + `{ result, page: { total } }` 로 감싼다 (portal/system/menu/list.vue 의 선례,
  이번에 life 화면 전부 같은 방식으로 맞췄다).
- requestListClient(dataField `data.result`)는 점 경로를 못 읽는다 — life API 레이어는
  requestClient + toList/toOne 헬퍼를 쓴다 (menu-favorite.ts 의 선례).
