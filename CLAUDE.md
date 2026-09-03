# JSini 관리 포털

장례식장(funeralv2) · 헬프데스크 · 프로젝트관리를 MSA 로 붙여 나가는 관리 포털이다.
인증 · 메뉴 · 권한은 **AuthServer 한 곳**이 관장한다.

## ⚠ 작업 전에 반드시 읽을 것

### [docs/준수사항.md](docs/준수사항.md)

이 저장소에서 지켜야 하는 규칙 목록이다. **작업 시작 전에 읽고, 끝나기 전에 어겼는지 확인한다.**
새 규칙은 이 파일에 번호를 이어 추가한다(번호는 바뀌지 않는다).

점검 기록: [docs/analysis/16-준수사항-점검.md](docs/analysis/16-준수사항-점검.md)
(무엇을 고쳤고 무엇이 판단 대기인지)

현재 규칙 요약:

| 번호 | 규칙 |
|---|---|
| 1 | (비어 있음) |
| 2 | (비어 있음) |
| 3 | 모든 팝업은 헤더를 잡고 옮길 수 있어야 한다 |
| 4 | 세로 스크롤 없이 한 화면에 담는 것을 지향한다 |
| 5 | 글꼴은 저장소 안의 파일만 쓴다 (바깥 CDN 금지) |
| 6 | 표는 `useVbenVxeGrid` 하나로만 그린다 (정렬·필터는 공통 레이어가 붙인다) |
| 7 | 응답 봉투는 API 모듈에서만 벗긴다 (`api/envelope.ts` 의 `unwrapList`·`unwrapOne`·`unwrapPage`) |

## 구조

```
ApiGateway/            YARP 게이트웨이 (:5265) — JWT 검증 후 X-User-* 헤더를 붙인다
microservices/
  AuthServer/          인증 · 계정 · 메뉴 · 권한 · 공지 · 생일 (포털의 중심)
  funeralv2Api/        장례식장
  HelpDeskServer/      헬프데스크 (이식)
  ProjMngServer/       프로젝트관리 (이식)
  FileServer/          파일 보관 — 첨부와 본문 이미지가 모두 여기로 간다
  AIAgentServer/
  SiteServer/          회사 소개 사이트(공개) — 문구 · 자료실 · 문의 접수
  NotificationServer/  푸시 · 이메일 (셋이 공유) — VAPID 키가 여기 한 곳에만 있다
  LifeEnvServer/       생활과환경 — 기상(기상청 연동, GHUB 에서 이식). 생일 화면의 API 는 AuthServer 다
fronts/apps/jsini-portal/   업무 프론트엔드 (Vue 3 + vben, 로그인 필요) — PWA (portal.jsini.co.kr, 39번 문서)
fronts/apps/jsini-site/     회사 소개 사이트 (Vue 3 + vite-ssg, @vben/* 의존 0)
docs/brand/            로고 · 모티프 · 사용 규칙 (SVG 는 generate.py 가 만든다)
docs/analysis/         작업 기록과 결정 사항
docs/sql/              실행한 SQL (전부 반복 실행 안전)
```

## 알아 두면 시간을 아끼는 것

- **메뉴는 백엔드 주도다.** `scom.system_menus` 에 없거나 `status = 0` 이면 라우트 자체가 생기지 않는다.
- **DB 는 여섯이다.** 서비스마다 하나씩이고, 이름 하나에 스키마 하나다.

  | DB | 스키마 | 쓰는 서비스 |
  |---|---|---|
  | `jsiniportal` | `scom` | AuthServer · FileServer · NotificationServer |
  | `funeralv2` | `smfr` | funeralv2Api |
  | `jinrecept` | `jsini` | HelpDeskServer |
  | `projmng` | `projmng` | ProjMngServer |
  | `jsinisite` | `site` | SiteServer |
  | `ghub` | `ghub` | LifeEnvServer |

  접속 문자열은 각 서비스의 `appsettings.Local.json` (git 제외).
  **`docs/sql` 의 파일은 대부분 `jsiniportal` 용이다** (scom 을 다룬다).
  `site_*.sql` 만 `jsinisite`, `funeralv2_*` 는 `funeralv2` 다 —
  각 파일 머리말에 어느 DB 인지 적혀 있다.
  **`funeralv2` 라는 이름이 이제 장례식장만 뜻한다.** 2026-08-29 전에는 포털 표(scom)도
  같은 DB 에 있었다. 옛 문서에서 "funeralv2 의 scom" 을 보면 `jsiniportal` 로 읽는다.
  왜 이렇게 나눴고 셋이 왜 아직 `scom` 을 함께 쓰는지는
  [docs/analysis/37-db-per-service.md](docs/analysis/37-db-per-service.md) 에 있다.
- **`funeralv2` 표는 SQL 파일이 아니라 EF 마이그레이션으로 바꾼다.** funeralv2Api 는
  `Migrations/` 와 `smfr.__EFMigrationsHistory` 로 스키마를 관리한다. 여기만
  `docs/sql` 에 손으로 쓰면 정본이 둘이 되고 인덱스 이름부터 어긋난다.
  **기동 때 스스로 적용하지 않으므로**(`Database.Migrate()` 는 FileServer 뿐)
  사람이 돌린다. `--no-build` 로 돌릴 때는 **Debug 빌드가 최신인지 먼저 본다** —
  낡은 어셈블리를 보면 EF 가 변경을 못 알아채고 빈 마이그레이션을 만든다.
  ```bash
  cd microservices/funeralv2Api && dotnet build -c Debug && dotnet ef database update
  ```
- **개발 중에는 `dotnet watch` 로 6개가 떠 있는 경우가 많다.** `.cs` 를 고치면 자동 재기동하지만
  `appsettings.json` 변경만으로는 재기동하지 않는다.
  다만 **`dotnet run --no-build` 로 떠 있는 경우도 있다.** 그때는 `.cs` 를 고쳐도 아무 일이 없다.
  고친 것이 반영됐는지 의심되면 실행 중인 바이너리 시각과 소스 시각을 비교한다.
  ```bash
  ls -l --time-style=+%H:%M:%S microservices/AuthServer/bin/Debug/net8.0/AuthServer.dll
  ```
- 개발 환경은 `Auth:SkipPasswordCheck=true` 라 아이디만으로 로그인된다.
- **JWT 서명 키는 `appsettings.Local.json` 에만 있다** (결정 D1-B). 추적 파일에는
  `__SET_IN_appsettings.Local.json__` 자리표시자만 있고, 키가 없거나 옛 평문 값이면
  `JwtKeyGuard` 가 **기동을 막는다.** 네 곳이 같은 값이어야 한다 —
  ApiGateway `Jwt:Key` · AuthServer `JwtSettings:SecretKey` ·
  HelpDeskServer `GatewayJwt:Key` · ProjMngServer `Jwt:Key`.
- **`appsettings.Local.json` 이 환경변수를 덮는다.** 각 서비스가 기본 소스 뒤에 붙이기
  때문이다. `Jwt__Key=...` 같은 환경변수로 덮으려 해도 안 먹는다.
- 검증: `pnpm vite build` · `./scripts/smoke-test.sh` ·
  백엔드는 **솔루션 파일이 없다.** 프로젝트별로 돌린다
  (실행 중이면 exe 가 잠겨 Debug 빌드가 실패한다 — 그때는 `-c Release`).
  ```bash
  for p in ApiGateway/*.csproj microservices/*/*.csproj; do dotnet build "$p" -c Release; done
  ```
  개발 서버는 `dev.bat`(윈도우) · `backend_run_ubuntu.sh` 로 띄운다. 하나만 재기동하려면
  `dev.bat gateway auth file` 처럼 이름을 나열한다.
  이름은 열둘이다 — `gateway auth funeral ai file helpdesk projmng site notify life portal web`.
  `portal`(:5555) 이 업무 프론트, `web`(:5556) 이 회사 소개 사이트 프론트다.
  **예전 이름 `front` 는 `portal` 이 되었다**(`front` 로 쳐도 받아 준다).

## 결정이 필요한 것

[docs/analysis/12-decisions-pending.md](docs/analysis/12-decisions-pending.md) 에 모아 둔다.
자율로 진행하기에 영향이 크거나 되돌리기 어려운 일은 여기에 적고 손대지 않는다.

- 배포 도구: [docs/analysis/28-release-tool.md](docs/analysis/28-release-tool.md) (D-R1~D-R5)
  화면이 `setTimeout` 으로 가짜 진행 단계를 초록색으로 찍던 것을 걷어내고, 배포 장비가
  **실제로 보고한 것만** 보여 주도록 바꿨다. 요청 한 건이 `scom.release_runs` 행이 된다.
  **진행 보고는 기본 꺼짐이라 지금은 켜기 전과 똑같이 동작한다.**
  켜는 법(배포 장비의 소비자는 고치지 않는다): [deploy/release-consumer/README.md](deploy/release-consumer/README.md)
  남은 것은 큐 공유(D-R1)·롤백(D-R3)·`VersionUrl`(D-R5).
- 장례 API 익명 노출: [docs/analysis/46-player-media-anonymous-access.md](docs/analysis/46-player-media-anonymous-access.md) (D-M1~D-M4)
  플레이어용 익명 라우트 여섯의 열쇠가 **장비 코드 하나**이고 추측 가능하다.
  파일 접근은 고쳤다 — `Files:AnonymousReadablePaths` 가 `funeralv2/` 만 익명에 연다
  (그 아래는 빈소 화면에 띄우려고 올린 미디어뿐이다. 비밀 문서를 여기 두면 안 된다).
  **응답 축약(D-M2)도 했다** — `DTOs/AnonymousDisplayProjection.cs` 가 익명 응답에서
  주민번호·사인·비고·계약자·장례지도사·상주 연락처·장비 IP/MAC 을 뺀다.
  고인은 허용 목록, 장비는 차단 목록으로 짰다(이유는 그 파일 주석).
  **요청 제한(D-M3)도 했다** — 게이트웨이 정책 `player-read` 가 익명 읽기 다섯 경로에
  IP 당 분당 900회를 건다(장례식장 하나가 공인 IP 를 공유하므로 값이 크다.
  SignalR 허브에는 걸지 않았다). **장비 토큰(D-M1)도 기반은 있다** —
  게이트웨이 `PlayerAuth:RequireDeviceToken`(기본 꺼짐) + 플레이어 설정의
  장비 인증 키 칸. 남은 것은 **켜는 결정**(전 장비에 키를 먼저 넣어야 한다,
  46번 문서 9절의 순서)과 장비별 DB 토큰 전환이다.
- 플레이어 새 버전 업그레이드: [docs/analysis/48-player-self-update.md](docs/analysis/48-player-self-update.md) (D-P1~D-P4)
  환경 설정에 **새 버전 확인**을 넣었다(전 플랫폼, GitHub Releases 를 직접 본다).
  **v1.0.1 로 릴리스됐다**(2026-09-03) — JSINI 아이콘·이름·업데이트 확인·장비 인증 키 칸이
  현장에 배포 가능한 상태다. v1.0.0 장비는 이 기능이 없으므로 첫 갱신만 수동이다.
  **실제 교체는 안드로이드만** 된다 — 시스템 설치 화면을 띄운다.
  데스크톱은 받기까지만 하고 설치 명령을 화면에 적어 준다.
  남은 것은 윈도우 자동 교체(D-P1)·리눅스 root(D-P2)·원격 지시(D-P3)·자동 설치(D-P4).
- 이식 시스템에서 '누구로서' 일할지 정하는 스위치 둘: [docs/analysis/19-msa-user-work-enablement.md](docs/analysis/19-msa-user-work-enablement.md) (Q9~Q13 · D14)
  둘 다 **기본 꺼짐**이라 지금은 켜기 전과 똑같이 동작한다. D13 을 먼저 처리해야 한다.
- 준수사항 점검에서 남은 것: [docs/analysis/16-준수사항-점검.md](docs/analysis/16-준수사항-점검.md) (R1~R4)
- 도움말 F.A.Q · Q&A: [docs/analysis/21-help-faq-qna.md](docs/analysis/21-help-faq-qna.md) (D-H1~D-H5)
  `docs/sql/help_faq_qna.sql` 은 실행했다. 남은 것은 PARTNER_ADMINISTRATOR 역할을
  관리자로 볼지(D-H1)와 Q&A 첨부파일(D-H3).
- 옛 장례식장 시스템 이식: [docs/analysis/40-old-funeral-migration.md](docs/analysis/40-old-funeral-migration.md) (D-F1~D-F3)
  옛 JSP 시스템(`C:\down\funeralfr_oldsrc`)과 그 DB(`funeralfr.jsini.co.kr:15432`,
  **조회만 한다**)를 읽어 비어 있던 화면 여덟과 그 백엔드를 채웠다.
  **옛 데이터는 옮기지 않았다** — 옛 `t_goin` 10,384행은 이름이 거의 다 "자동생성"이고
  확장 칸(봉안·종교·부고·주소·사망장소 등)은 하나도 채워져 있지 않다.
  남은 것은 과금 단가를 어디에 둘지(D-F1)와 vben 개인 설정과 겹치는 항목 정리(D-F3).
- GHUB(생활과환경) 이식에서 남은 것: [docs/analysis/38-ghub-migration.md](docs/analysis/38-ghub-migration.md) (D-G1)
  기상 이벤트·생일 메시지의 **알림 발송은 이식하지 않았다** — NotificationServer 연동
  (카카오 알림톡 채널 추가 포함)이 결정 대기다. 판정·기록은 돌고 있다.
  **"누가 받나" 는 답이 생겼다**(2026-09-02) — 내 알림 설정(`/system/push/setting`)의
  날씨 스위치가 `scom.notification_preferences` 에 남는다
  ([29번 문서 8절](docs/analysis/29-notification-server.md)). 남은 것은 발송 자체다.
- 응답 봉투 통일: [docs/analysis/45-api-envelope-unification.md](docs/analysis/45-api-envelope-unification.md) (D-A1~D-A3)
  게이트웨이를 지나 오는 응답은 모양이 같은데(`{ data: { result, page } }`) 프론트에서
  60곳 넘게 손으로 벗기고 있었다. 기준을 `api/envelope.ts` 한 곳으로 모았고
  그리드 어댑터도 배열·봉투를 모두 받게 했다(준수사항 7번).
  남은 것은 **백엔드가 단일 객체를 배열로 감싸는 것을 그대로 둘지**(D-A1) —
  이것이 뿌리라서, 고치면 프론트에서 고를 것이 없어지지만 와이어 포맷이 바뀐다.
- i18n 콘솔 경고와 언어 코드 정리: [docs/analysis/18-i18n-fallback-warning.md](docs/analysis/18-i18n-fallback-warning.md)
- vben-admin 상위 동기화에서 남은 것: [docs/analysis/17-vben-upstream-sync.md](docs/analysis/17-vben-upstream-sync.md)
  (D-U1·D-U2 는 완료. 남은 것은 6.6 `componentProps` 타입 표 · 6.10 vxe 경고 · D-U4~U6)
- 회사 소개 사이트와 브랜드: [docs/analysis/27-jsini-site-brand.md](docs/analysis/27-jsini-site-brand.md)
  D-S1~D-S6 은 결정됐고 브랜드 키트 · SiteServer · 사이트 스켈레톤까지 세웠다.
  로고 · 모티프 · 사용 규칙은 [docs/brand/](docs/brand/) 에 있다 (SVG 를 손으로 고치지 말고
  `python docs/brand/generate.py`).
  **문구는 2026-08-29 에 실제 내용으로 채웠다**(`docs/sql/site_content.sql`, D-S8·D-S9).
  첫 화면이 "만들고, 계속 함께 간다" 이고, 운영 중인 시스템 다섯을 `/ko/work` 에 실었다.
  **사례는 고객사 이름 없이 분야로만 적는다**(D-S11) — 레퍼런스 공개는 동의가 필요하다.
  아직 남은 것은 **개인정보 동의 문구(D-S7) 하나**다. 법률 검토 전이라 이것 없이는
  문의 폼을 공개할 수 없다.
  파일 익명 접근 구멍은 닫았다 — 쓰기는 인증, 읽기는 `is_public` 판정이다.
  로그인이 심는 `jsini_file_at` 쿠키가 있어야 `<img src>` 가 통한다(27번 문서 5절).
  쿠키가 없는 로그인 세션(잠금 전 로그인·쿠키 정리)은 게이트웨이가 인증 API 요청을 받을 때
  스스로 다시 심는다 — 사진이 안 보이면 아무 화면이나 한 번 여는 것으로 낫는다.

## vben-admin 은 갈라져 있다

`fronts/packages` · `fronts/internal` 은 **2026-08-23 에 상위 HEAD(2026-08-19, `e3369bd63`)로 맞췄다.**
우리 커스터마이즈와 한국어 주석은 그 위에 다시 올려 둔 상태다.
프레임워크를 고칠 때는 **상위에 이미 같은 수정이 있는지 먼저 본다.**

```bash
git clone https://github.com/vbenjs/vue-vben-admin.git /tmp/upstream
cd /tmp/upstream && git log e3369bd63..HEAD --oneline -- packages internal
```

무엇을 어떻게 가져왔는지, 무엇을 일부러 안 가져왔는지는
[17-vben-upstream-sync.md](docs/analysis/17-vben-upstream-sync.md) 에 있다.
**`apps/jsini-portal` 은 상위와 무관하게 우리 것이다.**

주의할 점 둘:
- 폼은 **zod 4 + TanStack Form** 이다. `required_error` 는 `error` 로, `refine` 의 두 번째 인자는
  함수를 받지 않는다. 스키마 콜백은 폼 값이 아니라 컨텍스트를 받는다(`ctx.rootValues`).
- 팝업 제네릭은 **팝업 데이터 타입**이다. `useVbenModal<Row>(...)` 로 주고 `getData()` 는 인자 없이 부른다.
