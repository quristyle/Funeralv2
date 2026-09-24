# 화물 거래처 신뢰정보 서비스 설계안

## 1. 문서 목적

화물운송업 종사자가 운송 전 거래처의 결제 이력과 거래 경험을 확인하고, 운송 후 실제 결제 결과를 기록할 수 있는 서비스를 구축한다.

기존 서비스의 핵심 아이디어를 참고하되 단순한 후기/블랙리스트 서비스가 아니라 다음을 목표로 한다.

- 거래처 사전 조회
- 실제 거래 및 결제 데이터 축적
- 결제 지연/미수금 관리
- 거래처별 객관적 통계 제공
- 차주의 거래 판단 지원
- 허위·악의적 정보 등록 방지
- 업체의 이의제기 및 사실관계 확인 지원

본 서비스는 현재 운영 중인 MFE/MSA 기반 시스템에 신규 MFE/MSA 영역으로 추가한다.

---

# 2. 서비스 개념

## 2.1 핵심 가치

> 거래하기 전에 거래처의 과거 결제 경험을 확인하고, 거래 후 실제 결제 결과를 기록하여 다음 거래자가 참고할 수 있도록 한다.

서비스 흐름:

```text
차주
 │
 ├─ 배차 제안 수신
 │
 ├─ 거래처 검색
 │      │
 │      ├─ 회사 기본정보
 │      ├─ 거래 건수
 │      ├─ 정상 결제
 │      ├─ 지연 결제
 │      ├─ 미지급
 │      └─ 최근 거래 이력
 │
 ├─ 거래 판단
 │
 ├─ 운송 수행
 │
 └─ 거래/결제 결과 등록
          │
          ↓
       거래 DB
          │
          ↓
     거래처 통계 갱신
```

---

# 3. 기존 시스템 편입 방향

현재 시스템이 MFE/MSA 구조로 운영되고 있으므로 신규 서비스도 독립적인 MFE와 MSA 집합으로 구성한다.

## 3.1 권장 구조

```text
                         기존 Web Host
                              │
                ┌─────────────┼─────────────┐
                │             │             │
             기존 MFE       기존 MFE      신규 MFE
                │             │             │
                │             │       CargoTrust MFE
                │             │             │
                └─────────────┼─────────────┘
                              │
                         API Gateway
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
   기존 MSA                 기존 MSA          CargoTrust MSA
                                                  │
                         ┌────────────────────────┼───────────────┐
                         │                        │               │
                    Company Service        Transaction       Settlement
                         │                    Service          Service
                         │                        │               │
                         └────────────────────────┼───────────────┘
                                                  │
                                             PostgreSQL
```

신규 서비스의 내부 서비스 분리는 초기 MVP에서는 과도하게 세분화하지 않는다.

---

# 4. MFE 구성

신규 MFE 이름 예시:

```text
CargoTrust
```

또는

```text
CargoCredit
CargoPartner
CargoSafe
```

등으로 결정할 수 있다.

추천:

> CargoTrust

의미가 직관적이며 특정 기존 서비스의 이름을 그대로 복제하지 않는다.

## 4.1 MFE 메뉴

```text
CargoTrust
├── 홈
├── 거래처 검색
├── 거래처 상세
├── 거래 등록
├── 결제 등록
├── 미수금
├── 내 거래
└── 내 정보
```

관리자 MFE:

```text
CargoTrust Admin
├── 대시보드
├── 거래처 관리
├── 거래 데이터 관리
├── 신고 관리
├── 이의제기 관리
├── 사용자 관리
├── 통계
└── 시스템 관리
```

---

# 5. 사용자 유형

## 5.1 차주

주요 사용자.

권한:

- 거래처 검색
- 거래처 상세 조회
- 거래 등록
- 결제 결과 등록
- 미수금 관리
- 거래 경험 작성
- 신고
- 이의제기 확인

## 5.2 운송사/주선사

거래처 정보의 대상이 될 수 있는 업체.

기능:

- 회사정보 확인
- 자신과 관련된 거래 데이터 확인
- 사실관계 이의제기
- 회사정보 수정 요청

## 5.3 관리자

- 업체 정보 관리
- 사용자 관리
- 거래 데이터 검증
- 신고 처리
- 이의제기 처리
- 비정상 데이터 탐지
- 통계

---

# 6. 거래처 검색

가장 중요한 화면이다.

검색 기준:

```text
회사명
사업자등록번호
대표자명
전화번호
주소
```

가급적 사업자등록번호를 거래처의 핵심 식별자로 사용한다.

## 검색 결과

```text
┌──────────────────────────────┐
│ ○○물류                      │
│ 사업자번호 123-45-67890      │
├──────────────────────────────┤
│ 거래 경험        128건       │
│ 정상 지급        109건       │
│ 지연 지급         15건       │
│ 미지급             4건       │
│                              │
│ 평균 지급 지연     4.2일     │
│                              │
│ 최근 거래          2026-09   │
└──────────────────────────────┘
```

---

# 7. 거래처 상세

거래처 상세는 별점보다 실제 거래 데이터를 중심으로 한다.

## 7.1 기본정보

```text
회사명
사업자등록번호
대표자
사업장 주소
업종
등록일
```

## 7.2 거래 통계

```text
전체 거래       128건
정상 지급       109건
지연 지급        15건
일부 지급         0건
미지급            4건
```

## 7.3 지급 통계

```text
평균 약속 지급일
평균 실제 지급일
평균 지연일
최장 지연일
최근 미지급 건수
```

## 7.4 기간별 변화

```text
최근 30일
최근 90일
최근 180일
최근 1년
전체
```

시간이 지나면서 거래처의 결제 패턴이 변화하는 것을 확인할 수 있어야 한다.

---

# 8. 거래 등록

실제 거래를 기준으로 데이터를 축적한다.

입력 항목:

```text
거래처
운송일
출발지
도착지
운송 유형
운송료
배차 경로
예정 지급일
실제 지급일
결제 상태
메모
```

결제 상태:

```text
예정
정상 지급
지연 지급
일부 지급
미지급
분쟁
```

---

# 9. 결제 등록

운송과 결제를 별도 이벤트로 관리한다.

예:

```text
운송
2026-09-20
450,000원

예정 지급일
2026-09-30

실제 지급일
2026-10-02

결과
지연 지급
지연 2일
```

이 구조를 사용하면 거래처별 지급 통계를 자동으로 계산할 수 있다.

---

# 10. 미수금

사용자의 개인 미수금 관리 기능.

```text
전체 미수금       3,280,000원

정상 예정         1,200,000원
지급 지연         1,580,000원
미지급              500,000원
```

거래별:

```text
○○물류
520,000원
예정 지급일 2026-09-20
현재 4일 지연

△△운송
380,000원
예정 지급일 2026-09-15
현재 9일 지연
```

---

# 11. 거래 경험/후기

단순 자유게시판보다 실제 거래와 연결한다.

```text
Transaction
     │
     └── Review
```

후기는 가능하면 실제 거래 데이터가 있는 사용자만 작성하도록 한다.

후기 예:

```text
거래일: 2026-09-20
운송료: 450,000원
지급 결과: 정상 지급

추가 내용:
약속된 지급일에 정상 입금되었습니다.
```

---

# 12. 위험정보 표현 방식

서비스가 업체를 임의로 "악성업체", "사기 업체" 등으로 판정하지 않도록 한다.

대신 객관적인 데이터를 제공한다.

예:

```text
최근 90일 거래 42건

정상 지급      36건
지연 지급       5건
미지급          1건

평균 지연       4.2일

최근 미지급 사례
2026-08-21
380,000원
```

사용자가 데이터를 보고 판단할 수 있도록 한다.

---

# 13. 데이터 신뢰성

이 서비스에서 가장 중요한 기술적/운영적 문제다.

## 13.1 문제

다음과 같은 문제가 발생할 수 있다.

- 허위 거래 등록
- 경쟁 업체의 악의적 신고
- 동일 사용자의 반복 신고
- 회사명 오인
- 동명이 업체
- 사업자번호 오류
- 거래금액 허위 입력
- 감정적인 후기
- 오래된 거래정보

## 13.2 대응

```text
거래 등록
   │
   ├─ 사용자 인증
   ├─ 사업자번호 확인
   ├─ 중복 거래 검사
   ├─ 비정상 패턴 검사
   └─ 신고/검토
```

가능하다면 거래 증빙을 선택적으로 제출할 수 있게 한다.

예:

```text
전자세금계산서
운송장
배차내역
입금내역
거래명세서
```

민감한 증빙은 일반 사용자에게 공개하지 않고 관리자 검증용으로만 사용한다.

---

# 14. 업체 이의제기

업체도 자신의 정보에 문제가 있다고 판단할 경우 이의제기할 수 있어야 한다.

예:

```text
[이의제기]

대상 거래
2026-08-21
380,000원

이의 사유
□ 거래 사실 없음
□ 이미 지급 완료
□ 금액 오류
□ 다른 업체
□ 기타

증빙자료 첨부
[파일 선택]

[이의제기 제출]
```

관리자가 검토한다.

---

# 15. 신고 시스템

사용자가 잘못된 정보를 신고할 수 있다.

신고 유형:

```text
허위 거래
중복 거래
사업자 오인
욕설/비방
개인정보 노출
광고/스팸
기타
```

신고 처리 상태:

```text
접수
검토중
반려
수정 요청
삭제
처리 완료
```

---

# 16. PostgreSQL DB 설계

MVP에서는 하나의 CargoTrust DB를 권장한다.

## 16.1 Company

```sql
CREATE TABLE company (
    company_id          BIGSERIAL PRIMARY KEY,
    business_number     VARCHAR(20) NOT NULL,
    company_name        VARCHAR(200) NOT NULL,
    ceo_name            VARCHAR(100),
    address             VARCHAR(500),
    phone               VARCHAR(50),
    business_type       VARCHAR(100),
    status              VARCHAR(30),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_company_business_number
        UNIQUE (business_number)
);
```

## 16.2 User

```sql
CREATE TABLE app_user (
    user_id             BIGSERIAL PRIMARY KEY,
    external_user_id    VARCHAR(100) NOT NULL,
    user_type           VARCHAR(30) NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_app_user_external
        UNIQUE (external_user_id)
);
```

기존 Auth MSA의 사용자 ID를 참조하는 구조를 권장한다.

## 16.3 Transaction

```sql
CREATE TABLE cargo_transaction (
    transaction_id          BIGSERIAL PRIMARY KEY,
    company_id              BIGINT NOT NULL,
    user_id                 BIGINT NOT NULL,

    transport_date          DATE NOT NULL,
    origin                  VARCHAR(300),
    destination             VARCHAR(300),
    transport_type          VARCHAR(50),

    amount                  NUMERIC(15,2) NOT NULL,

    expected_payment_date   DATE,
    actual_payment_date     DATE,

    payment_status          VARCHAR(30) NOT NULL,

    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_transaction_company
        FOREIGN KEY (company_id)
        REFERENCES company(company_id)
);
```

## 16.4 Review

```sql
CREATE TABLE transaction_review (
    review_id           BIGSERIAL PRIMARY KEY,
    transaction_id      BIGINT NOT NULL,
    user_id             BIGINT NOT NULL,
    content             TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_review_transaction
        FOREIGN KEY (transaction_id)
        REFERENCES cargo_transaction(transaction_id)
);
```

## 16.5 Dispute

```sql
CREATE TABLE transaction_dispute (
    dispute_id          BIGSERIAL PRIMARY KEY,
    transaction_id      BIGINT NOT NULL,
    company_id          BIGINT NOT NULL,
    reason              VARCHAR(50) NOT NULL,
    content             TEXT,
    status              VARCHAR(30) NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    resolved_at         TIMESTAMPTZ
);
```

## 16.6 Report

```sql
CREATE TABLE report (
    report_id           BIGSERIAL PRIMARY KEY,
    target_type         VARCHAR(30) NOT NULL,
    target_id           BIGINT NOT NULL,
    reporter_user_id    BIGINT NOT NULL,
    reason              VARCHAR(50) NOT NULL,
    content             TEXT,
    status              VARCHAR(30) NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    resolved_at         TIMESTAMPTZ
);
```

---

# 17. 서비스 API

## Company

```http
GET /api/cargotrust/companies
GET /api/cargotrust/companies/{companyId}
GET /api/cargotrust/companies/search?q=
POST /api/cargotrust/companies
```

## Transaction

```http
GET /api/cargotrust/transactions
GET /api/cargotrust/transactions/{id}
POST /api/cargotrust/transactions
PUT /api/cargotrust/transactions/{id}
```

## Payment

```http
POST /api/cargotrust/transactions/{id}/payment
```

## Review

```http
GET /api/cargotrust/companies/{companyId}/reviews
POST /api/cargotrust/transactions/{id}/review
```

## Report

```http
POST /api/cargotrust/reports
GET /api/cargotrust/reports/{id}
```

## Dispute

```http
POST /api/cargotrust/disputes
GET /api/cargotrust/disputes/{id}
```

---

# 18. MSA 구성

초기에는 지나치게 많은 MSA로 분리하지 않는다.

추천:

```text
CargoTrustServer
```

하나로 시작한다.

내부 모듈:

```text
CargoTrustServer
├── Company
├── Transaction
├── Payment
├── Review
├── Report
├── Dispute
└── Statistics
```

서비스 규모가 증가하면 분리한다.

향후:

```text
CargoCompanyServer
CargoTransactionServer
CargoSettlementServer
CargoTrustServer
```

등으로 확장할 수 있다.

---

# 19. API Gateway

외부에서는 하나의 서비스처럼 보이게 한다.

```text
/api/cargotrust/*
```

Gateway:

```text
/api/cargotrust/companies
        ↓
CargoTrustServer

/api/cargotrust/transactions
        ↓
CargoTrustServer
```

향후 MSA 분리:

```text
/api/cargotrust/companies
        ↓
CargoCompanyServer

/api/cargotrust/transactions
        ↓
CargoTransactionServer

/api/cargotrust/settlements
        ↓
CargoSettlementServer
```

---

# 20. 인증

기존 AuthServer를 재사용한다.

```text
Web/MFE
  │
  ↓
AuthServer
  │
  ↓
JWT / Access Token
  │
  ↓
API Gateway
  │
  ↓
CargoTrustServer
```

신규 서비스에서 별도 회원 시스템을 만들지 않는다.

기존 사용자 ID를 CargoTrust의 app_user.external_user_id와 연결한다.

---

# 21. MFE 기술 구조

현재 프론트 시스템을 그대로 활용한다.

예:

```text
web/
└── src/
    └── Apps/
        └── JSini.Web.CargoTrust/
```

Vue 기반 별도 MFE로 구성한다.

권장:

```text
JSini.Web.CargoTrust
├── pages
│   ├── Home
│   ├── CompanySearch
│   ├── CompanyDetail
│   ├── Transaction
│   ├── Settlement
│   └── MyTransactions
│
├── components
├── api
├── stores
├── types
└── router
```

---

# 22. 관리자 MFE

```text
JSini.Web.CargoTrust.Admin
```

메뉴:

```text
Dashboard
Company
Transactions
Payments
Reviews
Reports
Disputes
Users
Statistics
System
```

기존 관리자 UI 기술과 동일하게 구성한다.

---

# 23. 화면 목록

## 사용자

```text
CT-001 홈
CT-002 거래처 검색
CT-003 검색 결과
CT-004 거래처 상세
CT-005 거래 등록
CT-006 결제 등록
CT-007 거래 상세
CT-008 미수금
CT-009 내 거래
CT-010 후기 작성
CT-011 신고
CT-012 알림
```

## 관리자

```text
CTA-001 대시보드
CTA-002 거래처
CTA-003 거래
CTA-004 결제
CTA-005 후기
CTA-006 신고
CTA-007 이의제기
CTA-008 사용자
CTA-009 통계
```

---

# 24. 홈 화면

핵심은 검색이다.

```text
┌────────────────────────────┐
│       CargoTrust           │
│                            │
│ 거래처를 검색하세요        │
│ ┌────────────────────────┐ │
│ │ 회사명 / 사업자번호 🔍 │ │
│ └────────────────────────┘ │
│                            │
│ 최근 검색                  │
│                            │
│ ○○물류                    │
│ △△운송                    │
│                            │
│ ───────────────────────── │
│                            │
│ 내 미수금                  │
│ 1,580,000원                │
│                            │
│ [거래 등록]                │
└────────────────────────────┘
```

---

# 25. MVP 개발 순서

## Phase 1

```text
회원 인증
거래처 검색
거래처 등록
거래처 상세
```

## Phase 2

```text
거래 등록
결제 등록
거래 내역
기본 통계
```

## Phase 3

```text
미수금
후기
신고
이의제기
```

## Phase 4

```text
관리자
데이터 검증
통계
비정상 데이터 탐지
```

## Phase 5

```text
알림
증빙자료
고급 검색
거래처 변화 추이
```

---

# 26. 핵심 통계 계산

거래처의 통계는 원본 거래 데이터에서 계산한다.

예:

```text
total_count
normal_payment_count
delayed_payment_count
partial_payment_count
unpaid_count
average_delay_days
```

예를 들어:

```text
전체 128건

정상 109
지연 15
미지급 4

정상률 = 109 / 128 * 100
       = 85.16%
```

단, 화면에서는 통계 산출 기준과 기간을 함께 표시해야 한다.

---

# 27. 통계 신뢰도

거래 건수가 적을 때는 통계를 과도하게 해석하지 않도록 한다.

예:

```text
거래 2건
정상 2건
```

과

```text
거래 2,000건
정상 1,700건
```

은 동일하게 표시하면 안 된다.

따라서:

```text
거래 경험 1~4건
거래 경험 5~19건
거래 경험 20건 이상
```

등의 데이터 규모를 함께 표시하는 것을 고려한다.

---

# 28. 검색 최적화

거래처 검색은 핵심 기능이므로 PostgreSQL 인덱스를 적극적으로 사용한다.

```sql
CREATE INDEX ix_company_name
ON company(company_name);

CREATE INDEX ix_company_business_number
ON company(business_number);

CREATE INDEX ix_transaction_company
ON cargo_transaction(company_id);

CREATE INDEX ix_transaction_payment_status
ON cargo_transaction(payment_status);

CREATE INDEX ix_transaction_expected_payment
ON cargo_transaction(expected_payment_date);
```

대규모 데이터에서는 PostgreSQL의 `pg_trgm` 등을 활용한 부분 문자열 검색을 검토한다.

---

# 29. 개인정보/보안

공개 정보와 내부 정보를 명확히 분리한다.

## 공개 가능 정보

```text
회사명
사업자번호 일부
업종
지역
집계된 거래 통계
검증된 거래 데이터의 비식별 요약
```

## 비공개

```text
사용자 이름
전화번호
계좌번호
개인 주소
거래 증빙 원본
내부 관리자 메모
```

특히 증빙자료는 일반 사용자가 볼 수 없도록 한다.

---

# 30. 서비스 운영 정책

데이터가 서비스의 핵심 자산이므로 운영 정책을 초기부터 만든다.

필수 기능:

```text
이의제기
신고
삭제 요청
정보 수정
증빙 제출
관리자 검토
처리 이력
```

모든 관리자 변경은 Audit Log를 남긴다.

```sql
CREATE TABLE audit_log (
    audit_id        BIGSERIAL PRIMARY KEY,
    actor_user_id   BIGINT,
    action          VARCHAR(100) NOT NULL,
    target_type     VARCHAR(50),
    target_id       BIGINT,
    before_data     JSONB,
    after_data      JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

---

# 31. 향후 확장

서비스가 성장하면 다음 기능을 추가할 수 있다.

## 거래처 자동 조회

배차 메시지나 거래명세서에서 사업자번호를 추출하여 자동 검색.

## 미수금 알림

```text
D-3
지급 예정입니다.

D-Day
오늘 지급 예정입니다.

D+3
지급이 지연되고 있습니다.
```

## 반복 거래처

자주 거래하는 업체를 즐겨찾기.

## 거래처 변화 알림

```text
○○물류의 최근 30일 결제 패턴이 변경되었습니다.
```

## 증빙 기반 거래 인증

전자세금계산서, 운송장, 입금내역 등과 연결.

---

# 32. 수익모델

초기에는 무료 사용자를 확보하는 것을 우선한다.

가능한 모델:

```text
무료
- 거래처 검색
- 기본 통계
- 거래 등록

유료
- 상세 통계
- 고급 검색
- 거래처 변화 알림
- 미수금 자동관리
- 증빙 관리
- 기업용 기능
```

기업용:

```text
운송사/주선사
기업 계정
다중 사용자
정산 관리
거래처 관리
API
```

---

# 33. 개발 시 주의할 점

## 1. 블랙리스트 서비스로 만들지 않는다.

실제 데이터와 사실관계를 중심으로 한다.

## 2. 별점보다 거래 데이터를 우선한다.

```text
★★★★★
```

보다

```text
최근 90일
42건
정상 36
지연 5
미지급 1
```

이 훨씬 유용하다.

## 3. 사업자등록번호를 핵심 식별자로 사용한다.

회사명을 Primary Key처럼 사용하지 않는다.

## 4. 거래와 후기를 분리한다.

후기가 없어도 거래 통계가 만들어질 수 있어야 한다.

## 5. 모든 변경 이력을 기록한다.

분쟁 발생 시 데이터의 변경 과정을 확인할 수 있어야 한다.

---

# 34. 최종 시스템 구조

```text
                         ┌────────────────────┐
                         │   기존 Web Host     │
                         └─────────┬──────────┘
                                   │
                    ┌──────────────┴──────────────┐
                    │                             │
              기존 MFE들                    CargoTrust MFE
                                                  │
                                                  │
                                           API Gateway
                                                  │
                                           CargoTrustServer
                                                  │
                     ┌────────────────────────────┼─────────────────────┐
                     │                            │                     │
                 Company                     Transaction           Settlement
                     │                            │                     │
                     ├───────────────┬────────────┘                     │
                     │               │                                  │
                  Review          Report                             Payment
                     │               │                                  │
                     └───────────────┴──────────────────────────────────┘
                                             │
                                      PostgreSQL
                                             │
                                  ┌──────────┴──────────┐
                                  │                     │
                               거래 데이터            통계
                                  │                     │
                                  └──────────┬──────────┘
                                             │
                                        사용자 판단
```

---

# 35. 1차 개발 목표

첫 번째 배포 버전에서는 다음만 완성한다.

```text
[사용자]

로그인
   ↓
거래처 검색
   ↓
거래처 상세
   ↓
거래 등록
   ↓
결제 결과 등록
   ↓
거래처 통계


[관리자]

거래처 관리
거래 관리
사용자 관리
신고 관리
이의제기 관리
```

이 구조가 완성되면 실제 사용자에게 테스트 서비스를 제공할 수 있다.

---

# 36. 프로젝트 권장 명칭

개발 프로젝트:

```text
CargoTrust
```

MFE:

```text
JSini.Web.CargoTrust
```

Admin MFE:

```text
JSini.Web.CargoTrust.Admin
```

MSA:

```text
CargoTrustServer
```

Database:

```text
cargo_trust
```

API prefix:

```text
/api/cargotrust
```

---

# 37. 다음 개발 문서

실제 구현 단계에서는 이 문서에서 다음 문서를 분리한다.

```text
docs/
├── cargotrust/
│   ├── 01-service-overview.md
│   ├── 02-requirements.md
│   ├── 03-screen-design.md
│   ├── 04-db-design.md
│   ├── 05-api-design.md
│   ├── 06-mfe-architecture.md
│   ├── 07-msa-architecture.md
│   ├── 08-security.md
│   ├── 09-data-policy.md
│   └── 10-development-plan.md
```

이 문서는 그중 상위 설계 문서 역할을 한다.
