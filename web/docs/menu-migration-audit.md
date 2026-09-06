# 메뉴별 이관 점검 이력

Vue 원본(2026-09-05 에 저장소에서 걷어냄)과 Blazor 화면을 **메뉴 하나씩** 맞대어 보고,
빠진 기능을 채우는 작업의 기록이다. 시작 2026-09-06.

원본을 꺼내는 법:

```
git show 009c2835^:fronts/apps/jsini-portal/src/views/<경로>.vue
```

경로 대응표는 [menu-route-map.md](menu-route-map.md) 에 있다(179건 전수, Vue component 컬럼).

## 판정 기준

| 판정 | 뜻 |
|---|---|
| **그대로** | 원본 기능이 다 있다. 손대지 않았다 |
| **보완** | 빠진 기능을 채웠다 |
| **다시 씀** | 원본과 다른 화면이었다. 새로 옮겼다 |
| **의도적 차이** | 원본과 다르지만 그게 맞다고 판단했다. 까닭을 적는다 |

## 진행 규칙

- 메뉴 하나가 끝나면 빌드·아키텍처 테스트를 돌리고 커밋·푸시한다.
- 결정이 필요한 자리는 멈추지 않고 **여기 적고** 정한 대로 간다.
- 운영 DB 를 바꾸는 동작(저장·삭제·이동)은 **실행해 보지 않는다** —
  이 개발 장비의 AuthServer 가 운영 DB 를 직접 본다.

---

## 이력

### 1. `/funeral/player-download` — 플레이어 다운로드 · **다시 씀**

원본: `funeral/player-download/index.vue` (360줄)

**무엇이 달랐나.** 원본은 GitHub Releases 의 설치 파일을 **OS 별 카드 일곱 장**으로
보여 주는 화면인데, Blazor 는 자료실(`auth/help/archives`)의 「플레이어」 분류를
표로 보여 주고 있었다. 자료실에는 아무도 올리지 않고 릴리스 워크플로가 만든 파일은
GitHub 에만 있으므로 **화면이 늘 비어 있었다.** 자료실 화면의 뼈대를 복사해 옮긴
흔적으로 보인다. 짝이 되는 포털관리의 「플레이어 릴리스」가 GitHub 태그를 만드는
화면이라는 점에서도 원본 쪽이 맞다.

**채운 것**

- OS 별 카드 일곱(Windows · 라즈베리파이 · Ubuntu x64 · Ubuntu arm64 ·
  Android TV · 수동 arm64 · 수동 x64), 요구 사항·설치 명령·파일 크기
- 자산 짝짓기를 **배포판과 아키텍처까지** 본다 (`_debian13_` · `_ubuntu24_`).
  `.deb` 로만 고르면 라즈베리파이 카드가 Ubuntu 파일을 집어 온다
- 최신 태그·배포 시각, 「전체 버전 보기」(GitHub Releases 새 창), 새로 고침
- 릴리스가 없을 때/GitHub 이 응답하지 않을 때의 안내

**결정 — GitHub 을 브라우저가 아니라 서버가 부른다.** 원본은 화면에서
`api.github.com` 을 직접 불렀다. AuthServer 에 `GET /system/player-release/latest`
를 새로 두고 거기서 부른다. 토큰이 이미 그 서비스에 있어 저장소가 비공개로 바뀌어도
읽히고, 익명 호출의 시간당 60회 제한(사무실이 한 아이피다)에 걸리지 않는다.
**내려받기 자체는 브라우저가 GitHub 에서 바로 한다** — 파일이 수백 MB 라 포털이
중계하면 그동안 회로가 묶인다.

**함께 고친 곳**

- `AuthServer/DTOs/PlayerReleaseDto.cs` — `PlayerReleaseLatestDto` · `PlayerReleaseAssetDto`
- `AuthServer/Services/PlayerReleaseService.cs` — `GetLatestAsync`
- `AuthServer/Endpoints/PlayerReleaseEndpoints.cs` — `GET /latest`
- `JSini.Web.Funeral/Api/HelpApi.cs` — `GetPlayerLatestAsync` · `PlayerLatest` · `PlayerAsset`
- `funeral.css` — `.fn-pd*` 카드

**확인.** `dotnet build`(web · AuthServer) · 아키텍처 테스트 129건 통과.
화면 확인은 못 했다 — 이 장비에서 GitHub 토큰 설정 여부를 모른다. 설정이 없으면
카드는 다 「파일 없음」으로 뜨고 안내가 붙는다(그 자체가 정상 동작이다).
