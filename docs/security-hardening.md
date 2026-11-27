# 바깥에서 두드릴 수 있는 자리와 막은 길

2026-11-17 전수 점검. 「게이트웨이는 로그인만 본다」는 전제 위에서 **각 서비스가 무엇을
스스로 막아야 하는지**를 정리한다. 새 경로를 열 때 이 표에 비춰 본다.

## 신원은 어떻게 오나

- 게이트웨이는 바깥이 보낸 `X-User-*` 를 지우고, **검증한 JWT 에서만** 다시 싣는다.
  그래서 뒷단에서 `X-User-Id` 가 비어 있으면 **익명 인터넷 요청이거나 서버 대 서버 호출**이다.
- 게이트웨이 라우트가 `Anonymous` 이면 뒷단이 **모든 경로를 스스로** 막아야 한다.
  `/api/auth/**` 가 그렇다 — 그래서 AuthServer 에 경로 문지기가 있다(아래).
- 뒷단 포트는 바깥에 안 열려 있다(ProjMng 만 `127.0.0.1:5450`). 서버 대 서버 호출은
  `X-User-Id` 없이 오거나(헬프데스크→Auth 회사 목록, 장례식장→Auth 공통코드, 루프백 운영 도구)
  `AI_TASK`·`system:*` 같은 이름을 싣고 온다.

## 서비스별 문지기

| 서비스 | 자리 | 규칙 |
|---|---|---|
| 게이트웨이 | `ai-route` | 익명 → 인증. 사내 문서 검색·Gmail 계정 목록·LLM 이 로그인 없이 열려 있었다 |
| 게이트웨이 | `oadr-route` | 인증 + 「한주」 화면 역할(`oadr-users`), `POST /api/oadr/api/procedure/execute` 하나만. 고객사로 나갈 때 `Authorization`·`Cookie`·`X-User-*` 를 뗀다 |
| 게이트웨이 | 속도 제한 | 통 열쇠는 `X-Forwarded-For` 의 **마지막 값**(nginx 가 덧붙인 접속 주소). 첫 값은 지어낼 수 있다. 포털 BFF 도 마지막 값을 넘긴다 |
| 게이트웨이 | `/api/gateway/status` | 로그인 뒤에만 |
| 게이트웨이 | `/api/funeral/building/source/{id:guid}` | `list` 가 익명 라우트에 걸리던 것을 guid 로 묶음 |
| AIAgentServer | 전체 | `X-User-Id` 없으면 401(`/health` 제외) |
| AuthServer | `AdminPathGuardMiddleware` | `/system` · `/server-ping` · `/menu/move|keep-alive` · `/notices` 는 신원 필수. 계정·역할·권한·조직·메뉴·가입승인·공지를 **바꾸는** 호출은 `ADMINISTRATOR`/`SYSTEM_ADMINISTRATOR`. server-ping 은 읽기도 관리자·`SERVER_ADMIN`. 익명 예외는 공지 팝업(public)·회사 목록·공통코드 한 그룹 읽기 |
| AuthServer | 이메일 인증번호 | 운영에서는 응답에 코드를 싣지 않는다 |
| ProjMngServer | `ProjMngRoleGateMiddleware` | 포털 사용자는 프로젝트관리 역할(`PROJMNG_*`·개발자·`SERVER_ADMIN`·관리자)이 있어야 한다. DB 접속·AI 대상 쓰기는 관리자, 등록 질의 편집은 DB 편집 역할. 파일 읽기(`md_source_context`)는 등록된 소스 루트 안·비밀 파일 제외 |
| HelpDeskServer | 여러 곳 | 첨부 행 직접 만들기 삭제·옛 내려받기 경로 가둠, `PasswordHash` 직렬화 제외, auth-links·push 방송·공지 쓰기 관리자, 댓글·요청 수정은 주인·관리자 |
| NotificationServer | `CallerAccess` | 메일 임의 주소·역할 방송은 관리자·서버 호출·메일 화면 권한만. 남의 발송기록·위치·기기 정보는 해당 화면 권한. 남의 구독 주소를 가로채지 못한다 |
| SiteServer | `/admin/inquiries` | 관리자만 |
| SiteServer | `GET /captcha`(익명) | 자체 캡차 그림(180×60 PNG, 글자 5개)과 서명 토큰. 정답은 서버에 남지 않고 토큰의 `HMAC(열쇠, 발급정보+정답)` 안에만 있다. 게이트웨이가 IP 당 분당 30회(`captcha-issue`)로 조인다. 열쇠는 `Captcha:Key`, 비우면 기동마다 새로 만든다(재기동 순간 풀던 그림만 틀린 것이 된다) |
| SiteServer | `POST /inquiries`(익명) | **캡차**(토큰 한 번만 · 15분 · 발급 2초 안의 제출은 거절 → 400 `CAPTCHA`) · 본문 64KB 상한(413) · 칸 길이(본문 5000자 등, 넘으면 400) · 메일 형식 · 허니팟 · **폼 스팸 판별**(이름에 `://`, 주소 말고는 글자가 없는 본문 → 조용히 버림). 담당자 알림 메일은 **저장된 값(거른 본문)**으로 만든다 — 요청 원문을 메일에 끼우던 길이 있었다. 이름·제목의 줄바꿈·제어문자는 걷는다 |
| 게이트웨이 → 뒷단 | `X-Client-Ip` | YARP 는 `X-Forwarded-For` 를 **게이트웨이에 붙은 쪽 주소로 덮어쓴다**(소개 사이트 문의가 전부 컨테이너 주소 `172.18.0.14` 로 남았다). 바깥이 보낸 `X-Client-Ip` 를 지우고 속도 제한과 같은 방문자 주소를 싣는다. 소개 사이트는 방문자 User-Agent 를 `X-Visitor-User-Agent` 로 넘긴다 |
| LifeEnvServer | 쓰기 | 관리자·`SERVER_ADMIN`·서버 호출만 |
| McpServer | 웹 가져오기 | DNS 를 풀어 사설·루프백·링크로컬·CGNAT 주소를 막고, 넘김(redirect)은 손으로 따라가며 매번 다시 본다. 점 없는 호스트(컨테이너 이름) 거절 |
| FileServer | 크기 바꾸기·공개·삭제·업로드 | 2000px 상한·확대 안 함·50MP 넘는 원본 거절, 공개 전환은 주인·관리자, 삭제는 주인·관리자·묶음 규칙, `bizType` 은 한 낱말(+`funeralv2/building`), `nosniff` 와 위험 형식은 내려받기로 |

## 남아 있는 것

- **장례식장 API 의 회사 경계가 없다.** 로그인한 누구나 다른 회사 고인 정보(주민번호 칸 포함)를
  읽고 고칠 수 있다. `X-User-Company-Id` 를 모든 질의·쓰기에 강제해야 하는 큰 일이다.
- **장비 허브(SignalR)가 익명이다.** 장비 코드를 모으고 상태를 꾸밀 수 있다 —
  `PlayerAuth:RequireDeviceToken` 를 켜는 순서는 `ApiGateway/Program.cs` 주석에 있다.
- **ProjMng `/api/Dev` 의 `$이름` 끼워 넣기**는 모듈 역할 안에서 여전히 SQL 주입이다
  (`@` 매개변수로 바꿔야 한다). 루프백 `127.0.0.1:5450/api/dev/sql` 은 신원 없이 통과한다.
- **포털 오류 접수**(`POST /api/auth/portal-errors`)는 `PORTAL_ERROR_TOKEN` 이 비면 누구나 쓴다.
- **소개 사이트 문의 캡차는 쓴 토큰을 메모리에 기억한다.** SiteServer 를 둘 이상 띄우면 같은 토큰을
  인스턴스마다 한 번씩 쓸 수 있다. 그림은 OCR 을 겨냥해 만든 봇이면 풀 수 있는 세기다 — 그런 봇이
  보이면 글자 수·비틀기를 올리거나 쓴 토큰을 DB 로 옮긴다.
- 헬프데스크 요청 **읽기**는 회사로 좁혀지지 않았다. FileServer 묶음 파일은 같은 묶음을 쓰는
  동료가 지울 수 있다(업무 흐름상).
