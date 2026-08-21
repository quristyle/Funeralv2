# JSini 관리 포털 통합 — 구조 정리와 공통 권한

작성: 2026-08-21 (자율 진행 기록)

## 배경

이 시스템의 이름은 **JSini 관리 포털**이다. 그 안에 장례식장(funeralv2), 헬프데스크 등
MSA 를 하나씩 붙여 나가는 구조다. 그래서

- **인증과 권한은 JSini 전체에서 한 번만 관리**하고,
- 각 MSA 에는 **그 시스템 고유 업무만** 남긴다.

기존에 따로 돌던 헬프데스크와 장례식장 시스템은 자체 메뉴·권한·사용자를 들고 있었다.
그것을 포털 공통으로 모으고, 각 화면의 권한 판단도 공통 권한을 따르게 하는 작업이다.

---

## 1. 이름 정리

`funeralv2` 라는 이름이 세 가지 다른 대상을 가리켜 가장 큰 혼동이었다.
(`.env` 의 `VITE_APP_TITLE` 은 이미 `JSINI ADMIN` 이었다 — 이름만 뒤처져 있었다.)

| 전 | 후 | 정체 |
|---|---|---|
| `fronts/apps/funeralv2` | `fronts/apps/jsini-portal` | 포털 프론트엔드 전체 |
| `@vben/funeralv2` | `@vben/jsini-portal` | 패키지명 |
| `funeralv2_proj.sln` | `jsini.sln` | 백엔드 솔루션 전체 |
| `Funeralv2.Shared.{DTOs,Domain,Infrastructure}` | `JSini.Shared.*` | 전 MSA 공용 라이브러리 |
| `VITE_APP_NAMESPACE=funeralv2-web` | `jsini-portal-web` | 로컬스토리지 키 |

### 그대로 둔 것과 이유

- `microservices/funeralv2Api`, `funeralv2_player` — 실제로 장례식장 MSA 가 맞다. 혼동 없음.
- `views/_core`, `api/core` — vben 프레임워크 계층이다. MSA 이름과 겹치지 않는다.
- 저장소 루트 경로(`/home/quri/Funeralv2`) — 작업 디렉터리라 임의로 옮기지 않았다.

### 로컬스토리지 키 변경의 영향

`VITE_APP_NAMESPACE` 와 `VITE_APP_STORE_SECURE_KEY` 를 바꿨다.
**기존 세션의 토큰과 화면 설정(테마 등)을 읽지 못하므로 한 번은 다시 로그인해야 한다.**
이름 통일이 목적이라 감수했다. 되돌리려면 `.env` 의 두 값을 옛 이름으로 두면 된다.

### 곁들여 고친 것

솔루션 파일이 깨져 있었다. `auth-server\AuthServer`, `api-gateway\ApiGateway` 로
실제와 다른 경로를 가리키고 FileServer 와 공용 라이브러리 3개는 등록조차 없었다.
경로를 바로잡고 4개를 추가해 9개 프로젝트 전체가 빌드된다.

라우트 하나가 없는 파일을 가리키고 있었다(`#/views/system/role-custom/index.vue`,
실제는 `views/portal/auth/role-custom/index.vue`). 경로 재편과 함께 고쳤다.

---

## 2. 폴더 3분할

```
views/                            api/
  _core/     vben 프레임워크          core/     인증·사용자·메뉴·타임존
  portal/    JSini 공통 (18메뉴)      portal/   시스템관리·게이트웨이·AI
  funeral/   장례식장 MSA (28메뉴)     funeral/  장례식장 MSA
  helpdesk/  헬프데스크 MSA (54메뉴)   helpdesk/ 헬프데스크 MSA
  demos/ examples/  템플릿 예제        examples/
```

`system/player-download` 는 포털 설정 아래 있었지만 장례식장 전용 화면이라
`funeral/player-download` 로 옮겼다.

메뉴가 백엔드 주도(동적 라우터)라 **DB `scom.system_menus.component` 46건**도 함께 옮겼다
→ [`docs/sql/menu_component_repath.sql`](../sql/menu_component_repath.sql) (재실행 안전).

### 검증

- DB 의 `#/views/...` **172건 전부** 실제 파일과 일치 (자동 대조).
- `router/access.ts` 의 `import.meta.glob('../views/**/*.vue')` 매핑이 새 경로를 그대로 해석.
- git 이름변경 403건으로 파일 이력 보존.

### 곁들여 발견한 것 (미해결)

`api/funeral/{info,stat,setting,help}` 이 호출하는 `/api/info`, `/api/stat`,
`/api/setting`, `/api/help` 는 **게이트웨이에 라우트가 없고 funeralv2Api 에도 구현이 없다**
(funeralv2Api 는 `/building/*` 만 구현). 해당 17개 화면은 백엔드가 없는 상태다.
폴더는 도메인 기준으로 `funeral/` 에 두었다. 구현 여부는 별도 판단이 필요하다.

---

## 3. 공통 권한

### 왜 새로 만들었나

기존 `/auth/codes` 는 `["*"]` 를 반환하는 스텁이라 `hasAccessByCodes` 가 아무 역할도
하지 못하고 있었다. 실제 권한은 `scom.role_menus` 에만 있었다.

### 구성

| 계층 | 파일 | 역할 |
|---|---|---|
| 서버 | `MenuService.GetMenuPermissionsAsync` | 사용자가 속한 **여러 역할의 권한을 OR 로 합치고**, 메뉴가 안 쓴다고 지정한 항목(`use_*`)은 꺼서 반환 |
| 서버 | `GET /api/auth/menu/permissions` | 위 결과. 경로(`path`)를 함께 실어 화면이 자기 권한을 찾게 한다 |
| 스토어 | `store/menu-permission.ts` | 로그인 시 1회 수신, 경로로 조회 |
| 훅 | `composables/use-menu-permission.ts` | `perm.canCreate` 등 |
| 디렉티브 | `directives/perm.ts` | `v-perm:create`, `v-perm:delete.disable` |
| 렌더함수용 | `utils/permission.ts` | `can('update')` — vxe 액션 컬럼처럼 `h()` 로 그리는 자리 |
| 가드 | `router/guard.ts` `setupViewPermissionGuard` | 열람 권한 없는 메뉴 → `/403` |

### 결정 1 — 가드는 "정확히 일치하는 메뉴"에만 적용한다

접두어로 상위 메뉴 권한을 물려받게 하면 두 가지가 잘못 막힌다.

1. **디렉터리(CATALOG) 43건.** 화면이 없어 열람 권한이 꺼져 있다.
   물려받게 하면 그 아래 화면이 통째로 막힌다.
2. **메뉴에 등록되지 않은 하위 경로.** `/helpdesk/request/detail/123` 같은 상세 화면.

정확 일치만 보면 등록된 메뉴에서 열람을 끄면 확실히 막히므로 통제에는 문제가 없다.
추가로 부모 라우트(`redirect` 또는 자식이 있는 라우트)는 검사에서 건너뛴다.
첫 자식 화면으로 넘어가는 리다이렉트가 끊기지 않게 하기 위해서다.

**검증:** 활성 라우트 231건 중 가드가 막는 것 43건, **전부 CATALOG, 실제 화면 오차단 0건.**
계정 4개 전부에 대해 같은 결과.

### 결정 2 — 권한 정보가 없으면 막지 않는다 (fail-open)

가드·`v-perm`·`can()`·훅 **네 곳 모두** 같은 규칙을 쓴다.

```
권한 목록을 못 받았거나(isLoaded=false)
사용자에게 권한 행이 하나도 없으면(hasAnyData=false)  →  막지 않는다
```

이유: 현재 계정 4개 중 **2개(`administrator`, `admin`)에 역할이 배정되어 있지 않다.**
"데이터 없음 = 전부 거부"로 다루면 그 계정들이 통째로 잠긴다.
통합 이행 중에는 데이터가 없다는 이유로 사용자를 잠그지 않는 편이 안전하다.
**역할을 배정하는 순간부터 실제 권한이 그대로 적용된다.**

처음에는 가드만 fail-open 이고 버튼은 fail-closed 여서, 역할 없는 계정이
화면에는 들어가지는데 버튼이 하나도 안 보이는 엇갈린 상태였다. 네 곳을 같은 규칙으로 맞췄다.

### 결정 3 — 권한 데이터를 먼저 채우고 나서 가드를 켰다

가드를 켜기 전 `role_menus` 에는 **헬프데스크 66건만** 있고 나머지 165건은 행 자체가 없었다.
그대로 켰다면 시스템 관리 화면까지 165개가 전부 잠겼다.

→ [`docs/sql/role_menu_backfill.sql`](../sql/role_menu_backfill.sql) 로 660행을 채워
4개 역할 × 231메뉴 = 924행 완성. 값은 현재 동작 그대로(메뉴가 쓴다고 지정한 항목만 허용).
이제 역할 권한 화면에서 필요한 것만 끄면 된다.

### 결정 4 — 메뉴별 권한 항목 설정

헬프데스크에만 있던 기능이었다. 메뉴마다 어떤 권한 항목을 쓰는지 정하고
사용자정의 1~8 에 이름을 붙인다(원본 `Menu.UseExt1~8` / `Ext1Name~8Name`).
funeralv2 는 모든 메뉴에 15개 체크박스를 똑같이 띄우고 `C1`~`C8` 로만 보여주고 있었다.

→ `scom.system_menus` 에 `use_*` 15개 + `cust1_name~cust8_name` 추가
([`docs/sql/menu_permission_items.sql`](../sql/menu_permission_items.sql)).
메뉴 관리 폼에 설정 블록(`modules/permission-items.vue`), 역할 권한 그리드가 그 설정을 따른다.
`SaveRoleMenusAsync` 가 서버에서도 한 번 더 막는다(요청을 직접 만들어 보내도 통하지 않는다).

**대조 결과: 헬프데스크의 권한 항목은 funeralv2 에 전부 있었다.**
`CanRead/Create/Update/Delete` → `can_view·can_search/create/update/delete`,
`Ext1~8` → `can_cust1~8`. funeralv2 에는 `can_print`, `can_excel` 이 더 있다.
빠진 항목은 없었고, 없던 것은 위의 "항목별 사용 여부와 이름" 관리 기능이었다.

### 적용 현황

| 화면군 | `v-perm` | `can()` |
|---|---|---|
| helpdesk | 34 | 0 |
| portal | 15 | 15 |
| funeral | 24 | 0 |

헬프데스크 공용 `shared/crud-table.vue` 에 적용해 조직 5화면을 함께 덮었다.
포털의 vxe 액션 컬럼 6개는 `h()` 렌더 함수라 `can()` 을 쓴다.

`v-perm` 은 권한이 비동기로 늦게 도착해도 반영되도록 요소마다 `effectScope` +
`watchEffect` 를 붙였다. mounted 시점에 한 번만 계산하면 새로고침으로 바로 들어온
경우 버튼이 갱신되지 않는다.

### 릴리즈 도구의 배포 버튼

`jin114 배포` / `goldb 배포` 는 CRUD 어디에도 맞지 않아 **사용자정의 1번(`cust1`)** 에 묶었다.
메뉴 관리에서 그 메뉴의 `C1` 을 켜고 "배포 실행" 같은 이름을 붙이면 된다.

---

## 4. 남은 일

### 확인이 필요한 것

1. **역할 없는 계정 2개** (`administrator`, `admin`). fail-open 규칙 덕에 지금은 막히지 않지만,
   역할을 배정하거나 계정을 정리해야 실제 통제가 걸린다.
2. **브라우저 실사용 확인.** 가드 로직은 실제 API 응답으로 시뮬레이션해 검증했으나
   (231건 중 오차단 0건, 권한을 껐다 켜며 차단·해제 양방향 확인),
   화면을 눌러 보는 확인은 하지 못했다.
3. **백엔드 없는 17화면** (`info`/`stat`/`setting`/`help`). 위 2절 참고.

### 손대지 않은 것

- `demos`, `examples` 화면군(66메뉴). vben 템플릿 예제다. 권한 적용도 폴더 재편도 하지 않았다.
- `views/_core`, `api/core`.
- 프론트엔드의 기존 타입 오류 다수(`vue-tsc` 기준). 빌드에는 영향이 없다(vite 빌드 통과).
  이번 작업으로 새로 생긴 오류는 없다.

---

## 5. 관련 SQL (실행 순서)

1. [`menu_permission_items.sql`](../sql/menu_permission_items.sql) — 메뉴별 권한 항목 컬럼 추가
2. [`menu_component_repath.sql`](../sql/menu_component_repath.sql) — component 경로 재배치
3. [`role_menu_backfill.sql`](../sql/role_menu_backfill.sql) — 역할-메뉴 권한 채우기
4. [`helpdesk_menu_seed.sql`](../sql/helpdesk_menu_seed.sql) — 헬프데스크 메뉴 (기존)

전부 반복 실행해도 안전하다.
