# 구현 계획서: 역할별 사용자 지정 및 메뉴 세부 권한 관리 기능

본 문서는 역할(권한)에 사용자를 맵핑/해제하고, 역할에 메뉴를 맵핑하면서 세부 권한(열람, 조회, 추가, 삭제, 수정, 출력, 엑셀, Cust1~8)을 제어할 수 있는 화면 및 백엔드 CRUD 기능을 구현하기 위한 계획서입니다.

---

## 1. 개요 및 요구사항
1. **역할-사용자 지정/해제**:
   - 특정 역할에 여러 명의 사용자를 매핑하거나 매핑을 제거하는 기능.
2. **역할-메뉴 및 세부 권한 관리**:
   - 특정 역할에 메뉴를 할당하거나 해제.
   - 각 메뉴별로 15가지 세부 권한 설정 가능:
     - 열람 (`CanView`), 조회 (`CanSearch`), 추가 (`CanCreate`), 삭제 (`CanDelete`), 수정 (`CanUpdate`), 출력 (`CanPrint`), 엑셀 (`CanExcel`)
     - 사용자 정의 권한 1~8 (`CanCust1` ~ `CanCust8`)
3. **백엔드 구축**:
   - 엔티티 모델 (`RoleAccount`, `RoleMenu`) 추가 및 `AppDbContext` 등록.
   - DB 마이그레이션 (`AddRolePermissionMapping`) 추가 및 DB 업데이트 실행.
   - DTO 설계 및 비즈니스 서비스 (`RolePermissionService`), API 엔드포인트 작성.
4. **프론트엔드 구축**:
   - 역할 권한 관리 신규 화면 생성 (`views/system/role-custom/index.vue`).
   - 좌측: 역할 목록 그리드.
   - 우측: 탭(Tab) 영역 분할:
     - 탭 1: 사용자 맵핑 리스트 및 추가/제거 기능.
     - 탭 2: 메뉴 트리 테이블 기반의 세부 권한 체크박스 관리.

---

## 2. DB 및 엔티티 설계

### 2.1. `scom.role_accounts` 테이블 (역할 - 사용자 계정 매핑)
| 컬럼명 | 데이터 타입 | 제약조건 | 설명 |
| :--- | :--- | :--- | :--- |
| `id` | `int` | PK, Identity | 기본 키 |
| `role_id` | `varchar(255)` | FK (scom.roles.id), Unique(role_id, account_id) | 역할 고유 키 |
| `account_id` | `varchar(255)` | FK (scom.accounts.id) | 사용자 계정 고유 키 |
| `created_at` | `timestamp` | | 생성 일시 (Auditing) |
| `created_by` | `varchar(255)` | | 생성자 (Auditing) |
| `updated_at` | `timestamp` | | 수정 일시 (Auditing) |
| `updated_by` | `varchar(255)` | | 수정자 (Auditing) |

### 2.2. `scom.role_menus` 테이블 (역할 - 메뉴 세부 권한 매핑)
| 컬럼명 | 데이터 타입 | 제약조건 | 설명 |
| :--- | :--- | :--- | :--- |
| `id` | `int` | PK, Identity | 기본 키 |
| `role_id` | `varchar(255)` | FK (scom.roles.id), Unique(role_id, menu_id) | 역할 고유 키 |
| `menu_id` | `varchar(255)` | FK (scom.system_menus.id) | 메뉴 고유 키 |
| `can_view` | `boolean` | Default: false | 열람 권한 |
| `can_search` | `boolean` | Default: false | 조회 권한 |
| `can_create` | `boolean` | Default: false | 추가 권한 |
| `can_delete` | `boolean` | Default: false | 삭제 권한 |
| `can_update` | `boolean` | Default: false | 수정 권한 |
| `can_print` | `boolean` | Default: false | 출력 권한 |
| `can_excel` | `boolean` | Default: false | 엑셀 권한 |
| `can_cust1` ~ `can_cust8` | `boolean` | Default: false | 커스텀 권한 1 ~ 8 |
| `created_at` | `timestamp` | | 생성 일시 (Auditing) |
| `created_by` | `varchar(255)` | | 생성자 (Auditing) |
| `updated_at` | `timestamp` | | 수정 일시 (Auditing) |
| `updated_by` | `varchar(255)` | | 수정자 (Auditing) |

---

## 3. 백엔드 구현 계획

### 3.1. DTO 설계
* `RoleAccountAssignDto`: 역할에 사용자를 지정/제거할 때 사용할 요청 모델.
* `RoleMenuSaveDto`: 세부 권한 정보를 받아 한 번에 저장/업데이트하기 위한 모델.
* `RoleUserDto`: 해당 역할에 매핑된 사용자 정보를 반환하기 위한 모델.

### 3.2. 서비스 구현 (`RolePermissionService`)
* `GetUsersByRoleAsync(string roleId)`: 특정 역할의 사용자 목록 조회.
* `AssignUsersToRoleAsync(string roleId, List<string> accountIds)`: 특정 역할에 사용자 여러 명을 일괄 할당.
* `RemoveUserFromRoleAsync(string roleId, string accountId)`: 특정 역할에서 사용자 한 명 해제.
* `GetMenusByRoleAsync(string roleId)`: 특정 역할에 대한 전체 메뉴 세부 권한 상태 트리 목록 반환.
* `SaveRoleMenusAsync(string roleId, List<RoleMenuSaveDto> dtos)`: 특정 역할의 메뉴 세부 권한을 일괄 업데이트.

### 3.3. 엔드포인트 바인딩 (`SystemEndpoints.cs` 또는 신규 엔드포인트 파일)
* `GET /system/role-permission/roles/{roleId}/users`
* `POST /system/role-permission/roles/{roleId}/users/assign`
* `DELETE /system/role-permission/roles/{roleId}/users/{userId}`
* `GET /system/role-permission/roles/{roleId}/menus`
* `POST /system/role-permission/roles/{roleId}/menus/save`

---

## 4. DB 마이그레이션
* `dotnet ef migrations add AddRolePermissionMapping` 명령어를 `AuthServer` 디렉토리 내에서 실행하여 스키마 마이그레이션 스크립트를 빌드.
* `dotnet ef database update` 실행으로 로컬 DB 구조 최신화.

---

## 5. 프론트엔드 구현 계획

### 5.1. 신규 라우터 등록
* `apps/funeralv2/src/router/routes/modules/system.ts` 혹은 해당 영역에 `/system/role-custom` 엔트리를 개설하여 어드민 메뉴에 노출.

### 5.2. UI/UX 구성 (`views/system/role-custom/index.vue`)
* **좌우 분할 스플릿 레이아웃**:
  * **좌측 (Width: 35%)**: 역할 목록 그리드 (역할 선택 시 우측 상세 패널 자동 갱신).
  * **우측 (Width: 65%)**: Antd 탭 컨트롤 배치.
    * **탭 1: 사용자 지정 (`RoleUserTab.vue`)**:
      * 지정된 사용자 목록 그리드 및 "사용자 추가" 버튼 제공.
      * "사용자 추가" 클릭 시, 아직 이 역할이 없는 사용자 전체 목록에서 체크 선택하여 추가할 수 있는 팝업 모달 제공.
      * 그리드 행에 "지정 해제" 버튼 배치.
    * **탭 2: 메뉴 및 세부 권한 (`RoleMenuTab.vue`)**:
      * VXETable의 Tree Grid를 사용하여 메뉴 계층 트리 표출.
      * 컬럼 구성: 메뉴명, 열람, 조회, 추가, 삭제, 수정, 출력, 엑셀, Cust1 ~ Cust8 (전부 Checkbox 컴포넌트로 렌더링).
      * 상단에 "권한 저장" 일괄 반영 버튼 제공.

---

## 6. 품질 및 안정성 유지 정책
* **한글 주석 정책**: 백엔드 C# 소스 코드 및 프론트엔드 Vue/TypeScript 소스 코드의 주석은 명확하게 한글(한국어)로 작성.
* **타입 안전성**: strictNullChecks 및 TypeScript strict 정책 준수.
* **에러 처리**: 중복 매핑 시나리오 예외 처리 구현.
