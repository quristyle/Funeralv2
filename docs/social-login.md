# 소셜 로그인 · 소셜 회원가입 (구글 · 네이버 · 카카오)

로그인 화면과 가입 신청 화면에 소셜 단추를 세우는 방법과, 그 뒤에서 무슨 일이
일어나는지를 적는다.

**지금 상태** — 코드는 다 들어가 있고 **열쇠만 비어 있다.** 열쇠가 없는 공급자는
단추가 아예 안 그려지므로, 이 문서를 따라 값을 채우기 전까지는 화면이 지금과
똑같다(아이디·비밀번호 로그인만 보인다).

---

## 1. 무엇부터 정해야 하나 — 준비물

공급자마다 앱을 하나씩 만들고 **값 둘**(아이디·비밀 열쇠)과 **콜백 주소 하나**를
맞춰 주면 끝이다. 셋 다 할 필요는 없다 — **하나만 채워도 그 단추만 선다.**

| 준비물 | 어디서 | 어디에 넣나 |
|---|---|---|
| ClientId · ClientSecret | 각 공급자 콘솔 | `scripts/secrets.env` (운영은 `appsettings.Local.json`) |
| 콜백 주소 | 우리가 정해서 콘솔에 **등록** | 셸 설정 `Portal:BaseUrl` 이 앞부분을 만든다 |
| 동의 항목(이메일·이름) | 각 공급자 콘솔 | — |

콜백 주소는 이 모양이다. **글자 하나까지 같아야 한다** — 다르면 공급자가 인가
화면에서 바로 막는다.

```
https://portal.jsini.co.kr/social/google/callback
https://portal.jsini.co.kr/social/naver/callback
https://portal.jsini.co.kr/social/kakao/callback
```

개발 장비에서 시험하려면 `http://localhost:5557/social/{공급자}/callback` 도 함께
등록해 둔다(구글·카카오는 여러 개를 받는다).

---

## 2. 공급자별 발급 절차

### 구글

1. [console.cloud.google.com](https://console.cloud.google.com) → 프로젝트 선택
2. **API 및 서비스 → OAuth 동의 화면** — 앱 이름·지원 이메일을 채운다.
   사내에서만 쓰면 사용자 유형을 **내부**로 두는 편이 검수가 없어 빠르다.
3. **사용자 인증 정보 → 사용자 인증 정보 만들기 → OAuth 클라이언트 ID**
   - 유형: **웹 애플리케이션**
   - 승인된 리디렉션 URI: 위의 구글 콜백 주소
4. 나온 **클라이언트 ID** 와 **클라이언트 보안 비밀번호**를 받아 적는다.

범위(`openid email profile`)는 이미 `appsettings.json` 에 적혀 있다.

### 네이버

1. [developers.naver.com](https://developers.naver.com) → **애플리케이션 → 등록**
2. 사용 API 에서 **네이버 로그인**을 고르고,
   **제공 정보 선택**에서 `이메일 주소` · `이름` · `프로필 사진`을 켠다.
   **이것을 안 켜면 이메일이 비어서 온다** — 그러면 승인 안내 메일을 보낼 곳이 없다.
3. 환경에 **PC 웹**을 더하고 서비스 URL과 **Callback URL**(위의 네이버 주소)을 적는다.
4. **Client ID** 와 **Client Secret** 을 받아 적는다.

네이버는 `scope` 를 요청에 싣지 않는다. 받아 올 항목을 이 콘솔 화면이 정하기
때문이고, 그래서 우리 설정의 `naver.Scope` 는 빈 값이다.

### 카카오

1. [developers.kakao.com](https://developers.kakao.com) → **내 애플리케이션 → 추가**
2. **앱 키**에서 **REST API 키**를 받아 적는다.
   **JavaScript 키가 아니다** — 그것을 넣으면 인가 화면이 `KOE101` 로 막힌다.
3. **카카오 로그인** → 활성화 ON, **Redirect URI** 에 위의 카카오 주소를 등록.
4. **카카오 로그인 → 동의항목** 에서 `닉네임` · `프로필 사진` · `카카오계정(이메일)` 을 켠다.
   **하나라도 안 켜면 인가 화면이 막힌다**(`KOE205` · `KOE006`).
   이메일은 **비즈 앱**에서만 켤 수 있다 — [앱 설정 → 비즈니스] 에서 사업자 정보로 전환한다.
5. 비밀 열쇠를 쓰려면 **보안 → Client Secret** 을 켜고 값을 받아 적는다.
   **안 켰으면 비워 둔다** — 빈 값을 실어 보내면 카카오가 거절한다.

---

## 3. 값 넣기

`scripts/secrets.env` (없으면 `secrets.env.example` 을 복사해서 만든다):

```bash
Auth__Social__Providers__google__ClientId=...
Auth__Social__Providers__google__ClientSecret=...
Auth__Social__Providers__naver__ClientId=...
Auth__Social__Providers__naver__ClientSecret=...
Auth__Social__Providers__kakao__ClientId=...            # REST API 키
Auth__Social__Providers__kakao__ClientSecret=...        # 안 켰으면 넣지 않는다
```

운영 서버는 이 파일이 아니라
`/srv/jsini/config/AuthServer/appsettings.Local.json` 으로 넣는다.

```json
{
  "Auth": {
    "Social": {
      "Providers": {
        "google": { "ClientId": "...", "ClientSecret": "..." }
      }
    }
  }
}
```

그리고 **셸(:5557)의 설정에 포털 주소를 넣는다.** 이것이 콜백 주소의 앞부분이다.

```json
{ "Portal": { "BaseUrl": "https://portal.jsini.co.kr" } }
```

비워 두면 요청에서 만드는데, 운영은 nginx 뒤라 요청이 `127.0.0.1:5557` 로
들어온다. 그 주소를 공급자에게 콜백으로 알려 주면 **사람이 되돌아오지 못한다.**

넣은 뒤 AuthServer 와 셸을 다시 띄운다.

```
dev.bat auth blazor
```

단추는 **최대 5분 뒤에** 선다 — 셸이 공급자 목록을 그만큼 담아 두기 때문이다
(로그인 화면은 모두가 가장 먼저 받는 화면이라 왕복을 아낀다).

---

## 4. 처음 누르면 로그인이 아니라 **가입 신청**이다

이것이 이 기능의 가장 중요한 성질이다.

```
소셜 단추 → 공급자 인가 → 셸 콜백 → AuthServer
                                        │
                        ┌───────────────┴───────────────┐
                 연결된 계정이 있다                  처음 본다
                        │                               │
                    그냥 로그인               승인 대기(PENDING) 계정을 만든다
                                                        │
                                        관리자가 [계정 관리 → 가입 신청] 에서 승인
```

사내 업무 포털이라 아무나 들어와서는 안 되고, 아이디·비밀번호 가입이 이미
승인제다. 소셜만 즉시 통과시키면 **그 승인제를 우회하는 길**이 된다.

만들어진 계정은 아이디·비밀번호로 신청한 것과 **같은 자리에 같은 모양으로**
쌓인다(`account_profile_details` 의 `Status`). 그래서 승인 화면은 아무것도
고치지 않고 그대로 처리한다. 신청자가 적는 「하실 말씀」 자리에는
`구글 계정으로 신청 (name@example.com)` 이 들어가 어디로 들어왔는지가 보인다.

가입 신청 목록에는 **가입 경로**(카카오 · 네이버 · 구글 · 직접 입력)와 공급자가 준
**프로필 사진**도 함께 뜬다. 경로는 연결 표(`account_social_logins`)에서 읽고, 사진
주소는 프로필 상세의 `SocialPicture` 칸에 둔다.

### 프로필 사진이 계정 대표 사진이 된다

같은 사진의 **바이트를 받아 FileServer 에 올리고**(`bizType=PROFILE`, 새 그룹) 계정의
`Avatar` · `avatar_group_id` 에 건다(`SocialAvatarImporter`). 공급자 주소를 `Avatar` 에
그대로 적지 않는 것은 포털이 바깥 주소 사진을 「사진 없음」으로 보기 때문이다.

- 받는 주소는 https · 공급자별 `PictureHosts` · 5MB · `image/*` 를 모두 맞아야 한다.
- FileServer 는 **게이트웨이 없이 직접** 부른다(`FileServer:BaseUrl`, 운영은
  `http://file:8080`). 가입 신청은 익명이라 게이트웨이를 통과할 토큰이 없다.
- 못 옮겨도 신청은 그대로 된다 — AuthServer 로그에 경고가 남고 사진만 빠진다.
- **공급자의 기본 그림은 옮기지 않는다.** 사진을 안 올린 사람에게 카카오·네이버는
  회색 사람 그림을 준다. 카카오는 `PictureIsDefaultPath`(`is_default_image`)로,
  네이버는 알려 주는 칸이 없어 `DefaultPictureUrls`(주소)로 가린다.

로그인 아이디는 `이메일앞부분` → `앞부분_공급자` → `앞부분2` … → `공급자_번호앞10자리`
순서로 비어 있는 것을 쓴다. 네이버 사용자 번호는 43자 무작위 글자라 통째로 쓰지 않는다.

### 알림

신청이 들어오면 `Auth:Signup:NotifyRole`(시스템 관리자)에게 **메일과 앱푸시**가 함께
간다. 메일에는 사진·경로·아이디가 들어간 카드가, 푸시에는 **신청자 사진이 아이콘**으로
실린다. 누르면 `/admin/system/signup` 으로 간다.

### 승인하면 기본 역할이 붙는다

승인과 같은 저장으로 `Auth:Signup:DefaultRoles`(기본 `ALL_USERS`)를 붙인다. 자동 승인
(`AutoApprove`)으로 들어온 사람도 같다. 부서·업무 역할은 여전히 계정 관리에서 붙인다.

로그인 아이디는 이메일 앞부분으로 지어 준다(겹치면 뒤에 숫자를 붙인다).
비밀번호는 **본인도 모르는 무작위 값**이고, 비밀번호로도 들어오고 싶으면
[비밀번호 찾기] 로 정하면 된다.

### 바로 쓰게 하려면

사외 사용자를 받는 포털로 성격이 바뀌면 그때 켠다.

```json
{ "Auth": { "Social": { "AutoApprove": true } } }
```

---

## 5. 이미 계정이 있는 사람은 **연결**한다

그냥 소셜로 로그인하면 서버는 처음 보는 사람으로 읽고 **계정을 하나 더**
만든다. 승인하는 사람은 그것이 같은 사람인지 알 길이 없다.

정상 경로는 **로그인한 뒤에 붙이는 것**이다 —
**내 정보 → 보안 설정 → [소셜 계정으로 로그인]** 의 「구글 연결」 단추.
붙이고 나면 다음부터 로그인 화면의 그 단추로 아이디를 치지 않고 들어온다.
끊는 것도 같은 자리다.

### 이메일로 자동 연결 (기본 꺼짐)

공급자가 **「확인된 주소」라고 말한** 이메일이 기존 계정과 같으면 자동으로
붙이는 설정이 있다.

```json
{ "Auth": { "Social": { "LinkByVerifiedEmail": true } } }
```

**기본이 꺼져 있는 이유**는 이것이 계정 탈취의 고전적인 길이기 때문이다 —
공급자가 이메일을 제대로 확인하지 않으면 남의 이메일로 앱을 하나 만들어 그
사람 계정에 들어갈 수 있다. 켜더라도

- 공급자가 **확인 여부를 알려 주는 경우에만** 붙는다.
  네이버는 그 칸이 없어 **켜도 자동 연결되지 않는다**(모를 때는 안전한 쪽).
- 같은 이메일을 쓰는 계정이 둘 이상이면 붙이지 않는다.

---

## 6. 어디에 무엇이 있나

| 무엇 | 어디 |
|---|---|
| 공급자 설정 (주소 · 응답 칸 이름) | `microservices/AuthServer/appsettings.json` 의 `Auth:Social` |
| 인가 코드 → 신원, 계정 찾기·만들기 | `AuthServer/Services/SocialLoginService.cs` |
| API 여섯 (목록 · 설정 점검 · 인가 주소 · 로그인 · 연결 · 끊기) | `AuthServer/Endpoints/SocialLoginEndpoints.cs` |
| 연결 표 | `scom.account_social_logins` |
| 시도 제한 | `ApiGateway/appsettings.json` 의 `auth-social-*-route` |
| 브라우저가 지나가는 두 자리 | `web/.../Shell/Security/SocialLoginFlow.cs` |
| 단추 | `Login.razor` · `Register.razor` · `Profile.razor` |

### 공급자를 하나 더 붙이려면 — **코드는 안 고친다**

공급자마다 다른 것은 **주소 넷과 응답 JSON 의 칸 이름 넷**뿐이고 흐름은
완전히 같다. 그래서 그 여덟을 전부 설정으로 뺐다. `Providers` 아래에 한 덩이를
더 적고 열쇠를 채우면 단추가 선다.

```json
"github": {
  "DisplayName": "깃허브",
  "ClientId": "", "ClientSecret": "",
  "AuthorizeUrl": "https://github.com/login/oauth/authorize",
  "TokenUrl": "https://github.com/login/oauth/access_token",
  "UserInfoUrl": "https://api.github.com/user",
  "Scope": "read:user user:email",
  "IdPath": "id", "EmailPath": "email", "NamePath": "name",
  "EmailVerifiedPath": ""
}
```

`IdPath` 는 점으로 내려간다(`response.id` · `kakao_account.profile.nickname`).

---

## 7. 안 될 때

| 증상 | 대개 이것 |
|---|---|
| 단추가 아예 안 보인다 | `ClientId` 가 비었다. 넣었으면 5분(담아 두는 시간)을 기다리거나 셸을 다시 띄운다 |
| 공급자 화면이 `redirect_uri_mismatch` · `KOE006` | 콘솔에 등록한 콜백 주소와 `Portal:BaseUrl` 이 다르다 |
| 카카오 `KOE101` | JavaScript 키를 넣었다. **REST API 키**로 바꾼다 |
| 돌아왔는데 「시간이 지나 다시 확인이 필요합니다」가 반복된다 | 상태 쿠키가 안 실린다. https 인데 `Portal:BaseUrl` 이 http 로 적혀 있는지 본다 |
| 「가입 신청을 받았습니다」가 계속 뜬다 | 정상이다. 관리자가 [계정 관리 → 가입 신청] 에서 승인해야 한다 |
| 승인했는데 이메일이 안 온다 | 공급자에서 이메일 동의 항목을 안 켰다. 계정 관리에서 직접 넣어 준다 |
| 「이미 다른 계정에 연결된 소셜 계정입니다」 | 그 소셜 계정이 다른 포털 계정에 붙어 있다. 그쪽에서 먼저 끊는다 |

### 먼저 볼 곳 — [MSA 서버 상태] 의 「소셜 로그인」

**`/admin/status/server` 를 연다.** 공급자 카드가 셋 다 있고, 각각에
`ClientId 있음/없음` · `ClientSecret 있음/없음` · 콘솔에 등록할 콜백 주소가
그대로 적혀 있다. 열쇠 값은 안 나온다 — 있는지 없는지만 나온다.

이 구역이 따로 있는 까닭은 **증상이 조용해서**다. 열쇠를 안 넣으면
가입 화면에 단추가 한 개도 안 서는데, 화면은 멀쩡히 200 을 주고 헬스체크도
전부 초록이다. 그래서 「아직 안 만든 기능」으로 읽히기 쉽다 — 실제로 그렇게
읽힌 적이 있다. 갈래는 셋이고 할 일이 서로 다르다.

| 카드가 말하는 것 | 할 일 |
|---|---|
| 「소셜 로그인이 꺼져 있습니다」 | `Auth:Social:Enabled` 가 `false` 다 |
| 「쓸 수 있는 공급자가 없어 …」 | `ClientId` 를 넣는다 (위 3장) |
| `ClientId 있음` · `ClientSecret 없음` | **단추는 선다.** 다녀온 뒤 토큰 교환에서만 깨진다 |

읽어 오는 곳은 `GET /api/auth/social/config-status` 이고 로그인한 사람만 볼 수 있다.

로그는 AuthServer 쪽에 남는다 — `{Provider} 토큰 교환에 실패했다` ·
`{Provider} 프로필에서 사용자 번호(...)를 찾지 못했다` 가 설정을 짚어 주는 두 줄이다.

---

## 8. 기능을 통째로 되돌리려면

코드를 건드리지 않고 끄는 길이 둘이다.

```json
{ "Auth": { "Social": { "Enabled": false } } }   // 전부
```

특정 공급자만 빼려면 그 `ClientId` 를 비우면 된다.
