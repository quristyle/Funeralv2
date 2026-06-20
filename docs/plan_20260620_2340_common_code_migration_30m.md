# 구현 계획 및 결과 보고서 (공통코드 대량 마이그레이션)
---
> **작성 일시**: 2026-06-20 23:40  
> **예상 소요 시간**: 30분 | **실제 소요 시간**: 20분  
> **작성자**: Antigravity Lead Engineer Agent  

---

## 1. 문제 정의 및 목표
* **현황**: 사용자가 분석 및 정제를 위해 제공한 86개의 공통코드 데이터(`rowid`, `code`, `codename`, `parent_rowid` 관계 구조)가 존재함.
* **목표**: 
  1. 데이터를 분석하여 오타를 교정하고 한글 코드값을 시스템 표준에 맞는 적절한 영문 소문자/단일 문자 식별 코드로 변환.
  2. 관계형 트리 구조(그룹-코드)를 형성하여 데이터베이스의 `scom.common_code_groups` 및 `scom.common_codes` 테이블에 트랜잭션 단위로 직접 안전하게 적재(Insert).
  3. `is_deleted` NOT NULL 제약조건 및 `is_leaf`, `level`, `status`, `sort_order` 등의 제약 및 기본값 필드를 처리하여 무결성 유지.

## 2. 데이터 분석 및 매핑 설계
* **오타 정제**:
  * `complate` ➔ `complete` (완료)
  * `quetion` ➔ `question` (문의)
* **코드값 영문화 및 표준화**:
  * 성별: `남` ➔ `M`, `여` ➔ `F`
  * 빈호실구분: `empty` (공실), `full` ➔ `occupied` (입실)
  * 화면비율: `MSIZE1~4` ➔ `ratio_16_10`, `ratio_16_9`, `ratio_4_3`, `free`
  * 행정시도: 한국어 명칭(서울특별시 등) ➔ 영문 소문자 표준(`seoul`, `busan` 등)
  * 개인환경설정: `page_tab_view` 등 한글과 결합된 코드를 `hide_page_tabs`, `init_sidebar_collapsed` 등 표준 스네이크 케이스로 변경.
  * 종교 및 사망종류, 사망장소 등도 표준 영문 키워드로 일괄 매핑.

## 3. 구현 방식
* **도구**: Python 3 및 `psycopg2-binary` 패키지를 통한 직접 DB 접속 적재.
* **보안 및 트랜잭션**:
  * `conn.commit()`을 통해 최종 검증이 끝난 후 일괄 적재하며, 오류 발생 시 `conn.rollback()`이 수행되는 트랜잭션 구조 보장.
  * 중복 적재 방지를 위하여 `SELECT` 조회를 통해 이미 존재하는 `group_code` 및 `code_value`에 대해서는 건너뛰는(Skip) 방어적 코드 설계 적용.
  * `is_deleted` 컬럼이 NOT NULL 제약 조건이 걸려 있으므로, 쿼리 파라미터 맨 뒤에 `False` 값을 바인딩하여 제약 조건 오류 해결.

## 4. 수행 및 검증 결과
* **스크립트 파일**: [db_insert.py](file:///C:/Users/jjstyle/.gemini/antigravity-cli/brain/e32e2a72-5f3b-4028-a6c6-636a60665be7/scratch/db_insert.py)
* **검증 스크립트 파일**: [db_verify.py](file:///C:/Users/jjstyle/.gemini/antigravity-cli/brain/e32e2a72-5f3b-4028-a6c6-636a60665be7/scratch/db_verify.py)
* **수행 결과**:
  * 공통코드 그룹(`scom.common_code_groups`): **15개 그룹** 삽입 완료.
  * 세부 공통코드(`scom.common_codes`): **71개 코드** 삽입 완료.
  * 총 **86개 레코드**가 누락 없이 무결하게 매핑 적재됨을 검증 쿼리를 통해 교차 확인 완료.

---

## 5. 자가 코드 리뷰 (Self Code Review)
* **기능성**: 사용자가 요청한 한글 및 오타 포함 데이터의 적절한 코드명 및 코드값 변경 요건 충족 완료.
* **안정성**: `is_deleted` 누락으로 인한 제약조건 오류를 즉시 인지하여 SQL 수정 반영함. `try-except` 예외 발생 시의 롤백 보장 확인.
* **관찰성**: 터미널 출력을 통해 개별 데이터가 스킵되었는지, 신규 생성되었는지의 추적이 원활하도록 로깅함.
