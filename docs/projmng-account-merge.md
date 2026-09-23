# 개발자 관리를 포털 계정관리로 합친다

WBS 대시보드의 [개발자 관리](`/projmng/wbs/dev-users`)를 걷어내고, 그 화면이
쓰던 속성을 **포털 계정관리**(`/admin/system/account`)에서 다루도록 옮긴다.

상태: **설계 확정 · 구현 전** (2026-09-23)

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

## 남은 일 넷

### 1. AuthServer — `Dev.*` 읽기·쓰기

`Services/UserService.cs` 가 `Watermark` 를 다루는 방식(`UpsertWatermark`)을
접두사 한 벌로 넓힌다. `AccountDto` 에 `DevAttributes`(사전) 한 칸을 낸다.

### 2. 계정관리 화면 — 네 묶음 붙이기

`web/src/Apps/JSini.Web.Admin/Components/Pages/UserList.razor` 의 편집 창에
접는 구역 넷을 더한다. 칸이 스물다섯이라 **한 줄로 쏟지 않는다** — 묶음마다
접어 두고 필요한 것만 편다.

### 3. ProjMngServer — `wbs_user` 조인을 걷어낸다

조인이 **11곳**이다.

```
Services/WbsBoardService.cs     stats/monthly-by-user · weekly-by-user · users · rows
Services/WbsProgressService.cs  progress/by-user · progress/rows
Services/WbsDelayService.cs     delay/by-user · delay/rows
Services/WbsBoardUserService.cs 명부 CRUD (통째로 없앤다)
```

집계는 `user_bp_id` 로 묶고 이름 칸은 **안 채운다**. 화면이 채운다.

화면 설정(`wbs_user_pref`)의 주인 찾기도 함께 바뀐다 — 지금은
`login_id → bp_id` 로 푸는데, 원장이 계정을 가리키게 되면 **로그인 아이디가
곧 열쇠**라 그 단계가 사라진다.

### 4. 개발자 관리 화면·메뉴 제거

- `Components/Pages/WbsDevUserList.razor`
- `Api/WbsDevUserClient.cs` 의 명부 부분(화면 설정은 남긴다)
- `Controllers/WbsBoardDevUsersController.cs` · `Services/WbsBoardUserService.cs` 의 명부 부분
- 메뉴 `PM_WBS_DEVUSER` 와 권한 5건
- 적재 스크립트(`deploy/sql/projmng-wbs-data-load.sql`)의 `wbs_user` 적재를
  **사번 → 계정 대조**로 바꾼다

`projmng.wbs_user` 표는 **지우지 않는다** — 읽는 코드가 없어져도 지우는 것은
운영 DDL 이라 사람이 정할 일이다(`wbs_pv*` 셋과 같은 이유).

---

## 주의 — 이름이 안 보이는 구간이 생긴다

3 을 하고 4 를 하기 전까지는 화면이 사번만 보여 준다. **둘을 한 변경에서
함께** 해야 한다.

그리고 계정과 안 이어진 사람(퇴사자 · 외부 인력)은 옮긴 뒤에도 이름이 없다.
화면은 그때 「미할당」이 아니라 **저장된 값 그대로** 보여 준다 — 오타인지
퇴사자인지 가려낼 수 있어야 한다.
