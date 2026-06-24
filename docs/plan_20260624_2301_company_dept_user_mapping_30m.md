# 작업 계획서: 회사-부서-사용자 연결 기능 개선 (조직도 기반 사용자 관리)

- **작성일**: 2026년 6월 24일 23시 01분
- **예상 소요 시간**: 30분

## 1. 문제 요약
- 기존의 회사와 사용자 간 직접 연결 관계(1:N)를 **회사 -> 부서 -> 사용자**의 계층적 구조로 개편.
- 사용자는 소속 부서(`DepartmentId`)를 통해 소속 회사(`CompanyId`)를 자연스레 매핑하도록 비즈니스 로직과 API를 고도화함.
- `company-user/index.vue` 화면을 **회사 목록 -> 선택 회사의 부서 조직도 트리 -> 선택 부서의 사용자 목록** 형태의 3단 인터페이스로 개선함.

## 2. 해결 설계 및 구현 계획
1. **백엔드 구조 변경 (`AuthServer`)**:
   - `IDepartmentService` 및 `DepartmentService`에 특정 부서의 사용자 조회, 부서 미지정 사용자 조회, 사용자 부서 배정 및 해제 메서드 구현.
   - `SystemEndpoints.cs` 내에 `/system/dept` 하위로 사용자 매핑 관련 4가지 API 엔드포인트 등록.
   - 부서 배정 시 사용자의 `DepartmentId`를 저장하고 부서 엔티티에 등록된 `CompanyId`를 읽어 사용자의 `CompanyId`로 싱크 처리.
   - 부서 해제 시 사용자의 `DepartmentId`와 `CompanyId`를 모두 null로 처리하여 무소속 지정.
2. **프론트엔드 API 클라이언트 변경**:
   - `src/api/system/dept.ts`에 사용자 매핑 API(`getDeptUsers`, `getEligibleDeptUsers`, `assignDeptUsers`, `removeDeptUsers`) 호출 함수 정의.
3. **프론트엔드 UI/UX 리팩토링**:
   - `src/views/system/company-user/index.vue` 화면을 3단 레이아웃(col-span-3, col-span-3, col-span-6)으로 재구성.
   - 중앙에 Ant Design Vue의 `Tree` 컴포넌트를 사용해 부서 목록을 트리 구조(조직도)로 렌더링.
   - 부서 선택 시 해당 부서 소속 사용자를 우측 테이블에 로드하고 추가 모달은 해당 회사의 부서 미배정 사용자를 불러오도록 수정.
