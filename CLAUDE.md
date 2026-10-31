# Funeralv2 (JSini 관리 포털)

장례식장 관리 시스템 + JSini 업무 포털. .NET 10 마이크로서비스 백엔드, .NET 10 Blazor 프론트, Flutter 플레이어로 구성된 모노레포다.

**프론트는 전부 .NET 이다.** Vue3(vben) + pnpm 모노레포(`fronts/`)는 2026-09-05 에 걷어냈다.
이관이 덜 끝난 화면의 원본이 필요하면 git 이력에서 꺼낸다 —
`git log --diff-filter=D -- fronts` 로 지운 커밋을 찾고
`git show <그 커밋>^:fronts/apps/jsini-portal/src/views/<경로>` 로 읽는다.

## 저장소 구조

- `ApiGateway/` — API 게이트웨이 (:5265). 모든 프론트 요청이 여기를 거친다.
- `microservices/` — .NET 10 백엔드 서비스들 (EF Core + PostgreSQL)
  - `AuthServer` (:5264) 인증 · `funeralv2Api` (:5320) 장례식장 핵심 API.
    AuthServer 는 **포털 프론트가 잡은 미처리 예외도 들고 있다** —
    오류 화면의 추적 번호로 까닭을 찾는 길은 [docs/error-trace.md](docs/error-trace.md)
    **누가 어떤 화면을 언제 보았는지**도 여기 쌓인다 — 회로 안에서 라우팅만
    바뀌어 서버 로그에는 안 남으므로 셸이 적어 보낸다:
    [docs/menu-usage.md](docs/menu-usage.md)
    계정 관리 목록의 **마지막 로그인 · 마지막 사용**이 그 길에 얹혀 있다 —
    토큰이 7일·갱신 쿠키가 30일이라 **로그인 시각만 보면 매일 쓰는 사람도
    「한 달 전 사람」**이다: [docs/account-activity.md](docs/account-activity.md)
    **권한관리 밖의 주소는 기본으로 막히고** 막힌 사람과 주소가 여기 쌓인다 —
    메뉴에 올릴 수 없는 상세 화면은 `[ScreenAccess]` 로 **부모 메뉴를 가리킨다**:
    [docs/screen-access.md](docs/screen-access.md)
    **시스템 모니터링 보고서를 권한 역할에게 주기로 보내는 배치**도 여기 산다 —
    주소를 푸는 일은 알림 서버가 하고, 메일에 담기는 것은 숫자가 아니라 각자의
    화면으로 가는 길이다. **미리받아보기**는 저장 전에도, 나에게만 보낸다:
    [docs/report-mail.md](docs/report-mail.md)
  - `AIAgentServer` (:5029) · `FileServer` (:5350) · `HelpDeskServer` (:5400)
    AIAgentServer 는 **추론 장비(호스트명 `llm`)의 하드웨어 상태도 중계한다** —
    `/admin/status/llm` 이 읽는 그 수집기의 정본은 장비가 아니라
    [deploy/llm/](deploy/llm/) 에 있다. 그 화면은 **사내 문서 검색(RAG)과
    웹 검색(MCP)의 상태도 함께 보여 준다** — 장비가 멀쩡해도 그 둘은 따로
    끊어지고(임베딩 서버 · 색인 · 도구 서버), 그때도 대화는 아는 것만으로
    멀쩡히 돌아서 서비스 상태로는 안 갈린다. **설정만 보면 죽어도 초록이라**
    임베딩 서버를 실제로 한 번 눌러 본 값을 함께 담는다(2분 동안 재어 둔다).
    **「GPU 를 읽지 못했습니다」는 거의 늘
    드라이버 판 어긋남**이고(꾸러미만 올라가고 커널 모듈은 그대로),
    그때도 **추론은 멀쩡히 돌아서** 서비스 상태로는 안 갈린다

    **AI쳇은 말로도 쓴다** — 받아쓰기는 우리 장비가 하고(`POST /api/ai/stt`)
    읽어주기는 브라우저가 한다. 브라우저 받아쓰기를 안 쓰는 까닭은 **아이폰에
    아예 없고 말소리가 구글 서버로 나가기** 때문이다. 그 길도 같은 상태 화면에
    실리고 **무음 하나를 실제로 보내 본** 값이다 — 모델을 못 올린 서버는 멀쩡히
    200 을 주면서 받아쓰기만 전부 실패한다. 끊겨도 대화는 멀쩡하다(마이크 단추만
    접힌다): [docs/ai-voice-chat.md](docs/ai-voice-chat.md),
    올리는 길은 [deploy/stt/](deploy/stt/)

    내 요청글에 **댓글이 달렸을 때** 글 주인에게 가는 앱푸시·이메일과 그것을
    끄는 자리는 [docs/helpdesk-comment-notify.md](docs/helpdesk-comment-notify.md) —
    그 길도 한동안 **「자기 글에 자기가 단 답글은 뺀다」**가 막고 있었다
    (글쓴이가 곧 담당자라 그것이 전부였다)
    **접수·완료·종료**도 같은 길로 간다 — 그 전에는 보내는 쪽이 **옛 포털이
    채우던 빈 구독표**를 보고 있어서 완료 알림이 한 통도 안 나갔다:
    [docs/helpdesk-status-notify.md](docs/helpdesk-status-notify.md)
    **슈퍼관리자·임원 전용 업무 비서**(`/admin/ai/assistant`)는 사내 문서(RAG)뿐
    아니라 헬프데스크·프로젝트관리·메일 같은 **포털 업무 데이터**도 가로질러
    답한다 — 색인하지 않고 모델에게 **도구**를 쥐어 준다(범위 질의·매분 바뀌는
    자료라 RAG 와 안 맞는다). 묻는 사람의 평소 권한을 넘어서 답하므로 **질문과
    답 전문이 통째로 기록**된다. 실측 결과 지어냄(없는 것을 지어내는 것)은
    0건이었으나, 질문 다섯 중 셋은 **도구를 건너뛰고 "모른다"고 답해 버렸다**
    (정답이 있는데도) — 고칠 자리와 아직 안 끝난 것(내부 토큰·기록 표·「임원」
    역할 모두 미적용)은 [docs/portal-ai-assistant.md](docs/portal-ai-assistant.md)
  - `ProjMngServer` (:5450) · `SiteServer` (:5480) 회사 소개 사이트 백엔드
  - `McpServer` (:5510) — 모델이 쓰는 **바깥 도구**를 MCP 규격으로 연다
    (웹 검색 · 페이지 본문 가져오기). AIAgentServer 가 클라이언트로 붙고,
    **도구를 더해도 AIAgentServer 는 고치지 않는다** — 목록을 서버에게 물어 쓴다.
    검색 뒷단은 설정으로 갈린다(기본 DuckDuckGo · SearXNG · Brave).
    쓰는 길은 `POST /api/ai/chat/web` — 답에 **출처 주소가 붙는다.**
  - `NotificationServer` (:5460) 푸시·이메일 알림 (포털·장례식장·헬프데스크 공용).
    **어떤 일에 누가 알림을 받는지**는 이제 코드가 아니라 표가 정한다 —
    권한 역할 × 이벤트 × 길(앱푸시·이메일)을 포털관리에서 맨다. 정책이 없는
    이벤트는 **지금 그대로 나가고**, 본인이 꺼 두면 그것이 마지막 말이다 —
    다만 **소개 사이트 문의**는 그 표가 정본이라(설정 파일의 받는 역할을
    걷어냈다) 역할을 비우면 **아무에게도 안 간다**:
    [docs/notify-policy.md](docs/notify-policy.md)
    알림이 **한꺼번에 몰려 오거나 늦게 오는** 까닭 — 수명(TTL)·겹침(Topic) 설정과
    **보내기까지의 지연** 실측은 [docs/push-delivery.md](docs/push-delivery.md).
    **「기기 넷에 켜 두었는데 하나만 운다」**도 거기다 — 「보냈다」는 푸시 서비스가
    받아 주었다는 뜻일 뿐이라, 기기가 실제로 띄웠는지는 서비스워커가 되알려 준
    것(`delivered_at`)으로만 갈린다
    **쪽지**는 받는 사람을 치지 않고 고르고(최근·전체), 휴대폰에서는 창이 아니라
    화면을 채운다. 앱 푸시가 **기기에 닿지 않으면 메일로 돌린다** — 「보냈다」와
    「닿았다」를 가르는 자리: [docs/note-delivery.md](docs/note-delivery.md)
    **사람이 손으로 쓰는 편지**는 그 길이 아니라 따로다 — 주소를 직접 적거나
    사용자 중에서 고르고, 참조·첨부를 달고, **보낸 글이 전문으로 남는다.**
    AI 가 본문을 추려 머리에 얹는 자리도 여기: [docs/mail-send.md](docs/mail-send.md)
  - `LifeEnvServer` (:5490) 생활과환경(기상·생일)
  - `CargoTrustServer` (:5500) JSini 운송관리 — 화물 거래처 신뢰정보. 계약은 [docs/cargotrust/05-api-design.md](docs/cargotrust/05-api-design.md)
    **톨게이트 심야할인**(진입·진출 시각 ↔ 할인율)과 계정에 매다는 차량 관리는
    [docs/cargotrust/06-toll-night-discount.md](docs/cargotrust/06-toll-night-discount.md) —
    할인율은 차종이 아니라 **야간 체류 비율**로 정해진다
  - `Common/` — 서비스 간 공유 코드
- `web/` — .NET 10 + Blazor + DevExpress 프론트. 옛 Vue 포털을 대체한다.
  - **업무 포털 셸** (:5557) — Piral.Blazor MFE. 업무 모듈 여덟(장례식장·헬프데스크·
    포털관리·소개사이트·생활과환경·프로젝트관리·운송관리·운송관리 관리자)이
    **한 프로세스 안에** 실린다.
    모듈은 빌드 시점에 합성되고(셸 csproj 의 ProjectReference) 셸이 어셈블리를 훑어
    `IPortalModule` 로 등록한다. 게이트웨이는 각 모듈이 직접 부른다(BFF).
  - **회사 소개 사이트** (:5556, `src/Site/JSini.PublicSite`) — 정적 SSR 전용.
    포털과 무관하고 인증도 없다. 공유 프로젝트를 하나도 참조하지 않는다.
  - 화면은 옮기는 중이다. 아직 안 옮긴 메뉴는 404 가 아니라 "준비 중" 안내가 뜬다.
  세부 규칙은 [web/CLAUDE.md](web/CLAUDE.md) 참고 — 특히 **`@page` 가 DB 메뉴 경로와
  같아야 한다**는 것과 **라우팅 소유권이 DB 에서 `@page` 로 뒤집힌 것**.
- `funeralv2_player/` — Flutter 빈소 디스플레이 플레이어
- `deploy/` — 배포 관련 (docker, release-consumer, attachment-migration)
- `scripts/` — 개발 보조 스크립트, `secrets.env`(git 미포함, example 참고)

## 개발 명령

서비스 기동/중지는 반드시 `dev.bat`(Windows) / `backend_run_ubuntu.sh` / `backend_run_mac.sh`를 쓴다. 수동으로 `dotnet run` 하지 않는다.

```
dev.bat                 # 전체 재기동 (중지 → 빌드 → 기동)
dev.bat auth file       # 지정 서비스만 재기동
dev.bat stop helpdesk   # 지정 서비스만 중지
dev.bat allstop         # 전체 중지
dev.bat status          # 떠 있는 서비스 확인
dev.bat list            # 서비스 이름 목록
```

서비스 이름: `gateway auth funeral ai file helpdesk projmng site notify life cargo mcp blazor web`
(`blazor` 가 업무 포털 :5557, `web` 이 소개 사이트 :5556. `front`·`portal`·`mfe` 는 `blazor` 의 옛 이름이라 그대로 받아 준다.)

프론트도 이제 dotnet 서비스라 나머지와 똑같이 다룬다:

```
dev.bat blazor            업무 포털만 (:5557)
dev.bat web               소개 사이트만 (:5556)
dev.bat site web          소개 사이트 백엔드(:5480)와 프론트(:5556)
```

`./devui.sh` 는 위 스크립트를 감싸는 로컬 웹 제어판이다(:5600). 제어판 자신도
백그라운드로 뜨므로 부른 터미널을 닫아도 돌고, 끄는 것은 `./devui_stop.sh` 다
(제어판만 끈다 — 서비스는 그대로 돈다). 서비스마다 작업이 따로 돌아 하나를
재기동하는 동안에도 다른 것을 만질 수 있고, 서비스도 터미널 창 없이 백그라운드로
뜬다(`DEV_BACKGROUND=1`) — 출력은 `logs/<이름>.log` 에 쌓이고 화면의 ☰ 단추로 본다.
같은 환경변수를 주면 `backend_run_ubuntu.sh` 도 창 없이 띄운다.
Windows 에서는 `devui.cmd` / `devui_stop.cmd` 로 같은 제어판을 띄우고 끈다 —
그때 제어판이 부르는 것은 `dev.bat` 이고, 같은 `DEV_BACKGROUND=1` 을 받는다.

빌드 확인: 백엔드는 해당 서비스 디렉터리에서 `dotnet build`, 프론트는 `web/` 에서 `dotnet build` 와 `dotnet test`(아키텍처 규칙 검사).

## 배포

`main` 에 올라가면 `.github/workflows/deploy.yml` 이 **이미지 열넷**을 GHCR 에
올리고(백엔드 12 · 프론트 2) 운영 서버의 self-hosted 러너가 `docker compose pull`
후 `up -d` 한다. 태그는 커밋 SHA 이고 `/srv/jsini/.env` 의 `TAG` 가 그것을 가리킨다 —
**롤백은 그 값을 이전 SHA 로 되돌리고 `up` 하는 것**이다.

`ci.yml` 은 PR 과 main 푸시에서 빌드와 **아키텍처 테스트**(web/tests, 533건)를 돌린다.
배포 워크플로는 컨테이너 안에서 `dotnet publish` 만 하므로 그 테스트가 거기서는
한 번도 돌지 않는다.

프론트도 2026-09-13 부터 같은 길로 간다. 그 전에는 nginx 가 `/srv/jsini/portal`
(vben 정적 산출물)을 직접 서빙해서 **프론트만 자동 배포에서 빠져 있었다.**

| | 이미지 | 컨테이너 포트 | 도메인 |
|---|---|---|---|
| 업무 포털 셸 | `funeralv2-portal` | `127.0.0.1:5557` | portal.jsini.co.kr |
| 회사 소개 사이트 | `funeralv2-web` | `127.0.0.1:5556` | jsini.co.kr |
| 게이트웨이 | `funeralv2-gateway` | `127.0.0.1:5265` | 위 둘의 `/api/` |

배포가 끝나면 마지막 단계가 게이트웨이로 `POST /api/notification/deploy-event` 를
불러 **슈퍼관리자 전원에게 PWA 푸시**를 보낸다(실패한 배포도 보낸다).
공유 비밀 하나로 인증하며 — 저장소 시크릿 `DEPLOY_NOTIFY_TOKEN` 과 서버의
`DeployNotify:Token` 이 같아야 한다 — 값이 없으면 알림만 조용히 건너뛴다.
자세한 것은 [docs/deploy-notify.md](docs/deploy-notify.md).

nginx 설정 정본은 [deploy/nginx/](deploy/nginx/) 에 있다 — Blazor 회로(SignalR)
때문에 웹소켓 업그레이드 블록이 필요하고, 그것이 빠지면 **화면은 그려지는데
단추가 하나도 안 눌린다.**

컨테이너가 죽었을 때 사람이 보는 **502 화면도 거기 산다**(`html/50x.html`) —
두 사이트가 같은 파일 하나를 쓰고, **바깥을 하나도 부르지 않으며**(스타일·로고가
파일 안에 있다. 받아 오게 두면 그것들도 같이 502 다) 뒷단이 되살아나면 스스로
보던 화면으로 돌아간다. **`/api/` 만 JSON 으로 가른다** — 거기로 오는 것은
사람이 아니라 플레이어·북마크라, HTML 을 주면 파싱 오류가 진짜 원인을 덮는다.

## 규칙

- 커밋 메시지·주석·문서는 한국어로 쓴다.
- **주석은 코드를 보고는 알 수 없는 것만 한두 줄로 적는다.** 날짜 박힌 변경
  이력과 「한동안 …이었다」처럼 지금 없는 모양을 설명하는 글은 적지 않는다 —
  그런 것은 git 이력이 들고 있다. 규칙과 정리 진행은 [docs/comment-cleanup.md](docs/comment-cleanup.md).
- 설정 우선순위: 환경변수(`scripts/secrets.env`) > appsettings. `Jwt__Key` 같은 이중 밑줄 표기.
- 비밀값(JWT 키, VAPID 키, DB 비밀번호 등)은 절대 커밋하지 않는다. `scripts/secrets.env.example`만 갱신한다.
- EF Core 마이그레이션을 추가하면 배포 전 운영 DB 반영 여부를 반드시 확인한다.
- **시각은 전부 UTC 다.** DB 의 시각 칸은 모두 `timestamptz` 이고 컨테이너 시계도
  UTC 다(`TZ=Etc/UTC`). 코드에서 `DateTime.Now`·`DateTime.Today` 를 쓰지 않는다 —
  `AppTime` 을 쓴다. 한국 시각은 **보여 주기 직전에 한 번만** 만들고, 달력
  날짜(`date` 칸)만 한국 달력으로 센다. 경계가 어디에 그어져 있는지는
  [docs/utc-time.md](docs/utc-time.md).
- lefthook 설정이 루트에 있다 (`lefthook.yml` — 지금은 예시 주석뿐이라 거는 훅이 없다).
- 개발 장비에서 업로드한 파일은 운영 서버에 실제 바이트가 없다 — 로컬 저장소와 운영 DB가 분리되어 있음을 유의.

## 하위 문서

각 영역의 세부 규칙은 해당 디렉터리의 CLAUDE.md 참고:

- [docs/utc-time.md](docs/utc-time.md) — 시각을 UTC 로 다루는 규칙과 한국 시각이 남은 자리
- [docs/geolocation-background.md](docs/geolocation-background.md) — 포털을 닫은 뒤에도
  위치를 모을 수 있나(서비스워커에 `geolocation` 은 **없다** — 실측과 그래도 할 수 있는 일)
- [docs/ai-voice-chat.md](docs/ai-voice-chat.md) — AI쳇을 말로 쓰는 길: 받아쓰기와
  음성 모드를 가른 까닭 · 소리를 회로로 넘기는 까닭 · 서랍이 `fixed` 를 가두는 함정
- [deploy/stt/README.md](deploy/stt/README.md) — 받아쓰기 서버 정본과 모델 고르기
- [docs/note-delivery.md](docs/note-delivery.md) — 쪽지: 빠른 대상 선택 · 휴대폰
  전체화면 · 푸시가 안 닿았을 때 메일로 전환하는 길
- [docs/mail-send.md](docs/mail-send.md) — 메일 보내기: 「메시지 발송」과 무엇이
  다른가 · 받는 사람을 채우는 길 셋 · AI 요약 · 보낸 메일함과 발송 기록의 갈래
- [docs/notify-policy.md](docs/notify-policy.md) — 알림관리: 알림이 나가는 자리
  열여섯의 조사표와, 역할·이벤트·길을 매는 규칙(정책 → 본인 설정 → 구독)
- [docs/helpdesk-status-notify.md](docs/helpdesk-status-notify.md) — 요청글
  접수·완료·종료 알림: 구독표가 둘로 갈려 **완료 알림이 0 통**이던 까닭과 고친 길
- [docs/screen-access.md](docs/screen-access.md) — 메뉴에 없는 화면이 주소만으로
  열리던 구멍: 기본 차단으로 뒤집은 길과 「차단 접근기록」 화면
- [docs/account-activity.md](docs/account-activity.md) — 계정의 마지막 로그인과
  **마지막 사용**: 둘을 가른 까닭 · 쌓는 자리 둘 · 결합이 안 옮기던 로그인 시각
- [docs/gmail-archive.md](docs/gmail-archive.md) — Gmail 보관함: 계정 종류가
  인증을 가르는 까닭(Workspace 는 API · 개인은 IMAP) · 길 셋이 겹칠 때
  `Message-ID` 로 지우는 자리 · 적재가 폴더를 못 벗어나게 묶은 길
- [docs/portal-ai-assistant.md](docs/portal-ai-assistant.md) — 업무 비서: 포털
  데이터를 도구로 여는 까닭 · 도구 일곱 · 기록이 답 전문을 담는 까닭 · 실측
  숫자와 지어냄 0건 · 아직 안 끝난 것(내부 토큰 · 기록 표 · 「임원」 역할)
- [deploy/llm/README.md](deploy/llm/README.md) — 추론 장비의 상태 수집기 정본과,
  「GPU 를 읽지 못했습니다」를 재부팅 없이 푸는 길
- [docs/table-lineage.md](docs/table-lineage.md) — 테이블 계통도: 도면이 **DB 접속마다
  한 장**인 까닭 · 담는 곳 세 줄 · 폐쇄망 DB(HHIP_MM Oracle)에 못 붙어도 열리는 길
- [docs/projmng-scheduler-schema.md](docs/projmng-scheduler-schema.md) — 프로젝트 일정표가
  쓰는 표(`projmng.dev_wbs` — **WBS 와 같은 표**)와 곁 표·공통코드의 운영 스키마
- [docs/spa-migration-analysis.md](docs/spa-migration-analysis.md) — 현재 시스템의
  SPA 전환 시 기술적 문제 요소 및 영향도 분석
- [web/CLAUDE.md](web/CLAUDE.md) — Blazor 포털의 MFE 구조·의존 규칙·DevExpress 라이선스
- [.claude/agents/](.claude/agents/) — 전문 서브에이전트 정의

