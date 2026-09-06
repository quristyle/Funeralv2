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

### 2. `/funeral/building/info` — 건물 관리 · **보완**

원본: `funeral/building/info/index.vue` (287줄)

**빠져 있던 것**

| 무엇 | 어떻게 됐나 |
|---|---|
| 약어(3자리) | 칸이 없었다. **서버가 필수로 받는다**(`BuildingCreateDto.Abbreviation` 에 `[Required]`) — 등록이 400 으로 떨어지고 있었다. 목록 칸과 편집 칸을 붙였다 |
| 건물 전경 사진 (다중) | 올리는 자리가 없었다. 서버는 그동안에도 `building_photo_group_id` 를 들고 있었다 |
| 주차장 안내 이미지 (다중) | 위와 같다 (`parking_photo_group_id`) |

**새로 만든 공용 부품 — `ImageGroup`.** 사진 묶음은 건물 말고도 여러 화면이 쓴다
(고인 가족사진 등). Blazor Common 에 두고 클라이언트(`FileGroupClient`)는
`JSini.Web.Http` 에 뒀다 — 부품이 공용이라 모듈이 등록하면 다른 모듈이 못 쓴다.
AI 대화 클라이언트를 `AddJSiniGateway` 에서 등록하는 것과 같은 이유다.

- 고르는 즉시 올라간다. **첫 장을 올릴 때 서버가 그룹 아이디를 발급**하기 때문이다 —
  저장할 때 올리는 흐름이면 「저장 → 발급 → 다시 저장」이 되어야 한다
- 미리 보기는 셸 중계 경로(`/files/{id}`)를 쓴다. FileServer 가 주는
  `/api/file/...` 는 포털(:5557)에서 404 다
- 대표 사진 지정은 부품에 있지만 건물 화면에서는 끈다(원본에 없다)

**의도적 차이 둘**

1. **회사 고르개를 두지 않았다.** 원본은 위에 `BizSelect(funeralCompany)` 가 있고
   회사별로 건물을 걸러 봤다. Blazor 는 **회사를 서버가 토큰에서 정한다** —
   화면이 회사를 넘기면 남의 회사에 건물을 만드는 길이 열린다. 장례식장 모듈에는
   회사 목록 API 자체가 아직 없다. 여러 회사를 오가는 운영자가 생기면 그때
   붙인다(남는 일에 적어 둔다).
2. **목록의 사진 칸은 썸네일이 아니라 장수다.** 원본은 칸 안에 사진을 가로로
   늘어놓았는데, DevExpress 그리드는 줄 높이가 고정이라 그 자리에서 사진이
   납작해진다. 보고 고치는 것은 편집 창에서 한다.

**함께 고친 곳**: `JSini.Web.Http/FileGroupClient.cs`(신규) ·
`ServiceCollectionExtensions`(등록) · `Components/Data/ImageGroup.razor`(신규) ·
`app.css`(`.jsini-imggroup*`) · `FuneralModels.cs`(`BuildingPhotos` · `ParkingPhotos`)

**확인.** 빌드·아키텍처 테스트 129건 통과. 사진 올리기는 **실제로 해 보지 않았다** —
운영 DB·운영 파일 저장소에 그대로 들어가기 때문이다.

### 3. `/funeral/building/floor` — 층 관리 · **그대로**

원본: `funeral/building/floor/index.vue` (231줄)

원본 기능(건물별 조회 · 등록 · 수정 · 삭제 · 층명/정렬/비고)이 모두 있다.
오히려 **층별 호실 수**와 「호실이 남아 있으면 못 지운다」가 Blazor 에서 늘었다.

**의도적 차이 — 셀 즉시 편집을 편집 폼으로 바꿨다.** 원본은 표의 칸을 눌러
그 자리에서 고치면 곧바로 저장했다(`edit-closed` → `updateFloor`). Blazor 는
화면 백여 개가 `CommGrd` 의 편집 폼 한 가지를 쓴다. 고칠 수 있는 값은 같고,
표에서 잘못 눌러 저장되는 일이 없다.

**회사 고르개**는 건물 관리와 같은 이유로 두지 않았다(2번 항목 참고).

### 4. `/funeral/building/room` — 호실 관리 · **보완**

원본: `funeral/building/room/index.vue` (308줄)

**빠져 있던 것 — 정렬 순서.** 목록에도 편집 칸에도 없었다. 서버는 그동안에도
주고받고 있었고(`RoomDto.SortOrder`) 현황판이 그 값으로 호실 줄을 세운다.
**이름으로 세우면 「101호」보다 「10호」가 앞에 온다**(글자 순서라 그렇다) —
포털에서 순서를 못 고치는 동안 현황판의 차례를 바로잡을 길이 없었다.
새 호실은 마지막 뒤에 붙인다(0 으로 두면 맨 앞에 끼어든다).

그 밖(층 필터 · 등록 · 수정 · 삭제 · 호실유형 공통코드 이름 표시 · 상태 딱지)은
모두 있었다. Blazor 쪽이 나은 자리도 있다 — 건물을 바꾸면 층 고르개를 비운다
(원본은 다른 건물의 층이 붙은 채로 남았다).

**함께 고친 곳**: `FuneralModels.cs`(`Room.SortOrder`)

### 5. `/funeral/building/device` — 기기 관리 · **보완**

원본: `funeral/building/device/` 15개 파일 (화면 + 서랍 3탭 + 컴포저블)

원본의 서랍 세 탭(**화면 표시** · **하드웨어** · **장비 정보**)은 Blazor 에도 모두
있다 — 속성 마흔 칸, 하드웨어 설정(음량·밝기·자동 전원·재시작 시각), 장비 정보.
원격 명령(켜기·끄기·재시작)에 **새 판 확인**까지 Blazor 가 더 갖고 있다.

**빠져 있던 것 — 실시간 상태.** 장비가 붙고 떨어질 때 `DeviceHub` 가
`DeviceStatusChanged` 를 방송하는데 **아무도 듣지 않았다.** 목록을 읽은 시점의
상태가 그대로 굳어, 실제로는 켜져 있는 장비가 꺼짐으로 남았다. CLAUDE.md 의
남은 일에도 적혀 있던 항목이다.

**어떻게 붙였나 — 연결은 하나다(`DeviceStatusRelay`, 싱글턴).**
원본은 **브라우저마다** 허브에 붙었다(관리자 열 명이면 연결 열 개). 포털은
서버라서 한 번만 붙고 열려 있는 화면들에 나눠 줄 수 있다. 허브가 인증 없이
받으므로(플레이어가 붙는 곳이다) 싱글턴이어도 신원이 섞이지 않는다.

- 첫 구독자가 생길 때 붙고, 마지막이 나가도 끊지 않는다 — 화면을 들락거릴
  때마다 붙었다 끊으면 그 사이 방송을 놓친다
- 못 붙어도 예외를 올리지 않는다. 상태는 조회한 시점 값으로 남고 자동
  재연결이 계속 시도한다
- **방송을 받아도 목록을 다시 읽지 않는다.** 원본은 0.5초로 묶어 재조회했는데,
  정전 복구처럼 한 건물이 동시에 켜지면 그 한 번이 수십 장비 조회가 된다.
  바뀐 것은 상태 한 칸이라 들고 있는 자료에서 그 칸만 고친다

**함께 고친 곳**: `Directory.Packages.props`·`csproj`(SignalR 클라이언트,
**장례식장 모듈만** 참조) · `FuneralModule`(싱글턴 등록)

**확인.** 빌드·테스트 통과. 실제 방송 수신은 못 봤다 — 빈소 장비가 붙어야 한다.
