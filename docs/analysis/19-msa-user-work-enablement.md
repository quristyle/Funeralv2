# 공통 인증 사용자가 이식 시스템에서 업무를 처리할 수 있게 하기

작성: 2026-08-23 (자율 진행 기록)
대상: 헬프데스크(HelpDeskServer) · 프로젝트관리(ProjMngServer)

> 지시: "MSA 에는 헬프데스크 시스템이 있다. 이 시스템은 이전에 단독으로 구동되던 시스템이였기에
> 자체 로그인 사용자의 정보를 바탕으로 업무가 관리 보관되었었다. 이것을 이식하면서 일부 확인
> 수정하였지만 많은 부분이 남아 있을것이다. 단계별로 확인 하면서 공통에서 관리되는 인증시스템의
> 사용자가 업무를 처리할수 있도록 개선하라. (…) ProjMngServer 도 같은 개념이다."

앞선 작업([15-jsini-user-unification.md](15-jsini-user-unification.md))은 **로그인 신원**을
포털로 통일했다. 이번에 남아 있던 것은 **업무 처리**다 — 로그인은 되지만 일을 할 수 없었다.

---

## 0. 무엇이 문제였나 — 실측

측정은 짐작이 아니라 실제 서버·실제 토큰으로 했다. 계정 세 개를 골랐다.

| 계정 | 포털 역할 | 계정 연결 |
|---|---|---|
| `quristyle` | SYSTEM_ADMINISTRATOR | 있음 (담당자 #4) |
| `vben` | ADMINISTRATOR | **없음** |
| `hd_kdh` | PARTNER | **없음** |

포털 계정 46개 중 `jsini.auth_user_links` 에 연결된 것은 **1개**였다.
즉 사실상 한 사람만 헬프데스크를 쓸 수 있었다.

### 개선 전

```
                                              quristyle   vben        hd_kdh
GET /api/helpdesk/users/info                  200         404         404
GET /api/helpdesk/auth-links/me               200         400         400
GET /api/helpdesk/users                       200         403         403
GET /api/helpdesk/customers                   200         200 ←전체   200 ←전체
GET /api/helpdesk/dashboard/admin-stats       200         401         401
GET /api/helpdesk/dashboard/my-company-stats  200 ←유출   401         401
GET /api/helpdesk/comments/my                 200         400         400
GET /api/helpdesk/user-properties             200         401         401
```

### 개선 후

```
                                              quristyle   vben        hd_kdh
GET /api/helpdesk/users/info                  200         200         200
GET /api/helpdesk/auth-links/me               200         200         200
GET /api/helpdesk/users                       200         200         403 ✓의도
GET /api/helpdesk/customers                   200         200         200 ←빈 목록 ✓
GET /api/helpdesk/dashboard/admin-stats       200         200         403 ✓의도
GET /api/helpdesk/dashboard/my-company-stats  403 ✓고침   403         403
GET /api/helpdesk/comments/my                 200         200 ←빈    200 ←빈
GET /api/helpdesk/user-properties             200         200         200
```

---

## 1. 핵심 — '볼 수 있는가' 와 '내 것인가' 를 나눴다

전에는 하나로 겸했다. **연결이 없으면 조회조차 막혔다.**

두 가지는 성격이 다르다.

| | 무엇이 정하는가 | 예 |
|---|---|---|
| **볼 수 있는가** | 포털 역할 | 담당자 목록, 전체 요청 현황, 고객 목록 |
| **내 것인가** | 계정 연결 | 내가 쓴 댓글, 나에게 배정된 요청, 내 알림 구독 |

인증·권한을 포털이 단독으로 맡는다면 **전자는 포털 역할이 정해야 한다.**
후자만 연결이 필요하다 — 기존 데이터가 헬프데스크 내부 숫자 ID 를 참조하기 때문이다.

### 새 창구 — `Services/HelpdeskPrincipal.cs`

```csharp
var me = http.GetHelpdeskPrincipal();

me.IsAdmin          // 담당자 권한 (연결이 admin 이거나 포털 역할이 관리자)
me.IsLinked         // 헬프데스크 레코드에 이어져 있는가
me.IsUnlinkedAdmin  // 권한은 있고 연결만 없는 상태
me.HelpdeskUserId   // '내 것' 을 가리킬 때만 쓴다
me.CompanyId
me.JsiniRoles
```

담당자로 대우할 포털 역할은 설정으로 정한다.

```json
"HelpdeskIdentity": { "AdminRoles": [ "SYSTEM_ADMINISTRATOR", "ADMINISTRATOR" ] }
```

프론트도 같은 구분을 갖는다 — `store/helpdesk.ts` 의 `isAdmin` / `isLinked` /
`isUnlinkedAdmin` / `canUse`. **화면을 열지 말지는 `canUse` 로 판단하고
`helpdeskUserId` 로 판단하지 않는다.**

---

## 2. 고친 지점

### 2-1. 🔴 데이터 유출 둘

**`/dashboard/my-company-stats` · `/my-monthly-stats`** — `uid` 를 `login_type` 확인 없이
고객 ID 로 썼다. `uid` 는 담당자에게도 붙는 값이다.

```
담당자 #4(사용자A) 가 호출  →  고객 #4 의 회사 통계가 나온다
```

운영 데이터로 재현했다. 서로 다른 사람의 자료다. 이제 고객으로 연결된 계정만 받는다(403).

**`/api/customers`** — `login_type == "customer"` 일 때만 회사로 좁히고 **그 밖에는 전부**
반환했다. 연결이 없는 계정에는 `login_type` 자체가 없으므로, 권한 없는 사용자가
고객 27명 전원을 받아 갔다. 판정하지 못했을 때 열리는 방향이 반대였다.

이제 담당자면 전체, 고객이면 자기 회사, **그 밖에는 빈 목록**이다.

### 2-2. 차단 지점을 열었다

| 지점 | 전 | 후 |
|---|---|---|
| `GET /api/users` (사용자 목록) | `login_type=="admin"` 만 → 403 | `IsAdmin` |
| `GET /api/users/info` | `uid` 없으면 **null**(404) | 포털 신원 + `linked:false` |
| `GET /api/auth-links/me` | 예외(400) | 200 + `linked` / `isAdmin` / `adminByRole` |
| `GET /api/dashboard/admin-stats` | 401 | `IsAdmin` 이면 200. 연결 없으면 '본인 배정' 은 0, 미배정은 전체 범위(`pendingScope`) |
| `GET /api/comments/my` | 예외(400) | 빈 목록 (연결 없는 계정은 쓴 댓글이 없다) |
| `GET /api/push/notifications`, `/my-notifications` | 401 | 빈 목록 |
| `GET /api/user-properties` | 401 | 기본값 + `linked:false` |

**401 을 쓰지 않은 이유**가 있다. 프론트 인터셉터가 401 을 '토큰 만료' 로 보고 로그아웃시켜
증상이 엉뚱해진다. 상태를 알리는 것이 목적이면 200(빈 값) 이나 403/409 가 맞다.

### 2-3. 저장이 불가능한 것은 조용히 버리지 않는다

| 지점 | 처리 |
|---|---|
| `POST /api/push/subscribe` | 409 + 이유 (구독은 내부 ID 로 저장된다) |
| `PUT /api/user-properties` | 409 + 이유 (아래 Q11) |

### 2-4. 🟠 요청 작성자 위조

`POST /api/requests` 가 `CustomerId` 를 **폼 값**으로 받았다. 고객이 **남의 회사 이름으로**
요청을 만들 수 있었다. 담당자는 대신 등록할 일이 있으므로 그대로 두고,
고객으로 연결된 계정은 자기 것으로 고정한다.

(작성자 `CreatedBy` 는 앞선 작업에서 이미 고쳤다. 이번은 요청의 **주인**이다.)

### 2-5. 화면 안내가 사실과 달랐다

"연결이 없으면 자료를 볼 수 없습니다" 는 관리자 역할 계정에는 틀린 말이다.
이제 두 상황을 나눠 적는다.

- **연결 없음 + 관리자 역할** → 정보(파란색). "조회·관리는 그대로 하실 수 있습니다.
  다만 '내 것' 을 가리키는 기능은 비어 있습니다."
- **연결 없음 + 권한 없음** → 경고(노란색). 연결하라고 안내.

`org/profile.vue` · `system/account-link.vue` 의 '구분' 표시도 고쳤다 —
연결이 없는데 `담당자` 라고 적어 "내 배정 요청이 왜 비어 있나" 를 알 수 없게 만들었다.

---

## 3. 남아 있던 진짜 문제 — '데이터 속의 사람'

역할로 조회 권한은 열렸지만, **자기 자신으로서 일하는 것**은 여전히 연결이 필요하다.
그리고 연결은 46개 중 1개였다.

이관 스크립트가 남긴 기록이 답이었다.

### `MsaSource` — 추정이 아닌 확정 대응

`docs/sql/msa_user_import.sql` 이 계정을 만들 때 출처를 함께 적어 두었다.

```
scom.account_profile_details.detail_type = 'MsaSource'
  helpdesk:admin:4          → jsini.admin.id = 4
  helpdesk:customer:17      → jsini.customer.id = 17
  projmng:dev_user:jskim    → projmng.dev_user.user_id = 'jskim'
```

이것은 **아이디·이메일 대조와 성격이 다르다.** 그 둘은 "값이 같으니 같은 사람이겠지" 라는
추정이고 실제로 오탐이 있었다(포털 `admin` 과 헬프데스크 `admin` 은 다른 사람).
`MsaSource` 는 그 계정이 만들어진 근거 그 자체다.

### 신원 경로에 실었다

```
AuthServer 토큰 클레임 MsaSource
   → ApiGateway 헤더 X-User-Msa-Source
      → HelpDeskServer  : auth_user_links 다음 순위로 원본 레코드를 찾는다
      → ProjMngServer   : req_ss_user_id 를 projmng 아이디로 바꾼다
   → AuthServer /user/info 의 msaSource (화면이 자기 저쪽 아이디를 안다)
```

우선순위는 **사람이 이어 준 연결이 언제나 먼저**다.

```
1. auth_user_links   (사람이 확인하고 이어 준 값)
2. MsaSource         (이관 당시 기록)        ← 새로 추가, 기본 꺼짐
3. 로그인 아이디 대조 (추정)                  기본 꺼짐
4. 이메일 대조       (추정)                  기본 꺼짐
```

### 검증 (설정을 켜고 실측)

```
MatchByMsaSource=true
  hd_kdh     → linked=true  admin  #6  사용자B     (helpdesk:admin:6)
  hd_admin   → linked=true  admin  #4  사용자A     (helpdesk:admin:4)
  quristyle  → linked=true  admin  #4  사용자A     (명시적 연결이 우선 — MsaSource 없음)

Identity__UseMsaSource=true (ProjMng)
  [신원] 포털 계정 pm_jskim  → 프로젝트관리 사용자 jskim
  [신원] 포털 계정 pm_kggmvp → 프로젝트관리 사용자 kggmvp
```

**두 스위치는 기본 꺼짐이다.** 켜면 영향 범위가 크기 때문이다 → Q9 · Q10.

### 다만 '읽기' 는 스위치 없이 이미 고쳐졌다

프로젝트관리는 두 갈래를 나눠 두었다. 성격이 다르기 때문이다.

| | 스위치 | 이유 |
|---|---|---|
| **화면이 자기 레코드를 찾는 것** | 없음 (바로 적용) | 아무것도 못 찾던 것이 맞는 것을 찾는다. 정책 판단이 아니라 고침이다 |
| **`req_ss_user_id` 로 넘기는 값** | `Identity:UseMsaSource` | **감사 컬럼에 쌓이는 값이 바뀐다.** 되돌릴 수 없는 흔적이라 사람이 정해야 한다 |

전자는 `/auth/user/info` 의 `msaSource` 를 프론트(`use-jsini-user.ts` 의 `projmngUserId`)가
읽어 쓴다. 그래서 `pm_jskim` 은 지금 바로 자기 레코드를 본다(6절 확인).

---

## 4. 프로젝트관리에서 발견한 모순

앞선 결정 Q3 은 **A(포털 아이디 = 프로젝트관리 아이디로 맞춘다)** 였다.
그런데 같은 작업의 사용자 이관은 아이디 충돌을 피하려고 `pm_` 접두어를 붙였다.
**두 결정이 서로 어긋난다.**

```
projmng.dev_user   bmkim  hsstyle  jjstyle  jskim  kggmvp  kspark  quristyle  sglee  yws
포털 계정          pm_bmkim  pm_hsstyle  pm_jjstyle  pm_jskim  pm_kggmvp  pm_kspark  (quristyle)  pm_sglee  pm_yws
```

9명 중 **`quristyle` 한 명만** 아이디가 맞는다. 나머지 8명은

- `req_ss_user_id` 가 존재하지 않는 사용자를 가리킨다 (저장 프로시저의 감사 값)
- '내 프로젝트 정보' 화면이 빈 채로 뜬다

`MsaSource` 로 메꿀 수 있게 해 두었다(위 3절). 어느 쪽으로 갈지는 Q10.

### 곁들여 확인한 것 — 프로젝트관리는 사용자별 범위 구분이 없다

같은 프로시저를 서로 다른 계정으로 불러 비교했다.

```
                       quristyle   hd_kdh
sp_dev_proj_exec       7건         7건
sp_home_todo_exec      130건       130건
sp_dev_user_exec       9건         9건
sp_dev_menu_exec       38건        38건
```

**모두 같다.** 원본이 개발팀 내부 도구라 "전부 보이고 조건으로 걸러 쓴다" 는 설계다.
그래서 프로젝트관리는 연결이 없어도 조회 업무 자체는 막히지 않는다.
다만 이것이 의도인지 확인이 필요하다 → Q12.

---

## 5. 손대지 않은 것

- **헬프데스크·프로젝트관리 DB.** 스키마·데이터 모두 그대로다. 이번 변경은 전부 코드·설정이다.
- **`jsini.auth_user_links` 의 기존 1건.** 사람이 이어 준 값이라 우선순위 1위로 남겼다.
- **JinReception.** 이 저장소 밖이다.
- **프로젝트관리의 담당자 선택 목록.** `projmng.dev_user` 를 그대로 쓴다(업무 조직 데이터).

---

## 6. 확인한 것

```
dotnet build (7개 서비스)              오류 0
vite build --mode production           성공
vue-tsc (변경 파일)                     오류 0
```

실제 서버·실제 토큰으로 계정 3개 × 엔드포인트 8개를 개선 전후로 측정했다(0절 표).
`MsaSource` 경로는 설정을 켜고 따로 확인했다(3절).

### 브라우저로 화면까지 확인했다

로그인해서 계정별로 눈으로 봤다(게이트웨이·인증·헬프데스크·프로젝트관리·파일 + 프론트 기동).

| 계정 | 화면 | 결과 |
|---|---|---|
| `vben` (ADMINISTRATOR, 연결 없음) | 요청 처리 | **목록이 뜬다** (전에는 화면 자체가 렌더되지 않았다). 파란 안내 문구 |
| | 요청 모니터 | 회사별 현황 8개사 실제 수치. '나의 접수' 는 0 — 연결이 없으니 맞다 |
| | 내 프로필 | 구분 = `연결 없음 · 포털 역할로 담당자 권한` |
| | 계정 연결 | "포털 관리자 역할로 조회·관리하고 있습니다" + 연결 목록 1건 |
| | 내 프로젝트 정보 | `vben` 레코드 없음 안내 (출처 기록이 없어 포털 아이디로 찾음 — 맞다) |
| `pm_jskim` (PARTNER) | 내 프로젝트 정보 | **`jskim` / 사용자D 레코드를 찾았다** (전에는 '레코드 없음') |
| | 요청 처리 | 노란 경고 + 화면 닫힘 — 담당자 권한이 없으니 맞다 |
| `quristyle` (연결된 담당자) | 요청 모니터 | 나의 접수 552/699 · 완료율 96% — **개선 전과 같다** |
| | 내 댓글 | 댓글 목록 정상 — 회귀 없음 |

---

## 7. 결정이 필요한 것

### Q9. 헬프데스크 `MatchByMsaSource` 를 켤 것인가 🟠

**켜면** 이관 계정 34개가 각자의 원본 헬프데스크 레코드로 해석된다.
`hd_kdh` 는 담당자 사용자B(#6)으로, `hd_a0516z` 는 고객으로. 자기 데이터를 갖고 일할 수 있다.

**이것이 지시의 핵심에 가장 가까운 항목이다.** 다만 두 가지를 함께 판단해야 한다.

1. **포털 역할과 헬프데스크 권한이 어긋난다.** `hd_kdh` 의 포털 역할은 `PARTNER` 인데
   헬프데스크에서는 **담당자**로 해석된다(원본 레코드가 담당자이므로). 담당자 7명이
   이렇게 된다. 포털 역할이 권한의 정본이라면 이 계정들의 역할을 올려 주는 편이 일관된다.
2. **비밀번호가 여전히 아이디와 같다.** 이관 시 지시대로 넣은 값이다
   ([15-jsini-user-unification.md](15-jsini-user-unification.md) 11절). 아이디를 아는 사람이
   그 사람으로 로그인해 그 사람의 업무 데이터를 다룰 수 있게 된다.

| | 방법 | 비고 |
|---|---|---|
| **A** | 먼저 비밀번호를 잠그고(`password='!'`) 쓸 사람만 지정한 뒤 켠다 | 안전. 한 줄 + 계정별 지정 |
| B | 지금 바로 켠다 | 34명이 즉시 일할 수 있다. 위 2번이 열린 채로 남는다 |
| C | 켜지 않고 `auth_user_links` 를 채운다 | [`msa_user_link.sql`](../sql/msa_user_link.sql) 34건이 준비돼 있다. 헬프데스크 DB 쓰기 |
| D | 현행 유지 (연결 1건) | 한 사람만 쓴다 |

**의견: A.** 아래 한 줄을 먼저 실행하고, 실제로 쓸 사람에게만 비밀번호를 지정한 뒤
`MatchByMsaSource` 를 켜는 순서를 권합니다.

```sql
UPDATE scom.accounts SET password = '!' WHERE created_by = 'msa-user-import';
```

> C 와 A 는 결과가 거의 같습니다. 차이는 **연결의 출처를 어디에 둘 것인가** 입니다 —
> C 는 헬프데스크 DB 에 행으로 남기고, A 는 포털의 출처 기록을 그때그때 해석합니다.
> 사람이 나중에 연결을 바꿀 일이 있다면 C 가 눈에 보여 다루기 쉽습니다.

### Q10. 프로젝트관리 아이디를 어떻게 맞출 것인가 🟠

4절의 모순이다. 8명이 자기 레코드를 못 찾는다.

| | 방법 | 비고 |
|---|---|---|
| **A** | `Identity:UseMsaSource=true` 로 켠다 | 코드·설정만. **감사 컬럼에 쌓이는 값이 `pm_jskim` → `jskim` 으로 바뀐다** |
| B | 포털 계정 아이디에서 `pm_` 를 뗀다 | 결정 Q3-A 를 실제로 지킨다. **다른 출처와 충돌한다** (`kggmvp` 는 헬프데스크에도 있다) |
| C | `projmng.dev_user.user_id` 를 포털 아이디로 바꾼다 | 프로젝트관리 DB 변경. 기존 업무 데이터의 참조가 전부 걸린다 |
| D | 현행 유지 | 8명은 계속 못 찾는다. 감사 값이 계속 어긋난다 |

**의견: A.** 되돌릴 수 있고(설정 한 줄), 원본 두 DB 를 건드리지 않습니다.
B 는 이관 때 접두어를 붙인 이유(충돌)를 되살립니다.

> 켜면 그 시점 이후의 감사 값만 바뀝니다. 이전에 쌓인 `pm_*` 값은 그대로 남아
> 두 형식이 섞입니다 — 15절 Q1 과 같은 성격의 문제입니다.

### Q11. 헬프데스크 개인 설정을 어디에 둘 것인가 🟡

`jsini.userproperty` 는 사용자 키가 **정수**(`admin.id` / `customer.id`)다.
포털 로그인 아이디는 문자열이라 넣을 자리가 없어, 연결이 없는 계정은 저장할 수 없다.

지금은 조회 시 기본값 + `linked:false`, 저장 시 이유가 적힌 409 다.

| | 방법 | 비고 |
|---|---|---|
| **A** | 포털 개인 설정으로 옮긴다 | 계정 설정은 포털 소관이라는 원칙에 맞다. 헬프데스크 화면이 포털 API 를 봐야 한다 |
| B | `userproperty` 에 문자열 키 컬럼을 추가한다 | **헬프데스크 DB 변경** |
| C | 현행 유지 (연결된 계정만 저장) | Q9 를 켜면 대부분 해결된다 |

**의견: Q9 을 먼저 정하고 보면 C 로 충분할 수 있습니다.** 남는 것은 연결이 없는
관리자 계정뿐이고, 그 계정에 알림 설정이 필요한지가 판단 기준입니다.

### Q12. 프로젝트관리에 사용자별 범위가 필요한가 🟡

4절에서 확인했다 — 계정이 달라도 보이는 자료가 같다(프로젝트 7건, 할일 130건 전부).
원본이 개발팀 내부 도구라 그렇게 설계된 것으로 보인다.

이관 계정 42개에 `PARTNER` 역할이 배정돼 있고([15절](15-jsini-user-unification.md) Q6),
프로젝트관리 메뉴는 그 역할에 막혀 있어 지금은 드러나지 않는다.
**메뉴 권한이 유일한 방어선**이라는 뜻이다.

| | 방법 | 비고 |
|---|---|---|
| **A** | 현행 유지 + 메뉴 권한으로 막는다 | 지금 상태. 원본 설계를 그대로 둔다 |
| B | 프로시저에 참여 프로젝트 기준 범위를 넣는다 | **프로젝트관리 DB(프로시저) 변경** |

**의견: A.** 다만 프로젝트관리 메뉴를 어느 역할에 열어 줄지는 의식적으로 정해야 합니다.

### Q13. 담당자로 대우할 포털 역할 🟡

`HelpdeskIdentity:AdminRoles` 를 `SYSTEM_ADMINISTRATOR` · `ADMINISTRATOR` 로 두었다.
프로젝트관리의 `DevTools:RawSqlRoles` 와 같은 값이라 일관된다.

헬프데스크 담당자 업무를 볼 역할을 따로 두고 싶다면(예: `HELPDESK_AGENT`)
역할을 만들고 이 목록에 더하면 된다. **의견: 지금 값으로 시작.**

---

## 8. 관련 문서

- [15-jsini-user-unification.md](15-jsini-user-unification.md) — 로그인 신원 통일 (선행 작업)
- [14-account-msa-linking.md](14-account-msa-linking.md) — 계정 대조 화면
- [12-decisions-pending.md](12-decisions-pending.md) — 결정 대기 목록
- [13-projmng-migration.md](13-projmng-migration.md) — 프로젝트관리 이식

---

## 9. 이식 화면 제거 — 고객 사용자 · 회사 (2026-08-23)

> 지시: "`/helpdesk/org/customer` 는 불필요한 화면이다. 프론트·백엔드·메뉴데이터만 제거하라.
> **헬프데스크가 사용하는 DB 는 절대 제거하거나 수정하지 마라.**"
> 이어서 "`/helpdesk/org/company` 도 제거 대상이다. 다만 헬프데스크에서 데이터를 읽어
> `/system/company` 가 쓰는 DB 테이블에 회사데이터로 추가해 줘."

### 제거한 것

| 대상 | 조치 |
|---|---|
| `views/helpdesk/org/customer.vue` | 파일 삭제 |
| `views/helpdesk/org/company.vue` | 파일 삭제 |
| `createCustomer` · `updateCustomer` · `deleteCustomer` · `searchCustomers` · `getCustomer` | 제거 (이 화면 전용) |
| `createCompany` · `updateCompany` · `deleteCompany` · `searchCompanies` · `getCompany` | 제거 (이 화면 전용) |
| `POST/PUT/DELETE /api/customers` | 제거 |
| `POST/PUT/DELETE /api/companys` | 제거 |
| 메뉴 `HD_ORG_CUSTOMER` · `HD_ORG_COMPANY` | 삭제 (270 → 268건). `role_menus` 는 CASCADE 로 함께 정리됐다 |

### 남긴 것과 그 이유

지우기 전에 **누가 쓰는지 전수 조사**했다. 이름이 같은 함수가 헬프데스크와 포털에 각각 있어
import 출처로 갈라 확인했다.

| 남긴 것 | 쓰는 곳 |
|---|---|
| `GET /api/customers` (`getCustomerList`) | 요청 화면들의 고객 셀렉트(`store/helpdesk.ts`) · 계정 대조 화면(`api/portal/system/msa-users.ts`) |
| `getCustomerByLoginId` | 요청 등록 화면 — 회사별 `pub_*` 공용 계정을 작성자로 쓴다 |
| `GET /api/companys` (`getCompanyList`) | 요청 화면들의 회사 셀렉트 · 팀-회사 매핑 화면 |

즉 남은 것은 전부 **읽기**이고, 대상을 '관리'하는 것이 아니라 업무 데이터에서 **가리키기 위한** 것이다.

`POST /api/customers/srch` · `GET /api/customers/{id}` · 회사 쪽 `srch` · `{id}` 는
**건드리지 않았다.** 프론트에 쓰는 곳이 없지만 이 화면들의 것이 아니고, 아직 살아 있을지 모르는
JinReception 이 쓸 수 있다. 정리해도 되는지는 판단이 필요하다 → Q14.

### 헬프데스크 DB 는 손대지 않았다

지시대로 `jsini.customer` · `jsini.company` 의 스키마·데이터 모두 그대로다.
기존 요청·댓글·팀 데이터가 그 행들을 참조하고 있어 지울 수도 없다.

### 회사 데이터 이관 (9건)

헬프데스크 회사를 포털 `scom.companies` 로 옮겼다. 두 DB 가 서로 달라(`jinrecept` ↔ `jsiniportal`)
SQL 한 장으로는 못 옮기므로 **각 서비스의 API 를 경유**했다(14절의 방식과 같다).

```
한주 · 회원가입 · 진네트웍스 · 접수시스템 · 미러포트 · GHUB · SogoMail · 그리드위즈 · InCom
포털 회사 5건 → 14건
```

되돌릴 수 있게 각 행의 `remark` 에 출처를 남겼다 — `helpdesk:company:<원본ID>`.

```sql
-- 되돌리기
DELETE FROM scom.companies WHERE remark LIKE 'helpdesk:company:%';
```

헬프데스크 회사는 `Name` 하나뿐이라 사업자번호·주소 등은 비어 있다.

#### ⚠ 한 번 깨졌다가 다시 넣었다

PowerShell 로 등록했더니 한글이 `?` 로 저장됐다(`한주` → `??`). 본문을 기본 인코딩으로
보내서다. 9건을 지우고 **UTF-8 바이트로 직접** 보내 다시 넣었다. 지금은 정상이다.

---

## 10. 새로 생긴 판단거리

### Q14. 쓰는 곳이 없어진 조회 엔드포인트를 지울 것인가 🟡

`POST /api/customers/srch` · `GET /api/customers/{id}` · `POST /api/companys/srch` ·
`GET /api/companys/srch` · `GET /api/companys/{id}` — 프론트에 쓰는 곳이 없다.
JinReception 사용 여부를 확인해 주시면 함께 정리하겠다(D3 · Q4 와 같은 성격).

### Q15. 「회원가입」은 회사가 아니다 🟡

이관한 9건에 `회원가입` 이 있다. 회사명이 아니라 이식 전 시스템의 자리표시 값으로 보인다.
지시대로 전부 옮겼지만 포털 회사 목록에 이대로 두면 셀렉트에 섞인다.

**의견: 지우거나 비활성으로 내리는 편이 낫습니다.** 한 줄이면 된다.

### Q16. 「한주」와 「한주유틸리티」는 같은 회사인가 ✅ **같은 회사 (2026-09-04)**

> 지시: "같은 회사이다."

**정본 = 한주유틸리티** 로 병합했다(`docs/sql/company_hanju_merge.sql`, 실행 완료).
근거: 포털 참조(계정 10 · 부서 3)가 전부 이쪽이고, 이관 행의 참조는 0 이었다.
헬프데스크 매핑(`helpdesk:company:1`)은 코드 소비자가 없는 출처 표식임을 전수 검색으로
확인한 뒤 정본의 remark 로 옮겼다 — 요청·팀 데이터가 참조하는 헬프데스크 회사 ID=1
과의 연결 고리가 보존된다. 이관 행은 소프트 삭제(병합 기록 remark).
반복 실행 안전을 실제 두 번 실행으로 확인했다.

<details><summary>원래 적어 둔 내용</summary>

헬프데스크에서 옮긴 `한주` 와 포털에 원래 있던 `한주유틸리티` 가 나란히 있다.
이름이 달라 자동 병합하지 않았다(이 저장소가 계속 피해 온 **추정**이다).
같은 회사라면 하나로 합쳐야 하는데, 요청 데이터가 헬프데스크 회사 ID 를 참조하므로
어느 쪽을 정본으로 둘지 사람이 정해야 한다.

</details>

### Q17. 포털에서 만든 회사에는 `pub_*` 공용 고객이 생기지 않는다 ✅ **C 확정 (2026-09-04)**

> 지시: "C 로 처리해줘." — **현행 유지.** 코드 변경 없음.
> 새로 만든 회사로는 관리자 대행 등록이 되지 않는 것이 확정 동작이다.
> 대행 등록이 필요해지면 그때 A(공용 계정 없으면 담당자 자신을 작성자로)를 다시 본다.

제거한 헬프데스크 회사 등록 로직은 회사를 만들 때 `pub_<회사ID>` 공용 고객까지 함께 만들었다.
그 계정은 **관리자가 회사를 대신해 요청을 등록할 때 작성자로 쓰인다**
(요청 등록 화면의 `getCustomerByLoginId`).

이제 회사는 포털에서 만드는데 포털은 헬프데스크 고객을 만들지 않는다. 그래서
**앞으로 새로 만든 회사로는 관리자 대행 등록이 안 된다.**

| | 방법 | 비고 |
|---|---|---|
| **A** | 요청 등록 시 공용 계정이 없으면 담당자 자신을 작성자로 둔다 | 코드만. 헬프데스크 DB 를 건드리지 않는다 |
| B | 포털 회사 등록 시 헬프데스크 고객을 함께 만든다 | **헬프데스크 DB 쓰기** — 이번 지시에 어긋난다 |
| C | 현행 유지 | 새 회사는 대행 등록 불가 |

**의견: A.** 다만 작성자가 달라지는 일이라 확인이 필요합니다.
