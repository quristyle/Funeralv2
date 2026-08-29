# 결정이 필요한 항목

작성: 2026-08-22 (자율 진행 중 보류한 것들)

자율로 진행하기에는 영향이 크거나, 되돌리기 어렵거나, 살아 있는 다른 시스템에
영향을 주는 일들이다. 각 항목은 **① 지금 상태 ② 왜 문제인가 ③ 선택지 ④ 내 의견**
순서로 적었다. 고르신 대로 바로 진행할 수 있게 준비해 두었다.

상세 배경은 [11-msa-improvement-backlog.md](11-msa-improvement-backlog.md) 에 있다.

---

## D1. JWT 서명 키가 저장소에 평문으로 있다 ✅ **해결됨 (B안 + 키 교체)**

### ✅ 해결 완료 (2026-08-26)

지시: **B안 + 키는 교체 + 이 PC 가 유일한 환경.**

**⚠ 서비스를 재기동해야 반영됩니다. 모든 사용자가 다시 로그인해야 합니다.**
지금 돌고 있는 서비스들은 옛 키를 메모리에 들고 있어 **서로 일관되게 동작 중**입니다 —
재기동 전까지는 아무것도 깨지지 않습니다.

#### 1. 키를 저장소 밖으로 옮기고 새로 만들었습니다

새 키(48바이트 base64)를 각 서비스의 `appsettings.Local.json`(git 제외)에 넣었습니다.
**공용 키가 섹션 이름 셋으로 갈려 있었습니다** — 이것부터 찾아야 했습니다.

| 서비스 | 설정 경로 | 쓰임 |
|---|---|---|
| ApiGateway | `Jwt:Key` | 토큰 검증 |
| AuthServer | `JwtSettings:SecretKey` | **토큰 발급** |
| HelpDeskServer | `GatewayJwt:Key` | 게이트웨이 토큰 검증 |
| ProjMngServer | `Jwt:Key` | 토큰 검증 (게이트웨이 우회 대비) |

넷 모두 **같은 값**인 것을 sha256 으로 확인했습니다. 다르면 로그인이 안 됩니다.

헬프데스크 자체 로그인 키(`Jwt:Key` = `quristyle_blabbbbbla_...`)도 별도 새 값으로
옮겼습니다. `LocalLogin` 이 꺼져 있어 지금은 쓰이지 않지만 평문 비밀이었습니다.

#### 2. 하드코딩 폴백을 없앴습니다 — 이게 핵심입니다

**JSON 에서 키를 빼내는 것만으로는 아무 효과가 없었습니다.** 코드에 이런 폴백이
다섯 곳 있었기 때문입니다.

```csharp
var jwtKey = config["Jwt:Key"] ?? "a-very-secret-key-that-is-long-enough-for-security";
```

설정이 비어 있으면 **조용히 잘 알려진 키로 돌았습니다.** 이제 `JwtKeyGuard` 가
없거나·자리표시자거나·옛 평문 값이거나·32자 미만이면 **기동을 막습니다.**
조용히 취약하게 도는 것보다 뜨지 않는 편이 낫습니다.

추적 파일에는 `"__SET_IN_appsettings.Local.json__"` 자리표시자만 남았습니다.

> `JwtKeyGuard` 가 두 곳에 있습니다 — `JSini.Shared.Infrastructure`(서비스 3개용)와
> `ApiGateway/JwtKeyGuard.cs`(복사본). 게이트웨이는 공용 프로젝트를 하나도 참조하지
> 않아서(의존성을 얇게 둔 것으로 보임) 열 줄 때문에 그 구조를 바꾸지 않았습니다.
> **한쪽을 고치면 다른 쪽도 고쳐야 합니다.**

#### 3. 작업 중 버그 둘을 발견해 함께 고쳤습니다

**`AuthService.GenerateJwtToken` 이 만드는 토큰은 애초에 쓸 수 없는 것이었습니다.**
`Jwt:Key` 를 읽는데 **AuthServer 에는 `Jwt` 섹션이 없습니다**(이 서비스는 `JwtSettings`).
그래서 항상 폴백 `DefaultSecretKeyForDevelopmentOnly!` 로 서명했고,
`Issuer`·`Audience` 도 같은 없는 섹션에서 읽어 **null** 이었습니다.
게이트웨이는 셋 다 검증하므로 그 토큰은 통과할 수 없었습니다.
실제 로그인은 `AuthEndpoints` 가 처리해 눈에 띄지 않았던 것으로 보입니다.

**`GatewayJwt:Key` 가 비면 헬프데스크 자체 키로 조용히 대체됐습니다**
(`?? jwtKey`). 그러면 게이트웨이 토큰이 통째로 거부되는데 이유를 알기 어렵습니다.

#### 4. 알아 둘 것 — Local.json 이 환경변수를 덮습니다

각 서비스가 `AddJsonFile("appsettings.Local.json")` 을 **기본 소스 뒤에** 붙입니다.
설정 소스는 뒤에 오는 것이 이기므로 **`appsettings.Local.json` 이 환경변수보다 셉니다.**
`Jwt__Key=...` 같은 환경변수로 덮으려 해도 안 먹습니다 (확인하다 걸렸습니다).

### 검증

- 자리표시자 → 기동 실패(exit 127, `JwtKeyGuard.Require` 에서 `InvalidOperationException`)
- 정상 키 → 기동 성공
- 네 서비스의 공용 키가 동일(sha256 일치), 자리표시자 잔존 없음
- `git ls-files` 로 추적 파일 전수 검사 — **평문 비밀 0건**
- 백엔드 11개 프로젝트 `-c Release` 오류 0

> 작업 중 `appsettings.Local.json.d1bak` 백업을 만들었는데 `.gitignore` 대상이 아니어서
> 옛 키와 DB 비밀번호가 커밋될 수 있었습니다. 병합 확인 후 삭제했습니다.

### 재기동 방법

```bash
# 실행 중인 서비스를 내리고 (exe 가 잠겨 Debug 빌드가 실패하므로 먼저 내린다)
# 다시 띄우면 새 키로 뜬다. 모든 사용자가 다시 로그인해야 한다.
```

키를 다시 만들어야 하면 `openssl rand -base64 48` 로 만들어 **네 곳을 같은 값으로**
바꿉니다. 하나만 바꾸면 로그인이 안 됩니다.

### 남은 것

**커밋 이력의 옛 값은 그대로 있습니다.** 다만 키를 교체했으니 그 값은 쓸모없어졌고,
`JwtKeyGuard` 가 거부 목록에 넣어 두어 실수로 되돌려 쓰는 것도 막습니다.
이력 자체를 지우려면 `git filter-repo` 로 다시 써야 하고 협업자 전원이 다시 clone 해야 합니다.

**VAPID 웹푸시 키는 아직 평문입니다** (`funeralv2Api`·`HelpDeskServer` 3곳).
교체하면 **기존 구독이 전부 끊기므로** 지시 없이 손대지 않았습니다.
D8(NotificationServer)에서 한 곳으로 모으는 것과 함께 처리하는 것이 맞습니다.

---

<details>
<summary>원래 적어 둔 내용 (참고)</summary>

### 지금 상태

토큰을 서명하고 검증하는 키가 git 에 그대로 들어 있다.

```
a-very-secret-key-that-is-long-enough-for-security
```

ApiGateway · FileServer · funeralv2Api · HelpDeskServer 네 곳의 `appsettings.json` 이다.
웹푸시 발송 키(VAPID `PrivateKey`)도 마찬가지다.

### 왜 문제인가

이 값을 아는 사람은 **관리자 토큰을 직접 만들 수 있다.** 비밀번호가 필요 없다.
게이트웨이는 그 토큰을 정상으로 판정한다. 저장소 접근 = 시스템 전체 접근이다.

### 선택지

| | 방법 | 작업량 | 비고 |
|---|---|---|---|
| **A** | 환경변수 `Jwt__Key` 로 주입 | 작음 | 코드 변경 없음. ASP.NET 기본 지원 |
| **B** | `appsettings.Local.json` 으로 이동 | 작음 | 이미 쓰는 방식. 배포 시 파일 배치 필요 |
| **C** | Vault 등 시크릿 저장소 | 큼 | 회전·감사까지 되지만 인프라 도입 |

### 내 의견

**A → 나중에 C.** 지금 당장은 환경변수로 빼는 것만으로 위험이 크게 줍니다.

진행하면 이렇게 됩니다.

1. `openssl rand -base64 48` 로 새 키 생성
2. 6개 서비스에 `Jwt__Key` 환경변수 주입 (`backend_run_ubuntu.sh` 에 추가)
3. 추적 파일 값은 `"__SET_VIA_ENV__"` 로 교체
4. 전체 재기동 → **모든 사용자가 다시 로그인해야 합니다**

**자율로 하지 않은 이유:** 이 장비 밖에 다른 배포 환경이 있는지 확인할 수 없었습니다.
키를 돌렸는데 그런 환경이 있으면 자리 비우신 동안 로그인이 막힙니다.

> 커밋 이력에 남은 값을 지우려면 `git filter-repo` 로 이력을 다시 써야 하고
> 협업자 전원이 다시 clone 해야 합니다. **이력 정리보다 키 교체가 먼저입니다** —
> 교체하면 옛 값은 쓸모없어집니다.

</details>

---

## D2. 헬프데스크만 별도 DB·별도 계정을 쓴다 🟠

> **2026-08-29 — 이 항목의 전제가 뒤집혔습니다.**
> 아래 "모으자(B)" 는 헬프데스크만 혼자 떨어져 있다는 전제에서 나온 판단이었습니다.
> 그 사이 반대 방향으로 두 번 움직였습니다 —
> 소개 사이트를 `jsinisite` 로, 포털을 `jsiniportal` 로 각각 떼어냈습니다.
> 이제 **떨어져 있는 쪽이 표준이고 헬프데스크는 예외가 아닙니다.**
> 남은 문제는 "DB 가 나뉘어 있다" 가 아니라 **"DB 계정 하나를 여럿이 쓴다"** 입니다.

### 지금 상태 (2026-08-29 기준)

| 서비스 | DB | 스키마 | DB 계정 |
|---|---|---|---|
| AuthServer · FileServer · NotificationServer | `jsiniportal` | `scom` | `funeralv2` |
| funeralv2Api | `funeralv2` | `smfr` | `funeralv2` |
| SiteServer | `jsinisite` | `site` | `funeralv2` |
| **HelpDeskServer** | **`jinrecept`** | **`jsini`** | **`jsini`** |
| ProjMngServer | `projmng` | `projmng` | — |

DB 는 서비스별로 갈렸지만 **계정은 아직 `funeralv2` 하나가 네 DB 를 다 엽니다.**
즉 지금의 경계는 이름뿐이고, 권한으로 막혀 있지 않습니다.

### 아직 남는 문제

- 계정 연결 정보(`jsini.auth_user_links`)가 헬프데스크 DB 에만 있어,
  포털에서 계정을 지워도 연결이 남습니다. **트랜잭션이 걸치지 못합니다.**
  — 이것은 DB 를 합쳐야 풀리는 문제가 아니라, 지우는 쪽이 알려 줘야 하는 문제입니다.
- 백업·복구 단위가 다섯입니다.

### 선택지 (다시 씀)

| | 방법 | 작업량 | 비고 |
|---|---|---|---|
| **A** | 현행 유지 + 문서화 | 없음 | 이름은 갈렸고 권한은 안 갈렸다는 것을 적어 둔다 |
| **C** | DB 계정·권한도 서비스별로 나눈다 | 큼 | 지금 넷이 한 계정을 공유하는 것부터. 경계를 실제로 만드는 유일한 방법 |
| **D** | 계정 삭제를 이벤트로 전파 | 중간 | `auth_user_links` 고아 문제만 따로 푼다. DB 구조는 안 건드린다 |

옛 선택지 **B(`funeralv2` 의 `jsini` 스키마로 모으기)는 내립니다.** 방향이 반대입니다.

### 내 의견

**D 를 먼저, C 는 배포 형태가 정해진 뒤에.**
고아 링크는 실제로 아픈 곳이고 DB 구조와 무관하게 풀 수 있습니다.
계정 분리(C)는 서비스가 각자 다른 자격으로 붙어야 의미가 있는데,
지금은 전부 한 장비에서 뜨므로 권한을 나눠도 지킬 사람이 없습니다.

확인 필요: **JinReception 이 아직 `jinrecept` 를 보고 있는지.** 보고 있다면
그쪽 연결 문자열도 함께 걸립니다.

---

## D3. 헬프데스크 일정 API 만 응답 형식이 다르다 ✅ **해결됨**

### 무엇이 문제였나

헬프데스크 API 는 `{ success, message, data, meta }` 로 보내는데
`ScheduleEndpoints` 만 손으로 만든 모양을 내보내고 있었습니다.
**그것도 다섯 개가 제각각이었습니다** (처음 적을 때는 목록만 보고 있었습니다).

| | 예전 |
|---|---|
| `GET /` | `{ data: [...] }` — `success` 조차 없음 |
| `GET /{id}` | `{ data: {...} }` 또는 **본문 없는 404** |
| `POST /` | 엔티티를 그대로 (201) |
| `PUT /{id}` | 204, 본문 없음 |
| `DELETE /{id}` | 엔티티를 그대로 |

그래서 프론트가 `unwrapSchedule()` 이라는 예외 처리를 들고 있었습니다 —
서버가 표준을 안 지키는 걸 클라이언트가 떠안고 있던 셈입니다.

### ✅ 해결 완료 — A안 채택 (2026-08-26)

**JinReception 은 이미 동작하지 않는 것을 확인했습니다.** 이것이 이 건의 유일한 걸림돌이었습니다.

- **D10** 으로 게이트웨이가 `/api/helpdesk/**` 에 인증을 요구하게 됐습니다.
  JinReception 은 *자체 토큰을 서버가 믿어 주는* 구조였고, 그 전제가 사라졌습니다.
- **D11** 로 헬프데스크 자체 로그인이 닫혔습니다(`LocalLogin:Enabled` 기본 false).
  **로그인 자체가 되지 않습니다.**

저장소 전체를 훑어도 `/api/schedules` 를 부르는 곳은 포털 프론트뿐입니다
(그 외에는 게이트웨이 설정과 이 문서들).

1. `ScheduleEndpoints` 다섯 개를 모두 `ApiResponseBuilder.CreateAsync` 로 맞췄습니다.
   모양은 `ChecklistEndpoints` 와 동일합니다 — 없는 것을 찾으면 `null` 을 돌려주고
   빌더가 404 봉투로 바꿔 줍니다.
2. 프론트의 `unwrapSchedule()` 을 지웠습니다. 이제 체크리스트·고객과 똑같이 부릅니다.

함께 좋아진 것:

- **`PUT` 이 저장된 결과를 돌려줍니다** (예전 204·본문 없음). 화면이 다시 조회하지 않아도 됩니다.
- **404 에도 본문이 있습니다.** 예전에는 빈 404 라 클라이언트가 이유를 알 수 없었습니다.
- `DELETE` 는 지운 엔티티 대신 `{ deletedId }` 만 돌려줍니다. 이미 없는 데이터를
  응답에 실으면 화면이 살아 있는 것으로 오해할 수 있습니다.

### 검증

테스트 인스턴스(5401)를 따로 띄워 개발 DB 상대로 **28개 항목**을 확인했습니다 —
다섯 엔드포인트의 봉투 · `success`/`meta.rowCount` · 404 봉투 · 한글·따옴표 보존 ·
작성자가 서버에서 채워지고 수정이 그것을 덮지 않는 것 · 지운 뒤 404 · 목록 건수 원복.

`dotnet build -c Release` 오류 0 · `pnpm vite build` 성공 ·
`work.ts` 는 eslint·vue-tsc 지적 없음.
(`views/helpdesk/schedule/index.vue` 에 union 정렬 lint 오류 2건이 있으나
**이번 변경 전에도 있던 것**입니다 — 변경을 되돌려 확인했습니다.)

---

## D4. 템플릿 예제 메뉴 102개가 활성 상태 ✅ **해결됨**

### 무엇이 문제였나

활성 메뉴 231개 중 **102개가 vben 템플릿 예제**였습니다 (`/demos`, `/examples`, `/vben-admin`).
운영 사용자의 메뉴 트리에 그대로 보이고 있었습니다.

### 선택지

| | 방법 | 비고 |
|---|---|---|
| **A** | 비활성화 (`status = 0`) | 되돌리기 쉬움. 개발자가 볼 땐 다시 켜면 됨 |
| **B** | 삭제 | 화면 파일도 함께 정리하면 번들도 줄어듦 |
| **C** | 유지 | 개발 참고용으로 계속 사용 |

### ✅ 해결 완료 — A안 채택 (2026-08-22)

[`docs/sql/deactivate_demo_menus.sql`](../sql/deactivate_demo_menus.sql) 를 실행했습니다.

| 구분 | 전 | 후 |
|---|---|---|
| 운영 메뉴 | 활성 126 | 활성 126 (변화 없음) |
| `/demos` | 활성 56 | 비활성 |
| `/examples` | 활성 38 | 비활성 |
| `/vben-admin` | 활성 8 | 비활성 |

### 검증

임시 계정으로 로그인해 실제로 내려오는 메뉴를 확인했습니다.

```
GET /api/auth/menu/all  →  총 126건, 그중 예제 메뉴 0건
최상위: /dashboard, /deceased, /devs, /funerals, /help, /helpdesk,
        /portal/notice, /portal/release, /setting, /system, ...
```

AuthServer 가 `Status == 1` 만 내려주므로 예제 메뉴는 라우트 자체가 생성되지 않습니다.
DB component 경로 ↔ 실제 파일 정합성도 전건 일치를 재확인했습니다(비활성 포함).
스모크 테스트 19항목 통과. 임시 계정은 삭제했습니다.

### 되돌리기

같은 파일 아래쪽 주석 블록의 SQL 을 실행하면 됩니다.
`updated_by = 'demo-menu-cleanup'` 로 표시해 두었으므로 이번에 내린 것만 정확히 복구됩니다.

### 남는 것 (B안으로 갈 경우)

`views/demos` · `views/examples` 의 `.vue` 파일은 그대로 있습니다.
라우트가 백엔드 주도라 **런타임에는 불러오지 않지만**(지연 청크로 분리되어 있어
초기 로딩에는 영향 없음), `dist` 용량에는 남아 있습니다.
파일까지 지우려면 `router/routes/modules/demos.ts`·`examples.ts` 와 함께 정리해야 합니다.

---

## D5. 헬프데스크가 자체 파일 저장을 갖고 있다 🔵 **B안 진행 중 — 코드는 끝, 데이터 이전만 남음**

### ✅ 끝난 것 (2026-08-26)

**JinReception 이 이미 동작하지 않는 것을 확인했습니다** (D10·D11). 이것이 B안의
유일한 걸림돌이었습니다 — 자세한 근거는 D3 절에 적었습니다.

| | 내용 |
|---|---|
| 스키마 | `docs/sql/attachment_to_fileserver.sql` — `fileid`·`migratedat` 추가. **실행 완료** |
| 새 업로드 | `/api/files/upload` 가 **로컬 디스크에 쓰지 않습니다.** FileServer 로 보내고 파일 아이디만 받아 적습니다 |
| 내려받기 | `fileid` 가 있으면 FileServer 로 302, 없으면 예전 경로 (이전 완료 전까지만) |
| 이전 도구 | `deploy/attachment-migration/migrate.py` (+ README) |

**지금부터 새로 올라오는 파일은 FileServer 로 갑니다.** 로컬에 더 쌓이지 않습니다.

### 실제 데이터를 보고 알게 된 것

문서에 적어 둔 것과 달랐습니다.

- **저장 경로가 둘입니다** — `/home/lee/jinAttachment` 35건, `/home/quri/jinAttachment` 2건.
  (`FileStorage_BasePath` 기본값이 `/home/lee` 였습니다.)
  그래서 이전 도구는 디렉터리를 가정하지 않고 **행마다 `filepath` 를 읽습니다** —
  가정하면 2건을 놓칩니다.
- 37건 · 21.6MB · 최대 6.6MB · `entitytype` 은 전부 `ImprovementRequest` 하나.

### ⏳ 남은 것 — 배포 장비에서 도구를 한 번 돌려 주세요

**파일 바이트가 배포 장비 디스크에만 있습니다.** 개발 PC 에서는 DB 는 보이지만 파일이
보이지 않아 제가 옮길 수 없었습니다. 그 부분만 도구로 분리했습니다.

```bash
cd deploy/attachment-migration
python3 migrate.py --dry-run      # 확인만
python3 migrate.py --limit 1      # 한 건 시험
python3 migrate.py                # 전부
```

반복 실행해도 안전하고, 한 건씩 커밋하므로 중간에 끊겨도 이어서 돌리면 됩니다.
순서와 그 다음 정리(로컬 분기 삭제·원본 파일 삭제)는
[deploy/attachment-migration/README.md](../../deploy/attachment-migration/README.md) 에 있습니다.

### 검증

`fileid` 가 있는 행은 302 로 FileServer 로 넘어가고, 없는 행은 예전 경로를 타는 것을
테스트 인스턴스(5401)로 확인했습니다. 확인용으로 넣은 값은 원복했습니다(37건 중 0건 이전).
`dotnet build -c Release` 오류 0.

내려받기에서 파일 전체를 `MemoryStream` 에 담던 것도 함께 고쳤습니다 —
6.6MB mp4 가 있고 동시 요청에서 메모리가 그만큼 곱해집니다.

---

<details>
<summary>원래 적어 둔 내용 (참고)</summary>

### 지금 상태

FileServer 라는 전용 서비스가 있는데도 헬프데스크가 **따로** 첨부파일을 관리합니다.

| | FileServer | HelpDeskServer |
|---|---|---|
| 테이블 | `scom.filemetadatas` (966건) | `jsini.attachment` (37건) |
| 저장 위치 | `/home/quri/goldb_storage` | `/home/quri/jinAttachment` |
| API | `/api/file/*` | `/api/attachments`, `/api/files` |

### 왜 문제인가

같은 일을 두 곳에서 다르게 합니다. 백업 대상도 둘, 용량 관리도 둘입니다.
공통으로 처리할 부분은 JSini 로 모은다는 방향에 어긋납니다.

### 선택지

| | 방법 | 작업량 | 비고 |
|---|---|---|---|
| **A** | 신규 첨부만 FileServer 로, 기존 37건은 그대로 | 중간 | 한동안 두 경로 공존 |
| **B** | 37건까지 FileServer 로 이전 후 헬프데스크 파일 API 제거 | 큼 | 깔끔하지만 JinReception 영향 |
| **C** | 현행 유지 | 없음 | |

### 내 의견

**A → 시간 두고 B.** 37건이면 이전 자체는 가볍지만, **JinReception 이 같은 첨부를
읽고 있어** 헬프데스크 API 를 끊으면 그쪽이 깨집니다. JinReception 을 내린 뒤 B 가 맞습니다.

</details>

---

## D6. 플레이어용 익명 API 에 버전이 없다 🟡

### 지금 상태

모든 엔드포인트가 `/api/{서비스}/...` 로 버전이 없습니다.

### 왜 문제인가

**Flutter 플레이어**는 배포 주기가 다릅니다. 이미 설치된 앱이 쓰는
익명 엔드포인트(장비 코드·고인 조회)를 바꾸면 **현장 기기가 조용히 멈춥니다.**

해당 경로는 게이트웨이에 익명으로 열려 있는 이 다섯입니다.

```
/api/funeral/building/device/code/{code}
/api/funeral/building/deceased/deviceCode/{deviceCode}
/api/funeral/building/deceased/guide/deviceCode/{deviceCode}
/api/funeral/building/deceased/kiosk/deviceCode/{deviceCode}
/api/funeral/building/source/{id}
```

### 선택지

| | 방법 | 비고 |
|---|---|---|
| **A** | 위 5개만 `/api/funeral/v1/...` 로 고정, 구경로는 당분간 병행 | 플레이어 재배포 필요 |
| **B** | 전 API 버저닝 | 작업량 큼 |
| **C** | 현행 유지 + 변경 금지 규칙만 문서화 | 비용 0, 사람이 지켜야 함 |

### 내 의견

**A.** 다만 현장 기기 재배포 일정과 엮이므로 결정이 필요합니다.

---

## D7. 컨테이너화·기동 자동화가 없다 🟡

### 지금 상태

`Dockerfile` · `docker-compose.yml` 이 없습니다.
기동은 `backend_run_ubuntu.sh` 가 터미널 6개를 띄우는 방식입니다.
서비스 간 기동 순서가 스크립트에 암묵적으로만 있습니다.

### 선택지

| | 방법 | 비고 |
|---|---|---|
| **A** | compose 도입 | DB 는 외부(jin114.co.kr)라 앱 서비스만 대상 |
| **B** | systemd 유닛 | 배포 장비가 고정이면 실용적. 배포 스크립트가 이미 systemd 를 씀 |
| **C** | 현행 유지 | |

### 내 의견

**B.** 배포 스크립트가 이미 `systemctl stop jinRestApi.service` 를 쓰고 있어
운영이 systemd 기반입니다. compose 보다 현재 방식과 잘 맞습니다.

---

## D8. 알림/푸시를 별도 MSA 로 뺄까 🔵 **A안 — 서비스는 세웠고, 헬프데스크 전환만 남음**

### ✅ 끝난 것 (2026-08-27)

`microservices/NotificationServer/` (:5460) 를 세웠습니다.
상세는 [29-notification-server.md](29-notification-server.md).

| | 내용 |
|---|---|
| **VAPID 키가 한 곳에** | 두 서비스에 중복돼 있던 값을 옮겼습니다. **교체는 안 했습니다** — 교체하면 기존 구독이 전부 끊깁니다 |
| 구독 표 | `scom.push_subscriptions` — 주인을 `(owner_type, owner_key)` 문자열 쌍으로 두어 셋이 함께 씁니다 |
| 푸시·이메일 발송 | `/api/notification/notifications/{push,email}` |
| 게이트웨이 라우트 | `/api/notification/**` (인증 필요) |
| 기동 스크립트 | `dev.bat notify` · `backend_run_ubuntu.sh notify` |

**핵심 설계: 이 서비스는 보내는 일만 합니다.** 누구에게 보낼지는 부르는 쪽이 정합니다.
헬프데스크 구독 표는 주인이 `(int UserId, string UserType)` 이고 `Admin`·`Customer` 에
외래키로 묶여 있어 **헬프데스크 밖에서 쓸 수 없었습니다** — 그래서 주인을 문자열
쌍으로 바꿨고, `notify-team/{teamId}` 같은 도메인 API 는 옮기지 않았습니다.

알림 목록(읽음·전달)은 헬프데스크에 남겼습니다. 화면 기능이고 헬프데스크 테이블을 읽습니다.

### ⏳ 남은 것 — 헬프데스크를 이쪽으로 돌리는 일

**헬프데스크의 푸시·이메일은 지금도 예전 방식으로 돕니다.** 일부러 그대로 뒀습니다 —
돌리는 순간 기존 구독(다른 DB `jinrecept`)과 알림 목록 기능이 함께 걸립니다.
작동하는 것을 먼저 깨뜨리지 않는 편이 맞다고 보았습니다. 순서는 29번 문서 7절에 있습니다.

**D2 는 아직 열려 있지만 방향이 바뀌었습니다**(2026-08-29). 원래 판단이 "D1·D2 이후에"
였고 D1 만 끝났으므로 이 서비스는 자기 DB 를 만들지 않고 포털 DB 에 구독 표 하나만
두었습니다. 그 결론은 지금도 유효합니다 — 다만 이유가 "곧 합칠 테니까" 가 아니라
**"계정 · 파일 · 구독은 한 덩어리라서"** 로 바뀌었습니다.
그 포털 DB 는 이제 `funeralv2` 가 아니라 **`jsiniportal`** 입니다.

**화면은 아직 없습니다.** 서버는 준비됐고 `vapid-public-key` → `subscriptions`
두 API 만 부르면 됩니다.

### 검증

기동·구독 CRUD(중복 방지·남의 이름 403·해제)·푸시(실패 집계·구독 없음 안내)·
이메일(스풀 파일 규약)·게이트웨이 경유까지 테스트 인스턴스로 확인했습니다.
테스트 데이터는 지웠습니다. `dotnet build -c Release` 오류 0.

---

<details>
<summary>원래 적어 둔 내용 (참고)</summary>

### 지금 상태

푸시·이메일 발송이 **헬프데스크 안에만** 있습니다 (`/api/push`, `EMailUtil`, VAPID 키, 워커).

### 왜 검토하나

포털도 장례식장도 알림이 필요합니다. 지금 구조면 두 시스템이 헬프데스크를 거쳐야 합니다.
VAPID 키가 두 서비스에 중복으로 박혀 있는 것도 이 때문입니다.

### 선택지

| | 방법 | 비고 |
|---|---|---|
| **A** | `NotificationServer` 신설 | 세 시스템이 공유. VAPID 키도 한 곳에서 관리 |
| **B** | AuthServer(포털)로 흡수 | 서비스 수는 안 늘지만 포털이 더 커짐 |
| **C** | 현행 유지 | |

### 내 의견

**A. 단, D1·D2 이후에.** 지금 나누면 DB 를 공유하는 서비스가 하나 더 느는 것뿐입니다.
시크릿 관리와 DB 정리가 먼저입니다.

</details>

---

## D9. 인증 없이 남의 비밀번호를 초기화할 수 있다 ✅ **해결됨**

### 무엇이 문제였나

헬프데스크의 비밀번호 찾기 엔드포인트가 **인증 없이 인터넷에서 호출되고 있었습니다.**

```
POST /api/helpdesk/admins/find-password
{ "loginId": "...", "email": "..." }
```

동작은 이랬습니다 (당시 `AdminEndpoints.cs:154`).

1. `loginId` + `email` 이 맞는 계정을 찾고
2. **그 계정의 비밀번호를 임의의 임시값으로 바꿔 저장한 뒤**
3. 등록된 이메일로 임시 비밀번호를 보냈습니다

게이트웨이의 `/api/helpdesk/**` 경로가 Anonymous 라 그대로 통과했습니다.
존재하지 않는 계정으로 호출해 도달 가능함을 확인했습니다(404 응답 — 401 이 아님).

### 실제 위험도

**계정 탈취보다는 잠금(DoS)** 이었습니다. 임시 비밀번호는 등록된 이메일로만 가므로
공격자가 그 메일함을 갖고 있지 않으면 로그인하지는 못합니다.
하지만 **피해자의 기존 비밀번호가 이미 바뀌어 로그인이 막힙니다.**
loginId 와 email 은 대체로 추측 가능한 값이라 진입 장벽이 낮았습니다.

곁들여 발견한 버그가 하나 더 있었습니다. **고객 계정은 `MustChangePassword` 가
주석 처리**되어 있어 임시 비밀번호가 그대로 상용 비밀번호가 됐습니다.
관리자 계정에만 정상 적용되어 있었습니다.

### 선택지

| | 방법 | 비고 |
|---|---|---|
| **A** | 게이트웨이에서 이 경로만 차단 | 설정 한 줄. **JinReception 의 비밀번호 찾기가 막힙니다** |
| **B** | 엔드포인트 제거 | 포털 프론트는 이미 안 씁니다. JinReception 영향은 A 와 동일 |
| **C** | 메일 인증 링크 방식으로 교체 | 올바른 해법. 즉시 초기화하지 않고 본인 확인 후 변경. 작업량 있음 |
| **D** | 현행 유지 | |

### ✅ 해결 완료 — B안 채택 (2026-08-22)

지시에 따라 **엔드포인트를 제거**했습니다.

| 대상 | 조치 |
|---|---|
| `HelpDeskServer/Endpoints/AdminEndpoints.cs` | `/find-password` 핸들러 제거 (33줄), 사유 주석으로 대체 |
| `HelpDeskServer/Dtos/FindPasswordDto.cs` | 삭제 |
| `ApiGateway/appsettings.json` | 이 경로 전용 레이트리밋 라우트 제거 |
| `api/helpdesk/org.ts` | `findAdminPassword` 제거 (프론트는 이미 미사용) |

확인: 게이트웨이 경유·서비스 직접 모두 405(POST 핸들러 없음).
로그인 경로의 시도 제한은 그대로 살아 있습니다.

**JinReception 의 '비밀번호 찾기' 는 이제 동작하지 않습니다.** 의도한 결과입니다.
비밀번호 재설정이 필요하면 JSini 관리 포털에서 처리하시면 됩니다.

곁들여 있던 버그(고객 계정의 `MustChangePassword` 주석 처리)는 해당 코드가
통째로 사라져 함께 해소됐습니다.

바로 적용할 수 있는 게이트웨이 차단 설정을 [`docs/snippets/block-find-password.json`](../snippets/block-find-password.json)
에 준비해 두었습니다(**적용하지 않았습니다**).

최소한 **고객 계정의 `MustChangePassword` 주석은 푸는 게 맞습니다** —
임시 비밀번호가 영구 비밀번호가 되는 건 어느 선택지에서도 잘못입니다.
이것도 JinReception 사용자에게 비밀번호 변경을 강제하는 변경이라 손대지 않았습니다.


---

## D10. 헬프데스크의 쓰기 API 대부분이 인증 없이 열려 있다 ✅ **해결됨**

> D9 작업 중 발견했습니다. **D9 보다 심각합니다.**

### 무엇이 문제였나

헬프데스크 엔드포인트 파일 23개 중 **15개에 `RequireAuthorization` 이 하나도 없었습니다.**
게이트웨이의 `/api/helpdesk/**` 가 Anonymous 라 그대로 통과했습니다.

실제로 확인했습니다.

```
POST /api/helpdesk/schedules   (빈 본문, 토큰 없음)  →  201 Created
```

**일정 레코드가 실제로 만들어졌습니다.** (즉시 삭제했습니다.)
다른 경로들도 401 이 아니라 400(유효성 오류)을 돌려줍니다 — 인증을 통과한 뒤
본문 검증에서 걸렸다는 뜻입니다.

| 파일 | 쓰기 엔드포인트 | 인증 요구 |
|---|---|---|
| UtilEndpoints | 18 | **0** |
| RequestEndpoints | 6 | **0** |
| TeamEndpoints | 5 | **0** |
| CompanyEndpoints · NoticeEndpoints | 각 4 | **0** |
| ChecklistEndpoints · ProjectEndpoints · ScheduleEndpoints · WbsEndpoints · WbsLinkEndpoints | 각 3 | **0** |
| AttachmentEndpoints · RegisterEndpoints | 각 2 | **0** |
| ContactEndpoints · FileUploadEndpoints · WbsDiagramEndpoints | 각 1 | **0** |

`RoleEndpoints`(12) · `PushEndpoints`(8) · `MenuEndpoints`(6) 만 제대로 걸려 있습니다.

### 왜 이렇게 됐나

JinRestApi 시절 **JinReception 프론트가 자체 토큰으로 인증하고, 서버는 신뢰**하는
구조였던 것으로 보입니다. 게이트웨이 뒤로 들어오면서 그 전제가 사라졌습니다.

### 위험도

D9(계정 잠금)보다 큽니다. 데이터 생성·수정·삭제가 **아무 인증 없이** 가능합니다.
`RegisterEndpoints` 에는 관리자 생성도 있어, 본문만 맞추면 헬프데스크 관리자를
만들 수 있을 것으로 보입니다(빈 본문으로 DB 제약에 걸리는 것까지 확인).

### 선택지

| | 방법 | 비고 |
|---|---|---|
| **A** | 게이트웨이의 `/api/helpdesk/**` 정책을 `Anonymous` → 인증 필요로 변경 | **한 줄.** 익명이 필요한 경로만 예외 라우트로 열면 됨. JinReception 이 토큰 없이 호출하던 것은 전부 막힘 |
| **B** | 서비스의 각 그룹에 `RequireAuthorization()` 추가 | 15개 파일 수정. 세밀하지만 누락 위험 |
| **C** | A + B 둘 다 (심층 방어) | 권장 |

### ✅ 해결 완료 — A안 채택 (2026-08-22)

`ApiGateway/appsettings.json` 의 `helpdesk-route` 에서 `"AuthorizationPolicy": "Anonymous"`
한 줄을 걷어냈습니다. 지정이 없으면 `Program.cs` 의 `FallbackPolicy`(= 인증 필요)가 적용됩니다.

**예외 라우트는 두지 않았습니다.** 포털 프론트는 모든 헬프데스크 호출에 토큰을 붙이고
(`api/helpdesk/request.ts` 의 요청 인터셉터), 로그인 전에 헬프데스크를 부르는 곳이 없어
익명으로 열어 둘 경로가 하나도 없었습니다.

### 검증

| 확인 | 결과 |
|---|---|
| 토큰 없이 쓰기 (`schedules`·`companys`·`teams`·`admins`) | 전부 **401** |
| 토큰 없이 읽기 | 401 |
| **임시 계정으로 로그인 후 토큰 첨부** | `schedules`·`companys`·`customers` 모두 **200** |
| 다른 경로 영향 | 공지 익명 조회 200, 장례식장 401, 게이트웨이 health 200 — 변화 없음 |

정상 사용자에게는 영향이 없음을 실제 토큰으로 확인했습니다. 임시 계정은 삭제했습니다.

스모크 테스트에 헬프데스크 인증 경계 항목을 추가했습니다(19개 항목, 전부 통과).

### JinReception 에 미치는 영향

**JinReception 은 이제 헬프데스크 API 를 쓸 수 없습니다.** 자체 로그인 엔드포인트
(`/api/helpdesk/users/login`)도 함께 막혔습니다 — 토큰을 받으려면 토큰이 필요한 상태라
로그인 자체가 되지 않습니다. A안을 택하신 이상 의도된 결과입니다.

JinReception 을 당분간 살려 두셔야 한다면 되돌리는 방법은 두 가지입니다.

- **전체 되돌리기**: `helpdesk-route` 에 `"AuthorizationPolicy": "Anonymous"` 를 다시 추가
- **로그인만 열기**(권장): 아래 라우트를 추가해 로그인 경로만 익명으로 두고 나머지는 막힌 채로 유지

```json
"helpdesk-login-route": {
  "ClusterId": "helpdesk-cluster",
  "AuthorizationPolicy": "Anonymous",
  "RateLimiterPolicy": "auth-attempts",
  "Order": 0,
  "Match": { "Path": "/api/helpdesk/users/login", "Methods": [ "POST" ] },
  "Transforms": [
    { "PathRemovePrefix": "/api/helpdesk" },
    { "PathPrefix": "/api" }
  ]
}
```

### 남은 것 (B안 — 심층 방어)

게이트웨이에서 막았지만 **서비스 자체는 여전히 `RequireAuthorization` 이 없는 파일이 15개**입니다.
지금은 서비스가 루프백 전용이라 게이트웨이를 거치지 않고는 닿을 수 없어 실제 위험은 없습니다.
다만 나중에 서비스를 다른 장비로 분리하면 이 구멍이 되살아납니다.
그때는 B안(각 그룹에 `RequireAuthorization()` 추가)이 필요합니다.

---

---

## D11. 헬프데스크 자체 로그인에 만능 비밀번호가 있었다 ✅ **해결됨**

> 사용자 정보 통일 작업 중 발견했습니다. 상세는
> [15-jsini-user-unification.md](15-jsini-user-unification.md) 3-6 절에 있습니다.

### 무엇이 문제였나

`POST /api/helpdesk/users/login` 의 비밀번호 검증에 이런 분기가 있었습니다.

```csharp
else if (req.Password == "backdoor")   // backdoor
{
    isAuthenticated = true;            // ← 어떤 계정으로든 통과
}
```

**`backdoor` 라는 문자열만 알면 아무 계정으로나 헬프데스크 토큰을 받을 수 있었습니다.**
D10 으로 익명 접근은 막혔지만, **포털 토큰을 가진 정상 사용자라면 누구나** 이 경로로
헬프데스크 관리자 토큰을 만들 수 있는 상태였습니다.

### ✅ 해결 완료 (2026-08-22)

1. `backdoor` 분기를 3곳에서 제거했습니다(고객·관리자·인증 후 분기).
2. 자체 로그인 전체를 `LocalLogin:Enabled` 설정으로 닫았습니다(**기본 false**).
   인증은 JSini 포털이 단독으로 맡습니다.

확인: `{"loginId":"admin","password":"backdoor"}` → "헬프데스크 자체 로그인은 사용하지 않습니다."

**JinReception 을 되살려야 하면** `LocalLogin:Enabled=true` 로 열 수 있습니다.
다만 만능 비밀번호는 되살아나지 않습니다(코드에서 지웠습니다).

---

## D12. 토큰에 신원이 없어 보안 장치 두 개가 죽어 있었다 ✅ **해결됨**

> 상세는 [15-jsini-user-unification.md](15-jsini-user-unification.md) 0~2 절.

### 무엇이 문제였나

AuthServer 가 발급하는 토큰에 **이메일·역할 클레임이 없었습니다.** 그래서

| 기능 | 실제 동작 |
|---|---|
| 프로젝트관리 직접 쿼리 실행 역할 확인 | 게이트웨이가 늘 `X-User-Role: User` 를 보내 **모든 사용자가 항상 거부** |
| 헬프데스크 계정 이메일 대조 | 대조할 값이 없어 **한 번도 동작하지 않음** |

`/auth/user/info` 는 역할을 무조건 `["super"]` 로 만들어 내려보내고 있었습니다.

### ✅ 해결 완료 (2026-08-22)

토큰에 `email` · `role`(복수) · `RealName` · `CompanyId` 를 싣고,
게이트웨이가 `X-User-Roles` · `X-User-Name` · `X-User-Email` 로 전달합니다.
`/auth/user/info` 는 실제 배정 역할을 내려줍니다.

확인: `quristyle`(SYSTEM_ADMINISTRATOR)·`vben`(ADMINISTRATOR) 통과, `admin`(역할 없음) 403.

### 곁들여 생긴 판단거리

이메일 대조가 **이제 실제로 동작하므로** 기본값을 꺼짐으로 내렸습니다.
운영 데이터에 같은 이메일을 쓰는 다른 사람이 있습니다(사용자A ↔ 고객 사용자H).
켤지 여부는 [15-jsini-user-unification.md](15-jsini-user-unification.md) Q5 를 봐 주세요.

---

---

## D13. 이관한 계정 42개가 아이디만 알면 로그인된다 🔴

> [15-jsini-user-unification.md](15-jsini-user-unification.md) 11절에서 넘어온 항목입니다.

### 지금 상태

헬프데스크·프로젝트관리 사용자 43명을 포털 계정으로 옮겼습니다(42건 생성).
지시하신 대로 **비밀번호를 로그인 아이디와 같은 값**으로 넣었습니다 — `hd_kdh` 의 비밀번호는 `hd_kdh` 입니다.

여기에 하나가 겹칩니다. **역할이 없는 계정은 화면이 막히지 않습니다**(D 계열이 아니라
[10-jsini-portal-unification.md](10-jsini-portal-unification.md) 결정 2 의 fail-open 규칙).
역할 없는 계정 2개가 잠기지 않게 하려고 둔 규칙인데, 이제 그런 계정이 44개입니다.

**합치면 아이디만 알면 포털에 들어와 메뉴를 볼 수 있는 계정이 42개입니다.**

### 선택지

| | 방법 | 비고 |
|---|---|---|
| **A** | 이관 계정을 로그인 불가로 두고, 쓸 사람이 생길 때 비밀번호·역할을 함께 지정 | **한 줄.** 지금 바로 안전해집니다 |
| **B** | 이관 계정에 `PARTNER` 역할을 일괄 배정 | 열람 범위가 정의됩니다. 파트너 권한이 맞는지 확인 필요 |
| **C** | fail-open 을 fail-closed 로 전환 | 근본 해결이지만 역할 없는 기존 계정(`admin`·`administrator`)이 잠깁니다 |

```sql
-- A안
UPDATE scom.accounts SET password = '!' WHERE created_by = 'msa-user-import';

-- 이관 자체 되돌리기
DELETE FROM scom.accounts WHERE created_by = 'msa-user-import';
```

### 진행 상황 (2026-08-22)

**B 를 진행했습니다** — 이관 계정 42개에 `PARTNER` 역할을 배정했습니다
([`docs/sql/msa_user_role_partner.sql`](../sql/msa_user_role_partner.sql), 실행 완료).
fail-open 대상에서 벗어나 이제 `role_menus` 의 실제 권한이 적용됩니다.
`hd_kdh` 로 로그인해 토큰의 `role=PARTNER`, 프로젝트관리 직접 쿼리 403 까지 확인했습니다.

**다만 두 가지가 남았습니다.**

1. **PARTNER 가 아직 관리자 화면을 편집할 수 있습니다.** `role_menu_backfill.sql` 이
   전 역할의 항목을 모두 켜 뒀기 때문입니다. 활성 화면 136개 중 105개 열람,
   115개에서 등록·수정·삭제가 열려 있습니다 — 계정 관리·역할 관리·메뉴 관리가 포함됩니다.
   즉 아이디를 아는 사람이 들어와 **관리자 계정을 만들 수 있습니다.**
   → 닫는 스크립트 [`docs/sql/role_partner_tighten.sql`](../sql/role_partner_tighten.sql) 준비 완료(**미실행**).
   PARTNER 는 이번 이관 전까지 배정 계정이 0개였으므로 실행해도 기존 사용자 영향은 없습니다.
2. **비밀번호는 여전히 아이디와 같습니다.** 위 A안의 한 줄은 그대로 유효합니다.

상세는 [15-jsini-user-unification.md](15-jsini-user-unification.md) Q6 을 봐 주세요.

---

## D14. 이식 시스템에서 '누구로서' 일할지 정하는 두 스위치 🟠

> [19-msa-user-work-enablement.md](19-msa-user-work-enablement.md) 에서 넘어온 항목입니다.
> 그 문서의 Q9 · Q10 이고, 상세한 근거와 실측값은 거기에 있습니다.

### 지금 상태

이번 작업으로 **조회·관리 권한은 포털 역할이 정하게** 되었습니다. 계정 연결이 없어도
포털 관리자 역할이면 헬프데스크를 조회·관리합니다.

하지만 **자기 자신으로서 일하는 것**(내가 쓴 댓글, 나에게 배정된 요청, 내 알림)은
여전히 "이 포털 계정이 저쪽의 누구인가" 가 정해져야 합니다.
그 대응은 지금 **헬프데스크 1건 · 프로젝트관리 1명**뿐입니다.

이관 스크립트가 남긴 출처 기록(`MsaSource`)으로 메꿀 수 있게 코드를 준비해 두었고,
**두 스위치 모두 기본은 꺼져 있습니다.** 켜기 전과 똑같이 동작합니다.

| 스위치 | 켜면 | 영향 |
|---|---|---|
| `AccountLink:MatchByMsaSource` (헬프데스크) | 이관 계정 34개가 각자의 원본 담당자·고객 레코드로 해석된다 | 담당자 7명이 생긴다. 그 계정의 비밀번호가 아이디와 같다(D13) |
| `Identity:UseMsaSource` (프로젝트관리) | `pm_jskim` → `jskim` 으로 바꿔 저장 프로시저에 넘긴다 | 감사 컬럼에 쌓이는 값이 바뀐다. 8명이 자기 레코드를 찾게 된다 |

### 왜 자율로 켜지 않았나

**D13 과 맞물려 있습니다.** 헬프데스크 스위치를 켜면 34개 계정이 실제 업무 데이터의
주인으로 인정되는데, 그 계정들의 비밀번호는 아직 로그인 아이디와 같습니다.
아이디를 아는 사람이 그 사람으로 로그인해 그 사람의 업무를 다룰 수 있게 됩니다.

### 권하는 순서

```sql
-- 1) 이관 계정을 로그인 불가로 (D13 A안 — 한 줄)
UPDATE scom.accounts SET password = '!' WHERE created_by = 'msa-user-import';

-- 2) PARTNER 권한 범위 축소 (D13 남은 항목 1)
--    docs/sql/role_partner_tighten.sql
```

그다음 실제로 쓸 사람에게만 비밀번호를 지정하고 스위치를 켭니다.
프로젝트관리 스위치는 D13 과 무관하므로 먼저 켜도 됩니다.

### 곁들여 드러난 것 — 결정 두 개가 어긋나 있었습니다

[15절](15-jsini-user-unification.md) Q3 은 **A(포털 아이디 = 프로젝트관리 아이디로 맞춘다)**
였는데, 같은 작업의 사용자 이관은 충돌을 피하려고 `pm_` 접두어를 붙였습니다.
9명 중 `quristyle` 한 명만 아이디가 맞습니다. 어느 쪽으로 갈지도 함께 정해 주세요
(19절 Q10 에 선택지 4개를 정리했습니다).

---

## D15. 배포 도구 — 남은 판단 다섯 가지 🟠

배포 도구(`/portal/release`)를 "실제 진행 상황과 결과를 받는" 구조로 고쳤다.
상세는 [28-release-tool.md](28-release-tool.md) 에 있고, 여기에는 판단이 필요한 것만
옮겨 적는다.

**지금 상태로도 예전과 똑같이 동작한다.** 진행 보고 스위치가 셋 다 기본 꺼짐이고,
큐 이름과 메시지 모양을 예전과 호환되게 두었다. 달라진 것은 화면이 이제
성공했다고 거짓말하지 않는다는 것이다(보고를 안 하는 대상은 '요청 전송' 까지만 말한다).

| 번호 | 항목 | 급함 | 요지 |
|---|---|---|---|
| D-R1 | `run_script` 큐를 헬프데스크 메일과 계속 공유할 것인가 | 🟠 | 큐에 넣을 수 있는 쪽이면 무엇이든 실행시킬 수 있다. **소비자에 허용 목록을 두는 것(B)을 먼저 권합니다** — 큐를 안 건드리므로 배포가 멈추지 않습니다 |
| D-R2 | 메시지에서 `script` 경로를 뺄 것인가 | 🟡 | `targetKey` 로 소비자가 자기 표를 보게 하면 D-R1 이 근본적으로 사라진다. D-R1 의 C 와 함께 할 일 |
| D-R3 | 롤백 버튼 | 🔴 | **만들지 않았습니다.** 되돌리는 스크립트가 없고 범위(코드만? DB 도?)가 시스템마다 다릅니다. 잘못 만든 롤백은 배포 실패보다 위험합니다 |
| D-R4 | `ApiResponse.Fail` 의 인자 순서가 저장소 전체에서 뒤바뀌어 있다 | 🟡 | 배포 엔드포인트만 제자리에 넣었습니다. 전체를 맞추면 다른 화면의 오류 문구가 바뀔 수 있어 손대지 않았습니다 |
| D-R5 | 대상별 `VersionUrl` 을 무엇으로 채울 것인가 | 🟡 | jin114·goldb 가 자기 버전을 알려 주는 주소가 있는지 확인이 필요합니다 |

**진행 보고를 켜는 것은 판단이 필요하지 않습니다** — 배포 장비에 래퍼 파일 하나를
복사하고 설정 세 줄을 채우면 됩니다. 순서와 확인 방법은
[deploy/release-consumer/README.md](../../deploy/release-consumer/README.md) 에 있습니다.
소비자 코드는 고치지 않습니다.

---

## 우선순위 제안

| 순서 | 항목 | 이유 |
|---|---|---|
| — | ~~**D9** 비밀번호 초기화~~ | ✅ 해결됨 (B안) |
| — | ~~**D10** 헬프데스크 쓰기 API 무인증~~ | ✅ 해결됨 (A안) |
| — | ~~**D11** 헬프데스크 만능 비밀번호~~ | ✅ 해결됨 (제거 + 자체 로그인 차단) |
| — | ~~**D12** 토큰에 신원 없음~~ | ✅ 해결됨 |
| 0 | **D13** 이관 계정 42개의 비밀번호 + PARTNER 권한 범위 | 아이디만 알면 로그인되고, 그 계정이 관리자 화면을 편집할 수 있습니다. 각각 한 줄로 막을 수 있습니다 |
| 0 | **D14** 이식 시스템의 '누구로서' 스위치 두 개 | D13 을 먼저 처리한 뒤 켜면 됩니다. 켜기 전까지는 34명이 자기 업무 데이터를 다룰 수 없습니다 |
| 1 | **D1** 시크릿 | 저장소 접근 = 시스템 전체 접근. 작업량 작음 |
| 1 | **D15**(D-R1) 배포 큐의 임의 실행 | 큐에 넣을 수 있는 쪽이면 무엇이든 실행됩니다. 허용 목록만 두면 큐를 안 건드리고 막힙니다 |
| 2 | **D4** 예제 메뉴 | 5분. 사용성 개선 |
| 3 | **D2** DB 계정·권한 분리 | DB 는 서비스별로 갈렸는데(2026-08-29) 계정은 아직 `funeralv2` 하나가 넷을 다 엽니다. 경계가 이름뿐입니다 |
| 4 | **D3 · D5** 헬프데스크 정리 | JinReception 종료 시점과 함께 |
| 5 | **D6** 버저닝 | 현장 기기 배포 일정과 함께 |
| 6 | **D7 · D8** | 위가 끝난 뒤 |

---

## 이번 자율 작업에서 이미 처리한 것

결정이 필요 없다고 판단해 진행한 것들입니다.

| 항목 | 내용 | 검증 |
|---|---|---|
| **인증 우회 차단** | 내부 서비스 4개를 루프백에만 바인딩 | 외부 IP 에서 4포트 모두 연결 거부 확인 |
| 배포 도구 이전 | 헬프데스크 → 포털, 대상을 설정으로 일반화 | 대상 목록 조회·거절 경로 확인 |
| 공지 기능 | 포털 공통 공지 + 팝업 + 첨부 | 비인증/인증 노출 규칙, 첨부 업로드~다운로드 확인 |
| FileServer 복구 | 저장 경로·DB 연결이 없어 업로드가 아예 실패하던 상태 | 업로드·다운로드 확인 |
| 로깅 통일 | AuthServer·AIAgentServer 에 Serilog 추가 | 빌드 확인 |
| 스모크 테스트 | `scripts/smoke-test.sh` | 16개 항목 전부 통과 |

---

## 부록: 자율 작업 세션 기록 (2026-08-22)

자리를 비우신 동안 진행한 내용입니다. 모든 항목은 빌드·스모크 테스트로 확인했습니다.

### 검증 방법

```bash
./scripts/smoke-test.sh      # 17개 항목: 기동·우회차단·경로·인증경계·시도제한
dotnet build jsini.sln       # 백엔드 9개 프로젝트
cd fronts/apps/jsini-portal && pnpm vite build --mode production
```

세 가지 모두 통과한 상태로 남겨 두었습니다.

### 작업 중 한 번 사고가 있었습니다

레이트 리미팅 라우트를 추가하면서 `ReverseProxy.Routes` 안에 `"//rate-limit"` 이라는
주석용 키를 넣었는데, **YARP 는 그 아래 모든 키를 라우트로 읽습니다.**
경로가 없는 라우트라고 판정해 게이트웨이가 기동에 실패했고 몇 분간 전체가 멈췄습니다.

주석 키를 걷어내고 복구했으며, 지금은 정상입니다.
`Clusters` 쪽은 `//` 주석이 통하지만 `Routes` 아래는 안 됩니다 — 같은 실수를 막기 위해 적어 둡니다.

### 서비스 재기동에 대해

`dotnet watch` 는 **`appsettings.json` 변경으로는 재기동하지 않습니다.**
설정을 바꾼 뒤에는 해당 서비스의 `.cs` 파일을 건드려야 반영됩니다.
이번에 바인딩 설정이 반영되지 않아 한참 헤맨 원인이었습니다.

또한 `appsettings.Development.json` / `.Production.json` 이 같은 키를 다시 덮는 경우가 있어
설정을 바꿀 때는 **세 파일을 함께** 확인해야 합니다.
