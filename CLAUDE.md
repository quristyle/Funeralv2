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

## 구조

```
ApiGateway/            YARP 게이트웨이 (:5265) — JWT 검증 후 X-User-* 헤더를 붙인다
microservices/
  AuthServer/          인증 · 계정 · 메뉴 · 권한 · 공지 (포털의 중심)
  funeralv2Api/        장례식장
  HelpDeskServer/      헬프데스크 (이식)
  ProjMngServer/       프로젝트관리 (이식)
  FileServer/          파일 보관 — 첨부와 본문 이미지가 모두 여기로 간다
  AIAgentServer/
fronts/apps/jsini-portal/   프론트엔드 (Vue 3 + vben)
docs/analysis/         작업 기록과 결정 사항
docs/sql/              실행한 SQL (전부 반복 실행 안전)
```

## 알아 두면 시간을 아끼는 것

- **메뉴는 백엔드 주도다.** `scom.system_menus` 에 없거나 `status = 0` 이면 라우트 자체가 생기지 않는다.
- **DB 는 셋이다.** 포털 `funeralv2`(scom) · 헬프데스크 `jinrecept`(jsini) · 프로젝트관리 `jsini`(projmng).
  접속 문자열은 각 서비스의 `appsettings.Local.json` (git 제외).
- **개발 중에는 `dotnet watch` 로 6개가 떠 있는 경우가 많다.** `.cs` 를 고치면 자동 재기동하지만
  `appsettings.json` 변경만으로는 재기동하지 않는다.
- 개발 환경은 `Auth:SkipPasswordCheck=true` 라 아이디만으로 로그인된다.
- 검증: `dotnet build jsini.sln` · `pnpm vite build` · `./scripts/smoke-test.sh`

## 결정이 필요한 것

[docs/analysis/12-decisions-pending.md](docs/analysis/12-decisions-pending.md) 에 모아 둔다.
자율로 진행하기에 영향이 크거나 되돌리기 어려운 일은 여기에 적고 손대지 않는다.

- 준수사항 점검에서 남은 것: [docs/analysis/16-준수사항-점검.md](docs/analysis/16-준수사항-점검.md) (R1~R4)
- vben-admin 상위 동기화에서 남은 것: [docs/analysis/17-vben-upstream-sync.md](docs/analysis/17-vben-upstream-sync.md)
  (D-U1·D-U2 는 완료. 남은 것은 6.6 `componentProps` 타입 표 · 6.10 vxe 경고 · D-U4~U6)

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
