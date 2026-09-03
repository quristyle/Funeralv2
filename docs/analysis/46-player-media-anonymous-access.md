# 플레이어 영정사진이 안 보인 이유 — 익명 파일 읽기

작성: 2026-09-03
대상: `microservices/FileServer/Endpoints/PublicFileAccessFilter.cs` ·
`microservices/FileServer/appsettings.json` (파일 접근) ·
`microservices/funeralv2Api/DTOs/AnonymousDisplayProjection.cs` ·
`funeralv2Api/Endpoints/{Deceased,Device}Endpoints.cs` (D-M2) ·
`ApiGateway/Program.cs` · `ApiGateway/appsettings.json` (D-M3)

> 신고: "`/building/device` 에서 JSI-06-0001 장비의 속성을 변경했고 영정사진이 표출될 것을
> 기대했는데, 화면은 까만 배경에 성명만 보인다. 윈도우용 다운로드 파일을 실행했다."

**장비 설정도, 사진 파일도 정상이었다.** 막힌 곳은 파일 읽기 권한이다.

---

## 1. 원인 사슬

하나씩 짚어 보고 마지막에서 걸렸다.

| 확인한 것 | 결과 |
|---|---|
| `/api/funeral/building/device/code/JSI-06-0001` | `FUNERAL_PORTRAIT` · `isMemorialPhotoEnabled=true` · `isBackgroundImageEnabled=true` — **설정은 맞다** |
| 고인(홍길동) 조회 | `memorialPhotoUrl` · `memorialEditedPhotoUrl` 둘 다 채워져 있다 |
| 운영 디스크 | `/srv/jsini/files/funeralv2/deceased/Original/3a003d12….webp` 72,602바이트 **있다** |
| FileServer 컨테이너 안 | `/data/files/…` 같은 파일이 **보인다** (마운트 정상) |
| DB 메타데이터 | 행 있음, `isdeleted=false`, 크기 일치 |
| **인증 없이 내려받기** | **HTTP 404** `ERR_FILE_NOT_FOUND` ← 여기 |
| `Files:RequirePublicFlagForAnonymous` | `true` (운영 컨테이너에서 확인) |
| `scom.filemetadatas` 의 `ispublic` | **174개 전부 false. 공개 0건** |

`PublicFileAccessFilter` 가 익명 요청에 `is_public` 이 켜진 파일만 내려준다.
그런데 켜진 파일이 하나도 없었다. 그래서 **플레이어가 부르는 모든 미디어가 404** 다 —
영정만이 아니라 배경 · 리본 · 영상 · 음원 전부.

### 왜 이름만 보였나

이름은 파일이 아니라 **API 응답(JSON)** 에서 온다. 그 라우트는 익명 허용이라 통과한다.
사진과 배경은 파일 경로라 막힌다. 그래서 "까만 화면 + 성명" 이 된다.

깨진 이미지 표시조차 없는 이유도 코드에 있다.

```dart
// cache_manager.dart — 200 이 아니면 아무것도 저장하지 않고 조용히 끝난다
if (response.statusCode == 200) { ... }

// portrait_controller.dart
String? get deceasedPhotoPath => localPhotoPath;   // 캐시에 없으면 null

// portrait_view.dart
if (!dev.isMemorialPhotoEnabled || _controller.deceasedPhotoPath == null) {
  return const SizedBox();     // 레이어를 아예 그리지 않는다
}
```

내려받기가 실패하면 캐시가 비고, 캐시가 비면 사진 레이어를 그리지 않는다.
**로그도 남지 않는다** — 그래서 화면만 보고는 원인을 알 수 없었다.

## 2. 왜 이렇게 됐나

27번 문서 5절(R-S2)에서 **파일 아이디만 알면 누구나 남의 첨부를 내려받던 구멍**을 닫았다.
그때 "브라우저는 `<img src>` 에 Authorization 헤더를 붙이지 않는다" 는 문제를
로그인 때 심는 `jsini_file_at` 쿠키로 풀었다.

**그 해법은 브라우저만의 것이었다.** 빈소의 사이니지 플레이어는

- 브라우저가 아니라 Flutter 앱이고 (쿠키를 심어 줄 로그인 과정이 없다)
- 장비 코드만 들고 **익명으로** 부른다 (게이트웨이에 `*-anonymous-route` 가 따로 있다)

그래서 판정을 켠 순간 플레이어는 미디어를 하나도 받을 수 없게 되었다.
**운영만의 문제가 아니다** — 설정이 추적 파일(`appsettings.json`)에 있으므로 로컬
개발 서버에 붙여도 똑같이 안 보인다.

## 3. 고친 방법 — 표출 영역만 익명 허용

> 지시: 선택지 넷 중 **"표출 영역만 익명 허용"**.

`Files:AnonymousReadablePaths` 를 새로 두었다. 저장 경로 앞머리가 이 목록에 걸리면
익명도 읽는다. 기본값은 `funeralv2/` 하나다.

```json
"Files": {
  "RequirePublicFlagForAnonymous": true,
  "AnonymousReadablePaths": [ "funeralv2/" ]
}
```

`is_public` 판정은 그대로 남는다 — 둘 중 하나만 맞으면 통과다.

### 왜 `funeralv2/` 한 줄인가

저장 경로를 전수 조사했다. 영역이 성격으로 갈려 있었다.

| 영역 | 개수 | 무엇인가 | 익명 |
|---|---|---|---|
| `funeralv2/deceased/` | 32 | 영정사진 (원본·보정) | 허용 |
| `funeralv2/decoration/` | 18 | 근조 리본 장식 | 허용 |
| `funeralv2/Video/` · `Audio/` | 35 · 34 | 추모 영상 · 추모곡 | 허용 |
| `funeralv2/background/` · `building/` · `Original/` | 2 · 3 · 2 | 제단 배경 · 건물 사진 · 리본/로고 | 허용 |
| `notice/` · `qna/` · `helpdesk-request/` · `profile/` | 48 | 공지 첨부 · Q&A 첨부 · 헬프데스크 첨부 · 아바타 | **차단** |

`funeralv2/` 아래는 **빈소 화면에 띄우려고 올린 것**뿐이고, 실제로 이미지·영상·음원 외
파일이 하나도 없다(확인했다). 차단해야 할 것들은 최상위 영역이 다르다 —
이 판정을 만든 이유가 바로 그것들이었고, 그대로 막힌다.

영역을 하나하나 적는 대신 `funeralv2/` 로 묶은 이유는, 빈소 화면에 새 종류의 미디어가
생길 때마다 **같은 버그가 조용히 재발**하기 때문이다(이번 일이 그것이다).

> **비밀 문서를 `funeralv2/` 아래에 두면 안 된다.** 계약서 같은 것이 생기면 다른 영역에
> 올리거나 이 목록에서 뺀다. 설정이라 코드를 고치지 않고 바꿀 수 있다.

경로에 `..` 가 들어 있으면 앞머리 비교 전에 거부한다. 지금은 업로드가 경로를 직접
만들어서 그런 값이 들어올 수 없지만, `funeralv2/../notice/x` 같은 값에 속지 않게 해 둔다.

## 4. 확인한 것

새 빌드를 **다른 포트(5399)** 로 띄워 돌고 있는 개발 서버를 건드리지 않고 대조했다.
서비스 자체의 라우트는 `/download/{id}` 다 — `/api/file` 은 게이트웨이가 떼는 접두사다.

| 파일 | 고치기 전 (5350) | 고친 뒤 (5399) |
|---|---|---|
| 영정 보정본 `funeralv2/deceased/…webp` | 404 | **200 image/webp 72,602B** |
| 영정 원본 `funeralv2/deceased/…png` | 404 | **200 image/png 1,149,363B** |
| 제단 배경 `funeralv2/background/…jpg` | 404 | **200 image/jpeg 213,513B** |
| 아바타 `profile/…jpg` | 404 | **404 (그대로 차단)** |

```
dotnet build microservices/FileServer/FileServer.csproj -c Release   오류 0
appsettings.json                                                    JSON 파싱 확인
```

## 5. 운영에 반영하려면

**아직 운영은 그대로다.** FileServer 는 GHCR 이미지로 도는 컨테이너이고, 배포는
main push → GitHub Actions → 러너가 pull·up 이다(39번 문서). 그래서

- **운영**: 이 변경이 main 에 올라가 배포되면 적용된다. 설정이 추적 파일에 있어
  `/srv/jsini/config/FileServer/appsettings.Local.json` 은 손댈 필요가 없다.
- **로컬**: 돌고 있는 FileServer 를 다시 띄워야 한다 — `dev.bat file`.

현장 플레이어는 **갱신하지 않아도 된다.** 서버가 200 을 주면 그 다음은 이미 있는
캐시 로직이 알아서 받는다. v1.0.0 그대로 동작한다.

## 6. 함께 확인된 것 — 장례 API 가 익명으로 열려 있다

> 지시: 이것도 지금 같이 본다.

게이트웨이에 플레이어용 익명 라우트가 여섯 개 있다. **의도해서 열어 둔 것**이다
(이름에 `-anonymous-route` 가 붙어 있다).

```
/api/funeral/building/device/code/{code}
/api/funeral/building/deceased/deviceCode/{deviceCode}
/api/funeral/building/deceased/guide/deviceCode/{deviceCode}
/api/funeral/building/deceased/kiosk/deviceCode/{deviceCode}
/api/funeral/building/source/{id}
/api/funeral/hubs/device/{**remainder}
```

문제는 **열쇠가 장비 코드 하나이고 그것이 추측 가능하다**는 점이다. `JSI-06-0001` 은
회사코드-건물-순번 꼴이라 몇 번만 시도하면 맞는다. 맞히면 인터넷에서 그대로 나온다 —
고인 성명 · 성별 · 나이 · 사망/발인/안치 일시 · 빈소 · 상주 이름과 관계 · 사진 URL,
그리고 응답 스키마에는 `ssn` · `causeOfDeath` · 상주 `contact` · `email` · `address` ·
계약자 정보 칸까지 있다.

**지금 당장의 피해는 작다.** 실제 자료가 고인 2건뿐이고 `ssn` 은 한 건도 채워져 있지
않다(확인했다). 그러나 현장이 늘면 그대로 커진다. 이번 파일 변경은 이 노출을
**넓히지 않는다** — 사진 URL 은 이미 이 익명 API 가 알려 주고 있었다.

### 결정이 필요하다

| | 무엇 | 비용 | 상태 |
|---|---|---|---|
| D-M1 | **장비 코드를 추측 못 하게 한다** — 장비마다 임의 비밀(등록 토큰)을 발급하고 플레이어가 그것을 함께 보낸다 | 백엔드 + 플레이어 + 현장 재설정 | 결정 대기. 가장 확실하지만 현장 장비를 한 번 손대야 한다 |
| D-M2 | **응답에서 필요 없는 칸을 뺀다** | 백엔드만 | **✅ 했다 (7절)** |
| D-M3 | **익명 라우트에 요청 제한을 둔다** — 장비 코드 대입 시도를 IP 당 속도 제한 | 게이트웨이 설정 | **✅ 했다 (8절)** |
| D-M4 | 그대로 둔다 (내부망 전제) | 0 | 지금 `portal.jsini.co.kr` 로 공개돼 있어 전제가 성립하지 않는다 |

권장 순서는 **D-M2 → D-M3 → D-M1** 이었고 D-M2 를 먼저 했다.
D-M1 은 현장 장비를 손대는 일이라 플레이어 업그레이드 경로(48번 문서)가 정해진 뒤에
함께 하는 편이 낫다.

## 7. D-M2 — 익명 응답에서 필요 없는 칸을 뺐다

`DTOs/AnonymousDisplayProjection.cs` 를 새로 두고 익명 엔드포인트 넷에만 걸었다.

| 뺀 것 | 어디서 |
|---|---|
| `ssn` · `causeOfDeath` · `burialPlot` · `remark` | 고인 |
| `contractor` (이름·연락처·주소·서명) · `manager` (장례지도사·직원 연락처) | 고인 |
| `facilities` · `rooms` · `familyPhotoGroupId` | 고인 (표출에 쓰지 않는다) |
| 상주의 `contact` · `email` · `address` | 상주 |
| `ipAddress` · `macAddress` · `publicIpAddress` | 장비 |

남긴 것: 이름 · 성별 · 나이 · 종교 · 일시 · 호실 · 상태 · 대표상주 · 영정/가족 사진 ·
상주 이름·관계·대표여부 · 리본 · 텍스트 오버레이. **플레이어가 실제로 읽는 칸 전부**다
(`device_models.dart` 의 파서를 기준으로 골랐다).

### 고인은 허용 목록, 장비는 차단 목록으로 짰다

방향이 반대인 이유가 있다.

- **고인**: 필요한 칸이 스무 개인데 민감한 칸이 그만큼 많다. 새 칸이 DTO 에 생겼을 때
  **가만히 있으면 새지 않는** 쪽이 맞다 → 남길 것만 새 객체로 복사한다.
- **장비**: 표출 설정 칸이 **54개**이고 민감한 것은 셋뿐이다. 허용 목록으로 짜면
  새 표출 설정이 생길 때마다 **플레이어에서 조용히 빠진다** → 지울 셋만 지운다.

고인 쪽은 **원본을 고치지 않고 새 객체로 복사**한다. 지금은 서비스가 요청마다 새 DTO 를
만들지만(`DeceasedService` 의 `IMemoryCache` 는 FAM_TYPE 이름표만 담는다),
나중에 응답을 캐시하게 되면 제자리 수정은 **인증된 화면의 응답까지 깎는다.**

인증이 필요한 화면은 이 투영을 지나지 않는다. 포털의 고인 상세는
`/{id}/detail`, 장비 관리는 `/{id}` 라 경로가 다르고, 포털이 익명 경로를 부르지
않는 것도 확인했다.

### 확인한 것

새 빌드를 **다른 포트(5398)** 로 띄워 돌고 있는 개발 서버(5320)와 나란히 대조했다.

```
building/deceased/deviceCode/JSI-06-0001   ssn·burialPlot·remark·contractor·manager·
                                           rooms·familyPhotoGroupId → 전부 빠짐
                                           상주 연락처·이메일·주소 → 빠짐
                                           이름·관계·대표여부·사진 URL → 남음
building/device/code/JSI-06-0001           ip·mac·공인IP → null
                                           칸 수 54 → 54, **그 밖에 달라진 칸 없음**
building/deceased/guide/deviceCode/…       호실 칸(roomId·roomName·floorName·sortOrder)
                                           그대로, 안에 든 고인만 축약
building/deceased/kiosk/deviceCode/…       rooms 7 · buildingPhotos 1 · parkingPhotos 2
                                           그대로, 고인만 축약
dotnet build funeralv2Api -c Release       오류 0
```

**플레이어 파서로 실제 응답을 읽혀 봤다.** 축약된 JSON 넷을 `device_models.dart` 의
`DeceasedDto` · `DeviceDto` · `EntranceGuideRoomDto` · `KioskGuideResponseDto` 에
그대로 넣어 파싱했고 넷 다 통과했다 — 고인 홍길동/M/80, 상주 2명, 가족사진 16장,
장비 `FUNERAL_PORTRAIT` 영정=true 배경=true, 안내 호실 2개, 키오스크 호실 7개.
**현장 플레이어를 갱신하지 않아도 된다.**

### 반영

funeralv2Api 도 컨테이너다. main 에 올라가 배포되면 적용된다.
로컬은 `dev.bat funeral` 로 다시 띄운다.

## 8. D-M3 — 익명 읽기 경로에 요청 제한을 걸었다

게이트웨이에 이미 있던 방식을 그대로 따랐다 — `AddRateLimiter` 로 정책을 만들고
라우트에서 `RateLimiterPolicy` 로 지정한다(로그인 `auth-attempts`,
소개 사이트 익명 쓰기 `public-write` 와 같은 자리).

```
정책 player-read : IP 당 1분에 900회 (고정 창, 대기열 없음)
걸린 라우트 다섯 : device/code/{code} · deceased/deviceCode/{code} ·
                   deceased/guide/… · deceased/kiosk/… · building/source/{id}
걸지 않은 것     : SignalR 허브 (/api/funeral/hubs/device/**)
```

### 900 이라는 값은 실측에서 나왔다

**리미터 칸은 장비별이 아니라 장례식장별이다.** 한 건물의 화면이 NAT 뒤에서 공인 IP
하나를 공유하기 때문이다. DB 를 세어 보니 **장비 10대 중 8대가 IP 하나**
(`175.122.77.152`)를 쓰고 있었다. 그래서 "장비 한 대가 얼마나 부르나" 가 아니라
"한 건물이 동시에 얼마나 부르나" 로 잡아야 한다.

평상시 통행량은 **0 에 가깝다.** 설정 변경은 SignalR 로 밀어 주고, REST 는 장비가
뜰 때와 설정이 바뀔 때만 부른다(실패 시 재시도도 20초 간격이다 —
`device_dispatcher.dart` 의 1초 타이머는 화면에 남은 시간을 세는 것이다).

문제가 되는 순간은 **정전 복구처럼 건물의 화면이 동시에 켜지는 때**다.
장비 하나가 뜰 때 3~15회를 부르므로(장비 · 고인 · 안내/키오스크 · 리본별 미디어),
화면 40개면 한 창에 600회까지 몰린다. 900 은 그 1.5배다.

다섯 라우트가 **한 칸을 함께 쓴다**(정책이 하나라서). 그래서 900 은 경로별이 아니라
그 IP 의 총합이다 — 위 계산도 총합으로 했다.

### 이것이 막는 것과 막지 못하는 것

- 막는 것: **지속적으로 긁어 가기**와 그로 인한 서비스 마비.
- **막지 못하는 것: 코드 대입 자체다.** 900/분이면 네 자리 코드 공간을 10여 분에 훑는다.
  추측 자체를 막는 것은 D-M1(장비별 임의 비밀)이고, 이것은 그 전까지의 완화책이다.

더 조이는 것은 위험 쪽이 크다고 봤다. 100~200 으로 낮추면 정전 복구 때 화면 일부가
429 를 받고, 그 장비는 20초 뒤에나 다시 시도한다 — 빈소 화면이 몇 분간 비어 있게 된다.
**더 나은 형태는 '빗나간 요청'만 세는 것**이다(없는 장비 코드로 온 요청). 정상 장비는
언제나 맞히므로 영향이 없고 대입만 걸린다. 다만 게이트웨이에 상태를 들고 있는
미들웨어를 새로 얹어야 해서, 표준 리미터로 먼저 깔았다. D-M1 을 하게 되면 필요 없어진다.

SignalR 허브에 걸지 않은 이유는 하나다 — 재연결이 몰릴 때 negotiate 가 거부되면
밀어 주기 통로가 끊겨서, **화면이 옛 내용을 계속 띄운 채 아무도 모르는 상태**가 된다.

### 확인한 것

한도를 **임시로 5 로 낮춰** 배선이 맞는지부터 봤다(900 을 시험하려면 프록시를 통해
운영 DB 로 900번을 쏴야 한다).

```
읽기 경로 8회 연속        200 200 200 200 200 → 429 429 429   (6회째부터 막힌다)
429 본문                  {"success":false,"code":"429","message":"시도가 너무 잦습니다…"}
다섯 경로가 한 칸인지      고인 · 안내 · 키오스크 · 미디어 경로도 즉시 429
SignalR 허브 negotiate     8회 전부 200 (제한 없음 — 의도대로)
dotnet build ApiGateway    경고 0 · 오류 0
```

시험은 **다른 포트(5397)** 의 별도 인스턴스로 했고, 돌고 있던 게이트웨이(5265)는
그대로 200 이다. 임시로 낮춘 한도는 **900 으로 되돌리고 다시 빌드했다.**

### 설정을 Routes 안에 주석 키로 넣지 않았다

12번 문서에 남은 사고가 그것이다 — `Routes` 아래에 `"//rate-limit"` 같은 **키**를
넣으면 YARP 가 그것도 라우트로 읽어 **게이트웨이가 기동에 실패한다.**
설명은 `ReverseProxy` 블록 **위**에 `//` 줄 주석으로, 계산 근거는 `Program.cs` 의
정책 주석에 두었다.

## 8-1. 운영 반영 결과 (2026-09-03)

5절에서 "아직 운영은 그대로다" 라고 했던 것이 반영됐다.

```
282e658  파일 접근(FileServer) + 장비 응답 축약 + 요청 제한   deploy-backend 성공
0c6a480  장비 인증 키 기반(게이트웨이, 기본 꺼짐)              deploy-backend 성공
운영 검증  영정 파일 200 · 아바타 404 · 익명 장비 응답 ip/mac null ·
          토큰 기본꺼짐으로 익명 조회 200 (변화 없음) · 포털 200
```

**고인 쪽 응답 축약(D-M2 의 절반)은 아직 운영에 없다** — DeceasedEndpoints.cs 가
빈소현황 개편(47번)의 미커밋 변경과 같은 파일이라, 그 커밋에 실려 나간다
(`.ToAnonymousDisplay()` 호출과 투영 클래스는 이미 준비돼 있다).

## 9. D-M1 기반 — 장비 인증 키 (2026-09-03 · 기본 꺼짐)

> 지시(자율 진행): "남은 모든 작업을 진행해줘." D-M1 은 켜는 순간 현장 장비가 전부
> 갱신돼 있어야 하는 결정 항목이라, **이 저장소의 관례대로 기본 꺼짐 스위치**로
> 기반만 만들어 두었다(19번 문서의 스위치 둘·배포 진행 보고와 같은 방식).
> **켜는 것은 결정으로 남는다.**

### 무엇이 생겼나

| 쪽 | 무엇 | 기본 상태 |
|---|---|---|
| 게이트웨이 | `PlayerAuth:RequireDeviceToken` — 켜면 익명 표출 경로 여섯이 `X-Device-Token` 헤더(웹소켓은 `?deviceToken=`)를 요구 | **꺼짐** |
| 게이트웨이 | `PlayerAuth:DeviceTokens` — 허용 토큰 목록. **비밀이라 Local.json 에만 둔다** | 비어 있음 |
| 플레이어 | 환경 설정에 **장비 인증 키 (선택)** 입력 칸. `DeviceAuth` 가 저장하고 ApiService(5곳)·SignalR 허브 주소에 싣는다 | 비어 있음 |

키가 비어 있으면 플레이어는 **아무것도 싣지 않고**, 서버 검증이 꺼져 있으면
게이트웨이는 **아무것도 보지 않는다** — 양쪽 다 지금과 완전히 같다.
막을 때는 401 이 아니라 **404** 다(파일 필터와 같은 이유 — 장비 코드의 존재를 확인해 주지 않는다).
로그인 사용자(JWT)는 토큰 없이 통과한다.

reloadOnChange 덕에 Local.json 만 고치면 **게이트웨이 재기동 없이** 켜고 끌 수 있다.

### 왜 장비별 DB 토큰이 아니라 공유 목록인가

최종 모습은 `devices` 테이블의 장비별 토큰이 맞다. 그런데 funeralv2 스키마는
빈소현황 개편(47번)이 한창이라 **지금 마이그레이션을 만들면 그쪽의 엔티티 변경
(FuneralNotice 삭제·DeceasedStatus 신설)이 내 마이그레이션에 섞여 나온다.**
공유 목록은 게이트웨이 설정만으로 되고, **플레이어 쪽 동작은 개별 토큰이 되어도
그대로다** — 서버 판정만 목록 대조에서 DB 대조로 바뀐다. 47번 작업이 커밋된 뒤
장비별 토큰(+ 포털 장비 화면의 재발급 단추)으로 올리면 된다.

### 켜는 순서 — 순서가 틀리면 현장 화면이 꺼진다

```
1. 플레이어 v1.0.1 이상을 현장 전 장비에 설치
2. 각 장비의 환경 설정에 장비 인증 키 입력 (키 생성: openssl rand -base64 24)
3. 전 장비 입력 확인 후, 게이트웨이 appsettings.Local.json 에
     "PlayerAuth": { "RequireDeviceToken": true, "DeviceTokens": [ "<키>" ] }
4. 화면이 계속 나오는지 확인 (SignalR 재연결 포함)
```

### 확인한 것

```
게이트웨이(별도 포트, 켬)   토큰 없음/틀림 → 404 · 맞는 헤더 → 200 · 맞는 쿼리 → 200 ·
                           무관 경로(/health) → 200
게이트웨이(기본값=끔)       토큰 없이 200 — 지금과 동일
flutter test               25개 전부 통과 (DeviceAuth 4개 신규 — 키 없으면 헤더·주소
                           불변, 저장·삭제, 쿼리 인코딩)
flutter build windows      성공 · dotnet build ApiGateway 오류 0
```
