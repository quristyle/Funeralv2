# 계정 접속 기록과 90일 비밀번호 만료

`/profile` 화면에서 **가입일 · 최근 로그인 시간 · 접속 아이피**를 볼 수 있게 하고,
**90일마다 비밀번호 변경을 요구**하도록 했다.

작업일: 2026-08-25

---

## 1. 무엇을 만들었나

### 1.1 DB — [`docs/sql/account_login_audit.sql`](../sql/account_login_audit.sql) (실행 완료)

`scom.accounts` 에 세 칸을 더했다. **가입일은 새로 만들지 않았다** — 이미 있는
`created_at` 이 그 값이다.

| 칸 | 쓰임 |
|---|---|
| `last_login_at` | 최근 로그인 성공 시각 |
| `last_login_ip` | 최근 로그인 시 접속 IP |
| `password_changed_at` | 비밀번호를 마지막으로 바꾼 시각 (90일 계산의 기준) |

**기존 계정의 `password_changed_at` 은 일부러 `created_at` 이 아니라 `now()` 로 채웠다.**
`created_at` 으로 채우면 오래전에 만든 계정이 스크립트 실행 순간 전부 만료되어,
활성 계정 43개가 한꺼번에 비밀번호 변경 화면에 갇힌다. `now()` 면 모두가 오늘부터
90일을 새로 받는다. 실행 후 확인: 만료 0건 / 전체 43건.

### 1.2 AuthServer

- [`Entities/Account.cs`](../../microservices/AuthServer/Entities/Account.cs) — 위 세 칸
- [`Services/PasswordPolicy.cs`](../../microservices/AuthServer/Services/PasswordPolicy.cs) — 만료 계산 한 곳
- [`Endpoints/AuthEndpoints.cs`](../../microservices/AuthServer/Endpoints/AuthEndpoints.cs)
  - 로그인 성공 시 접속 기록을 남긴다. **기록에 실패해도 로그인은 막지 않는다.**
  - 토큰에 `PwdChangedAt` 클레임을 싣는다.
  - 응답에 `passwordExpired` · `passwordExpiryDays` · `passwordDaysRemaining` 을 담는다.
- [`Services/UserService.cs`](../../microservices/AuthServer/Services/UserService.cs)
  - `GetUserInfoAsync` 가 계정 이력과 만료 정보를 함께 내려준다.
  - `ChangePasswordAsync` 가 `password_changed_at` 을 다시 찍고,
    **지금 쓰는 것과 같은 값은 거부**한다.
- `ChangePasswordAsync` 의 반환형을 `bool` → `ChangePasswordResult` 로 바꿨다.
  만료 때문에 **어쩔 수 없이** 변경 화면에 온 사람에게 "변경에 실패했습니다" 한 마디만
  주면 무엇을 고쳐야 할지 알 수 없다. 이제 이유를 구분해 돌려준다.

### 1.3 ApiGateway — 실제 차단

화면에서 안내만 하면 **요구가 아니라 부탁**이다. API 를 직접 부르면 그대로 통과한다.
그래서 모든 요청이 지나가는 게이트웨이에서 막는다
([`ApiGateway/Program.cs`](../../ApiGateway/Program.cs), `UseAuthorization()` 바로 뒤).

만료면 `403` + `code: "E403_PWD_EXPIRED"` 를 돌려준다.

**만료 여부(불린)를 토큰에 싣지 않고 시각을 싣는 이유**: 토큰 수명이 7일이다.
불린이면 발급 시점에는 아직 안 지났다가 그 뒤에 만료되는 구간을 놓친다.
시각을 싣고 게이트웨이가 매 요청마다 다시 계산하면 그 구간이 없다.

비밀번호를 바꾸려면 로그인 상태로 그 화면까지 가야 하므로 **꼭 필요한 만큼만** 열어 둔다.

```
/api/auth/login  /api/auth/logout  /api/auth/user/change-password
/api/auth/user/info  /api/auth/codes  /api/auth/menu
/api/file/download  /api/file/thumbnail   ← 프로필 사진(읽기 전용)
```

`/api/auth/menu` 를 열어 두는 이유: 메뉴가 없으면 라우트 자체가 생기지 않아
`/profile` 에도 갈 수 없다(메뉴는 백엔드 주도다).

### 1.4 프론트엔드

- 새 탭 **계정 정보** — [`account-info.vue`](../../fronts/apps/jsini-portal/src/views/_core/profile/account-info.vue)
  가입일 · 최근 로그인 · 접속 IP · 비밀번호 변경일 · 만료까지 남은 일수.
  전부 읽기 전용이다. 기본 설정 탭은 입력 폼이라 거기에 섞으면 고칠 수 있는 값처럼 보인다.
  남은 기간이 7일 이하면 색으로 구분한다.
- [`password-setting.vue`](../../fronts/apps/jsini-portal/src/views/_core/profile/password-setting.vue)
  만료·임박 안내를 띄우고, 같은 값 입력은 서버까지 가기 전에 먼저 걸러 준다.
- [`store/auth.ts`](../../fronts/apps/jsini-portal/src/store/auth.ts)
  로그인 응답이 만료면 원래 가려던 곳 대신 `/profile?tab=password` 로 보낸다.
  (그대로 보내면 화면만 열리고 데이터를 하나도 못 받는 상태가 된다.)
- [`api/request.ts`](../../fronts/apps/jsini-portal/src/api/request.ts)
  `E403_PWD_EXPIRED` 를 잡아 안내를 **한 번만** 띄우고 변경 화면으로 보낸다.
  이미 로그인해 둔 탭에서 만료 시점을 넘기는 경우가 있고, 그때 화면마다 빨간 토스트만
  쌓이면 무슨 일인지 알 수 없다.

---

## 2. 판단이 필요했던 것

### 2.1 비밀번호를 바꾸면 반드시 다시 로그인시킨다

만료 판정 근거는 토큰의 `PwdChangedAt` 이고 토큰 수명은 7일이다.
지금 들고 있는 토큰에는 **바꾸기 전** 시각이 들어 있으므로, 그대로 두면 두 가지가 어긋난다.

1. 만료되어 들어온 사람이 비밀번호를 바꿨는데도 계속 막힌다.
2. 미리 바꿔 둔 사람도 옛 시각 기준으로 며칠 뒤 갑자기 막힌다.
   (그때 같은 값으로 다시 바꾸려 하면 "같은 값" 으로 거부되어 빠져나갈 길이 좁아진다.)

change-password 응답으로 새 토큰을 발급하는 방법도 있지만 토큰 발급 코드가 갈라진다.
재로그인이 가장 확실하고 사용자에게도 익숙한 동작이다.

**변경 사항**: 만료된 경우만이 아니라 **비밀번호를 바꾸면 늘** 재로그인한다.

### 2.2 기준 시각을 모르면 만료로 보지 않는다

`password_changed_at` 이 null 이면 AuthServer 도 게이트웨이도 통과시킨다.
기준을 **모르는 것**과 **오래된 것**은 다르다. 모른다는 이유로 잠그면
칸을 새로 만든 직후처럼 데이터가 아직 없는 상황에서 전원이 갇힌다.

### 2.3 접속 IP 는 참고용이다

게이트웨이 뒤이므로 `X-Forwarded-For` 의 첫 값을 쓴다. `RemoteIpAddress` 를 그대로 쓰면
모든 계정의 IP 가 게이트웨이 주소로 똑같이 남는다.

다만 이 헤더는 **클라이언트가 보내는 값이라 위조할 수 있다.** 게이트웨이가 덧붙이는
방식이라 앞에 임의의 값을 심어 둘 수 있다. **기록으로만 쓰고 권한 판단에는 쓰지 않는다.**

### 2.4 끄는 길을 남겼다

`Auth:PasswordExpiryDays` 를 `0` 으로 두면 정책이 꺼진다.
사고가 났을 때 코드를 고치지 않고 설정 한 줄로 되돌릴 수 있어야 한다.

**설정이 두 곳에 있다** — AuthServer(화면에 보여 줄 값)와 ApiGateway(실제 차단).
둘을 함께 맞춰야 한다. 차단만 끄고 싶으면 게이트웨이 쪽만 0 으로 둔다.

---

## 3. 확인한 것

격리된 게이트웨이 인스턴스(:15265)를 따로 띄워 실제 요청으로 확인했다.
개발자가 띄워 둔 게이트웨이(:5265)는 건드리지 않았다.

| 확인 | 결과 |
|---|---|
| 로그인이 접속 기록을 남기는가 | `last_login_at` · `last_login_ip`(127.0.0.1) 기록됨 |
| 토큰에 `PwdChangedAt` 이 실리는가 | 실림 |
| `/user/info` 가 계정 이력을 내려주는가 | 9개 필드 모두 정상 |
| 만료 계정 로그인 | `passwordExpired: true`, 남은 일수 0 |
| 만료 계정의 일반 경로 | `403 E403_PWD_EXPIRED` |
| 만료 계정의 허용 경로 | `user/info` · `codes` · `menu/all` · `change-password` 모두 통과 |
| 만료 아닌 계정 | 영향 없음 (200) |
| 같은 값으로 변경 | 거부 + 전용 메시지 |
| 새 값으로 변경 | 성공, `password_changed_at` 이 현재 시각으로 재설정, 해시로 저장 |
| 변경 후 새 토큰 | 일반 경로 200 |
| 변경 전 옛 토큰 | 여전히 403 (→ 재로그인이 필요한 이유) |

검증용으로 만든 임시 계정과 바꿔 둔 값은 모두 되돌렸다. 확인 후 만료 0건 / 전체 43건.

`dotnet build` · `pnpm vite build` 통과.

---

## 4. 남은 것

- **게이트웨이를 다시 띄워야 차단이 켜진다.** 현재 실행 중인 프로세스는 이 변경 전
  빌드다(`dotnet watch` 가 아니라 exe 직접 실행). 재시작 전까지 기록과 화면은 동작하지만
  차단만 걸리지 않는다.
- 이관 계정 42개는 비밀번호가 로그인 아이디와 같은 상태다.
  이 만료 정책으로 해결되지 않는다 → [15-jsini-user-unification.md](15-jsini-user-unification.md) 의 D13.
- ~~'최근 로그인' 은 지금 보고 있는 로그인이다. 보안 신호로는 **이전 로그인**을 함께
  보여 주는 편이 낫다.~~ → 아래 5절에서 처리했다.
- ~~로그인 이력을 표로 쌓지 않고 마지막 한 건만 덮어쓴다. 감사 추적이 필요하면
  별도 이력 표가 필요하다.~~ → 아래 5절에서 처리했다.

---

## 5. 계정 정보 화면 확장 (2026-08-25)

> 지시: "`account-info.vue` 은 사용자의 계정이 활동한 정보가 보여지는 곳이다.
> 사용자 계정이 활동한 정보를 추가 할수 있는것이 있다면 더 추가하라.
> 필요 하다면 관리항목을 더 늘려서 관리하라. 그리고 프로필에서 보여주는 화면중에
> 하나인데 다른 화면들과 글씨 크기가 차이를 많이 보인다."

4절에 남겨 둔 두 가지(이전 로그인 · 이력 표)가 정확히 이 지시의 내용이라 함께 처리했다.

### 5.1 왜 표를 하나 더 두었나

`accounts` 의 `last_login_at` · `last_login_ip` 는 **마지막 한 번**만 남는다.
그래서 화면이 "지금 이 접속" 밖에 보여 줄 수 없었다. 사람이 자기 계정 화면에서
실제로 궁금해하는 것은 그것이 아니다.

| 궁금한 것 | 마지막 값만으로 | 이력이 있으면 |
|---|---|---|
| 지난번엔 언제·어디서 들어왔나 | ✗ | ✅ 이전 접속 |
| 누가 내 아이디를 두드렸나 | ✗ | ✅ 최근 실패 |
| 이 계정을 얼마나 써 왔나 | ✗ | ✅ 로그인 횟수 |

앞의 둘은 **남의 접근을 알아채는 단서**다. 그래서 시도를 한 줄씩 쌓는 표를 두었다.
`accounts` 의 마지막 값도 계속 남긴다 — 로그인 화면과 게이트웨이가 이미 그 값을 쓰고 있고,
표를 매번 훑는 것보다 싸다.

**실패도 남긴다.** 성공만 남기면 두드림을 볼 수 없다. 응답 메시지는 그대로 두었다 —
아이디가 있는지 없는지는 여전히 알려 주지 않는다(실패 이유는 기록에만 남는다).

### 5.2 만든 것

```
docs/sql/account_login_log.sql           scom.account_login_logs (실행 완료)
Entities/AccountLoginLog.cs              + LoginFailReason 상수
DTOs/AccountActivityDto.cs               LoginLogDto · AccountActivityDto
Services/LoginLogService.cs              쓰기 · 활동 정보 계산 · User-Agent 요약
Endpoints/AuthEndpoints.cs               로그인 성공·실패 모두 기록
Endpoints/UserEndpoints.cs               GET /user/activity
```

프론트: `api/core/user.ts`(`getAccountActivityApi`) ·
`views/_core/profile/account-info.vue`(다시 만듦)

표를 만들 때 **이미 아는 마지막 접속을 첫 줄로 넣어 두었다**(계정마다 한 줄).
그러지 않으면 표가 생긴 뒤 처음 로그인할 때까지 화면이 "기록 없음" 으로 보인다 —
이미 아는 사실이 있는데 비어 보이는 것은 이상하다.

**자기 것만 볼 수 있다.** `/user/activity` 는 조회할 계정을 요청에서 받지 않고
게이트웨이가 넘긴 신원을 쓴다. 남의 접속 기록을 여는 길이 없다.

`User-Agent` 는 원문을 남기되 화면에는 `Chrome · Windows` 처럼 줄여 보여 준다.
원문은 길어서 표를 무너뜨리고, 정확한 판별이 목적이 아니다 —
"내가 쓰는 그 브라우저가 맞는지" 만 알면 된다. 원문은 마우스를 올리면 보인다.

### 5.3 화면 구성

값을 성격별로 세 묶음(`계정` · `접속` · `비밀번호`)으로 나누고 그 아래 접속 기록 표를 둔다.
최근 30일 안에 실패가 있으면 **표를 펼쳐 보지 않아도 알 수 있게** 위에 띠로 알린다.

늘어난 항목: 계정 사용 일수 · 역할(이름) · 이관 출처 · 이전 접속 · 로그인 횟수 ·
최근 실패 · 접속 기록 10건.

활동 정보를 못 받아도 계정 값은 그대로 보여 준다(`catch` 로 흘려보낸다).
화면 전체가 비는 것보다 낫고, 서버를 아직 다시 띄우지 않은 상태에서도 쓸 수 있다.

### 5.4 글씨 크기 — 왜 이 탭만 커 보였나

재 보니 원인이 분명했다.

| 곳 | 크기 |
|---|---|
| 다른 프로필 탭 (vben 폼 라벨·입력) | **12.25px** (`0.875rem`, 루트 14px) |
| 계정 정보 (antd Descriptions) | **14px** |

vben 폼은 Tailwind 기준(`0.875rem`)으로 그려지고 antd 부품은 자기 토큰(14px)을 쓴다.
antd 쪽을 폼 기준으로 내렸다 — 반대로 폼을 올리면 프로필 밖의 모든 화면이 함께 커진다.
묶음 제목만 `0.9375rem` 으로 조금 키워 본문과 구분되게 두었다.

고친 뒤 측정: 라벨·내용·표 전부 `12.25px`, 묶음 제목 `13.125px`.

### 5.5 확인한 것 · 못 한 것

```
dotnet build AuthServer   오류 0
pnpm vite build           성공
화면                       세 묶음 · 표 · 안내 렌더링 확인, 글씨 크기 12.25px 로 일치
```

**활동 정보는 실제 값으로 확인하지 못했다.** `/auth/user/activity` 가 지금 404 다 —
실행 중인 AuthServer 가 이 변경 이전 빌드이기 때문이다(`dotnet run --no-build`).
표는 만들었고 이미 5개 계정의 마지막 접속이 첫 줄로 들어가 있으니,
AuthServer 를 다시 띄우면 바로 값이 채워진다.
