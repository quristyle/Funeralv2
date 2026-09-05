# Funeralv2 (JSini 관리 포털)

장례식장 관리 시스템 + JSini 업무 포털. .NET 10 마이크로서비스 백엔드, .NET 10 Blazor 프론트, Flutter 플레이어로 구성된 모노레포다.

**프론트는 전부 .NET 이다.** Vue3(vben) + pnpm 모노레포(`fronts/`)는 2026-09-05 에 걷어냈다.
이관이 덜 끝난 화면의 원본이 필요하면 git 이력에서 꺼낸다 —
`git log --diff-filter=D -- fronts` 로 지운 커밋을 찾고
`git show <그 커밋>^:fronts/apps/jsini-portal/src/views/<경로>` 로 읽는다.

## 저장소 구조

- `ApiGateway/` — API 게이트웨이 (:5265). 모든 프론트 요청이 여기를 거친다.
- `microservices/` — .NET 10 백엔드 서비스들 (EF Core + PostgreSQL)
  - `AuthServer` (:5264) 인증 · `funeralv2Api` (:5320) 장례식장 핵심 API
  - `AIAgentServer` (:5029) · `FileServer` (:5350) · `HelpDeskServer` (:5400)
  - `ProjMngServer` (:5450) · `SiteServer` (:5480) 회사 소개 사이트 백엔드
  - `NotificationServer` (:5460) 푸시·이메일 알림 (포털·장례식장·헬프데스크 공용)
  - `LifeEnvServer` (:5490) 생활과환경(기상·생일)
  - `Common/` — 서비스 간 공유 코드
- `web/` — .NET 10 + Blazor + DevExpress 프론트. 옛 Vue 포털을 대체한다.
  - **업무 포털 셸** (:5557) — Piral.Blazor MFE. 업무 모듈 여섯(장례식장·헬프데스크·
    포털관리·소개사이트·생활과환경·프로젝트관리)이 **한 프로세스 안에** 실린다.
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

서비스 이름: `gateway auth funeral ai file helpdesk projmng site notify life blazor web`
(`blazor` 가 업무 포털 :5557, `web` 이 소개 사이트 :5556. `front`·`portal`·`mfe` 는 `blazor` 의 옛 이름이라 그대로 받아 준다.)

프론트도 이제 dotnet 서비스라 나머지와 똑같이 다룬다:

```
dev.bat blazor            업무 포털만 (:5557)
dev.bat web               소개 사이트만 (:5556)
dev.bat site web          소개 사이트 백엔드(:5480)와 프론트(:5556)
```

빌드 확인: 백엔드는 해당 서비스 디렉터리에서 `dotnet build`, 프론트는 `web/` 에서 `dotnet build` 와 `dotnet test`(아키텍처 규칙 검사).

## 규칙

- 커밋 메시지·주석·문서는 한국어로 쓴다.
- 설정 우선순위: 환경변수(`scripts/secrets.env`) > appsettings. `Jwt__Key` 같은 이중 밑줄 표기.
- 비밀값(JWT 키, VAPID 키, DB 비밀번호 등)은 절대 커밋하지 않는다. `scripts/secrets.env.example`만 갱신한다.
- EF Core 마이그레이션을 추가하면 배포 전 운영 DB 반영 여부를 반드시 확인한다.
- lefthook 설정이 루트에 있다 (`lefthook.yml` — 지금은 예시 주석뿐이라 거는 훅이 없다).
- 개발 장비에서 업로드한 파일은 운영 서버에 실제 바이트가 없다 — 로컬 저장소와 운영 DB가 분리되어 있음을 유의.

## 하위 문서

각 영역의 세부 규칙은 해당 디렉터리의 CLAUDE.md 참고:

- [web/CLAUDE.md](web/CLAUDE.md) — Blazor 포털의 MFE 구조·의존 규칙·DevExpress 라이선스
- [.claude/agents/](.claude/agents/) — 전문 서브에이전트 정의
