# 개발자 관리를 포털 계정관리로 합친다

WBS 대시보드의 [개발자 관리](`/projmng/wbs/dev-users`)를 걷어내고, 그 화면이
쓰던 속성을 **포털 계정관리**(`/admin/system/account`)에서 다루도록 옮긴다.

상태: **구현 완료** (2026-09-23)

---

## 왜 옮기나

사람을 두 곳에서 관리하고 있다.

| | 어디 | 무엇 |
|---|---|---|
| 포털 계정 | `jsiniportal` DB · `scom.accounts` | 로그인 · 이름 · 부서 · 역할 · 얼굴 |
| 개발자 명부 | `projmng` DB · `projmng.wbs_user` | 사번 · 직급 · 장비 · 계정 발급 현황 |

같은 사람이 두 줄이고 잇는 것은 손으로 채우는 칸(`wbs_user.login_id`) 하나다.
**둘이 어긋나면 어느 쪽이 맞는지 알 방법이 없다.**

---

## 정한 것 둘

### ① 원장의 담당자 칸이 포털 계정을 가리킨다

`projmng.wbs_work.user_bp_id` 가 **사번 대신 로그인 아이디**를 담는다.

그래야 하는 까닭 — 명부를 없애면 **사번을 이름으로 바꿀 길이 끊긴다.**
원장은 `projmng` DB 에 있고 계정은 `jsiniportal` DB 에 있어서 SQL 조인이
아예 불가능하다(같은 PostgreSQL 인스턴스지만 데이터베이스가 다르다).

그래서 역할을 나눈다.

```
서버   계정 아이디로만 집계한다 (조인 없음)
화면   이름·얼굴을 포털에서 붙인다 (BizOptions · UserFaceClient — 이미 쓰는 길)
```

**지금이 가장 싼 때다.** 원장도 명부도 0건이라 옮길 자료가 없다. 사내 자료를
들여올 때 사번 → 계정 대조를 한 번 하면 끝난다.

### ② 속성은 DDL 없이 얹는다

`scom.account_profile_details` 가 **열쇠-값 표**다
(`detail_type` · `content`). 지금도 `Email` · `Phone` · `Avatar` ·
`Watermark` · `Status` 가 그렇게 들어 있다.

그래서 칸을 스물다섯 개 만들지 않고 **`Dev.` 접두사로 묶은 확장 속성** 한
벌로 다룬다.

```
Dev.BpId · Dev.Position · Dev.EmergTel · Dev.NotebookNo · Dev.Git · …
```

- 서버는 `Dev.` 로 시작하는 것만 읽고 쓴다 — 다른 내부 속성
  (`MsaCompany` · `SecurityEmail` 따위)이 관리 화면에 새어 나오지 않는다.
- 속성을 더할 때 **목록에 한 줄**만 보태면 된다. 마이그레이션이 없다.

---

## 옮길 속성 (사용자 결정 — 넷 다 가져간다)

| 묶음 | 속성 |
|---|---|
| 업무 | 사번 · 직급 · 비상연락처 |
| 장비 대장 | 노트북 번호·확인번호 · 모니터 3대(번호·확인번호) · MAC · 사용 IP · 허브/HDMI |
| 계정 발급 현황 | git · startkit · dxb · VM · aipro · claudecode · dev DB · wiki · projectview · svn · notebook |
| 옷 치수 | 하계 · 동계 |

이메일·연락처·생일은 **계정에 이미 있다** — 옮기지 않는다.

`Dev.BpId`(사번)는 옮긴 뒤에도 남긴다. 사내 자료를 들여올 때 **사번 → 계정
대조 열쇠**로 쓰이고, 그 뒤로는 사람을 찾는 단서로 남는다.

---

## 한 일 넷

### 1. AuthServer — `Dev.*` 읽기·쓰기

`Services/UserService.cs` 의 `SyncDevAttributes` 가 접두사 한 벌을 다룬다.
`AccountDto`·`UpdateAccountDto` 에 `DevAttributes`(사전) 한 칸이 났다.

- **사전을 주면 그것이 그 계정의 전부다** — 빠진 열쇠는 지운다. 칸을 비워
  저장하는 것이 「지운다」는 뜻이어야 하기 때문이다.
- **`null` 은 「건드리지 않음」이다** — 이 속성을 모르는 화면이 저장해도 값이
  사라지지 않는다. 사진·생일·워터마크와 같은 규칙이다.
- 빈 글자는 담지 않는다. 담아 두면 「안 적었다」와 「비워 두기로 했다」가 같은
  그림이 된다.

### 2. 계정관리 화면 — 네 묶음

`web/src/Apps/JSini.Web.Admin/Components/Pages/UserList.razor` 의 편집 창에
접는 구역 넷이 붙었다(업무 · 장비 대장 · 계정 발급 현황 · 옷 치수).
**닫힌 채로 시작한다** — 계정을 만드는 사람 대부분은 이 값을 안 적는다.

열쇠 글자는 `Api/AdminModels.cs` 의 `DevAttributeView` 한 곳에만 있다.
화면이 사전을 열쇠로 직접 바인딩하면 그 글자가 스물몇 곳에 흩어지고,
오타가 오류가 아니라 **조용히 빈 칸**으로 나온다.

### 3. ProjMngServer — `wbs_user` 조인을 걷어냈다

열한 곳이었다. 집계는 이제 아이디로만 묶고 이름 칸은 **안 채운다.**

화면 설정(`wbs_user_pref`)의 주인 찾기도 함께 바뀌었다 — `login_id → bp_id`
단계가 사라지고 **로그인 아이디가 곧 열쇠**다. 이미 사번으로 담긴 설정은 그
사람에게 안 보인다(화면 설정이라 다시 고르면 그만이다).

### 4. 이름은 화면이 붙인다 — `WbsBoardNames`

이름을 쓰는 자리가 **일곱 화면**이라 각자 붙이면 「어떤 화면은 이름, 어떤
화면은 아이디」로 갈린다. `WbsBoardClient` 가 응답을 돌려주기 전에 한 번
채운다 — 포털 계정 목록(`portal_account`)을 참조자료 통에 담아 쓴다.

**못 찾으면 적힌 값 그대로 둔다.** 계정과 안 이어진 사람(퇴사자 · 외부 인력 ·
오타)을 「미할당」으로 덮으면 셋을 가려낼 수 없다.

### 그리고 걷어낸 것

- `Components/Pages/WbsDevUserList.razor`
- `Api/WbsDevUserClient.cs` 의 명부 부분 (화면 설정은 남았다)
- `Controllers/WbsBoardDevUsersController.cs` · `WbsBoardUserService` 의 명부 부분
- `Models/WbsBoardUser`(모델) · `WbsBoardSql.UserCols`
- 메뉴 `PM_WBS_DEVUSER` 와 권한 —
  `deploy/sql/portal-menu-wbs-devuser-remove-2026-09-23.sql`

`projmng.wbs_user` 표는 **지우지 않았다.** 읽는 코드가 없어져도 지우는 것은
운영 DDL 이라 사람이 정할 일이고(`wbs_pv*` 셋과 같은 이유), **사번 → 계정
대조 열쇠**가 아직 그 안에 있다.

### 적재 스크립트

`deploy/sql/projmng-wbs-data-load.sql` 에 절이 하나 늘었다 —
**「사번을 계정으로 바꾼다」.** `wbs_user.login_id` 를 손으로 채운 뒤 파일을
다시 돌리면 원장의 담당자 칸이 계정으로 바뀐다(앞의 적재는 전부 멱등이다).

---

## 주의 — 계정과 안 이어진 사람

3 과 4 는 **한 변경에서 함께** 했다. 갈라 두면 그 사이에 화면이 사번만
보여 준다. 계정과 안 이어진 사람(퇴사자 · 외부 인력)은 옮긴 뒤에도 이름이 없다.
화면은 그때 「미할당」이 아니라 **저장된 값 그대로** 보여 준다 — 오타인지
퇴사자인지 가려낼 수 있어야 한다.
