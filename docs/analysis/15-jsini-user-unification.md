# 이식 시스템의 사용자 정보를 JSini 계정으로 통일

작성: 2026-08-22 (자율 진행 기록)
대상: 헬프데스크(HelpDeskServer) · 프로젝트관리(ProjMngServer) 와 그 화면들

> 지시: "이식된 화면이나 로직에서 사용자 정보를 활용하는 것이 있다면 모두 JSini 사용자를 쓰도록 개선하라.
> 단 두 시스템이 쓰는 DB 는 수정하거나 변경하지 마라. 로직만 바꿔라."
>
> **DB 는 손대지 않았다.** 스키마 변경·마이그레이션·데이터 이관이 하나도 없다.
> 지금까지 화면이 하던 정상적인 저장 동작(예: 프로젝트관리 사용자 레코드 저장)은 그대로다.

---

## 0. 먼저 알아야 할 것 — 토큰에 신원이 없었다

작업을 시작하고 가장 먼저 걸린 것이다. **포털이 발급하던 토큰에는 이름·이메일·역할이 없었다.**

```
발급 클레임 (변경 전)
  nameid  = 로그인 아이디
  unique_name = 사용자 이름
  Id      = 계정 GUID
```

이 때문에 두 가지가 **한 번도 동작하지 않고 있었다.**

| 기능 | 왜 죽어 있었나 |
|---|---|
| 헬프데스크 계정 이메일 대조 (`AccountLink:MatchByEmail`, 기본 켜짐) | 토큰에 이메일 클레임이 없어 대조할 값 자체가 없었다 |
| 프로젝트관리 직접 쿼리 실행 역할 확인 (`DevTools:RawSqlRoles`) | 토큰에 역할 클레임이 없어 게이트웨이가 늘 `X-User-Role: User` 를 보냈다. 허용 목록은 `SYSTEM_ADMINISTRATOR`/`ADMINISTRATOR` 라 **모든 사용자가 항상 거부**됐다 |

즉 "JSini 사용자를 쓰게 한다" 는 일은 화면 몇 개를 고치는 문제가 아니라
**신원을 실어 보내는 길부터 뚫어야 하는 일**이었다. 그래서 아래 순서로 진행했다.

```
AuthServer 토큰 → ApiGateway 헤더 → 각 MSA 의 신원 해석 → 화면
```

---

## 1. AuthServer — 토큰에 실제 신원을 싣는다

`Endpoints/AuthEndpoints.cs`

```
추가된 클레임
  RealName   실명 (real_name 우선)
  CompanyId  소속 회사
  email      대표 이메일   ← scom.account_profile_details 의 Email (is_primary 우선)
  role       배정된 역할   ← scom.role_accounts × scom.roles (status=1), 여러 개면 여러 개
```

`Services/UserService.cs` 의 `GetUserInfoAsync` 도 함께 고쳤다.
**역할을 무조건 `["super"]` 로 만들어 내려보내고 있었다.** 화면 접근 제어가 백엔드 메뉴 기준
(`accessMode: 'backend'`)이라 당장 티가 나지 않았을 뿐, 이 값을 보고 판단하는 코드가 생기면
모든 사용자가 관리자로 보인다. 실제 배정값을 내려준다.

> `Services/AuthService.cs` 에도 토큰 발급 코드가 있지만 **어디에서도 쓰이지 않는다**
> (DI 등록조차 없다). 실제 로그인은 `AuthEndpoints` 가 처리한다. 혼동하지 않도록 여기 적어 둔다.

## 2. ApiGateway — 검증한 신원을 내부 헤더로 전달

`ApiGateway/Program.cs`

| 헤더 | 전 | 후 |
|---|---|---|
| `X-User-Id` | 로그인 아이디 | 그대로 |
| `X-User-Role` | 항상 `User` | 첫 번째 역할 (없으면 `User`) |
| `X-User-Roles` | — | **전체 역할 목록**(쉼표 구분) |
| `X-User-Name` | — | 표시 이름 (**URL 인코딩**) |
| `X-User-Email` | — | 대표 이메일 |
| `X-User-Company-Id` | 회사 | 그대로 |

이름은 한글이라 그대로 실으면 HTTP 헤더(Latin-1)에서 깨진다. `Uri.EscapeDataString` 으로 실어 보내고
받는 쪽에서 되돌린다. 외부에서 들어온 `X-User-*` 는 **전부 먼저 지운다**(위조 방지). 새 헤더 두 개도
지우는 목록에 넣었다.

## 3. HelpDeskServer

### 3-1. `Services/JsiniUser.cs` (신규) — 신원을 꺼내는 단일 창구

```csharp
JsiniUserInfo? user = httpContext.GetJsiniUser();   // UserId / UserName / Email / CompanyId / Roles
string who = httpContext.AuditUser();               // 감사 기록에 남길 표기
```

**헬프데스크 자체 토큰과 구분해야 한다.** `NameIdentifier` 로는 구분되지 않는다 —
자체 토큰의 `sub`(로그인 아이디)도 JwtBearer 기본 매핑이 `NameIdentifier` 로 바꿔 놓기 때문이다.
그래서 JSini 토큰임이 확실한 두 가지만 본다.

- `FuneralIdentityMiddleware` 가 심은 `jsini_user_id` 클레임
- 게이트웨이가 붙인 `X-User-Id` 헤더 (게이트웨이는 포털 키로 검증한 토큰에만 붙인다)

### 3-2. 신원 미들웨어 — 연결이 없어도 "누구인지" 는 안다

전에는 헬프데스크 계정 연결에 성공해야만 클레임이 생겼다. 이제 **JSini 계정 자체를 먼저 심는다**
(`jsini_user_id` / `jsini_user_name` / `jsini_email`). 연결이 없어도 화면 안내와 감사 기록이 제대로 남는다.
헤더(`X-User-*`)도 보조 출처로 함께 본다.

### 3-3. 감사 기록에 JSini 아이디를 남긴다

`Data/AppDbContext.cs` 의 `SetAuditProperties` 가 남기던 값이 **헬프데스크 내부 숫자 ID**(`uid`) 였다.
나중에 `createdby = '4'` 를 보고 누구인지 알 방법이 없다. 이제 `quristyle` 처럼 JSini 로그인 아이디가 남는다.
(헬프데스크 자체 토큰으로 들어온 요청은 예전과 같이 내부 ID 를 남긴다.)

> **기존 행은 손대지 않았다.** 앞으로 쌓이는 값만 바뀐다. 두 형식이 섞이는 것이 문제라면
> 별도 판단이 필요하다 → 아래 [판단이 필요한 것](#8-판단이-필요한-것) Q1.

### 3-4. 작성자를 요청 본문에서 받지 않는다

`CreatedBy` 를 **클라이언트가 보내는 값**으로 쓰던 자리들이다. 남의 이름으로 데이터를 만들 수 있었다.

| 경로 | 전 | 후 |
|---|---|---|
| `POST /api/requests` | 폼의 `CreatedBy` | `http.AuditUser()` |
| `POST /api/notices` | 본문의 `CreatedBy` | `http.AuditUser()` |
| `POST /api/schedules` | 본문의 `CreatedBy` | `http.AuditUser()` |
| `PUT /api/schedules/{id}` | 본문 값으로 **덮어씀** | 최초 작성자로 고정 |
| `POST /api/admins`, `POST /api/customers` | 본문의 `CreatedBy ?? "system"` | `http.AuditUser()` |

### 3-5. `/api/users/info` · `/api/auth-links/me` — JSini 값을 정본으로

이름·이메일은 JSini 계정 값을 우선해 내려준다. 헬프데스크 레코드의 값은
`helpdeskUserName` / `helpdeskEmail` 로 함께 실어 화면이 둘을 나란히 보여 줄 수 있게 했다.
`jsiniUserId` / `jsiniUserName` / `jsiniEmail` / `jsiniRoles` 도 추가했다.

실제 응답(운영 데이터)이다. 두 체계가 서로 다른 사람처럼 보이는 상황이 그대로 드러난다.

```
loginId          admin                       ← 헬프데스크 계정 아이디
helpdeskEmail    quristyle@jinnets.co.kr
jsiniUserId      quristyle                   ← JSini 계정
jsiniEmail       user15@example.invalid
jsiniRoles       [SYSTEM_ADMINISTRATOR]
```

### 3-6. 🔴 헬프데스크 자체 로그인 — 만능 비밀번호를 걷어내고 기본으로 닫았다

`POST /api/helpdesk/users/login` 안에 이런 코드가 있었다.

```csharp
else if (req.Password == "backdoor")   // backdoor
{
    isAuthenticated = true;            // ← 어떤 계정으로든 통과
}
```

**`backdoor` 라는 문자열만 알면 아무 계정으로나 헬프데스크 토큰을 받을 수 있었다.**
관리자 계정도 예외가 아니다. 게이트웨이가 익명 접근을 막았지만(D10), 포털 토큰을 가진
**정상 사용자라면 누구나** 이 경로로 헬프데스크 관리자 토큰을 만들 수 있는 상태였다.

조치는 둘이다.

1. **`backdoor` 분기를 제거했다** (고객·관리자 양쪽, 그리고 인증 후 분기까지 3곳).
2. 자체 로그인 전체를 설정으로 닫았다 — `LocalLogin:Enabled`, **기본 `false`**.
   인증은 JSini 포털이 단독으로 맡는다. 되살리려면 이 값만 `true` 로 둔다.

확인:

```
POST /api/helpdesk/users/login  {"loginId":"admin","password":"backdoor"}
→ "헬프데스크 자체 로그인은 사용하지 않습니다. JSini 포털 계정으로 로그인하세요."
```

### 3-7. 🟠 이메일 자동 매칭을 기본 꺼짐으로 바꿨다

`AccountLink:MatchByEmail` 의 기본값을 `true` → **`false`** 로 내렸다.

이유는 두 가지다.

- **지금까지 한 번도 동작한 적이 없다.** 토큰에 이메일이 없었기 때문이다(0절).
  이제 동작하게 되었으므로, 켜 둔 채로 두면 신원 해석 규칙이 **조용히 달라진다.**
- **실제 데이터에 오탐이 있다.** 포털 계정 `quristyle`(사용자A)의 이메일
  `user15@example.invalid` 이 헬프데스크 **고객 3번(사용자H)** 의 이메일과 같다.
  지금은 명시적 연결이 있어 가려지지만, 연결이 없는 계정이라면 남의 고객 계정으로 붙는다.

[14-account-msa-linking.md](14-account-msa-linking.md) 가 아이디 자동 매칭을 꺼 둔 것과 같은 이유다 —
**추정하지 않는다.** 신원은 '계정 연결' 화면에서 사람이 확인하고 이어 준 값으로만 정한다.

## 4. ProjMngServer

### 4-1. 직접 쿼리 실행 역할 확인이 실제로 동작한다

`Filters/RawSqlGuardMiddleware.cs` 가 단수 `X-User-Role` 만 보고 있었다(0절 참고).
`X-User-Roles` 를 먼저 보고 없으면 단수로 떨어지게 했다. 역할이 여럿인 계정도 제대로 판정된다.

확인 (실제 서버 · 실제 토큰):

| 계정 | 역할 | `/api/projmng/Dev/sql` |
|---|---|---|
| `quristyle` | SYSTEM_ADMINISTRATOR | 통과 |
| `vben` | ADMINISTRATOR | 통과 |
| `admin` | 없음 | **403 거부** |

### 4-2. `sp_proj_login` 을 막고 `/Proj/login` 라우트를 없앴다

프로젝트관리에도 자체 로그인 프로시저가 있었고 전용 라우트로 열려 있었다.
인증은 포털이 단독으로 맡으므로 라우트를 지웠다. 다만 **이 서비스는 프로시저 이름을 클라이언트가 정한다** —
라우트만 지우면 범용 경로(`/api/Proj`)로 그대로 부를 수 있다. 그래서 `UserIdentityActionFilter` 가
프로시저 이름 자체를 막는다.

```
POST /api/projmng/Proj  {"ProcName":"sp_proj_login", ...}
→ 403  "인증은 JSini 포털이 담당합니다. 이 프로시저는 사용하지 않습니다."
POST /api/projmng/Proj/login → 404
```

### 4-3. 헤더가 없으면 본문의 사용자 아이디를 믿지 않는다

`SSUserId`(→ 프로시저의 `req_ss_user_id`)는 게이트웨이 헤더로 덮어쓴다. 헤더가 없을 때
예전에는 **본문 값을 그대로 뒀다.** 개발 편의였지만, 게이트웨이를 지나지 않는 경로가 생기면
아무나 남의 아이디로 감사 기록을 남길 수 있다.
이제 **Development 환경에서만** 본문 값을 남기고, 그 밖에서는 비운다(기록도 남긴다).

## 5. 화면

| 화면 | 무엇이 달라졌나 |
|---|---|
| 헬프데스크 **내 프로필** (`/helpdesk/org/profile`) | 헬프데스크 계정의 이름·이메일·사진을 고치고 **헬프데스크 비밀번호를 바꾸던** 화면이었다. 이제 **JSini 계정(아이디·이름·이메일·연락처·소속·역할)** 을 보여 주고, 그 계정이 어떤 헬프데스크 사용자로 연결됐는지 나란히 보여 준다. 비밀번호 변경 칸은 없앴다 — 인증은 포털 소관이고 헬프데스크 자체 로그인은 꺼져 있다 |
| 헬프데스크 **계정 연결** (`/helpdesk/system/account-link`) | 포털 계정 아이디를 **직접 타이핑**하던 것을 실제 계정 목록에서 고르게 바꿨다. 오타가 나면 아무 데도 연결되지 않는 매핑이 조용히 만들어지고, 그 계정으로 로그인해도 데이터가 빈 채로 보였다. 이미 연결된 계정은 `(연결됨)` 으로 표시한다 |
| 헬프데스크 **계정 연결 안내**(공용 부품) | "이 계정에" → "`quristyle (사용자A)` 계정에" 처럼 어느 계정인지 적는다 |
| 프로젝트관리 **내 프로젝트 정보** (`/projmng/proj/user-setting`) | 위에 **JSini 계정** 카드를 두어 정본을 보여 주고, 아래에 프로젝트관리 레코드를 둔다. `포털 정보로 채우기` 로 이름·이메일·연락처를 옮길 수 있다(저장은 눌러야 한다). 레코드가 없으면 그 사실을 알린다 |

프론트에는 `composables/use-jsini-user.ts` 를 새로 뒀다. 화면마다 `userStore.userInfo as any` 로
꺼내 쓰던 것을 한곳에 모은 것이다.

---

## 6. Monaco 에디터 복원

지시대로 **이식 전에 Monaco 를 쓰던 화면은 다시 Monaco 를 쓴다.**
이식 당시에는 포털에 없는 의존성이라 자체 편집기로 대체해 두었다
([13-projmng-migration.md](13-projmng-migration.md) 6절).

| 원본 | 이식 직후 | 지금 |
|---|---|---|
| 프로젝트관리 `QuriCodeEditor` (BlazorMonaco) | textarea + 줄번호 | **Monaco** |
| 헬프데스크 바이너리 파서 (Monaco) | ant-design Textarea | **Monaco** |

- `monaco-editor@0.54` 를 앱 의존성으로 추가했다.
- 부품을 `src/components/code-editor/` 로 옮겼다 — 두 화면군이 함께 쓰기 때문이다.
  프로젝트관리 화면들은 `views/projmng/shared` 에서 그대로 가져다 쓴다(재수출).
- **필요한 언어만 등록한다.** `monaco-editor` 를 통째로 가져오면 TypeScript·CSS·HTML 언어 서비스까지
  딸려 온다(워커만 7MB). `edcore.main` + `sql`·`pgsql`·`csharp`·`json` 만 등록해
  지연 청크 1.1MB(gzip 270KB) + 워커 2개로 줄였다. 화면에 들어갈 때만 내려받는다.
- 인터페이스(`v-model` · `language` · `readonly` · `height` · `placeholder`)는 그대로다.
  쓰는 화면 8곳은 한 줄도 고치지 않았다.
- 바이너리 파서는 원본처럼 **편집기에서 커서를 옮기면 그 줄을 다시 해석한다**
  (줄을 훑고 지나갈 때 서버를 연달아 부르지 않도록 250ms 뒤에 한 번만 부른다).

### 곁들여 고친 것 — 높이가 0 으로 접히는 문제

Monaco 는 textarea 와 달리 **내용에 따른 고유 높이가 없다.** 그래서 높이가 정해지지 않은 부모 안에
`height:100%` 로 넣으면 서로를 참조하며 5px 로 접힌다. 실제로 그렇게 접혔다.

바깥 상자가 높이를 받고, 편집기 자리는 `absolute inset-0` 으로 잡고, 최소 높이(10rem)를 둬서
어떤 배치에서도 쓸 수 있게 했다.

---

## 7. 확인한 것

### 빌드·정적 검사

```
dotnet build jsini.sln                → 오류 0
vite build --mode production          → 성공
oxlint (변경 파일)                     → 오류 0
./scripts/smoke-test.sh               → 24 통과 · 0 실패
```

### 실제 서버 · 실제 계정으로 확인한 것

| 확인 | 결과 |
|---|---|
| 로그인 토큰에 `email` · `role` · `RealName` · `CompanyId` 가 실린다 | 확인 |
| `/api/auth/user/info` 의 `roles` 가 실제 배정값(`SYSTEM_ADMINISTRATOR`) | 확인 (전에는 항상 `super`) |
| `/api/helpdesk/auth-links/me` 가 JSini 신원을 함께 준다 | 확인 |
| `/api/helpdesk/users/info` 가 JSini 값을 정본으로 준다 | 확인 |
| 헬프데스크 자체 로그인이 `backdoor` 로도 뚫리지 않는다 | 확인 (경로 자체가 닫힘) |
| 역할 없는 계정(`admin`)이 헬프데스크 계정으로 오인되지 않는다 | 확인 |
| 프로젝트관리 직접 쿼리 실행 — 역할별 허용/거부 | 확인 (위 4-1 표) |
| `sp_proj_login` 차단 · `/Proj/login` 제거 | 확인 (403 / 404) |
| 작성자 위조 차단 — 본문에 `createdBy: "spoofed-name"` 을 넣어 일정 생성 | 저장된 값은 `quristyle` (확인용 레코드는 즉시 삭제) |

### 화면

실제 브라우저(로그인 후)로 확인했다.

- **내 프로필** — JSini 계정 카드에 `quristyle / 사용자A / user15@example.invalid / 010-0000-0000 /
  준 시스템 · 개발실 / SYSTEM_ADMINISTRATOR`, 옆에 헬프데스크 연결(`admin` · 담당자 · 내부 ID 4)
- **계정 연결** — 계정 선택 목록에 `admin — 미르작은사장님`, `administrator — 미르`,
  `quristyle — 사용자A (연결됨)`, `vben — TestUser`
- **내 프로젝트 정보** — JSini 계정 카드 + 프로젝트관리 레코드(`quristyle` / `사용자A`) 조회
- **DB 쿼리 테스터** — Monaco 가 줄번호·SQL 구문 강조와 함께 뜬다.
  타이핑한 내용이 `v-model` 로 오간다(상태 표시줄 `pgsql · 3줄 · 66자`)
- **바이너리 파서** — 입력칸이 Monaco 로 바뀌었고 안내 문구가 겹쳐 보인다

> 아바타 이미지는 뜨지 않는다. 파일 서버 인증서 이름이 맞지 않아
> (`ERR_CERT_COMMON_NAME_INVALID`) 브라우저가 막는 것으로, 이번 작업과 무관한 기존 문제다.

---

## 8. 판단이 필요한 것

### Q1. 감사 기록의 표기가 섞인다 🟡

지금까지 헬프데스크의 `createdby` / `modifiedby` 에는 **헬프데스크 내부 숫자 ID** 가 쌓였고,
이제부터는 **JSini 로그인 아이디**가 쌓인다. 앞으로 이 컬럼을 읽는 화면·리포트는 두 형식을 다 만난다.

| | 방법 | 비고 |
|---|---|---|
| **A** | 그대로 둔다 (현재) | 새 값은 사람이 알아볼 수 있다. 옛 값은 숫자로 남는다 |
| B | 옛 값을 아이디로 일괄 변환 | **DB 데이터 변경이라 이번 지시 범위 밖이다** |
| C | 예전처럼 숫자 ID 로 되돌린다 | 통일되지만 누가 한 일인지 알 수 없는 상태로 돌아간다 |

**의견: A.** 앞으로 쌓이는 기록이 읽히는 편이 낫습니다. 변환이 필요하면 말씀해 주세요.

### Q2. 헬프데스크 사용자 32명을 어떻게 할 것인가 ✅ **진행함 — 11절 참고**

**이번 작업으로 "지금 로그인한 사람" 은 전부 JSini 계정이 됐다.** 남은 것은 **데이터 속의 사람**이다.

헬프데스크에는 담당자·고객이 32명 있고, 포털 계정은 4개다. 요청 작성자·담당자·댓글 작성자는
모두 헬프데스크 내부 ID 를 가리킨다. 이 사람들까지 JSini 사용자로 바꾸려면 **32명에 대응하는
포털 계정을 만들고 이어야** 한다. 계정 생성은 되돌리기 어렵고 사람 판단이 필요해 하지 않았다.

| | 방법 | 비고 |
|---|---|---|
| **A** | 현행 유지 — 로그인 신원만 JSini, 조직 데이터는 헬프데스크 | 지금 상태. 담당자 선택 목록 등은 헬프데스크 조직을 그대로 쓴다 |
| B | 실제 로그인하는 사람만 포털 계정을 만들어 연결 | 점진적. '계정 연결' 화면으로 바로 가능 |
| C | 32명 전부 포털 계정으로 옮긴다 | 계정 단일화 완성. 비밀번호 정책·통보가 따라온다 |

**의견: B.** 쓰는 사람부터 이으면 위험이 없고, 필요해지면 C 로 이어집니다.

### Q3. 프로젝트관리 사용자(`projmng.dev_user`) 9명도 같은 문제 ✅ **함께 이관함 — 11절 참고**

프로젝트관리는 포털 로그인 아이디를 그대로 `req_ss_user_id` 로 쓴다. `quristyle` 처럼
**아이디가 같으면 자연스럽게 이어지지만**, 나머지 8명은 포털 계정이 없다.
헬프데스크의 `auth_user_links` 같은 연결 테이블도 없다(만들려면 DB 변경이라 하지 않았다).

| | 방법 | 비고 |
|---|---|---|
| **A** | 포털 아이디 = 프로젝트관리 아이디로 맞춰 쓴다 | 표 하나 안 늘린다. 아이디를 맞춰야 한다 |
| B | 연결 테이블을 만든다 | 헬프데스크와 같은 방식. **DB 변경** |
| C | 현행 유지 | 아이디가 다른 사람은 자기 레코드를 못 찾는다 |

**의견: A.** 프로젝트관리는 사용자가 9명이라 아이디를 맞추는 편이 표를 늘리는 것보다 쌉니다.

### Q4. 헬프데스크의 남은 자체 계정 기능 ✅ **제거함 — 12절 참고**

인증은 닫았지만 **계정을 만드는 경로는 남아 있다.**

| 경로 | 지금 상태 |
|---|---|
| `POST /api/users/singup` (자체 가입) | 열려 있음. 비밀번호를 받아 헬프데스크 계정을 만든다 |
| `POST /api/admins/change-password` | 서버에 남아 있음 (포털 화면에서는 제거) |
| `/api/menus`, `/api/roles` (자체 메뉴·역할) | 남아 있음 — 포털은 쓰지 않는다 |

자체 로그인이 꺼진 지금 여기서 만든 비밀번호는 **아무 데서도 쓰이지 않는다.**
지우는 것이 맞아 보이지만, 아직 살아 있을지 모르는 JinReception 과 얽혀 있어 손대지 않았다.

**의견: JinReception 사용 여부를 확인한 뒤 한꺼번에 정리.** 확인해 주시면 바로 진행하겠습니다.

### Q5. 이메일 자동 매칭을 켤 것인가 🟡

3-7 에서 기본 꺼짐으로 두었다. 켜면 포털 계정과 이메일이 같은 헬프데스크 사용자에게
**자동으로** 연결된다. 지금 데이터에는 오탐이 하나 있다(사용자A ↔ 고객 사용자H).

**의견: 꺼진 채로 두기.** 연결은 '계정 연결' 화면에서 확인하고 잇는 편이 안전합니다.

---

## 9. 손대지 않은 것

- **헬프데스크·프로젝트관리 DB.** 스키마·데이터 모두 그대로다.
  (11절의 사용자 이관은 **포털 DB** 로 넣은 것이고, 원본 두 DB 는 읽기만 했다.)
- **헬프데스크 조직 화면**(담당자·고객·팀·회사). 여기서 다루는 것은 로그인 신원이 아니라
  업무 조직 데이터다. 담당자 선택 목록 등은 헬프데스크 데이터를 그대로 쓴다 → Q2.
- **`jsini.userproperty` 개인 설정.** 헬프데스크 내부 ID 로 묶여 있다. 연결을 통해 잘 동작하고,
  키를 바꾸면 기존 행이 떨어져 나간다.
- **JinReception.** 이 저장소 밖이다. 영향은 3-6 · Q4 에 적었다.

---

## 11. 사용자 이관 — MSA 사용자를 포털 계정으로 (Q2 · Q3)

지시: "헬프데스크·프로젝트관리의 사용자를 JSini 포털에 저장해 줘."

### 옮긴 것

| 원본 | 건수 | 포털 계정 |
|---|---|---|
| `jsini.admin` (헬프데스크 담당자) | 7 | `hd_*` 7 |
| `jsini.customer` (헬프데스크 고객) | 27 | `hd_*` 27 |
| `projmng.dev_user` | 9 | `pm_*` 8 |
| | **43** | **42** (+ 기존 1) |

스크립트: [`docs/sql/msa_user_import.sql`](../sql/msa_user_import.sql) — **실행 완료**. 반복 실행해도 안전하다.

### 왜 아이디에 접두어를 붙였나

**원본 아이디를 그대로 쓰면 서로 다른 사람이 겹친다.**

| 아이디 | 포털 | 원본 |
|---|---|---|
| `admin` | 미르작은사장님 | 헬프데스크 담당자 **사용자A** |
| `quristyle` | 사용자A | 헬프데스크 고객 **사용자H** |
| `kggmvp` | — | 헬프데스크 `wwe`(삭제) vs 프로젝트관리 **김원욱** |

그래서 전부 `hd_` · `pm_` 를 붙였다(`hd_admin`, `pm_kggmvp`). 규칙이 하나라 예측 가능하고
앞으로 생길 충돌도 미리 막는다.

**원본 아이디와 이름이 둘 다 같은 계정은 만들지 않았다** — 프로젝트관리 `quristyle`(사용자A)은
포털 `quristyle`(사용자A)과 같은 사람이라 건너뛰었다. 그래서 43건 중 42건이 새로 생겼다.

### 사람을 합치지는 않았다

같은 사람이 여러 원본에 있다. 이름·이메일로 자동으로 합치는 것은 이 프로젝트가 계속 피해 온
**추정**이라 하지 않았다. 각 원본 레코드가 각각 계정이 되었다.

| 사람 | 계정 |
|---|---|
| 사용자C | `hd_puni`(담당자) · `hd_uspuni`(고객) · `hd_puni2`(고객, 이름 '우선') |
| 사용자D | `hd_frogtok`(담당자) · `hd_a0516z`(고객) · `pm_jskim` |
| 사용자A | `quristyle`(기존) · `hd_admin` |

합치는 것은 사람이 확인하고 판단할 일이다. 어느 계정이 어디서 왔는지는
`MsaSource` 프로필 값(`helpdesk:admin:4`, `projmng:dev_user:jskim` …)으로 조회할 수 있다.

### 저장한 값

| 항목 | 값 |
|---|---|
| 로그인 아이디 | `hd_` / `pm_` + 원본 아이디 |
| 이름 · 실명 | 원본 이름 |
| 비밀번호 | **로그인 아이디와 같은 값** (지시). PBKDF2-HMAC-SHA256 600,000회 해시로 저장 |
| 회사 · 부서 | 비움 (포털에 원본 회사가 없고, 회사를 넣으면 부서까지 맞춰야 하는 복합 외래키다) |
| 프로필 | `Email` · `Phone` · `HomePath` · `Status` · `MsaSource` · `MsaCompany` |
| 상태 | 원본에서 삭제 표시된 2건(`hd_kggmvp` · `hd_suzymoon`)은 `DISABLED` |
| `created_by` | `msa-user-import` — 되돌릴 때 이 표시로 정확히 골라낸다 |

역할(`scom.role_accounts`)은 **하나도 배정하지 않았다.** 누구에게 무엇을 열어 줄지는 판단이 필요하다.

### 🔴 지금 상태의 위험 — 반드시 읽어 주세요

두 가지가 겹쳐 있습니다.

1. **비밀번호가 로그인 아이디와 같습니다.** `hd_kdh` 계정의 비밀번호가 `hd_kdh` 입니다.
   지시하신 대로 넣었지만, 아이디를 아는 사람은 누구나 로그인할 수 있습니다.
2. **역할이 없으면 화면이 막히지 않습니다.** [10-jsini-portal-unification.md](10-jsini-portal-unification.md)
   결정 2 의 fail-open 규칙 때문입니다. 역할 없는 계정 2개가 잠기지 않게 하려고 둔 규칙인데,
   이제 그런 계정이 **44개**가 되었습니다.

둘을 합치면 **아이디만 알면 포털에 들어와 메뉴를 볼 수 있는 계정이 42개** 생긴 셈입니다.

바로 쓸 수 있는 조치를 준비해 두었습니다. 하나만 골라 실행하시면 됩니다.

```sql
-- (권장) 이관 계정 전부를 로그인 불가로 만든다. 쓸 사람이 생기면 계정 관리에서 비밀번호를 지정한다.
UPDATE scom.accounts SET password = '!' WHERE created_by = 'msa-user-import';
```

```sql
-- 되돌리기 — 이관 자체를 취소한다 (프로필 값은 CASCADE 로 함께 지워진다)
DELETE FROM scom.accounts WHERE created_by = 'msa-user-import';
```

역할 쪽은 별도 판단이 필요합니다 → 아래 Q6.

### 계정 연결은 만들지 않았다 (지시대로)

새 포털 계정은 아직 헬프데스크 데이터와 이어져 있지 않다. 그 계정으로 로그인하면
헬프데스크 화면이 빈 채로 뜬다. 이으려면 `jsini.auth_user_links` 에 행을 넣어야 하는데
그건 헬프데스크 DB 쓰기라 하지 않았다.

**바로 실행할 수 있는 스크립트를 준비해 두었다** → [`docs/sql/msa_user_link.sql`](../sql/msa_user_link.sql) (34건).
매핑 전용 테이블 하나만 건드리고, `createdby = 'msa-user-import'` 로 표시해 정확히 되돌릴 수 있다.

### 확인

```
scom.accounts          46건 (기존 4 + 이관 42)
프로필 값              186건
계정 관리 화면          46건 표시 · 비활성 2건
```

비밀번호가 실제로 통하는지도 확인했다. 개발 환경은 로그인 시 비밀번호 검사를 건너뛰므로
비밀번호를 진짜로 검증하는 경로(`/auth/user/change-password`)로 확인했다.

```
hd_kdh · 현재 비밀번호 'wrong-value' → 거부   (이전 비밀번호가 일치하지 않습니다)
hd_kdh · 현재 비밀번호 'hd_kdh'      → 통과
토큰 클레임: nameid=hd_kdh · unique_name=사용자B · email=user09@example.invalid · role=없음
```

---

## 12. 자체 계정관리 기능 제거 (Q4)

계정·인증·권한은 JSini 관리 포털이 단독으로 맡는다. 이식본에 남아 있던 자체 계정관리 기능을 걷어냈다.

### 헬프데스크

| 대상 | 조치 |
|---|---|
| `POST /api/users/singup` (자체 가입) | **제거** |
| `POST /api/admins/change-password` | **제거** |
| `/api/menus/*` (자체 메뉴 6개) | **제거** — 파일 삭제 |
| `/api/roles/*` · `/api/common/users` (자체 역할·권한 12개) | **제거** — 파일 삭제 |
| `POST /api/admins` | 유지. 임시 비밀번호 발급을 없앴다 — 아무도 모르는 임의값을 넣는다 |
| `POST /api/customers` | 유지. 요청에서 `Password` 항목을 없앴다(DTO 에서 제거) |
| 담당자·고객 등록 화면의 '비밀번호' 칸 | **제거** |
| `POST /api/users/login` (자체 로그인) | 이미 꺼져 있다(`LocalLogin:Enabled`, 기본 false). 되살릴 여지를 남겨 코드는 두었다 |

`jsini.menu` · `approle` · `menurole` · `rolemenupermission` 테이블은 **그대로 두었다** — DB 는 건드리지 않는다.

담당자·고객 화면 자체는 남겼다. 거기서 다루는 것은 로그인 계정이 아니라 **조직 데이터**다
(요청의 담당자·고객이 그 레코드를 가리킨다).

### 프로젝트관리

| 대상 | 조치 |
|---|---|
| **프로젝트 사용자 그룹** (`/projmng/comm/user-group`) | **제거** — 자체 사용자 그룹 + 그룹별 화면 권한 관리 화면. 메뉴는 비활성(`PM_COMM_USERGRP`) |
| **프로젝트 참여자** (`/projmng/proj/user`) | 왼쪽 사용자 목록을 **읽기 전용**으로. 오른쪽 '누가 어느 프로젝트에 참여하는가' 는 업무 데이터라 그대로 편집 가능 |
| **내 프로젝트 정보** (`/projmng/proj/user-setting`) | **읽기 전용**으로. JSini 계정(정본)과 프로젝트관리 레코드를 나란히 보여 주기만 한다 |
| `POST /api/Proj/login` · `sp_proj_login` | 이미 제거·차단됨 (4-2 절) |

메뉴 비활성 스크립트: [`docs/sql/msa_selfaccount_menu_off.sql`](../sql/msa_selfaccount_menu_off.sql) — 실행 완료, 되돌리기 포함.

### 확인

```
GET  /api/helpdesk/menus              404
GET  /api/helpdesk/roles              404
GET  /api/helpdesk/common/users       404
POST /api/helpdesk/users/singup       404
POST /api/helpdesk/admins/change-password  405 (POST 핸들러 없음)
GET  /api/helpdesk/admins             200   ← 조직 관리는 그대로
GET  /api/helpdesk/users/info         200
dotnet build · vite build · oxlint · 스모크 24/24  전부 통과
```

---

## 13. 새로 생긴 판단거리

### Q6. 이관 계정 42개에 역할을 어떻게 줄 것인가 ✅ **B 진행 — 아래 참고**

[`docs/sql/msa_user_role_partner.sql`](../sql/msa_user_role_partner.sql) 로 이관 계정 42개에
`PARTNER` 역할을 배정했다(**실행 완료**). 이제 역할 배정 현황은 이렇다.

| 역할 | 계정 수 |
|---|---|
| 파트너 | 42 (이관 계정 전부) |
| 관리자 | 1 (`vben`) |
| 시스템관리자 | 1 (`quristyle`) |
| 없음 | 2 (`admin`, `administrator`) |

**확인** — `hd_kdh` 로 로그인해 실제 토큰과 응답으로 봤다.

```
토큰 클레임        role = PARTNER
메뉴 권한 응답     228행 · 열람 가능 186 · 프로젝트관리 열람 0
/api/projmng/Dev/sql   403 (직접 쿼리 실행 거부)
```

역할이 생겼으므로 이 계정들은 더 이상 fail-open 대상이 아니다 —
이제부터 `role_menus` 의 실제 권한이 그대로 적용된다.

#### ⚠ 다만, B 만으로는 범위가 좁아지지 않는다

선택지에 "열람 범위가 정의된다" 고 적었는데, **실제로 재어 보니 거의 좁아지지 않는다.**
[`role_menu_backfill.sql`](../sql/role_menu_backfill.sql) 이 4개 역할 × 전 메뉴를
'메뉴가 쓴다고 지정한 항목은 모두 허용' 으로 채워 뒀기 때문이다.

| | PARTNER | 시스템관리자 |
|---|---|---|
| 활성 화면 136개 중 열람 가능 | **105** | 136 |
| 등록·수정·삭제가 열린 화면 | **115** | — |
| 막힌 것 | 프로젝트관리 31개뿐 | — |

**여기에 관리자 화면이 들어 있다.** 열람뿐 아니라 편집까지 열려 있다.

```
/system/account   계정 관리        /auth/role-user   롤사람
/system/role-map  역할 관리        /auth/user-role   사람롤
/system/menu      메뉴 관리        /auth/role-menu   롤메뉴
/system/company   회사 관리        /setting/environment  환경설정
```

이관 계정의 비밀번호가 로그인 아이디와 같으므로(지시), **아이디를 아는 사람이 들어와
관리자 계정을 만들 수 있는 상태다.**

닫는 스크립트를 준비해 두었다 → [`docs/sql/role_partner_tighten.sql`](../sql/role_partner_tighten.sql)
(**실행하지 않았다** — 권한 범위는 판단이 필요한 일이다).

**실행해도 기존 사용자에게는 영향이 없다.** PARTNER 역할은 이번 이관 전까지
배정된 계정이 하나도 없었으므로, 영향 받는 것은 이관 계정 42개뿐이다.

그리고 **비밀번호는 여전히 아이디와 같다.** 역할을 준 것과 별개의 문제라
D13 의 한 줄은 그대로 유효하다.

```sql
UPDATE scom.accounts SET password = '!' WHERE created_by = 'msa-user-import';
```

### Q7. 같은 사람의 계정을 합칠 것인가 🟡

사용자C 3개, 사용자D 3개, 사용자A 2개처럼 한 사람이 계정 여럿을 갖고 있다.
합치려면 "어느 계정이 정본인가" 와 "옛 계정을 지울 것인가" 를 사람이 정해야 한다.

`MsaSource` 프로필 값으로 출처를 조회할 수 있으니, 목록을 뽑아 드리면 짚어 주시는 편이 빠릅니다.

### Q8. `pub_*` 공용 계정 9개를 남길 것인가 🟡

`접수공통`·`한주공통` 처럼 회사 등록 시 자동 생성되던 공용 로그인이다(사람이 아니다).
지시대로 전부 옮겼지만, 공용 로그인은 누가 한 일인지 남지 않아 감사에 약하다.

**의견: 비활성으로 내리고 실제 담당자 계정을 쓰는 편이 낫습니다.** 한 줄로 가능합니다.

---

## 14. 관련 문서

- [10-jsini-portal-unification.md](10-jsini-portal-unification.md) — 포털 통합과 공통 권한
- [13-projmng-migration.md](13-projmng-migration.md) — 프로젝트관리 이식
- [14-account-msa-linking.md](14-account-msa-linking.md) — 계정 대조 화면
- [12-decisions-pending.md](12-decisions-pending.md) — 결정 대기 목록
