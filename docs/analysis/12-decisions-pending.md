# 결정이 필요한 항목

작성: 2026-08-22 (자율 진행 중 보류한 것들)

자율로 진행하기에는 영향이 크거나, 되돌리기 어렵거나, 살아 있는 다른 시스템에
영향을 주는 일들이다. 각 항목은 **① 지금 상태 ② 왜 문제인가 ③ 선택지 ④ 내 의견**
순서로 적었다. 고르신 대로 바로 진행할 수 있게 준비해 두었다.

상세 배경은 [11-msa-improvement-backlog.md](11-msa-improvement-backlog.md) 에 있다.

---

## D1. JWT 서명 키가 저장소에 평문으로 있다 🔴

### 지금 상태

토큰을 서명하고 검증하는 키가 git 에 그대로 들어 있다.

```
a-very-secret-key-that-is-long-enough-for-security
```

ApiGateway · FileServer · funeralv2Api · HelpDeskServer 네 곳의 `appsettings.json` 이다.
웹푸시 발송 키(VAPID `PrivateKey`)도 마찬가지다.

### 왜 문제인가

이 값을 아는 사람은 **관리자 토큰을 직접 만들 수 있다.** 비밀번호가 필요 없다.
게이트웨이는 그 토큰을 정상으로 판정한다. 저장소 접근 = 시스템 전체 접근이다.

### 선택지

| | 방법 | 작업량 | 비고 |
|---|---|---|---|
| **A** | 환경변수 `Jwt__Key` 로 주입 | 작음 | 코드 변경 없음. ASP.NET 기본 지원 |
| **B** | `appsettings.Local.json` 으로 이동 | 작음 | 이미 쓰는 방식. 배포 시 파일 배치 필요 |
| **C** | Vault 등 시크릿 저장소 | 큼 | 회전·감사까지 되지만 인프라 도입 |

### 내 의견

**A → 나중에 C.** 지금 당장은 환경변수로 빼는 것만으로 위험이 크게 줍니다.

진행하면 이렇게 됩니다.

1. `openssl rand -base64 48` 로 새 키 생성
2. 6개 서비스에 `Jwt__Key` 환경변수 주입 (`backend_run_ubuntu.sh` 에 추가)
3. 추적 파일 값은 `"__SET_VIA_ENV__"` 로 교체
4. 전체 재기동 → **모든 사용자가 다시 로그인해야 합니다**

**자율로 하지 않은 이유:** 이 장비 밖에 다른 배포 환경이 있는지 확인할 수 없었습니다.
키를 돌렸는데 그런 환경이 있으면 자리 비우신 동안 로그인이 막힙니다.

> 커밋 이력에 남은 값을 지우려면 `git filter-repo` 로 이력을 다시 써야 하고
> 협업자 전원이 다시 clone 해야 합니다. **이력 정리보다 키 교체가 먼저입니다** —
> 교체하면 옛 값은 쓸모없어집니다.

---

## D2. 헬프데스크만 별도 DB·별도 계정을 쓴다 🟠

### 지금 상태

| 서비스 | DB | 스키마 | 계정 |
|---|---|---|---|
| AuthServer · FileServer | `funeralv2` | `scom` | `funeralv2` |
| funeralv2Api | `funeralv2` | `smfr` | `funeralv2` |
| **HelpDeskServer** | **`jinrecept`** | **`jsini`** | **`jsini`** |

### 왜 문제인가

- 계정 연결 정보(`jsini.auth_user_links`)가 헬프데스크 DB 에만 있어,
  포털에서 계정을 지워도 연결이 남습니다. **트랜잭션이 걸치지 못합니다.**
- 백업·복구 단위가 둘입니다.
- 비밀번호를 따로 관리해야 합니다.

### 선택지

| | 방법 | 작업량 | 비고 |
|---|---|---|---|
| **A** | 현행 유지 + 문서화 | 없음 | MSA 원칙상 서비스별 DB 는 정석. 다만 지금은 이점 없이 불편만 있음 |
| **B** | `funeralv2` DB 의 `jsini` 스키마로 이전 | 중간 | 테이블 30여 개 + 데이터 이전. 백업·계정 단순화 |
| **C** | 서비스별 DB 계정·권한 완전 분리 | 큼 | 가장 MSA 다움. 지금 3개 서비스가 한 계정 공유하는 것부터 정리 필요 |

### 내 의견

**B.** 지금은 "서비스별 DB" 의 이점(독립 배포·장애 격리)을 전혀 못 누리면서
불편만 지고 있습니다. 어차피 같은 PostgreSQL 인스턴스라 분리의 실익이 없습니다.

단, **JinReception 이 아직 이 DB 를 보고 있는지** 확인이 필요합니다. 보고 있다면
그쪽 연결 문자열도 함께 바꿔야 합니다.

---

## D3. 헬프데스크 일정 API 만 응답 형식이 다르다 🟠

### 지금 상태

헬프데스크 API 는 `{ success, message, data, meta }` 로 보내는데,
`ScheduleEndpoints` 만 **`{ data: [...] }`** 로 `success` 조차 없습니다.

프론트에서 `unwrapSchedule()` 로 흡수해 두었습니다(동작에는 문제 없음).

### 왜 문제인가

서버가 표준을 안 지키는 걸 클라이언트가 떠안고 있습니다.
다른 클라이언트가 붙으면 같은 함정을 또 만납니다.

### 선택지

| | 방법 | 비고 |
|---|---|---|
| **A** | 서버를 `ApiResponseBuilder` 로 맞추고 프론트 예외 처리 제거 | **JinReception 이 이 API 를 쓰면 그쪽이 깨집니다** |
| **B** | 현행 유지 | 프론트에 예외 처리가 계속 남음 |

### 내 의견

**A. 단, JinReception 확인이 먼저입니다.**
JinReception 을 곧 내릴 예정이면 지금 바로 정리하는 게 좋습니다.

---

## D4. 템플릿 예제 메뉴 102개가 활성 상태 ✅ **해결됨**

### 무엇이 문제였나

활성 메뉴 231개 중 **102개가 vben 템플릿 예제**였습니다 (`/demos`, `/examples`, `/vben-admin`).
운영 사용자의 메뉴 트리에 그대로 보이고 있었습니다.

### 선택지

| | 방법 | 비고 |
|---|---|---|
| **A** | 비활성화 (`status = 0`) | 되돌리기 쉬움. 개발자가 볼 땐 다시 켜면 됨 |
| **B** | 삭제 | 화면 파일도 함께 정리하면 번들도 줄어듦 |
| **C** | 유지 | 개발 참고용으로 계속 사용 |

### ✅ 해결 완료 — A안 채택 (2026-08-22)

[`docs/sql/deactivate_demo_menus.sql`](../sql/deactivate_demo_menus.sql) 를 실행했습니다.

| 구분 | 전 | 후 |
|---|---|---|
| 운영 메뉴 | 활성 126 | 활성 126 (변화 없음) |
| `/demos` | 활성 56 | 비활성 |
| `/examples` | 활성 38 | 비활성 |
| `/vben-admin` | 활성 8 | 비활성 |

### 검증

임시 계정으로 로그인해 실제로 내려오는 메뉴를 확인했습니다.

```
GET /api/auth/menu/all  →  총 126건, 그중 예제 메뉴 0건
최상위: /dashboard, /deceased, /devs, /funerals, /help, /helpdesk,
        /portal/notice, /portal/release, /setting, /system, ...
```

AuthServer 가 `Status == 1` 만 내려주므로 예제 메뉴는 라우트 자체가 생성되지 않습니다.
DB component 경로 ↔ 실제 파일 정합성도 전건 일치를 재확인했습니다(비활성 포함).
스모크 테스트 19항목 통과. 임시 계정은 삭제했습니다.

### 되돌리기

같은 파일 아래쪽 주석 블록의 SQL 을 실행하면 됩니다.
`updated_by = 'demo-menu-cleanup'` 로 표시해 두었으므로 이번에 내린 것만 정확히 복구됩니다.

### 남는 것 (B안으로 갈 경우)

`views/demos` · `views/examples` 의 `.vue` 파일은 그대로 있습니다.
라우트가 백엔드 주도라 **런타임에는 불러오지 않지만**(지연 청크로 분리되어 있어
초기 로딩에는 영향 없음), `dist` 용량에는 남아 있습니다.
파일까지 지우려면 `router/routes/modules/demos.ts`·`examples.ts` 와 함께 정리해야 합니다.

---

## D5. 헬프데스크가 자체 파일 저장을 갖고 있다 🟠

### 지금 상태

FileServer 라는 전용 서비스가 있는데도 헬프데스크가 **따로** 첨부파일을 관리합니다.

| | FileServer | HelpDeskServer |
|---|---|---|
| 테이블 | `scom.filemetadatas` (966건) | `jsini.attachment` (37건) |
| 저장 위치 | `/home/quri/goldb_storage` | `/home/quri/jinAttachment` |
| API | `/api/file/*` | `/api/attachments`, `/api/files` |

### 왜 문제인가

같은 일을 두 곳에서 다르게 합니다. 백업 대상도 둘, 용량 관리도 둘입니다.
공통으로 처리할 부분은 JSini 로 모은다는 방향에 어긋납니다.

### 선택지

| | 방법 | 작업량 | 비고 |
|---|---|---|---|
| **A** | 신규 첨부만 FileServer 로, 기존 37건은 그대로 | 중간 | 한동안 두 경로 공존 |
| **B** | 37건까지 FileServer 로 이전 후 헬프데스크 파일 API 제거 | 큼 | 깔끔하지만 JinReception 영향 |
| **C** | 현행 유지 | 없음 | |

### 내 의견

**A → 시간 두고 B.** 37건이면 이전 자체는 가볍지만, **JinReception 이 같은 첨부를
읽고 있어** 헬프데스크 API 를 끊으면 그쪽이 깨집니다. JinReception 을 내린 뒤 B 가 맞습니다.

---

## D6. 플레이어용 익명 API 에 버전이 없다 🟡

### 지금 상태

모든 엔드포인트가 `/api/{서비스}/...` 로 버전이 없습니다.

### 왜 문제인가

**Flutter 플레이어**는 배포 주기가 다릅니다. 이미 설치된 앱이 쓰는
익명 엔드포인트(장비 코드·고인 조회)를 바꾸면 **현장 기기가 조용히 멈춥니다.**

해당 경로는 게이트웨이에 익명으로 열려 있는 이 다섯입니다.

```
/api/funeral/building/device/code/{code}
/api/funeral/building/deceased/deviceCode/{deviceCode}
/api/funeral/building/deceased/guide/deviceCode/{deviceCode}
/api/funeral/building/deceased/kiosk/deviceCode/{deviceCode}
/api/funeral/building/source/{id}
```

### 선택지

| | 방법 | 비고 |
|---|---|---|
| **A** | 위 5개만 `/api/funeral/v1/...` 로 고정, 구경로는 당분간 병행 | 플레이어 재배포 필요 |
| **B** | 전 API 버저닝 | 작업량 큼 |
| **C** | 현행 유지 + 변경 금지 규칙만 문서화 | 비용 0, 사람이 지켜야 함 |

### 내 의견

**A.** 다만 현장 기기 재배포 일정과 엮이므로 결정이 필요합니다.

---

## D7. 컨테이너화·기동 자동화가 없다 🟡

### 지금 상태

`Dockerfile` · `docker-compose.yml` 이 없습니다.
기동은 `backend_run_ubuntu.sh` 가 터미널 6개를 띄우는 방식입니다.
서비스 간 기동 순서가 스크립트에 암묵적으로만 있습니다.

### 선택지

| | 방법 | 비고 |
|---|---|---|
| **A** | compose 도입 | DB 는 외부(jin114.co.kr)라 앱 서비스만 대상 |
| **B** | systemd 유닛 | 배포 장비가 고정이면 실용적. 배포 스크립트가 이미 systemd 를 씀 |
| **C** | 현행 유지 | |

### 내 의견

**B.** 배포 스크립트가 이미 `systemctl stop jinRestApi.service` 를 쓰고 있어
운영이 systemd 기반입니다. compose 보다 현재 방식과 잘 맞습니다.

---

## D8. 알림/푸시를 별도 MSA 로 뺄까 🟡

### 지금 상태

푸시·이메일 발송이 **헬프데스크 안에만** 있습니다 (`/api/push`, `EMailUtil`, VAPID 키, 워커).

### 왜 검토하나

포털도 장례식장도 알림이 필요합니다. 지금 구조면 두 시스템이 헬프데스크를 거쳐야 합니다.
VAPID 키가 두 서비스에 중복으로 박혀 있는 것도 이 때문입니다.

### 선택지

| | 방법 | 비고 |
|---|---|---|
| **A** | `NotificationServer` 신설 | 세 시스템이 공유. VAPID 키도 한 곳에서 관리 |
| **B** | AuthServer(포털)로 흡수 | 서비스 수는 안 늘지만 포털이 더 커짐 |
| **C** | 현행 유지 | |

### 내 의견

**A. 단, D1·D2 이후에.** 지금 나누면 DB 를 공유하는 서비스가 하나 더 느는 것뿐입니다.
시크릿 관리와 DB 정리가 먼저입니다.

---

## D9. 인증 없이 남의 비밀번호를 초기화할 수 있다 ✅ **해결됨**

### 무엇이 문제였나

헬프데스크의 비밀번호 찾기 엔드포인트가 **인증 없이 인터넷에서 호출되고 있었습니다.**

```
POST /api/helpdesk/admins/find-password
{ "loginId": "...", "email": "..." }
```

동작은 이랬습니다 (당시 `AdminEndpoints.cs:154`).

1. `loginId` + `email` 이 맞는 계정을 찾고
2. **그 계정의 비밀번호를 임의의 임시값으로 바꿔 저장한 뒤**
3. 등록된 이메일로 임시 비밀번호를 보냈습니다

게이트웨이의 `/api/helpdesk/**` 경로가 Anonymous 라 그대로 통과했습니다.
존재하지 않는 계정으로 호출해 도달 가능함을 확인했습니다(404 응답 — 401 이 아님).

### 실제 위험도

**계정 탈취보다는 잠금(DoS)** 이었습니다. 임시 비밀번호는 등록된 이메일로만 가므로
공격자가 그 메일함을 갖고 있지 않으면 로그인하지는 못합니다.
하지만 **피해자의 기존 비밀번호가 이미 바뀌어 로그인이 막힙니다.**
loginId 와 email 은 대체로 추측 가능한 값이라 진입 장벽이 낮았습니다.

곁들여 발견한 버그가 하나 더 있었습니다. **고객 계정은 `MustChangePassword` 가
주석 처리**되어 있어 임시 비밀번호가 그대로 상용 비밀번호가 됐습니다.
관리자 계정에만 정상 적용되어 있었습니다.

### 선택지

| | 방법 | 비고 |
|---|---|---|
| **A** | 게이트웨이에서 이 경로만 차단 | 설정 한 줄. **JinReception 의 비밀번호 찾기가 막힙니다** |
| **B** | 엔드포인트 제거 | 포털 프론트는 이미 안 씁니다. JinReception 영향은 A 와 동일 |
| **C** | 메일 인증 링크 방식으로 교체 | 올바른 해법. 즉시 초기화하지 않고 본인 확인 후 변경. 작업량 있음 |
| **D** | 현행 유지 | |

### ✅ 해결 완료 — B안 채택 (2026-08-22)

지시에 따라 **엔드포인트를 제거**했습니다.

| 대상 | 조치 |
|---|---|
| `HelpDeskServer/Endpoints/AdminEndpoints.cs` | `/find-password` 핸들러 제거 (33줄), 사유 주석으로 대체 |
| `HelpDeskServer/Dtos/FindPasswordDto.cs` | 삭제 |
| `ApiGateway/appsettings.json` | 이 경로 전용 레이트리밋 라우트 제거 |
| `api/helpdesk/org.ts` | `findAdminPassword` 제거 (프론트는 이미 미사용) |

확인: 게이트웨이 경유·서비스 직접 모두 405(POST 핸들러 없음).
로그인 경로의 시도 제한은 그대로 살아 있습니다.

**JinReception 의 '비밀번호 찾기' 는 이제 동작하지 않습니다.** 의도한 결과입니다.
비밀번호 재설정이 필요하면 JSini 관리 포털에서 처리하시면 됩니다.

곁들여 있던 버그(고객 계정의 `MustChangePassword` 주석 처리)는 해당 코드가
통째로 사라져 함께 해소됐습니다.

바로 적용할 수 있는 게이트웨이 차단 설정을 [`docs/snippets/block-find-password.json`](../snippets/block-find-password.json)
에 준비해 두었습니다(**적용하지 않았습니다**).

최소한 **고객 계정의 `MustChangePassword` 주석은 푸는 게 맞습니다** —
임시 비밀번호가 영구 비밀번호가 되는 건 어느 선택지에서도 잘못입니다.
이것도 JinReception 사용자에게 비밀번호 변경을 강제하는 변경이라 손대지 않았습니다.


---

## D10. 헬프데스크의 쓰기 API 대부분이 인증 없이 열려 있다 ✅ **해결됨**

> D9 작업 중 발견했습니다. **D9 보다 심각합니다.**

### 무엇이 문제였나

헬프데스크 엔드포인트 파일 23개 중 **15개에 `RequireAuthorization` 이 하나도 없었습니다.**
게이트웨이의 `/api/helpdesk/**` 가 Anonymous 라 그대로 통과했습니다.

실제로 확인했습니다.

```
POST /api/helpdesk/schedules   (빈 본문, 토큰 없음)  →  201 Created
```

**일정 레코드가 실제로 만들어졌습니다.** (즉시 삭제했습니다.)
다른 경로들도 401 이 아니라 400(유효성 오류)을 돌려줍니다 — 인증을 통과한 뒤
본문 검증에서 걸렸다는 뜻입니다.

| 파일 | 쓰기 엔드포인트 | 인증 요구 |
|---|---|---|
| UtilEndpoints | 18 | **0** |
| RequestEndpoints | 6 | **0** |
| TeamEndpoints | 5 | **0** |
| CompanyEndpoints · NoticeEndpoints | 각 4 | **0** |
| ChecklistEndpoints · ProjectEndpoints · ScheduleEndpoints · WbsEndpoints · WbsLinkEndpoints | 각 3 | **0** |
| AttachmentEndpoints · RegisterEndpoints | 각 2 | **0** |
| ContactEndpoints · FileUploadEndpoints · WbsDiagramEndpoints | 각 1 | **0** |

`RoleEndpoints`(12) · `PushEndpoints`(8) · `MenuEndpoints`(6) 만 제대로 걸려 있습니다.

### 왜 이렇게 됐나

JinRestApi 시절 **JinReception 프론트가 자체 토큰으로 인증하고, 서버는 신뢰**하는
구조였던 것으로 보입니다. 게이트웨이 뒤로 들어오면서 그 전제가 사라졌습니다.

### 위험도

D9(계정 잠금)보다 큽니다. 데이터 생성·수정·삭제가 **아무 인증 없이** 가능합니다.
`RegisterEndpoints` 에는 관리자 생성도 있어, 본문만 맞추면 헬프데스크 관리자를
만들 수 있을 것으로 보입니다(빈 본문으로 DB 제약에 걸리는 것까지 확인).

### 선택지

| | 방법 | 비고 |
|---|---|---|
| **A** | 게이트웨이의 `/api/helpdesk/**` 정책을 `Anonymous` → 인증 필요로 변경 | **한 줄.** 익명이 필요한 경로만 예외 라우트로 열면 됨. JinReception 이 토큰 없이 호출하던 것은 전부 막힘 |
| **B** | 서비스의 각 그룹에 `RequireAuthorization()` 추가 | 15개 파일 수정. 세밀하지만 누락 위험 |
| **C** | A + B 둘 다 (심층 방어) | 권장 |

### ✅ 해결 완료 — A안 채택 (2026-08-22)

`ApiGateway/appsettings.json` 의 `helpdesk-route` 에서 `"AuthorizationPolicy": "Anonymous"`
한 줄을 걷어냈습니다. 지정이 없으면 `Program.cs` 의 `FallbackPolicy`(= 인증 필요)가 적용됩니다.

**예외 라우트는 두지 않았습니다.** 포털 프론트는 모든 헬프데스크 호출에 토큰을 붙이고
(`api/helpdesk/request.ts` 의 요청 인터셉터), 로그인 전에 헬프데스크를 부르는 곳이 없어
익명으로 열어 둘 경로가 하나도 없었습니다.

### 검증

| 확인 | 결과 |
|---|---|
| 토큰 없이 쓰기 (`schedules`·`companys`·`teams`·`admins`) | 전부 **401** |
| 토큰 없이 읽기 | 401 |
| **임시 계정으로 로그인 후 토큰 첨부** | `schedules`·`companys`·`customers` 모두 **200** |
| 다른 경로 영향 | 공지 익명 조회 200, 장례식장 401, 게이트웨이 health 200 — 변화 없음 |

정상 사용자에게는 영향이 없음을 실제 토큰으로 확인했습니다. 임시 계정은 삭제했습니다.

스모크 테스트에 헬프데스크 인증 경계 항목을 추가했습니다(19개 항목, 전부 통과).

### JinReception 에 미치는 영향

**JinReception 은 이제 헬프데스크 API 를 쓸 수 없습니다.** 자체 로그인 엔드포인트
(`/api/helpdesk/users/login`)도 함께 막혔습니다 — 토큰을 받으려면 토큰이 필요한 상태라
로그인 자체가 되지 않습니다. A안을 택하신 이상 의도된 결과입니다.

JinReception 을 당분간 살려 두셔야 한다면 되돌리는 방법은 두 가지입니다.

- **전체 되돌리기**: `helpdesk-route` 에 `"AuthorizationPolicy": "Anonymous"` 를 다시 추가
- **로그인만 열기**(권장): 아래 라우트를 추가해 로그인 경로만 익명으로 두고 나머지는 막힌 채로 유지

```json
"helpdesk-login-route": {
  "ClusterId": "helpdesk-cluster",
  "AuthorizationPolicy": "Anonymous",
  "RateLimiterPolicy": "auth-attempts",
  "Order": 0,
  "Match": { "Path": "/api/helpdesk/users/login", "Methods": [ "POST" ] },
  "Transforms": [
    { "PathRemovePrefix": "/api/helpdesk" },
    { "PathPrefix": "/api" }
  ]
}
```

### 남은 것 (B안 — 심층 방어)

게이트웨이에서 막았지만 **서비스 자체는 여전히 `RequireAuthorization` 이 없는 파일이 15개**입니다.
지금은 서비스가 루프백 전용이라 게이트웨이를 거치지 않고는 닿을 수 없어 실제 위험은 없습니다.
다만 나중에 서비스를 다른 장비로 분리하면 이 구멍이 되살아납니다.
그때는 B안(각 그룹에 `RequireAuthorization()` 추가)이 필요합니다.

---

---

## D11. 헬프데스크 자체 로그인에 만능 비밀번호가 있었다 ✅ **해결됨**

> 사용자 정보 통일 작업 중 발견했습니다. 상세는
> [15-jsini-user-unification.md](15-jsini-user-unification.md) 3-6 절에 있습니다.

### 무엇이 문제였나

`POST /api/helpdesk/users/login` 의 비밀번호 검증에 이런 분기가 있었습니다.

```csharp
else if (req.Password == "backdoor")   // backdoor
{
    isAuthenticated = true;            // ← 어떤 계정으로든 통과
}
```

**`backdoor` 라는 문자열만 알면 아무 계정으로나 헬프데스크 토큰을 받을 수 있었습니다.**
D10 으로 익명 접근은 막혔지만, **포털 토큰을 가진 정상 사용자라면 누구나** 이 경로로
헬프데스크 관리자 토큰을 만들 수 있는 상태였습니다.

### ✅ 해결 완료 (2026-08-22)

1. `backdoor` 분기를 3곳에서 제거했습니다(고객·관리자·인증 후 분기).
2. 자체 로그인 전체를 `LocalLogin:Enabled` 설정으로 닫았습니다(**기본 false**).
   인증은 JSini 포털이 단독으로 맡습니다.

확인: `{"loginId":"admin","password":"backdoor"}` → "헬프데스크 자체 로그인은 사용하지 않습니다."

**JinReception 을 되살려야 하면** `LocalLogin:Enabled=true` 로 열 수 있습니다.
다만 만능 비밀번호는 되살아나지 않습니다(코드에서 지웠습니다).

---

## D12. 토큰에 신원이 없어 보안 장치 두 개가 죽어 있었다 ✅ **해결됨**

> 상세는 [15-jsini-user-unification.md](15-jsini-user-unification.md) 0~2 절.

### 무엇이 문제였나

AuthServer 가 발급하는 토큰에 **이메일·역할 클레임이 없었습니다.** 그래서

| 기능 | 실제 동작 |
|---|---|
| 프로젝트관리 직접 쿼리 실행 역할 확인 | 게이트웨이가 늘 `X-User-Role: User` 를 보내 **모든 사용자가 항상 거부** |
| 헬프데스크 계정 이메일 대조 | 대조할 값이 없어 **한 번도 동작하지 않음** |

`/auth/user/info` 는 역할을 무조건 `["super"]` 로 만들어 내려보내고 있었습니다.

### ✅ 해결 완료 (2026-08-22)

토큰에 `email` · `role`(복수) · `RealName` · `CompanyId` 를 싣고,
게이트웨이가 `X-User-Roles` · `X-User-Name` · `X-User-Email` 로 전달합니다.
`/auth/user/info` 는 실제 배정 역할을 내려줍니다.

확인: `quristyle`(SYSTEM_ADMINISTRATOR)·`vben`(ADMINISTRATOR) 통과, `admin`(역할 없음) 403.

### 곁들여 생긴 판단거리

이메일 대조가 **이제 실제로 동작하므로** 기본값을 꺼짐으로 내렸습니다.
운영 데이터에 같은 이메일을 쓰는 다른 사람이 있습니다(사용자A ↔ 고객 사용자H).
켤지 여부는 [15-jsini-user-unification.md](15-jsini-user-unification.md) Q5 를 봐 주세요.

---

---

## D13. 이관한 계정 42개가 아이디만 알면 로그인된다 🔴

> [15-jsini-user-unification.md](15-jsini-user-unification.md) 11절에서 넘어온 항목입니다.

### 지금 상태

헬프데스크·프로젝트관리 사용자 43명을 포털 계정으로 옮겼습니다(42건 생성).
지시하신 대로 **비밀번호를 로그인 아이디와 같은 값**으로 넣었습니다 — `hd_kdh` 의 비밀번호는 `hd_kdh` 입니다.

여기에 하나가 겹칩니다. **역할이 없는 계정은 화면이 막히지 않습니다**(D 계열이 아니라
[10-jsini-portal-unification.md](10-jsini-portal-unification.md) 결정 2 의 fail-open 규칙).
역할 없는 계정 2개가 잠기지 않게 하려고 둔 규칙인데, 이제 그런 계정이 44개입니다.

**합치면 아이디만 알면 포털에 들어와 메뉴를 볼 수 있는 계정이 42개입니다.**

### 선택지

| | 방법 | 비고 |
|---|---|---|
| **A** | 이관 계정을 로그인 불가로 두고, 쓸 사람이 생길 때 비밀번호·역할을 함께 지정 | **한 줄.** 지금 바로 안전해집니다 |
| **B** | 이관 계정에 `PARTNER` 역할을 일괄 배정 | 열람 범위가 정의됩니다. 파트너 권한이 맞는지 확인 필요 |
| **C** | fail-open 을 fail-closed 로 전환 | 근본 해결이지만 역할 없는 기존 계정(`admin`·`administrator`)이 잠깁니다 |

```sql
-- A안
UPDATE scom.accounts SET password = '!' WHERE created_by = 'msa-user-import';

-- 이관 자체 되돌리기
DELETE FROM scom.accounts WHERE created_by = 'msa-user-import';
```

### 진행 상황 (2026-08-22)

**B 를 진행했습니다** — 이관 계정 42개에 `PARTNER` 역할을 배정했습니다
([`docs/sql/msa_user_role_partner.sql`](../sql/msa_user_role_partner.sql), 실행 완료).
fail-open 대상에서 벗어나 이제 `role_menus` 의 실제 권한이 적용됩니다.
`hd_kdh` 로 로그인해 토큰의 `role=PARTNER`, 프로젝트관리 직접 쿼리 403 까지 확인했습니다.

**다만 두 가지가 남았습니다.**

1. **PARTNER 가 아직 관리자 화면을 편집할 수 있습니다.** `role_menu_backfill.sql` 이
   전 역할의 항목을 모두 켜 뒀기 때문입니다. 활성 화면 136개 중 105개 열람,
   115개에서 등록·수정·삭제가 열려 있습니다 — 계정 관리·역할 관리·메뉴 관리가 포함됩니다.
   즉 아이디를 아는 사람이 들어와 **관리자 계정을 만들 수 있습니다.**
   → 닫는 스크립트 [`docs/sql/role_partner_tighten.sql`](../sql/role_partner_tighten.sql) 준비 완료(**미실행**).
   PARTNER 는 이번 이관 전까지 배정 계정이 0개였으므로 실행해도 기존 사용자 영향은 없습니다.
2. **비밀번호는 여전히 아이디와 같습니다.** 위 A안의 한 줄은 그대로 유효합니다.

상세는 [15-jsini-user-unification.md](15-jsini-user-unification.md) Q6 을 봐 주세요.

---

## D14. 이식 시스템에서 '누구로서' 일할지 정하는 두 스위치 🟠

> [19-msa-user-work-enablement.md](19-msa-user-work-enablement.md) 에서 넘어온 항목입니다.
> 그 문서의 Q9 · Q10 이고, 상세한 근거와 실측값은 거기에 있습니다.

### 지금 상태

이번 작업으로 **조회·관리 권한은 포털 역할이 정하게** 되었습니다. 계정 연결이 없어도
포털 관리자 역할이면 헬프데스크를 조회·관리합니다.

하지만 **자기 자신으로서 일하는 것**(내가 쓴 댓글, 나에게 배정된 요청, 내 알림)은
여전히 "이 포털 계정이 저쪽의 누구인가" 가 정해져야 합니다.
그 대응은 지금 **헬프데스크 1건 · 프로젝트관리 1명**뿐입니다.

이관 스크립트가 남긴 출처 기록(`MsaSource`)으로 메꿀 수 있게 코드를 준비해 두었고,
**두 스위치 모두 기본은 꺼져 있습니다.** 켜기 전과 똑같이 동작합니다.

| 스위치 | 켜면 | 영향 |
|---|---|---|
| `AccountLink:MatchByMsaSource` (헬프데스크) | 이관 계정 34개가 각자의 원본 담당자·고객 레코드로 해석된다 | 담당자 7명이 생긴다. 그 계정의 비밀번호가 아이디와 같다(D13) |
| `Identity:UseMsaSource` (프로젝트관리) | `pm_jskim` → `jskim` 으로 바꿔 저장 프로시저에 넘긴다 | 감사 컬럼에 쌓이는 값이 바뀐다. 8명이 자기 레코드를 찾게 된다 |

### 왜 자율로 켜지 않았나

**D13 과 맞물려 있습니다.** 헬프데스크 스위치를 켜면 34개 계정이 실제 업무 데이터의
주인으로 인정되는데, 그 계정들의 비밀번호는 아직 로그인 아이디와 같습니다.
아이디를 아는 사람이 그 사람으로 로그인해 그 사람의 업무를 다룰 수 있게 됩니다.

### 권하는 순서

```sql
-- 1) 이관 계정을 로그인 불가로 (D13 A안 — 한 줄)
UPDATE scom.accounts SET password = '!' WHERE created_by = 'msa-user-import';

-- 2) PARTNER 권한 범위 축소 (D13 남은 항목 1)
--    docs/sql/role_partner_tighten.sql
```

그다음 실제로 쓸 사람에게만 비밀번호를 지정하고 스위치를 켭니다.
프로젝트관리 스위치는 D13 과 무관하므로 먼저 켜도 됩니다.

### 곁들여 드러난 것 — 결정 두 개가 어긋나 있었습니다

[15절](15-jsini-user-unification.md) Q3 은 **A(포털 아이디 = 프로젝트관리 아이디로 맞춘다)**
였는데, 같은 작업의 사용자 이관은 충돌을 피하려고 `pm_` 접두어를 붙였습니다.
9명 중 `quristyle` 한 명만 아이디가 맞습니다. 어느 쪽으로 갈지도 함께 정해 주세요
(19절 Q10 에 선택지 4개를 정리했습니다).

---

## 우선순위 제안

| 순서 | 항목 | 이유 |
|---|---|---|
| — | ~~**D9** 비밀번호 초기화~~ | ✅ 해결됨 (B안) |
| — | ~~**D10** 헬프데스크 쓰기 API 무인증~~ | ✅ 해결됨 (A안) |
| — | ~~**D11** 헬프데스크 만능 비밀번호~~ | ✅ 해결됨 (제거 + 자체 로그인 차단) |
| — | ~~**D12** 토큰에 신원 없음~~ | ✅ 해결됨 |
| 0 | **D13** 이관 계정 42개의 비밀번호 + PARTNER 권한 범위 | 아이디만 알면 로그인되고, 그 계정이 관리자 화면을 편집할 수 있습니다. 각각 한 줄로 막을 수 있습니다 |
| 0 | **D14** 이식 시스템의 '누구로서' 스위치 두 개 | D13 을 먼저 처리한 뒤 켜면 됩니다. 켜기 전까지는 34명이 자기 업무 데이터를 다룰 수 없습니다 |
| 1 | **D1** 시크릿 | 저장소 접근 = 시스템 전체 접근. 작업량 작음 |
| 2 | **D4** 예제 메뉴 | 5분. 사용성 개선 |
| 3 | **D2** DB 통합 | 이후 작업들의 전제 |
| 4 | **D3 · D5** 헬프데스크 정리 | JinReception 종료 시점과 함께 |
| 5 | **D6** 버저닝 | 현장 기기 배포 일정과 함께 |
| 6 | **D7 · D8** | 위가 끝난 뒤 |

---

## 이번 자율 작업에서 이미 처리한 것

결정이 필요 없다고 판단해 진행한 것들입니다.

| 항목 | 내용 | 검증 |
|---|---|---|
| **인증 우회 차단** | 내부 서비스 4개를 루프백에만 바인딩 | 외부 IP 에서 4포트 모두 연결 거부 확인 |
| 배포 도구 이전 | 헬프데스크 → 포털, 대상을 설정으로 일반화 | 대상 목록 조회·거절 경로 확인 |
| 공지 기능 | 포털 공통 공지 + 팝업 + 첨부 | 비인증/인증 노출 규칙, 첨부 업로드~다운로드 확인 |
| FileServer 복구 | 저장 경로·DB 연결이 없어 업로드가 아예 실패하던 상태 | 업로드·다운로드 확인 |
| 로깅 통일 | AuthServer·AIAgentServer 에 Serilog 추가 | 빌드 확인 |
| 스모크 테스트 | `scripts/smoke-test.sh` | 16개 항목 전부 통과 |

---

## 부록: 자율 작업 세션 기록 (2026-08-22)

자리를 비우신 동안 진행한 내용입니다. 모든 항목은 빌드·스모크 테스트로 확인했습니다.

### 검증 방법

```bash
./scripts/smoke-test.sh      # 17개 항목: 기동·우회차단·경로·인증경계·시도제한
dotnet build jsini.sln       # 백엔드 9개 프로젝트
cd fronts/apps/jsini-portal && pnpm vite build --mode production
```

세 가지 모두 통과한 상태로 남겨 두었습니다.

### 작업 중 한 번 사고가 있었습니다

레이트 리미팅 라우트를 추가하면서 `ReverseProxy.Routes` 안에 `"//rate-limit"` 이라는
주석용 키를 넣었는데, **YARP 는 그 아래 모든 키를 라우트로 읽습니다.**
경로가 없는 라우트라고 판정해 게이트웨이가 기동에 실패했고 몇 분간 전체가 멈췄습니다.

주석 키를 걷어내고 복구했으며, 지금은 정상입니다.
`Clusters` 쪽은 `//` 주석이 통하지만 `Routes` 아래는 안 됩니다 — 같은 실수를 막기 위해 적어 둡니다.

### 서비스 재기동에 대해

`dotnet watch` 는 **`appsettings.json` 변경으로는 재기동하지 않습니다.**
설정을 바꾼 뒤에는 해당 서비스의 `.cs` 파일을 건드려야 반영됩니다.
이번에 바인딩 설정이 반영되지 않아 한참 헤맨 원인이었습니다.

또한 `appsettings.Development.json` / `.Production.json` 이 같은 키를 다시 덮는 경우가 있어
설정을 바꿀 때는 **세 파일을 함께** 확인해야 합니다.
