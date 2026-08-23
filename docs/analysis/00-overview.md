# JSini 관리 포털 — 전체 개요 및 프로젝트 관계

> 최초 작성: 2026-08-14 · 최종 갱신: 2026-08-22
> 대상 저장소: `/home/quri/Funeralv2`

> **[갱신 안내]** 이 시스템의 이름은 **JSini 관리 포털**이다.
> 예전 문서는 저장소 이름을 따라 "Funeralv2 시스템"으로 적었는데,
> 장례식장(funeralv2)은 포털에 붙은 **여러 MSA 중 하나**다.
> 최근 대규모 변경 내용은 다음 문서를 함께 볼 것.
>
> - [10-jsini-portal-unification.md](10-jsini-portal-unification.md) — 이름·폴더 정리, 공통 권한 통합
> - [11-msa-improvement-backlog.md](11-msa-improvement-backlog.md) — MSA 구성 점검과 개선 백로그
> - [12-decisions-pending.md](12-decisions-pending.md) — 결정이 필요한 항목
> - [13-projmng-migration.md](13-projmng-migration.md) — 프로젝트관리(Blazor WASM) 이식

## 1. 시스템 개요

**JSini 관리 포털**은 여러 업무 시스템(MSA)을 하나의 관리 화면 아래 모으는 포털이다.
**인증과 권한은 포털이 한 곳에서 관리**하고, 각 MSA 는 자기 고유 업무만 담당한다.

현재 붙어 있는 업무 시스템은 셋이다.

| 업무 시스템 | 내용 | 담당 서비스 |
|---|---|---|
| 장례식장 (funeralv2) | 빈소·고인·장비 관리, 사이니지 표출 | `funeralv2Api` |
| 헬프데스크 | 요청 접수, WBS, 일정, 리포트 | `HelpDeskServer` |
| 프로젝트관리 | 프로젝트·WBS·설계(ERD)·DB 도구·소스 분석 | `ProjMngServer` |

포털 자신은 계정·역할·메뉴·권한·공지·배포 도구를 담당한다(`AuthServer`).

전체 폴더 구성이다.

| 폴더 | 기술 스택 | 역할 |
|---|---|---|
| `ApiGateway/` | .NET (YARP Reverse Proxy) | **유일한 외부 진입점.** 경로 라우팅 + JWT 검증 |
| `microservices/` | .NET + EF Core + PostgreSQL | 포털·업무 서비스들 |
| `microservices/Common/JSini.Shared.*` | .NET 클래스 라이브러리 | 전 서비스 공용 (응답 봉투·엔티티 기반·미들웨어) |
| `fronts/apps/jsini-portal/` | Vue 3 + Vben Admin | 포털 웹 프론트엔드 |
| `funeralv2_player/` | Flutter (Dart) | 장례식장 사이니지 플레이어 |

> 참고: 루트에는 이 외에도 `funeralv2/`(backend-mock), `AI.md`(코딩 표준),
> `docs/`(분석·운영 문서), `scripts/`(스모크 테스트·시크릿 템플릿),
> 실행 스크립트(`backend_run_ubuntu.sh` 등)가 있다.
> 솔루션 파일은 `jsini.sln` 이다.

### 프론트엔드 화면 구성

화면은 시스템 경계에 맞춰 세 갈래로 나뉜다.

```
fronts/apps/jsini-portal/src/views/
  _core/     vben 프레임워크 (로그인·404·프로필)
  portal/    포털 공통 — 계정·역할·메뉴·권한·공지·배포
  funeral/   장례식장 MSA
  helpdesk/  헬프데스크 MSA
  projmng/   프로젝트관리 MSA
```

`api/` 도 같은 기준으로 나뉜다.

## 2. 아키텍처 관계도

```
                    ┌─────────────────────────────┐
                    │  관리자 (브라우저)           │
                    │  fronts (Vue3, vite :dev)    │
                    └──────────────┬──────────────┘
                                   │  /api/* (vite proxy → :5265)
                                   ▼
   ┌───────────────┐      ┌──────────────────────────────┐
   │ funeralv2_    │      │  ApiGateway (YARP)            │
   │ player        │─────▶│  http://localhost:5265        │
   │ (Flutter,     │ SignalR + REST                       │
   │  라즈베리파이)│      │  경로 기반 라우팅 + JWT 인증  │
   └───────────────┘      └───┬─────────┬─────────┬───────┘
                              │         │         │        │
              ┌───────────────┘   ┌─────┘    ┌────┘    ┌───┘
              ▼                    ▼          ▼         ▼
     ┌────────────────┐  ┌────────────────┐ ┌────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
     │ AuthServer     │  │ funeralv2Api   │ │FileSvr │ │ AIAgentServer│ │ HelpDeskSvr  │ │ ProjMngSvr   │
     │ :5264          │  │ :5320          │ │ :5350  │ │ :5029        │ │ :5400        │ │ :5450        │
     │ JWT 발급/인증  │  │ 업무 API +     │ │파일    │ │ AI 에이전트  │ │ 헬프데스크   │ │ 프로젝트관리 │
     │                │  │ DeviceHub(RT)  │ │업/다운 │ │              │ │ 요청/WBS/일정│ │ 저장프로시저 │
     └───────┬────────┘  └───────┬────────┘ └───┬────┘ └──────┬───────┘ └──────┬───────┘ └──────┬───────┘
             └───────────────────┴──────────────┴─────────────┴────────────────┴────────────────┘
                                   │
                          PostgreSQL (EF Core / Dapper)
              microservices/Common/*  (Shared.DTOs / Domain / Infrastructure)
```

## 3. 라우팅 및 포트 매핑 (ApiGateway 기준)

게이트웨이(YARP)는 `ApiGateway/appsettings.json`에 선언된 경로 규칙으로 요청을 각 서비스(클러스터)로 전달한다.

| 게이트웨이 경로 | 대상 클러스터 | 대상 서비스 | 포트 | 인증 |
|---|---|---|---|---|
| `/api/auth/**` | auth-cluster | AuthServer | 5264 | Anonymous(로그인 등) |
| `/api/funeral/hubs/device/**` | funeral-cluster | funeralv2Api (SignalR `DeviceHub`) | 5320 | Anonymous |
| `/api/funeral/building/device/code/{code}` 등 device/deceased/kiosk 조회 | funeral-cluster | funeralv2Api | 5320 | Anonymous(플레이어용) |
| `/api/funeral/**` (그 외) | funeral-cluster | funeralv2Api | 5320 | JWT 필요 |
| `/api/file/download/**`, `/thumbnail/**` | file-cluster | FileServer | 5350 | Anonymous |
| `/api/file/**` (그 외) | file-cluster | FileServer | 5350 | JWT 필요 |
| `/api/ai/**` | ai-cluster | AIAgentServer | 5029 | JWT 필요 |
| `/api/helpdesk/**` | helpdesk-cluster | HelpDeskServer | 5400 | Anonymous(서비스가 자체 검증) |
| `/api/projmng/**` | projmng-cluster | ProjMngServer | 5450 | JWT 필요 |

- `/api/helpdesk/**` 는 프리픽스를 떼고 다시 `/api` 를 붙여 전달한다(`/api/helpdesk/users/login` → HelpDeskServer 의 `/api/users/login`). HelpDeskServer 는 자체 로그인 토큰(`helpdesk-api`)과 게이트웨이 토큰(`funeralv2-auth`)을 모두 수용한다.
- `/api/oadr/**` 는 외부 시스템(`nums.hanjucorp.co.kr`)으로 프록시한다. 헬프데스크 리포트 화면이 브라우저 CORS 없이 쓰기 위한 경로다.
- 게이트웨이 자체 포트: **5265** (`ApiGateway/Properties/launchSettings.json`)
- 프론트엔드 개발 프록시: `fronts/apps/jsini-portal/vite.config.ts` → `http://127.0.0.1:5265`

### 네트워크 노출 (2026-08-22 변경)

**외부에 열려 있는 것은 게이트웨이(5265) 하나뿐이다.** 내부 서비스는 전부 루프백에만 바인딩한다.

| 서비스 | 바인딩 |
|---|---|
| ApiGateway | `0.0.0.0:5265` — 유일한 외부 창구 |
| AuthServer | `127.0.0.1:5264` |
| funeralv2Api | `127.0.0.1:5320` |
| FileServer | `127.0.0.1:5350` |
| HelpDeskServer | `127.0.0.1:5400` |
| ProjMngServer | `127.0.0.1:5450` |

이유가 있다. 내부 서비스들은 신원을 **게이트웨이가 붙여 주는 `X-User-Id` 헤더**로 판단한다.
게이트웨이는 외부에서 들어온 같은 이름의 헤더를 지우고 JWT 를 검증한 뒤에만 다시 붙이므로 안전하지만,
서비스 포트가 외부에 열려 있으면 그 검증을 통째로 건너뛸 수 있다.
실제로 헤더만 위조해 관리자 데이터를 읽을 수 있었고, 그래서 루프백으로 잠갔다.

> **서비스를 다른 장비로 분리하려면** 이 포트를 다시 열어야 하는데,
> 그때는 반드시 서비스 간 인증(mTLS 또는 공유 시크릿)을 먼저 붙여야 한다.
> 헤더를 그대로 믿는 구조는 변하지 않았다.

배포·설정 변경 후에는 `./scripts/smoke-test.sh` 로 이 조건을 포함해 한 번에 확인할 수 있다.

### 개발 서버 기동

세 플랫폼 스크립트가 **같은 명령을 받는다.**

| 플랫폼 | 스크립트 |
|---|---|
| Linux | `./backend_run_ubuntu.sh` |
| macOS | `./backend_run_mac.sh` |
| Windows | `dev.bat` |

서비스별로 골라 재기동할 수 있다. 한 서비스만 지정하면 **그 서비스만 빌드**하므로
코드 한 곳을 고치고 확인하는 흐름이 빠르다.

```bash
./backend_run_ubuntu.sh                # 전체 재기동 (중지 → 빌드 → 기동)
./backend_run_ubuntu.sh auth           # AuthServer 만 재기동
./backend_run_ubuntu.sh projmng front  # 여러 개 지정
./backend_run_ubuntu.sh stop helpdesk  # 헬프데스크만 중지
./backend_run_ubuntu.sh allstop        # 전체 중지
./backend_run_ubuntu.sh status         # 지금 무엇이 떠 있는지
./backend_run_ubuntu.sh list           # 서비스 이름 목록
./backend_run_ubuntu.sh help           # 사용법
```

| 이름 | 서비스 | 포트 |
|---|---|---|
| `gateway` | ApiGateway | 5265 |
| `auth` | AuthServer | 5264 |
| `funeral` | funeralv2Api | 5320 |
| `ai` | AIAgentServer | 5029 |
| `file` | FileServer | 5350 |
| `helpdesk` | HelpDeskServer | 5400 |
| `projmng` | ProjMngServer | 5450 |
| `front` | 프론트엔드(vite) | 5555 |

서비스를 추가할 때는 스크립트 위쪽의 서비스 표에 한 줄만 더하면 된다
(`SERVICES` 배열, 윈도우는 `SVC_KEYS` + `SVC_<이름>`) —
빌드·기동·중지·상태 확인이 모두 그 표를 읽는다.

> **한 서비스만 어떻게 골라 죽이나.** `pkill -f "dotnet watch run"` 은 못 쓴다.
> 모든 서비스가 똑같은 명령줄을 갖고 있어 구분이 안 되기 때문이다.
> 플랫폼마다 구분할 수 있는 다른 표식을 쓴다.
>
> | 플랫폼 | 찾는 방법 |
> |---|---|
> | Linux | `/proc/<pid>/cwd` — 한 서비스의 프로세스 묶음(셸 → dotnet watch → dotnet run → 서비스)이 모두 같은 작업 디렉터리를 갖는다 |
> | macOS | 같은 원리인데 `/proc` 이 없어 `lsof -d cwd` 로 읽는다 |
> | Windows | 포트(`netstat -ano`)로 PID 를 찾고, 서비스별 **창 제목**으로 한 번 더 정리한다 |
>
> 어느 쪽이든 포트를 잡고 있는 프로세스를 마지막에 한 번 더 확인해 보루를 둔다.
> 부모(`dotnet watch`)를 먼저 종료해야 자식을 되살리지 않으므로 PID 작은 것부터 보낸다.

> **윈도우만 다른 점.** 기동이 `dotnet run --no-build` 다(리눅스·맥은 `dotnet watch`).
> 파일 변경 감지를 쓰려면 `dev.bat` 위쪽의 `START_CMD` 한 줄을 바꾸면 된다.
> 또 배치는 지연확장 때문에 `secrets.env` 값에 든 `!` 가 사라지므로 `^^!` 로 적어야 한다.

## 4. 프로젝트 간 연동 관계

### 4.1 프론트엔드 ↔ 백엔드
- `fronts`(관리자 웹)는 모든 요청을 `/api/*` 접두어로 게이트웨이(:5265)에 보낸다.
- 실시간 빈소 현황판은 SignalR로 연결: `fronts/apps/jsini-portal/src/views/building/status/index.vue:37` → `/api/funeral/hubs/device`.
- 인증은 AuthServer가 발급한 JWT를 게이트웨이가 검증한다.

### 4.2 플레이어(사이니지) ↔ 백엔드
- `funeralv2_player`(Flutter)는 게이트웨이 base URL + `/api/funeral/hubs/device`로 SignalR 접속한다: `funeralv2_player/lib/services/signalr/signalr_service.dart:224`.
- 디바이스 코드 기반 익명 조회(`/api/funeral/building/device/code/{code}`, `.../deceased/kiosk/...` 등)로 표출할 고인/빈소 데이터를 받는다. → 게이트웨이에서 이 경로들은 `AuthorizationPolicy: Anonymous`로 열려 있음.
- 배포 환경은 **라즈베리파이 / 데스크톱 키오스크**(참고: `docs/raspberry실행환경.md`, `funeralv2_player`의 `window_manager` 사용).

### 4.3 서비스 간 공유
- `microservices/Common/`의 3개 공유 라이브러리(`JSini.Shared.DTOs`, `.Shared.Domain`, `.Shared.Infrastructure`)를 각 서비스가 참조하여 DTO/도메인/인프라(Repository 패턴 등)를 공유한다.
- 코딩 표준(`AI.md`): Repository 패턴 의무화, 무거운 집계는 PostgreSQL Materialized View + BackgroundService 리프레시.

## 5. 데이터 흐름 예시 (사이니지 표출)

1. 관리자가 웹(`fronts`)에서 고인/빈소/미디어를 등록 → 게이트웨이 → `funeralv2Api` → PostgreSQL 저장.
2. 변경 발생 시 `funeralv2Api`의 `DeviceHub`가 SignalR로 연결된 클라이언트에 브로드캐스트.
3. 장례식장의 `funeralv2_player`가 이벤트를 수신하여 화면을 갱신하고, 필요한 미디어는 `FileServer`에서 다운로드.

## 6. 개별 분석 문서

각 프로젝트의 상세 분석 및 개선점은 아래 문서를 참조한다.

- [ApiGateway 분석](./apigateway-analysis.md)
- [Microservices 분석](./microservices-analysis.md)
- [Frontend(Vue3/Vben) 분석](./frontend-analysis.md)
- [Player(Flutter) 분석](./player-analysis.md)

## 7. 시스템 통합 우선순위 (교차 이슈)

4개 분석에서 반복적으로 드러난, 여러 프로젝트에 걸친 최우선 이슈를 통합 정리한다. 상세 근거는 각 개별 문서 참조.

### 🔴 P0 — 보안 치명

> 2026-08-22 갱신: 1·2번은 조치했다. 3·4번은 남아 있다.
> 상세는 [11-msa-improvement-backlog.md](11-msa-improvement-backlog.md) 와
> [12-decisions-pending.md](12-decisions-pending.md) 참조.

1. ~~**비밀번호 평문 저장·비교**~~ → **조치 완료 (2026-08-22)**
   PBKDF2(HMAC-SHA256, 600k회) 해시를 도입했다(`Services/PasswordHasher.cs`).
   **기존 평문 값과 함께 쓸 수 있게 만들어** 도입 시점에 잠기는 계정이 없고,
   각자 다음 로그인 때 조용히 해시로 승격된다. 비밀번호 변경도 해시로 저장한다.
   로그인 경로가 둘(`AuthEndpoints`/`AuthService`)이라 양쪽 모두 적용했다.

2. ~~**리소스 서비스 전면 무인증 (게이트웨이 우회)**~~ → **조치 완료 (2026-08-22)**
   내부 서비스를 루프백에만 바인딩해 게이트웨이 밖에서는 닿지 않게 했다.
   위 "네트워크 노출" 절 참조. 실제로 헤더 위조로 관리자 데이터를 읽을 수 있던 상태였다.
   서비스별 `UseAuthentication` 적용(심층방어)은 여전히 권장 사항으로 남는다.

3. **시크릿 하드코딩 커밋** — **남아 있음.** JWT 서명 키(게이트웨이·3개 서비스 동일 문자열),
   VAPID 개인키가 저장소에 평문. 적용 준비만 해 두었다(`scripts/secrets.env.example`,
   실행 스크립트가 있으면 환경변수로 실어 준다). 키 교체는 전 사용자 재로그인을 부르므로
   결정이 필요하다 → **D1**.

4. **레이트 리미팅 전무** — **남아 있음.** 로그인 브루트포스, AI(과금) 남용,
   디바이스코드 열거가 무방비다. 특히 인증 없이 남의 비밀번호를 초기화할 수 있는
   경로가 하나 있다 → **D9**.

### 🟠 P1 — 아키텍처/복원력

5. **하드코딩된 서비스 주소·평문 HTTP**: 게이트웨이 destination이 `http://localhost:*` 고정, 클러스터 간 TLS·HSTS 부재 → `X-User-*` 신뢰 헤더가 평문 전송. 환경별 설정 분리/서비스 디스커버리/내부 TLS 필요.
6. **복원력** — **게이트웨이 쪽은 해소됨 (2026-08-22 확인).**
   클러스터마다 용도에 맞는 `ActivityTimeout`(인증 30초, SignalR 5분, 파일 10분, AI 5분)과
   능동 헬스체크(15초 간격, 연속 3회 실패 시 제외)가 걸려 있다.
   `/api/gateway/status` 로 실시간 상태를 볼 수 있고, 확인 시점에 전 서비스 UP 이었다.
   **남은 것**: 서비스 내부의 `new HttpClient()` 남용(소켓 고갈), 플레이어 SignalR 워치독 부재.
7. **AI.md 핵심 표준 미준수**: Repository 패턴 의무화에도 전 서비스가 `AppDbContext` 직접 주입, Materialized View 표준 미적용.
8. **테스트·관측성 0건**: 백엔드 테스트/헬스체크 없음, 상관관계 ID 로깅 없음, 플레이어의 유일한 테스트는 컴파일 불가.

### 🟡 P2 — 품질/유지보수

9. **프론트 개발 잔재 상주**: 프로덕션 devtools 활성화, `console.*` 72곳, 매 네비게이션 로그, `any`/`as any` 357곳, 비즈니스 뷰 `$t()` 미사용(한국어 하드코딩).
10. **응답 언래핑 규약 불일치**: 프론트에 `?.result ?? res` 방어 패턴 22곳 → API 응답 봉투 규약 통일 필요.
11. **플레이어 캐시 무제한 증가**: TTL/용량 상한/동일파일 갱신 없음 → SD카드 고갈·stale 미디어 표출.
12. **죽은 코드**: 프론트 스텁 뷰 15개 + `-custom` 실구현 17개 병존.

### 공통 테마 요약

| 테마 | 게이트웨이 | 마이크로서비스 | 프론트 | 플레이어 |
|---|:---:|:---:|:---:|:---:|
| 시크릿 하드코딩 | ● | ● | ● | ○ |
| 인증/인가 공백 | ○(익명경로) | ●(무인증) | ●(SignalR/AI) | ●(무인증) |
| 복원력(재시도/타임아웃) | ● | ● | – | ●(SignalR) |
| 관측성/로깅 | ● | ● | ●(과다) | ● |
| 테스트 부재 | ● | ● | ● | ● |
| 코딩표준 미준수 | – | ●(Repo/MV) | ●(TS/i18n) | – |

## 8. 개별 분석 문서 (재게시)

- [ApiGateway 분석](./apigateway-analysis.md)
- [Microservices 분석](./microservices-analysis.md)
- [Frontend(Vue3/Vben) 분석](./frontend-analysis.md)
- [Player(Flutter) 분석](./player-analysis.md)
