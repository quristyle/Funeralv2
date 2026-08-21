# HelpDeskServer

헬프데스크(HelpDesk) 마이크로서비스. `projects/JinRestApi`(JinReception 의 백엔드)를 Funeralv2 MSA 로 이식한 서비스다.
개선요청/댓글/첨부, WBS·프로젝트, 일정, 체크리스트, 공지, 조직(회사·팀·관리자·고객), 메뉴·역할 권한, 웹푸시를 담당한다.

원본(`/home/quri/projects/JinRestApi`)은 **변경하지 않는다.** 이 디렉터리는 복제본이며, 이후 개선은 여기서만 진행한다.

---

## 1. 기본 정보

| 항목 | 값 |
|---|---|
| 프로젝트 | `microservices/HelpDeskServer/HelpDeskServer.csproj` (net8.0, Minimal API) |
| 리스닝 포트 | **5400** (`appsettings.json` 의 Kestrel) |
| 게이트웨이 경로 | `/api/helpdesk/**` → `helpdesk-cluster` |
| DB | PostgreSQL, 스키마 `jsini` (마이그레이션 히스토리 `jsini.__EFMigrationsHistory`) |
| 헬스체크 | `GET /health` (익명) |
| Swagger | Development 환경에서 `http://localhost:5400/swagger` |
| 콘솔 배너 | `SERVER_NAME=HELPDESK` |

## 2. 게이트웨이 라우팅

`ApiGateway/appsettings.json` 의 `helpdesk-route` 는 프리픽스를 떼고 다시 `/api` 를 붙인다.
서비스 내부 엔드포인트가 모두 `/api/...` 로 매핑되어 있기 때문이다.

```
브라우저  GET /api/helpdesk/requests
게이트웨이 → PathRemovePrefix(/api/helpdesk) → PathPrefix(/api)
서비스    GET /api/requests        (http://localhost:5400)
```

## 3. 인증 — funeralv2 계정으로 단일화

로그인은 funeralv2(AuthServer) 하나로 통일했다. 헬프데스크 자체 로그인(`/api/users/login`)도 남아 있지만
(살아있는 JinReception 이 아직 쓴다), funeralv2 화면에서는 AuthServer 토큰만 사용한다.

- 서비스가 **두 발급자를 모두 검증**한다 — 자체 `Jwt:Issuer`(기본 `helpdesk-api`)와 `GatewayJwt:Issuer`(`funeralv2-auth`).
- 게이트웨이 라우트는 `AuthorizationPolicy: Anonymous` 로 두고 **인가 판단은 서비스가 직접 한다**
  (엔드포인트별 `RequireAuthorization`).

### 계정 매핑

기존 데이터(요청 작성자·담당자·댓글)가 전부 헬프데스크 내부 계정 ID 를 참조하기 때문에 그 ID 를 버릴 수 없다.
그래서 기존 테이블은 손대지 않고 매핑 테이블 `jsini.auth_user_links` 를 추가해 두 체계를 잇는다.

`FuneralIdentityMiddleware` 가 인증 직후에 이 매핑을 찾아 헬프데스크 내부 클레임
(`uid` / `login_type` / `company_id`)을 채워 넣는다. 기존 엔드포인트는 이 세 클레임만 보므로 **엔드포인트 코드는 한 줄도 바뀌지 않았다.**

해석 우선순위:

1. `auth_user_links` 에 등록된 명시적 매핑
2. 로그인 아이디 일치 — **기본 비활성** (`AccountLink:MatchByLoginId`).
   운영 데이터에 아이디는 같지만 다른 사람인 계정이 있어, 자동으로 이으면 남의 계정으로 붙는다.
3. 이메일 일치 (`AccountLink:MatchByEmail`, 기본 활성)

매핑은 funeralv2 의 **헬프데스크 설정 › 계정 연결** 화면(`/helpdesk/system/account-link`)에서 관리한다.
연결되지 않은 계정으로 접근하면 각 화면이 안내 문구를 띄우고 데이터를 조회하지 않는다.

## 4. 설정

접속 문자열과 같은 비밀값은 Git 에 올리지 않는다. `appsettings.Local.json`(gitignore 대상)이나 환경변수로 주입한다.

```jsonc
// microservices/HelpDeskServer/appsettings.Local.json
{
  "ConnectionStrings": {
    "helpdesk": "Host=...;Port=...;Database=...;Username=...;Password=...;Search Path=jsini"
  }
}
```

조회 우선순위: `ConnectionStrings:helpdesk` → `helpdesk` → 환경변수 `helpdesk` → `ConnectionStrings:DefaultConnection` → 환경변수 `Help_JSINI`(이식 전 호환).
어느 것도 없으면 기동 시점에 명시적으로 실패한다.

`dotnet ef` 설계 시점(`AppDbContext.OnConfiguring`)에는 환경변수 `helpdesk`(또는 `Help_JSINI`)만 사용한다.

| 설정 키 | 기본값 | 설명 |
|---|---|---|
| `Workers:HealthCheckEnabled` | `true` (Development 는 `false`) | 외부 API 상태를 주기 점검하는 `HealthCheckWorker` |
| `Workers:AutoCheckEnabled` | `true` (Development 는 `false`) | 주기적 자동 처리 `AutoCheckWorker` |
| `RabbitMQ:HostName` | `localhost` | 연결 실패해도 서비스는 계속 동작한다 |
| `Vapid:*` | - | 웹푸시(VAPID) 키 |

## 5. 실행

```bash
cd microservices/HelpDeskServer && SERVER_NAME=HELPDESK dotnet run
```

전체 스택은 루트의 `backend_run_ubuntu.sh` / `backend_run_mac.sh` / `dev.bat` 로 게이트웨이와 함께 기동한다.

동작 확인:

```bash
curl http://localhost:5400/health
curl http://localhost:5265/api/helpdesk/notices
curl http://localhost:5265/api/gateway/status
```

## 6. 원본 대비 변경점

코드(엔드포인트·서비스·모델·마이그레이션) 로직은 그대로 두고, MSA 편입에 필요한 부분만 손봤다.

- 네임스페이스/어셈블리 `JinRestApi` → `HelpDeskServer`
- 포트 5223 → **5400**, Kestrel 바인딩 `localhost` → `+`
- 접속 문자열 하드코딩 제거 → `appsettings.Local.json`/환경변수 주입
- `/health` 엔드포인트 추가(게이트웨이 능동 헬스체크 및 서버상태 화면 대상)
- Serilog + `UseSerilogRequestLogging`, Spectre.Console 기동 배너 — 다른 MSA 와 동일한 형태
- `JSini.Shared.*` 참조 및 `UseGlobalExceptionHandler()` 적용
- CORS `AllowAll` 정책 추가(게이트웨이 우회 직접 호출/디버깅용)
- JWT 검증을 다중 발급자(`helpdesk-api` + `funeralv2-auth`)로 확장
- 게이트웨이 뒤에서 HTTP 로만 수신하므로 `UseHttpsRedirection()` 제거
- 백그라운드 워커 2종을 설정으로 on/off 가능하게 함
- 미사용 백업 파일(`UtilEndpoints250610.cs_bk`), 빈 `initial_migration.sql` 제거

## 7. 프론트엔드 이식 상태

JinReception 화면은 funeralv2 프론트(`fronts/apps/jsini-portal`)에 Ant Design Vue 로 다시 작성해 옮기고 있다.

- API 계층: `#/api/helpdesk/*` — 응답 봉투 차이(`{success,data,meta}` ↔ `{code:'S000',data}`)를
  전용 요청 클라이언트가 흡수한다. 목록의 총건수는 봉투 최상위 `totalcount`/`totalpagecount` 에 온다.
- 메뉴: `docs/sql/helpdesk_menu_seed.sql` 이 `scom.system_menus` 에 트리를 등록한다.
  화면이 아직 안 만들어진 메뉴는 `status = 0` 으로 두고, 이식이 끝나면 1 로 올린다.

남은 결정 사항:

- 파일 업로드를 FileServer 로 위임할지, 헬프데스크의 `/api/files` 를 유지할지
- 헬프데스크 자체 메뉴·역할 테이블(`jsini.menu`, `jsini.approle`)을 계속 쓸지,
  funeralv2 의 `scom.system_menus`/`roles` 로 흡수할지
