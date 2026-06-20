# 작업 계획서: 공통코드 폼의 비고(Remark) 입력 컴포넌트 오류 수정

## 1) 문제 요약
* **현상**: 공통코드 그룹 폼 및 코드 폼 모달에서 비고(`remark`) 입력란 컴포넌트가 화면에 정상적으로 렌더링되지 않고 보이지 않는 문제가 있습니다.
* **원인**: Vben Form 프레임워크에 정의되어 있지 않은 잘못된 컴포넌트명인 `'InputTextArea'`를 스키마에 정의하여 렌더링 오류가 발생했습니다.

---

## 2) 설계 요약
* **수정 대상 파일**: 
  * `fronts/apps/funeralv2/src/views/system/common-code/data.ts`
* **설계 상세**:
  * `groupFormSchema` 및 `codeFormSchema` 내의 비고(`remark`) 필드의 `component` 명칭을 기존 `'InputTextArea'`에서 Vben Form 어댑터의 표준 텍스트에리어 식별자인 `'Textarea'`로 수정하여 정상 노출시킵니다.

---

## 3) 구현 계획
* **Task 1: data.ts 스키마 비고 컴포넌트명 치환** (완료)
* **Task 2: 빌드 및 무결성 검증** (진행 중)

---

## 4) 자가 코드 리뷰 계획
* 스키마 이름 치환 후 정상적으로 타입 체크를 통과하는지 컴파일 안정성을 검사합니다.
