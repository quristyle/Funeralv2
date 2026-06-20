# 구현 계획 및 결과 보고서 (공통코드 다국어 리소스 마이그레이션)
---
> **작성 일시**: 2026-06-21 00:06  
> **예상 소요 시간**: 15분 | **실제 소요 시간**: 10분  
> **작성자**: Antigravity Lead Engineer Agent  

---

## 1. 문제 정의 및 목표
* **현황**: 마이그레이션된 공통코드의 `i18n_key` (다국어 키)는 데이터베이스 테이블 `scom.common_codes`에는 존재하지만, 실질적인 번역 리소스 테이블인 `scom.i18n_resources`에 등록되어 있지 않아 다국어 지원 렌더링 시 빈값으로 노출될 수 있음.
* **목표**: `scom.common_codes` 에 있는 모든 다국어 키들을 수집하여, `scom.i18n_resources` 테이블에 존재하지 않는 번역값(`ko-KR`, `en-US` 로케일)을 대조 인서트 처리.

## 2. 해결 방안
* **로케일 정보 분석**: `scom.i18n_resources` 내 사용 중인 언어는 `ko-KR` 과 `en-US` 임을 파악함.
* **다국어 매핑 로직**:
  1. `ko-KR`: 공통코드 한글 명칭(`code_name`)을 번역어로 삽입.
  2. `en-US`: 공통코드 영문 코드값(`code_value`)에 대해 단어 단위 첫 글자 대문자화(`title()`)를 수행하여 자연스러운 영문 표기명으로 가공 삽입. (예: `GANGWON` ➔ `Gangwon`, `MONITORING` ➔ `Monitoring`). 단, 성별 문자 `M` ➔ `Male`, `F` ➔ `Female`로 가독성 고도화 매핑 처리.
* **보안 및 무결성**: 중복 확인 조회를 사전에 수행하여 기존에 존재하는 번역 자원은 훼손하거나 덮어쓰지 않도록 가드 설계.

## 3. 수행 및 검증 결과
* **스크립트 파일**: [db_insert_i18n.py](file:///C:/Users/jjstyle/.gemini/antigravity-cli/brain/e32e2a72-5f3b-4028-a6c6-636a60665be7/scratch/db_insert_i18n.py)
* **적재 결과**:
  * 총 **140개**의 다국어 번역 데이터(70개 키 * 2개 언어)가 예외 없이 완벽하게 트랜잭션 단위로 최종 적재 성공.

---

## 4. 자가 코드 리뷰 (Self Code Review)
* **일관성**: 시스템에서 제공하는 표준 다국어 카테고리(`category = 'common'`)와 생성자 식별값(`created_by = 'MigrationSystem'`)을 통일 적용함.
* **안정성**: 덮어쓰기 위험이 있는 무조건적 Insert 대신 `key + locale` 유일 복합성 기준의 검사 가드를 수행하여 멱등성 및 정합성 보장.
