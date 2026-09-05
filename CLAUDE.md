# Funeralv2 (JSini 관리 포털)

장례식장 관리 시스템 + JSini 업무 포털. .NET 8 마이크로서비스 백엔드, Vue3(vben) 프론트, Flutter 플레이어로 구성된 모노레포다.

## 저장소 구조

- `ApiGateway/` — API 게이트웨이 (:5265). 모든 프론트 요청이 여기를 거친다.
- `microservices/` — .NET 8 백엔드 서비스들 (EF Core + PostgreSQL)
  - `AuthServer` (:5264) 인증 · `funeralv2Api` (:5320) 장례식장 핵심 API
  - `AIAgentServer` (:5029) · `FileServer` (:5350) · `HelpDeskServer` (:5400)
  - `ProjMngServer` (:5450) · `SiteServer` (:5480) 회사 소개 사이트 백엔드
  - `NotificationServer` (:5460) 푸시·이메일 알림 (포털·장례식장·헬프데스크 공용)
  - `LifeEnvServer` (:5490) 생활과환경(기상·생일)
  - `Common/` — 서비스 간 공유 코드
- `fronts/` — pnpm + turbo 프론트 모노레포 (vben-admin 기반). **이행 중 — `web/` 으로 옮기는 원본이다.**
  - `apps/jsini-portal` (`@vben/jsini-portal`, :5555) — 업무 포털
  - `apps/jsini-site` (`@jsini/site`, :5556) — 회사 소개 사이트
  - `apps/funeralv2` — 빌드 산출물(dist)만 있음. 직접 수정하지 않는다.
- `web/` — .NET 10 + Blazor + DevExpress 업무 포털. `fronts/apps/jsini-portal` 을 대체한다.
  **업무별로 독립 배포되는 MFE 구조** — 셸(:5557)이 YARP 로 경로를 나눠 주고,
  업무 앱 여섯 개가 각자 프로세스로 돈다(:5561~:5566). 각 앱은 게이트웨이를 직접 부른다.
  지금은 골격만 있고 화면은 옮기는 중이다.
  세부 규칙은 [web/CLAUDE.md](web/CLAUDE.md) 참고 — 특히 **프로세스가 갈라져서 생긴
  규칙 세 가지**(`@page` 접두사 중복 금지 · 셸 설정과 앱 선언 일치 · Data Protection
  키 공유)와 **라우팅 소유권이 DB 에서 `@page` 로 뒤집힌 것**.
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

서비스 이름: `gateway auth funeral ai file helpdesk projmng site notify life portal web`
(`front`는 `portal`의 옛 이름, `web`이 소개 사이트 프론트)

Blazor 포털은 프로세스가 일곱 개라 그룹으로 묶어 두었다 — `dev.bat mfe`
(셸 `blazor` :5557 + 업무 MFE `uifuneral uihelpdesk uiadmin uisite uilife uiprojmng`).
창이 열아홉 개가 되므로 기본 `dev.bat`(=all)에는 넣지 않았다.

이행하는 동안 Vue 포털(:5555)과 Blazor 포털(:5557)을 나란히 띄워 화면을 대조한다:
`dev.bat portal mfe`

프론트 단독 작업:

```
cd fronts && pnpm install
pnpm --filter @vben/jsini-portal dev   # 포털 (:5555)
pnpm --filter @jsini/site dev          # 소개 사이트 (:5556)
```

빌드 확인: 백엔드는 해당 서비스 디렉터리에서 `dotnet build`, 프론트는 `fronts/`에서 `pnpm run check`(순환참조·타입·cspell 포함).

## 규칙

- 커밋 메시지·주석·문서는 한국어로 쓴다.
- 설정 우선순위: 환경변수(`scripts/secrets.env`) > appsettings. `Jwt__Key` 같은 이중 밑줄 표기.
- 비밀값(JWT 키, VAPID 키, DB 비밀번호 등)은 절대 커밋하지 않는다. `scripts/secrets.env.example`만 갱신한다.
- EF Core 마이그레이션을 추가하면 배포 전 운영 DB 반영 여부를 반드시 확인한다.
- lefthook이 커밋 훅으로 걸려 있다 (루트와 `fronts/` 각각 `lefthook.yml`).
- 개발 장비에서 업로드한 파일은 운영 서버에 실제 바이트가 없다 — 로컬 저장소와 운영 DB가 분리되어 있음을 유의.

## 하위 문서

각 영역의 세부 규칙은 해당 디렉터리의 CLAUDE.md 참고:

- [web/CLAUDE.md](web/CLAUDE.md) — Blazor 포털의 MFE 구조·의존 규칙·DevExpress 라이선스
- [.claude/agents/](.claude/agents/) — 전문 서브에이전트 정의
