# 프로젝트관리 TOBE 정리 — 공통이 맡을 것과 남길 것

작성: 2026-08-29
앞 문서: [35-projmng-db-tobe-migration.md](35-projmng-db-tobe-migration.md) (DB 이관) ·
[13-projmng-migration.md](13-projmng-migration.md) (화면 이식)

## 0. 무엇을 정하려는 것인가

프로젝트관리는 이제 포털의 MSA 하나다. **인증 · 사용자 · 메뉴 · 파일은 포털이 단독으로
맡는다.** ASIS 가 자기 것으로 들고 있던 그 기능들은 TOBE 에서 걷어내고, 업무 기능은
쓰던 그대로 남긴다.

이 문서는 **무엇을 걷어내고 무엇을 남길지**를 근거와 함께 적은 것이다.
아직 아무것도 지우지 않았다.

## 1. 한눈에

| | 테이블 | 행 | 루틴 | 화면 |
|---|---:|---:|---:|---:|
| 지금 (TOBE) | 21 | 4,736 | 40 | 32 |
| 걷어낼 것 | 7 | 93 | 10 | 3 |
| **남을 것** | **14** | **4,643** | **30** | **29** |

지우는 자료는 전체의 **2%** 다. 업무 자료는 손대지 않는다.

## 2. 걷어낼 것 — 공통이 대신한다

### 2.1 자체 로그인

| ASIS | 포털 |
|---|---|
| `sp_proj_login` (아이디·비밀번호로 사용자 행을 돌려줌) | AuthServer JWT |
| `dev_user.upwd` (비밀번호 컬럼) | `scom.accounts.password` |

**서비스 쪽은 이미 막아 두었다.** `POST /api/Proj/login` 라우트를 지웠고,
`UserIdentityActionFilter` 가 `sp_proj_login` 이라는 이름 자체를 거부한다
([UserIdentityActionFilter.cs:29](../../microservices/ProjMngServer/Filters/UserIdentityActionFilter.cs:29)).
남은 것은 DB 에서 프로시저와 비밀번호 컬럼을 지우는 일뿐이다.

### 2.2 자체 메뉴

`dev_menu` 38행이 무엇인지 열어 보면 판단이 바로 선다.

```
19 | ProjMng   | 프로젝트 | ProjMngWasm.Pages.Proj.ProjMng
38 | Signin    | /login  | ProjMngWasm.Layout.Signin
33 | MenuMng   | 메뉴관리 | ProjMngWasm.Pages.Comm.MenuMng
```

`pgm_id` 가 **지워진 Blazor 앱의 .NET 클래스 이름**이다. 관리 대상 프로젝트의 메타데이터가
아니라 ProjMngWasm 자신의 내비게이션이다. 가리키는 화면이 이 세상에 없으므로 쓸모가 없다.

> ⚠️ [13-projmng-migration.md](13-projmng-migration.md) 5절에 "[프로젝트 화면 메뉴]가
> 다루는 것은 관리 대상 프로젝트의 메뉴"라고 적혀 있는데 **사실과 다르다.**
> 자료를 열어 확인한 결과 ProjMngWasm 자체 메뉴다. 그 문서를 고쳐야 한다.

| ASIS | 포털 |
|---|---|
| `dev_menu` | `scom.system_menus` |
| `dev_grp_menu_map` (그룹별 메뉴 권한) | `scom.role_menus` |
| `dev_menu_favorites` (0행) | `scom.menu_favorites` |
| `sp_dev_menu_auth` (로그인 사용자의 메뉴 트리) | `/auth/menu/all` |

### 2.3 자체 사용자 · 그룹

| ASIS | 행 | 포털 |
|---|---:|---|
| `dev_user` | 9 | `scom.accounts` |
| `dev_user_grp` | 4 | `scom.roles` |
| `dev_user_grp_map` | 9 | `scom.role_accounts` |
| `dev_user_prop` | 18 | `scom.account_preferences` |

**사용자 9명이 전원 포털 계정에 같은 아이디로 이미 있다.** 확인했다 —
`bmkim hsstyle jjstyle jskim kggmvp kspark quristyle sglee yws` 전부 `scom.accounts.user_id`
에 존재한다. 매핑 테이블을 새로 만들 필요가 없다는 뜻이라, 이 정리의 난이도를 크게 낮춘다.

> `proj/user.vue` 주석에 "포털 계정(`pm_*`)으로 옮겨져 있다"고 적혀 있는데 사실이 아니다.
> `pm_` 접두 계정은 하나도 없고, 원래 아이디 그대로 존재한다. 주석을 고쳐야 한다.

`dev_user_prop` 이 담고 있는 것은 `THEME` · `FONTSIZE` · `LASTPAGE` ·
`LASTPAGE_OPEN_YN` · `SIDEBAR_AUTO_CLOSE` — 전부 포털 환경설정이 하는 일이다.
`SERVER_URL` 2행만 성격이 다르다(4.5 참조).

### 2.4 지울 목록

**테이블 7개 (93행)**
`dev_user` · `dev_user_prop` · `dev_user_grp` · `dev_user_grp_map` ·
`dev_grp_menu_map` · `dev_menu` · `dev_menu_favorites`

**루틴 10개**
`sp_proj_login` · `sp_dev_user_exec` · `sp_dev_user_exec_all` · `sp_dev_user_prop_exec` ·
`sp_dev_user_grp_exec` · `sp_dev_user_grp_map_exec` · `sp_dev_menu_exec` ·
`sp_dev_menu_auth` · `sp_dev_grp_menu_map_exec` · `sp_dev_program_exec`

마지막 `sp_dev_program_exec` 은 성격이 다르다 — 참조하는 `dev_program` 테이블이 아예 없어
부르면 무조건 실패하고, 부르는 코드도 없다. 이참에 같이 치운다.

**화면 3개**

| 화면 | 메뉴 | 왜 |
|---|---|---|
| `comm/menu.vue` | `PM_COMM_MENU` 프로젝트 화면 메뉴 | 지워진 Blazor 앱의 메뉴를 편집하는 화면 |
| `comm/user-group.vue` | `PM_COMM_USERGRP` 프로젝트 사용자 그룹 | **파일이 아예 없다**(4.1 참조) |
| `proj/user-setting.vue` | `PM_PROJ_MYINFO` 내 프로젝트 정보 | 이미 읽기 전용이고 포털 계정 화면과 겹친다 |

## 3. 남길 것 — 업무

테이블 14개 · 4,643행. 프로젝트관리가 존재하는 이유다.

| 갈래 | 테이블 |
|---|---|
| 프로젝트 | `dev_proj` · `dev_proj_prop` · `dev_proj_user_map` |
| 일정 | `dev_wbs` · `home_todo` |
| 소스 분석 | `dev_srcinfo` · `dev_srcinfo_dtl` · `dev_activityinfo` |
| DB 도구 | `devdbinfo` · `dev_db_prop` · `devsqlresp` · `devsqlresp_base` |
| 기타 | `devcomm`(공통코드) · `dev_excel` |

## 4. 판단이 필요한 것

### 4.1 🔴 지금 8명은 화면을 아예 못 본다

가장 큰 "동일한 경험" 구멍이다.

```
프로젝트관리 메뉴를 볼 수 있는 역할 : ADMINISTRATOR · SYSTEM_ADMINISTRATOR
ASIS 사용자 9명의 포털 역할       : quristyle = SYSTEM_ADMINISTRATOR
                                   나머지 8명 = PARTNER
```

**PARTNER 에는 프로젝트관리 메뉴가 한 개도 붙어 있지 않다.** ASIS 에서 쓰던 8명이
지금 포털로 들어오면 메뉴가 보이지 않는다.

선택지는 셋이다.
- ⓐ 프로젝트관리 전용 역할(`PROJMNG_USER` 같은 것)을 만들어 8명에게 준다 — 권장
- ⓑ PARTNER 에 프로젝트관리 메뉴를 붙인다 — 파트너 전체가 개발자 도구를 보게 된다
- ⓒ 8명을 ADMINISTRATOR 로 올린다 — 포털 전체 권한이 열린다

ⓐ 를 권한다. 개발자 도구([DB 쿼리 테스터] 등)는 그 역할에서도 빼는 편이 안전하다.

### 4.2 🟠 담당자 드롭다운이 빈다

`dev_user` 를 지우면 **[할일]·[할일 정산 현황]** 두 화면의 담당자 드롭다운이 빈다.
`sp_projcommon` 의 `user` · `family` 코드가 `dev_user` 를 읽기 때문이다.

```sql
elsif p_code_id = 'user' then
  open p_cur for select a.user_id as code, a.user_name as name, '' as desc, a.*
    from projmng.dev_user a;
```

셋 중 하나로 메꿔야 한다.
- ⓐ 포털 계정 셀렉트(`biz_select_configs`)로 바꿔 끼운다 — 화면 2곳만 고치면 된다. 권장
- ⓑ `dev_user` 를 포털 계정을 읽는 **뷰**로 바꾼다 — 프로시저를 안 고쳐도 된다. 다만 DB 가 둘로 나뉘어 있어 `dblink`/FDW 가 필요하다
- ⓒ `dev_user` 를 이름만 담은 참여자 테이블로 줄인다

### 4.3 🟠 [프로젝트 참여자] 화면의 왼쪽

`proj/user.vue` 는 왼쪽에 `sp_dev_user_exec` 로 사용자 목록을 그리고, 오른쪽에서
그 사람의 참여 프로젝트(`dev_proj_user_map`)를 편집한다.
**오른쪽은 업무 자료라 그대로 남긴다.** 왼쪽 목록만 포털 계정에서 가져오도록 바꾼다.

`dev_proj_user_map` 은 지우면 안 된다 — [프로젝트 목록] 드롭다운이 이 표로 걸러진다
(`sp_projcommon` 의 `projlist`). 지우면 모두에게 프로젝트가 다 보이거나 하나도 안 보인다.

### 4.4 🟠 `sp_dev_user_exec` 를 부르는 곳이 셋 더 있다

지우기 전에 함께 처리해야 한다.

| 부르는 곳 | 무엇에 쓰나 |
|---|---|
| `views/projmng/proj/user.vue` | 왼쪽 사용자 목록 (4.3) |
| `views/projmng/proj/monitoring.vue` | 프로젝트 참여자 카드 |
| `views/projmng/proj/user-setting.vue` | 내 정보 표시 (화면째 제거 대상) |
| `api/portal/system/msa-users.ts` | **포털의 MSA 사용자 연결 화면** ([19번 문서](19-msa-user-work-enablement.md)) |

마지막 것이 특히 중요하다. 프로젝트관리 밖의 기능이 이 프로시저에 기대고 있다.
19번 문서의 D13·Q9~Q13 과 함께 봐야 한다.

### 4.5 🟡 담당자 컬럼은 자유 텍스트다

`dev_wbs` 의 담당자 컬럼은 아이디와 한글 이름이 섞여 있다.

```
dev_wbs.dev_user 값 : quristyle, 김병만, 사용자D, 남은, 박경수     (318/322행 채워짐)
home_todo.target_user: hsstyle, jjstyle                        (아이디만)
```

포털 계정으로 정규화하려면 `남은` 같은 값을 사람이 판정해야 한다.
**지금은 손대지 않는 편이 낫다.** 표시에는 지장이 없고, 정규화는 나중에 별도로 한다.

### 4.6 🟡 `SERVER_URL`

`dev_user_prop` 에서 유일하게 성격이 다른 값이다(`kggmvp`·`quristyle` 2행,
`https://10.2.110.191:51669`). 개발자마다 다른 대상 서버를 가리키는 것으로 보인다.
쓰는 화면을 찾지 못했다. **버려도 되는지 확인이 필요하다.**

### 4.7 🟡 개발 도구가 아직 ASIS 를 본다

`devdbinfo` 는 [DB 개체 탐색]·[테이블 설명 관리]·[DB 쿼리 테스터]가 **접속할 대상 DB
목록**이다(서비스 자신의 접속 문자열과 별개다). 12행 중 두 행이 아직 `jsini.co.kr:15432`
를 가리킨다 — 이관 전 ASIS 다.

바꿀지 말지는 용도에 달렸다. 개발 도구로 ASIS 를 계속 들여다볼 생각이면 그대로 두고,
TOBE 를 보게 하려면 행을 고치거나 추가한다.

> 이 표에는 **접속 비밀번호가 평문으로 들어 있다.** 원본부터 그랬다. 이관과 별개로
> 언젠가 다뤄야 할 문제다.

### 4.8 ⚪ 그밖에

- **`user-group.vue` 는 파일이 없다.** 메뉴(`PM_COMM_USERGRP`)는 DB 에 등록돼 있는데
  화면 파일이 만들어진 적이 없다. [access.ts:61](../../fronts/apps/jsini-portal/src/router/access.ts:61)
  이 없는 컴포넌트를 콘솔 경고만 남기고 라우트에서 빼므로, **사이드바에 보이지만 열리지 않는다.**
  어차피 제거 대상이라 메뉴 행만 지우면 정리된다.
- **`dev_user_grp_map` 에 고아 행이 있다.** `grp_id = 'Administrator'` 인데
  `dev_user_grp` 에 그 그룹이 없다. 어차피 지울 표다.
- `dev_srcinfo.src_path` 가 윈도우 경로라 소스 추적이 실패하는 문제는 그대로다
  ([13번 문서](13-projmng-migration.md) 8절).

## 5. 단계 제안

앞 단계가 끝나야 다음이 안전한 순서로 놓았다. 각 단계는 되돌릴 수 있다.

| 단계 | 하는 일 | 걸리는 결정 |
|---|---|---|
| **1** | 권한 정리 — 프로젝트관리 역할을 만들고 8명에게 부여 ✅ **완료** | 4.1 |
| **2** | 담당자 드롭다운을 포털 계정으로 교체 (`user`·`family`) ✅ **완료** | 4.2 |
| **3** | `sp_dev_user_exec` 의존 4곳 정리 (참여자·모니터링·내정보·MSA 연결) ✅ **완료** | 4.4 |
| **4** | 화면 3개와 메뉴 3행 제거 ✅ **완료** | — |
| **5** | DB 에서 테이블 7개·루틴 10개 제거 ✅ **완료** | 4.6 확인 후 |
| **6** | 개발 도구 대상 DB(`devdbinfo`) 정리 ✅ **완료** | 4.7 |

**1단계를 먼저 하는 이유**가 있다. 지금은 `quristyle` 말고는 화면을 열 수 없어
"기존과 같은 경험"인지 확인할 방법이 없다. 권한부터 열어야 나머지 단계의 결과를
사람이 눈으로 확인하며 진행할 수 있다.

5단계는 **되돌리기 가장 어려운 단계**다. 그전에 백업을 남긴다 —
`scripts/projmng-db-migration/dump_asis.py` 가 ASIS 원본을 언제든 다시 뽑을 수 있고,
ASIS 자체가 계속 살아 있으므로 최후의 정본은 남아 있다.

## 6. 실행 기록

### 1단계 — 권한 정리 (2026-08-29, 완료)

ASIS 가 권한을 실제로 걸고 있었다는 것을 먼저 확인했다. `CreateDto` 가 로그인 사용자를
`SSUserId` 로 실어 보냈고([WasmShear/AppData.cs:90](../../../ProjMng/WasmShear/AppData.cs:90)),
`sp_dev_menu_auth` 가 그 값의 그룹으로 `dev_menu` 를 걸러 냈다.
**그래서 "동일한 경험"의 정답이 자료에 남아 있었다** — 짐작할 필요가 없었다.

적용한 것: [docs/sql/projmng_role_seed.sql](../sql/projmng_role_seed.sql) (반복 실행 안전).

| ASIS 그룹 | 새 역할 | 화면 |
|---|---|---:|
| Administrator | `PROJMNG_ADMIN` | 32 (전체) |
| JsiniTeam | `PROJMNG_JSINITEAM` | 6 |
| MNM_SMG | `PROJMNG_MNM_SMG` | 8 |

`Project`(구성원이 quristyle 뿐이고 화면은 `PROJMNG_ADMIN` 에 포함) 과
`Family`(걸린 메뉴 0개)는 만들지 않았다.

결과다. ASIS 에서 각자 보던 화면 수와 정확히 같다.

```
bmkim        0     0   PARTNER
hsstyle      0     0   PARTNER
jjstyle      0     0   PARTNER
jskim        0 ->  9   PARTNER, PROJMNG_JSINITEAM, PROJMNG_MNM_SMG
kggmvp       0 ->  8   PARTNER, PROJMNG_MNM_SMG
kspark       0     0   PARTNER
quristyle   30    30   PROJMNG_ADMIN, SYSTEM_ADMINISTRATOR
sglee        0 ->  6   PARTNER, PROJMNG_JSINITEAM
yws          0     0   PARTNER
```

> quristyle 이 30 으로 보이는 것은 집계가 `type = 'MENU'` 만 세기 때문이다.
> 서버 모니터 2개는 `EMBEDDED` 타입이라 빠졌을 뿐, `PROJMNG_ADMIN` 은 32개를 전부 가진다.

조상 폴더까지 함께 부여했는지 확인했다. 화면만 주면 사이드바에 뜨지 않는다 —
두 역할 모두 부모가 빠진 항목이 없다.

**[DB 쿼리 테스터] 실행 권한 — 열기로 결정했다.**
`DevTools:RawSqlRoles` 에 `PROJMNG_MNM_SMG` 를 더했다
([appsettings.json](../../microservices/ProjMngServer/appsettings.json)).
ASIS 에서 jskim · kggmvp 가 쓰던 화면이라 "동일한 경험"을 택했다.

이 값은 `RawSqlGuardMiddleware` 생성자에서 한 번만 읽으므로 **재기동해야 반영된다.**
재기동 뒤 확인했다 — `PROJMNG_MNM_SMG` 는 200, `PARTNER` 단독은 여전히 403 이다.

> 고치다 한 번 넘어졌다. 주석용 키 `"//DevTools"` 를 새로 넣었는데 파일에 이미
> `"//devTools"` 가 있었다. .NET 설정 로더는 **대소문자를 구분하지 않아** 중복 키로 보고
> 기동을 거부한다(`A duplicate key '//DevTools' was found`).
> 파이썬 `json` 은 조용히 마지막 것만 취하므로 검증을 통과해 버렸다 —
> 이 파일을 검사할 때는 중복 키를 직접 잡아야 한다.

### 2단계 — 담당자 드롭다운 (2026-08-29, 완료)

[할일]·[할일 정산 현황]의 담당자 드롭다운이 `sp_projCommon` 의 `user` 코드를 거쳐
`projmng.dev_user` 를 읽던 것을 **포털 계정**으로 바꿨다.

| | 바꾼 것 |
|---|---|
| 메타데이터 | [docs/sql/projmng_account_select.sql](../sql/projmng_account_select.sql) — `portal_account` 셀렉트 등록 (`auth` MSA · `GET /system/account/list` · label `userName` · value `loginId`) |
| 화면 | `home/todo.vue` · `home/todo-monitor.vue` 의 `<CodeSelect code-id="user">` → `<BizSelect type="portal_account">` |

**값으로 `loginId` 를 쓴 것이 핵심이다.** `home_todo.target_user` 에 쌓인 값이
`hsstyle` · `jjstyle` 같은 로그인 아이디라, 계정 UUID(`id`)를 값으로 쓰면 기존 자료와 어긋난다.
브라우저에서 확인했다 — 사용자A을 고르면 요청에 `target_user: "quristyle"` 가 실린다.

ASIS 와 달라지는 점은 **목록이 9명에서 43명이 된다**는 것이다.
ASIS `dev_user` 9명이 전원 포털 계정에 같은 아이디로 있어
**기존 자료의 값이 선택 불가능해지는 일은 없다**(상위 집합이다).
길어진 목록은 검색으로 메꿨다(`show-search`) — 확인했다, "사용자A"을 치면 1건으로 좁혀진다.

이로써 `dev_user` 를 읽는 곳이 화면 2곳 줄었다. 남은 곳은 4.4 의 넷이다.

### 3단계 — `sp_dev_user_exec` 의존 정리 (2026-08-29, 완료)

먼저 **프로시저를 하나 더했다**. 기존 것은 손대지 않았다 —
[docs/sql/projmng_user_map_list.sql](../sql/projmng_user_map_list.sql) 의 `sp_proj_user_map_list`.
`dev_user` 를 읽지 않고 `dev_proj_user_map` + `dev_proj` 만 본다.
사람의 **이름**은 포털 계정이 대고, 프로젝트관리는 **참여 여부**만 안다.
원본이 주던 `inv_cnt`(참여 프로젝트 수)도 같은 뜻으로 살렸다.

| 부르던 곳 | 바꾼 것 |
|---|---|
| `proj/user.vue` 왼쪽 목록 | 포털 계정(`portal_account`) + `sp_proj_user_map_list` 의 참여 수를 합쳐 그린다. 프로젝트를 고르면 그 참여자만 남는다 |
| `proj/monitoring.vue` 참여자 카드 | `sp_proj_user_map_list` 로 바꿨다 |
| `api/portal/system/msa-users.ts` | **프로젝트관리 대조를 걷어냈다** |
| `proj/user-setting.vue` | 손대지 않았다 — 4단계에서 화면째 없앤다 |

**MSA 대조에서 프로젝트관리를 뺀 이유.** 그 화면은 "이 포털 계정이 그 시스템에 어떤
사용자로 있는가"를 보여 준다. `dev_user` 를 걷어내면 프로젝트관리의 사용자가 곧 포털
계정이므로 **대조할 상대가 없다.** 헬프데스크는 여전히 자체 사용자 테이블을 들고 있어 남는다.
계정 관리 화면([system/account](../../fronts/apps/jsini-portal/src/views/portal/system/account/index.vue))의
'프로젝트관리' 열도 함께 뺐다.

확인한 것이다.

```
프로젝트별 참여자 — 옛 프로시저 vs 새 프로시저
  prj=1  1명 ['quristyle']                                    일치
  prj=2  4명 ['jskim','kspark','quristyle','sglee']            일치
  prj=3  1명 ['quristyle']                                    일치
  prj=7  2명 ['kggmvp','quristyle']                           일치
```

브라우저에서도 봤다. [프로젝트 참여자] 왼쪽에 포털 계정 43명이 뜨고 참여 수가
`quristyle 7 · jskim 1 · kggmvp 1 · kspark 1 · sglee 1` 로 DB 와 같다.
사람을 고르면 오른쪽에 프로젝트 7개가 참여 여부와 함께 뜬다.
계정 관리 화면의 열은 `… 역할 · 계정 상태 · 헬프데스크 · 작업` 으로 '프로젝트관리'가 사라졌다.

> [프로젝트 참여자]의 프로젝트 드롭다운이 `administrator` 로 로그인하면 "전체" 하나뿐이다.
> **ASIS 와 같은 동작이다** — `sp_projCommon` 의 `projlist` 가 `dev_proj_user_map` 으로
> 거르는데 그 계정은 참여 프로젝트가 없다. `quristyle` 로 보면 7개가 나온다.

### 4단계 — 화면·메뉴 제거 (2026-08-29, 완료)

| 지운 것 | 무엇 |
|---|---|
| `views/projmng/comm/menu.vue` | [프로젝트 화면 메뉴] |
| `views/projmng/proj/user-setting.vue` | [내 프로젝트 정보] |
| `views/projmng/shared/menu-tree.ts` | 위 화면 하나만 쓰던 트리 헬퍼라 함께 걷어냈다 |
| `scom.system_menus` 3행 · `scom.role_menus` 9행 | `PM_COMM_MENU` · `PM_COMM_USERGRP` · `PM_PROJ_MYINFO` |

`comm/user-group.vue` 는 **지울 파일이 없었다.** 메뉴만 등록돼 있고 화면이 만들어진 적이 없다(4.8).

[docs/sql/projmng_menu_seed.sql](../sql/projmng_menu_seed.sql) 에서 세 행을 빼고,
'쓰지 않기로 한 화면' 절에 삭제문을 더했다. **다시 실행해도 되살아나지 않는다** —
`PM_EXT_FRDATA` 때와 같은 방식이다.

`PM_COMM`(기준정보) 폴더는 남긴다. [프로젝트 공통코드]가 그 아래 있다.

확인한 것이다.

```
/projmng/comm/menu           → 404
/projmng/proj/user-setting   → 404
사이드바 프로젝트관리          → 29화면 (셋 다 사라짐, 나머지 그대로)
프로젝트 공통코드 화면          → 정상 (24행)
vite build --mode production → 성공
vue-tsc                      → projmng 신규 오류 0
서비스 스모크                  → 18 통과 · 0 실패
```

프론트에서 **걷어낼 프로시저 10개를 부르는 곳이 0** 이 되었다.
남은 언급 둘은 주석이다(`api/projmng/index.ts`, `proj/user.vue`).
5단계(DB 제거)의 전제 조건이 갖춰졌다.

> 이 단계 도중 포털 DB 가 `funeralv2` → `jsiniportal` 로 바뀌었다(저장소의 다른 작업).
> 이 문서의 스크립트는 접속 정보를 `AuthServer/appsettings.Local.json` 에서 매번 새로 읽으므로
> 자동으로 따라간다. 적용 결과는 `jsiniportal` 에서 확인했다.

### 5단계 — DB 제거 (2026-08-29, 완료)

`SERVER_URL` 은 버리기로 정했다(4.6 종결).

**먼저 백업을 뽑았다.** ASIS 가 살아 있지만 TOBE 에는 그 뒤에 더한 것
(`sp_proj_user_map_list`)이 있어 ASIS 만으로는 그대로 복구되지 않는다.
[scripts/projmng-db-migration/backup_before_drop.py](../../scripts/projmng-db-migration/backup_before_drop.py) 가
지우기 직전의 TOBE 를 `out/backup-step5/` 로 남긴다 — 테이블 7개 93행 · 루틴 10개 ·
손대기 전 `sp_projcommon`. 되돌리려면 `routines.sql` → `tables.sql` → `projcommon.sql` 순으로 실행한다.

적용한 것: [docs/sql/projmng_drop_auth_objects.sql](../sql/projmng_drop_auth_objects.sql).

**`sp_projcommon` 을 먼저 고치고 지웠다.** 이 프로시저의 `user` · `family` 분기가
`dev_user` 를 읽는다. 부르는 곳은 2단계에서 없앴지만, 표만 지우면 그 분기가 실행 시점에
터지는 지뢰가 남는다. 그래서 분기를 걷어낸 뒤 표를 지웠다.

> 이 정의는 **손으로 옮겨 적지 않았다.** 원본에서 두 분기만 잘라내 생성했다.
> 인자 이름 하나만 달라도 `CREATE OR REPLACE` 가 거부하는데,
> 12번째 인자가 `p_req_type` 이 아니라 `sess_userid` 였다 — 손으로 썼으면 틀렸을 자리다.

지운 것이다.

| | 개수 |
|---|---|
| 루틴 | 10 (`sp_proj_login` · `sp_dev_user_*` · `sp_dev_menu_*` · `sp_dev_grp_menu_map_exec` · `sp_dev_program_exec`) |
| 테이블 | 7 (93행) — FK 때문에 `dev_menu_favorites` 를 먼저 지운다 |

**남은 것: 테이블 14개 · 4,643행 · 루틴 31개.** 목표와 정확히 같다.

확인한 것이다.

```
verify.py --deep   테이블 14/14 · 컬럼 131/131 · 루틴 29/29 · 루틴본문 29/29 · 제약 6/6
                   14개 테이블 내용 해시 전부 일치 → "모두 일치한다"
smoke_tobe.py      통과 17 · 실패 0     (DB 직접)
smoke_service.py   통과 15 · 실패 0     (서비스 경유)
sp_projcommon      projlist 7 · projdb 12 · projdb2 12 · sourcelist 8 ·
                   wbsflowlist 97 · db 4 · CODE_TYPE 3 · compstat 4 — 남은 분기 전부 정상
```

`verify.py` 는 이제 **일부러 다른 것**(걷어낸 표 7·루틴 10, 더한 루틴 1, 본문이 바뀐 루틴 1)을
알고 비교한다. 그 밖의 차이가 생기면 그때 잡힌다. `--raw` 로 있는 그대로도 볼 수 있다.

지운 `user` · `family` 코드는 이제 **모르는 코드와 똑같이** 동작한다(커서를 열지 않는다).
새로운 실패 유형이 생긴 것이 아니다 — `존재하지않는코드` 를 넣었을 때와 같은 결과다.

브라우저에서도 확인했다. [할일 관리]가 뜨고 담당자 드롭다운에 포털 계정이 채워진다.

### 6단계 — 개발 도구 대상 DB (2026-08-29, 완료)

`devdbinfo` 는 **개발 도구가 접속할 대상 DB 목록**이다. 서비스 자신의 접속 문자열과 별개다 —
[DB 개체 탐색]·[테이블 · 컬럼 설명]·[DB 쿼리 테스터]가 이 표를 보고 붙는다.

12행 중 **한 행만 고쳤다**. 적용: [docs/sql/projmng_devdbinfo_tobe.sql](../sql/projmng_devdbinfo_tobe.sql).

| | 전 | 후 |
|---|---|---|
| `db_rid 4` **jsini** | `jsini.co.kr:15432 / jsini / projmng` | `jin114.co.kr:31015 / projmng / projmng` |

이 행이 프로젝트관리 DB 그 자체다(`prj_rid 3` = JsiniProjMng). 이관이 끝났으니 TOBE 를 보게 했다.

**`db_rid 12` `jmcs` 는 손대지 않았다.** 같은 `jsini.co.kr:15432` 를 가리키지만
**다른 데이터베이스**(`jmcs`)이고 다른 프로젝트(`prj_rid 4` = JinmoonCarSrch) 것이라
이관 대상이 아니다. 나머지 10행은 한주·LSMnM·장례 프레임 등 외부 시스템이라 그대로다.

> **서버가 이 표를 메모리에 캐시한다**(`AppData.DB_Infos`). 고치는 것만으로는 반영되지 않는다.
> `POST /api/Sys` 로 캐시를 비웠다(`SysService.AppDataClear`). 재기동해도 된다.

캐시를 비우기 전후로 같은 요청을 넣어 확인했다 — 대상이 바뀌는 것이 눈에 보인다.

```
[DB 개체 탐색] tablelist (dbnick=jsini)
  캐시 비우기 전 : 21개  ← ASIS
  캐시 비운 뒤   : 14개  ← TOBE (dev_activityinfo … home_todo, 업무 표 14개 그대로)

[DB 쿼리 테스터] select current_database(), inet_server_port()
  → {'db': 'projmng', 'port': 31015, 'tbl': 14}   ← TOBE
     (kggmvp / PROJMNG_MNM_SMG 로 실행 — 1단계에서 연 역할이 실제로 통한다)
```

`verify.py` 에 `devdbinfo` 를 '내용이 일부러 다른 표' 로 등록했다. 행수는 12/12 로 맞춰 보고
내용 해시는 비교하지 않는다.

## 7. 남은 것

여섯 단계를 모두 마쳤다. 이 정리에서 손대지 않기로 한 것들이다.

- **`dev_wbs` 담당자 컬럼의 자유 텍스트**(4.5). 아이디와 한글 이름이 섞여 있어
  (`quristyle` · `김병만` · `남은` …) 포털 계정으로 정규화하려면 사람이 판정해야 한다.
  표시에는 지장이 없다.
- **`devdbinfo` 의 평문 비밀번호**(4.7). 원본부터 그랬고 이관과 별개 문제다.
  서버가 이 컬럼으로 접속 문자열을 만들기 때문에(`Models/DbInfo.cs`) 형식을 그대로 따랐다.
- **`dev_srcinfo.src_path` 가 윈도우 경로**라 소스 추적이 실패하는 것
  ([13-projmng-migration.md](13-projmng-migration.md) 8절). 이관 이전부터의 자료 문제다.
- **ASIS 는 계속 돈다.** 정리하려면 별도 결정이 필요하다.
  전환 시점 이후 ASIS 에 쌓인 자료가 있으면 다시 옮겨야 한다
  ([35번 문서](35-projmng-db-tobe-migration.md) 7절).

고쳐야 할 옛 기록 둘이다.

- [13-projmng-migration.md](13-projmng-migration.md) 5절의 "[프로젝트 화면 메뉴]가 다루는 것은
  관리 대상 프로젝트의 메뉴" 라는 설명은 **사실과 다르다**(2.2). 그 화면은 이제 없다.
- 같은 문서의 화면 수(32)·테이블 수도 이 정리 뒤로는 맞지 않는다 —
  화면 29 · 테이블 14 · 루틴 31 이다.
