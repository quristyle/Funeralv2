# 추적 번호로 오류 까닭 찾기

사용자가 보는 오류 화면은 이렇게 생겼다.

```
오류가 발생했습니다
잠시 뒤 다시 시도해 주세요. 계속되면 아래 번호와 함께 알려 주세요.

추적 번호: 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00   [복사]
```

그 번호를 **포털관리 > 상태 관리 > 「오류 추적」**(`/admin/status/error`)에
붙여 넣으면 그 요청의 예외 타입 · 메시지 · 스택 추적 · 기기 · 사용자가 나온다.

## 번호가 무엇인가

W3C traceparent 다. `00-{trace-id 32자리}-{span-id 16자리}-00` 꼴이고,
**가운데 32자리가 열쇠**다. 전체를 붙여 넣어도 가운데만 붙여 넣어도 같은 것이
나온다 — 전화로 불러 주는 값이라 어느 쪽으로 받아 적을지 고를 수 없기 때문이다.

드물게 `0HN7ABCD1234:00000003` 꼴이 나올 수 있다. 분산 추적이 안 서 있을 때
`HttpContext.TraceIdentifier` 로 떨어진 것이고, 그 값도 그대로 받아 준다.

## 왜 컨테이너 로그로는 못 찾나

**번호로 `docker logs` 를 뒤져도 한 줄도 안 나온다.** .NET 콘솔 로거는 기본값이
`IncludeScopes=false` 라 추적 번호를 찍지 않는다. 그래서 이 기능이 생기기
전에는 신고를 받아도 「몇 시쯤 났다」로 시각을 더듬는 수밖에 없었고, 운영 로그는
`json-file` 10MB 세 개를 돌려 쓰므로 바쁜 날에는 그 전에 밀려 나갔다.

지금은 셸이 예외를 잡을 때 **화면에 보여 준 바로 그 번호로** 표에 적어 둔다.
덤으로 컨테이너 로그에도 번호를 한 줄 남기므로(`PortalErrorHandler`), 로그를
볼 때도 신고와 짝지을 수 있다.

## 어떻게 도나

```
 브라우저                포털 셸(:5557)            게이트웨이        AuthServer
    │                        │                        │                │
    │  화면 요청             │                        │                │
    ├───────────────────────▶│                        │                │
    │                   예외 발생                     │                │
    │                        │                        │                │
    │                 ① PortalErrorHandler            │                │
    │                    (IExceptionHandler)          │                │
    │                    큐에 넣고 바로 돌아옴         │                │
    │                        │                        │                │
    │  ② /error 화면         │                        │                │
    │◀───────────────────────┤                        │                │
    │   추적 번호 표시        │                        │                │
    │                        │                        │                │
    │                 ③ 배경 작업이 전송              │                │
    │                        ├───────────────────────▶├───────────────▶│
    │                        │  POST auth/portal-errors        scom.portal_error_logs
```

| 자리 | 파일 |
|---|---|
| 번호 짓기 (화면·기록 공용) | `web/src/Shared/JSini.Web.Components/Diagnostics/TraceNumber.cs` |
| 예외 잡기 | `.../Diagnostics/PortalErrorHandler.cs` |
| 큐와 전송 | `.../Diagnostics/PortalErrorReporter.cs` |
| 등록 | `.../JSiniWebApp.cs` (`AddExceptionHandler` · `AddHostedService`) |
| 오류 화면 | `web/src/Shell/JSini.Web.Shell/Components/Pages/Error.razor` |
| 표·엔드포인트 | `microservices/AuthServer/Endpoints/PortalErrorEndpoints.cs` |
| 조회 화면 | `web/src/Apps/JSini.Web.Admin/Components/Pages/ErrorTracePage.razor` |

**보내는 일은 요청을 붙잡지 않는다.** 큐에 넣고 바로 돌아오고 배경 작업이
보낸다 — 거기서 기다리면 오류 화면이 게이트웨이 왕복만큼 늦게 뜨고, 보내다
또 던지면 오류 처리기 안에서 난 예외라 아무 데도 안 잡힌다. 게이트웨이가 죽은
것이 원래 오류의 까닭일 때 그 오류가 영영 기록되지 않는 문제도 함께 없어진다.

## 설정

공유 비밀 하나를 양쪽에 같은 값으로 넣는다. 다르면 기록이 401 로 떨어지는데
**화면은 아무 차이 없이 돌아간다** — 오류 추적 화면만 영원히 비어 있다.

| 어디 | 이름 |
|---|---|
| 포털 컨테이너 | `ErrorReport__Token` |
| AuthServer | `PortalError__Token` |

운영은 `docker-compose.prod.yml` 이 둘 다 `/srv/jsini/.env` 의
`PORTAL_ERROR_TOKEN` 에서 읽는다. 값을 **비워 두면 검사하지 않는다**(개발
장비 기준) — 운영에서 비워 두면 바깥에서 아무나 가짜 오류를 밀어 넣을 수 있다.

```
openssl rand -hex 32
```

끄는 손잡이는 포털 쪽 `ErrorReport__Enabled=false` 다. 코드를 고치지 않고
기록만 멈춘다.

## 운영에 올릴 때 할 일 둘

1. **EF 마이그레이션.** `AddPortalErrorLogs` 가 `scom.portal_error_logs` 를
   만든다. AuthServer 는 기동 때 자동 적용하지 않으므로(그렇게 하는 것은
   FileServer 하나뿐이다) 손으로 적용한다.
2. **DB 메뉴.** [portal-error-menu.sql](portal-error-menu.sql) 을 돌리면
   사이드바에 뜨고 역할 권한을 걸 수 있다. **안 돌려도 주소를 치면 열린다** —
   라우팅 소유권은 DB 가 아니라 `@page` 에 있다(web/CLAUDE.md).

## 누가 볼 수 있나

관리자 계열(`ADMINISTRATOR` · `SYSTEM_ADMINISTRATOR`)만이다. 스택 추적에는
내부 경로와 질의가 묻어 나오므로 배포 현황·컨테이너 로그와 같은 급으로 다룬다.
메뉴 권한을 풀어 주어도 역할이 아니면 서버가 403 을 준다 — 두 겹이다.

## 보관 기간

180일. 넘은 줄은 새 오류가 들어올 때 하루 한 번 걷어낸다
(`PortalErrorEndpoints.SweepAsync`).

## 여기 안 잡히는 것

**대화형 화면(Blazor 회로) 안에서 던진 예외는 들어오지 않는다.** 그 예외는
HTTP 파이프라인이 아니라 회로를 타고 죽어서, 사용자가 보는 것도 오류 화면이
아니라 아래쪽 「연결이 끊겼습니다」 막대다 — **추적 번호 자체가 안 보인다.**
번호가 없으니 번호로 찾을 일도 없다.

여기 잡히는 것은 정적 SSR(첫 그림 · 폼 제출 · 셸의 중계 경로)에서 난 것,
곧 **사용자가 번호를 받아 든 바로 그 경우**다.

회로 오류까지 모으려면 로그 공급자(`ILoggerProvider`)로 받는 길이 있다.
그때는 지금의 HTTP 경로와 중복으로 잡히므로 걸러 내는 규칙이 함께 필요하다 —
필요해지면 그것이 다음 단계다.

## 번호를 못 받았을 때

신고의 절반은 번호 없이 온다(「아까 모바일에서 오류 났어요」). 그때는 같은
화면에서 기간과 검색어(경로 · 예외 · 메시지 · 아이디)로 훑는다. 기본 기간은
최근 사흘이고, 기기 칸이 있어 「모바일에서만 난다」를 바로 확인할 수 있다.
