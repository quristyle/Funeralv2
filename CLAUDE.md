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
  - `AIAgentServer` (:5029) · `FileServer` (:5350) · `HelpDeskServer` (:5400)
    내 요청글에 **댓글이 달렸을 때** 글 주인에게 가는 앱푸시·이메일과 그것을
    끄는 자리는 [docs/helpdesk-comment-notify.md](docs/helpdesk-comment-notify.md)
  - `ProjMngServer` (:5450) · `SiteServer` (:5480) 회사 소개 사이트 백엔드
  - `NotificationServer` (:5460) 푸시·이메일 알림 (포털·장례식장·헬프데스크 공용).
    알림이 **한꺼번에 몰려 오거나 늦게 오는** 까닭 — 수명(TTL)·겹침(Topic) 설정과
    **보내기까지의 지연** 실측은 [docs/push-delivery.md](docs/push-delivery.md)
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

서비스 이름: `gateway auth funeral ai file helpdesk projmng site notify life cargo blazor web`
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

빌드 확인: 백엔드는 해당 서비스 디렉터리에서 `dotnet build`, 프론트는 `web/` 에서 `dotnet build` 와 `dotnet test`(아키텍처 규칙 검사).

## 배포

`main` 에 올라가면 `.github/workflows/deploy.yml` 이 **이미지 열셋**을 GHCR 에
올리고(백엔드 11 · 프론트 2) 운영 서버의 self-hosted 러너가 `docker compose pull`
후 `up -d` 한다. 태그는 커밋 SHA 이고 `/srv/jsini/.env` 의 `TAG` 가 그것을 가리킨다 —
**롤백은 그 값을 이전 SHA 로 되돌리고 `up` 하는 것**이다.

`ci.yml` 은 PR 과 main 푸시에서 빌드와 **아키텍처 테스트**(web/tests, 236건)를 돌린다.
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

## 규칙

- 커밋 메시지·주석·문서는 한국어로 쓴다.
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
- [web/CLAUDE.md](web/CLAUDE.md) — Blazor 포털의 MFE 구조·의존 규칙·DevExpress 라이선스
- [.claude/agents/](.claude/agents/) — 전문 서브에이전트 정의
