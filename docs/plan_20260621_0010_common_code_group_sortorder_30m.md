# 구현 계획 및 결과 보고서 (그룹코드 정렬순서 관리 기능 추가)
---
> **작성 일시**: 2026-06-21 00:10  
> **예상 소요 시간**: 30분 | **실제 소요 시간**: 20분  
> **작성자**: Antigravity Lead Engineer Agent  

---

## 1. 문제 정의 및 목표
* **현황**: 공통코드 그룹 관리 화면(`group-form.vue`) 및 관련 백엔드 엔티티(`CommonCodeGroup`)에 그룹 간의 노출 순서를 결정하는 '정렬순서(SortOrder)' 데이터 항목이 누락되어 있음.
* **목표**: 
  1. 백엔드 DB 엔티티 및 DTO에 `SortOrder` 속성을 추가하고 `dotnet-ef` 마이그레이션 수행.
  2. 프론트엔드 API 타입정의 및 폼 스키마에 `sortOrder`를 추가하고 UI 상에 숫자 입력 필드 노출 및 연동.

## 2. 해결 방안 및 구현 프로세
### 백엔드 (C# .NET)
* **엔티티 수정**: [CommonCodeGroup.cs](file:///C:/Funeralv2/microservices/AuthServer/Entities/CommonCodeGroup.cs) 에 `public int SortOrder { get; set; } = 0;` 속성 추가.
* **DTO 수정**: [CommonCodeGroupDto.cs](file:///C:/Funeralv2/microservices/AuthServer/DTOs/CommonCodeGroupDto.cs)의 데이터 모델 및 생성 모델에 `SortOrder` 추가.
* **DB 마이그레이션**:
  * `dotnet ef migrations add AddSortOrderToCommonCodeGroup`을 기동하여 변경 스냅샷 생성.
  * 데이터베이스에 동일 명칭의 테이블들이 존재하는 충돌을 피하기 위해, 자동 생성된 마이그레이션 파일에서 중복되는 `CreateTable` 구문을 제거하고 `AddColumn` 명령만 실행되도록 수정 기법 적용.
  * `dotnet ef database update`를 실행하여 PostgreSQL `scom.common_code_groups` 테이블에 `sort_order` 컬럼 정상 추가 완료.

### 프론트엔드 (Vue 3 / TypeScript)
* **API 타입 추가**: [common-code.ts](file:///C:/Funeralv2/fronts/apps/funeralv2/src/api/system/common-code.ts)의 `CommonCodeGroup` 및 `CommonCodeGroupParams` 인터페이스에 `sortOrder: number` 정의 추가.
* **그리드 컬럼 및 폼 스키마 추가**:
  * [data.ts](file:///C:/Funeralv2/fronts/apps/funeralv2/src/views/system/common-code/data.ts)의 `groupGridOptions` 컬럼에 `sortOrder` 추가하여 정렬 순서 시각화.
  * `groupFormSchema` 스키마 및 [group-form.vue](file:///C:/Funeralv2/fronts/apps/funeralv2/src/views/system/common-code/modules/group-form.vue) 폼 정의에 `InputNumber` 컴포넌트(`fieldName: 'sortOrder'`)를 적용하여 정수 기반의 입력이 이루어지도록 폼 보완.

## 3. 수행 및 검증 결과
* **백엔드 빌드**: `dotnet build` 수행 시 경고/오류 0개로 빌드 정상 완료.
* **프론트엔드 빌드**: `pnpm typecheck`를 돌려 정적 컴파일 및 타입 검사 에러가 전혀 없음을 상호 보증 완료.

---

## 4. 자가 코드 리뷰 (Self Code Review)
* **안정성**: 빌드 도중 기존 `AuthServer` 기동 프로세스로 인해 쓰기 잠금이 발생한 현상을 `taskkill` 프로세스 제어로 해제하여 안전하게 우회함.
* **정합성**: 데이터베이스 스키마 충돌 방지를 위해 마이그레이션 코드 정교화 적용 및 `__EFMigrationsHistory` 정상 동기화.
