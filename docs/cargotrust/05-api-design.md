# CargoTrust API 계약

상위 설계는 [01-service-overview.md](01-service-overview.md), 스키마 정본은
[deploy/sql/cargotrust-schema-2026-09-24.sql](../../deploy/sql/cargotrust-schema-2026-09-24.sql).
**이 문서가 백엔드(CargoTrustServer)와 프론트(두 MFE)의 약속이다.** 한쪽만 고치지 않는다.

## 경로와 봉투

| | |
|---|---|
| 게이트웨이 | `/api/cargotrust/{**}` → `PathRemovePrefix /api/cargotrust` → CargoTrustServer `http://127.0.0.1:5500` |
| 프론트가 부르는 경로 | `GatewayClient` 기준 `cargotrust/...` (예: `cargotrust/companies/search?q=`) |
| 봉투 | 모든 그룹에 `.AddApiResponseWrapper()` — `{success, code:"S000", data:{result:[…]}}` |
| 실패 | `Results.BadRequest(ApiResponse<object>.Fail("사람이 읽을 이유", "E400"))` · 403 은 `"E403"` · 404 `"E404"` · 409 `"E409"` |
| 신원 | 게이트웨이가 붙이는 `X-User-Id` · `X-User-Roles` · `X-User-Name`(URL 인코딩). 없으면 401 |
| JSON | camelCase, enum 은 **대문자 문자열**(아래 코드표 그대로), 날짜는 `"yyyy-MM-dd"`(DateOnly), 시각은 ISO-8601 |

목록은 배열로 준다(`GetListAsync`). 한 건은 객체 하나(`GetOneAsync`). 서버 페이징은 쓰지 않고
목록마다 상한(기본 500)을 둔다.

### 사용자 줄은 서버가 만든다

어느 엔드포인트든 처음 부르면 `app_user` 에 `external_user_id = X-User-Id` 줄이 생긴다
(`user_type = DRIVER`). `display_name` 은 `X-User-Name` 으로 매번 갱신한다.
`status = BLOCKED` 인 사용자는 **조회는 되고** 등록·수정·신고·이의제기는 403.

### 관리자 판정

`X-User-Roles` 에 `CargoTrust:AdminRoles`(설정, 기본 `ADMINISTRATOR`, `SYSTEM_ADMINISTRATOR`)
중 하나가 있거나 `app_user.user_type = ADMIN` 이면 관리자다. `/admin/*` 는 관리자가 아니면 403.

## 코드표

| 코드 | 값 |
|---|---|
| `PaymentStatus` | `SCHEDULED` 예정 · `PAID` 정상 지급 · `DELAYED` 지연 지급 · `PARTIAL` 일부 지급 · `UNPAID` 미지급 · `DISPUTE` 분쟁 |
| `ReviewStatus`(거래 검증) | `NORMAL` · `FLAGGED` 의심 · `VERIFIED` 확인됨 · `HIDDEN` 통계 제외 |
| `UserType` | `DRIVER` 차주 · `CARRIER` 운송사/주선사 · `ADMIN` 관리자 |
| `UserStatus` | `ACTIVE` · `BLOCKED` |
| `CompanyStatus` | `ACTIVE` 정상 · `CLOSED` 폐업 · `HIDDEN` 숨김 |
| `ReviewVisibility`(후기) | `VISIBLE` · `HIDDEN` |
| `DisputeReason` | `NO_TRANSACTION` 거래 사실 없음 · `ALREADY_PAID` 이미 지급 완료 · `WRONG_AMOUNT` 금액 오류 · `WRONG_COMPANY` 다른 업체 · `OTHER` 기타 |
| `DisputeStatus` | `RECEIVED` 접수 · `REVIEWING` 검토중 · `ACCEPTED` 인용 · `REJECTED` 기각 |
| `ReportTarget` | `COMPANY` · `TRANSACTION` · `REVIEW` |
| `ReportReason` | `FAKE` 허위 거래 · `DUPLICATE` 중복 거래 · `WRONG_COMPANY` 사업자 오인 · `ABUSE` 욕설/비방 · `PRIVACY` 개인정보 노출 · `SPAM` 광고/스팸 · `OTHER` 기타 |
| `ReportStatus` | `RECEIVED` 접수 · `REVIEWING` 검토중 · `REJECTED` 반려 · `REVISION` 수정 요청 · `DELETED` 삭제 · `DONE` 처리 완료 |
| `Confidence` | `NONE` 0건 · `LOW` 1~4건 · `MEDIUM` 5~19건 · `HIGH` 20건 이상 |
| `TransportType`(자유 입력, 권장값) | 일반 · 냉장/냉동 · 컨테이너 · 중량물 · 이사 · 기타 |

## 사업자등록번호

- 저장은 **숫자 10자리만**. 받을 때 하이픈·공백을 걷어 내고, 10자리가 아니거나 **검증 숫자**가
  틀리면 400 (가중치 `1,3,7,1,3,7,1,3,5` — 국세청 규칙).
- 내보낼 때는 `123-45-67890` 꼴. **관리자가 아니면 뒤 5자리를 가린다**(`123-45-*****`, 설계안 29).
  검색은 전체 번호로 하므로 가려도 찾을 수 있다.

## 통계 계산 (설계안 26·27)

원본 거래에서 매번 계산한다(집계 테이블 없음). **`is_deleted` 와 `review_status = HIDDEN` 은 뺀다.**
기간은 `transport_date` 기준이다.

| 필드 | 뜻 |
|---|---|
| `totalCount` | 기간 안 거래 건수(예정 포함) |
| `normalCount` · `delayedCount` · `partialCount` · `unpaidCount` · `disputeCount` · `scheduledCount` | 상태별 건수 |
| `settledCount` | 예정을 뺀 건수 — 정상률의 분모 |
| `normalRate` | `normalCount / settledCount * 100`, 소수 둘째 자리. `settledCount = 0` 이면 `null` |
| `averagePromisedDays` | 평균(예정 지급일 − 운송일) |
| `averageActualDays` | 평균(실제 지급일 − 운송일), 실제 지급일이 있는 것만 |
| `averageDelayDays` | 평균 지연일 — `DELAYED` 거래의 `max(0, 실제 − 예정)` 평균 |
| `maxDelayDays` | 최장 지연일 |
| `recentUnpaidCount` | 최근 90일 `UNPAID` 건수(기간과 무관) |
| `lastTransactionDate` | 가장 최근 운송일 |
| `confidence` · `confidenceLabel` | 전체 건수로 정한 데이터 규모(「거래 경험 5~19건」) |
| `periodDays` · `periodLabel` | 30 · 90 · 180 · 365 · `null`(전체) / 「최근 90일」「전체」 |

**화면은 통계 옆에 기간과 기준을 반드시 함께 적는다**(설계안 26 마지막 줄). 「악성」「위험」 같은
판정어를 서버도 화면도 쓰지 않는다(설계안 12).

## 자료 모양 (DTO)

```jsonc
// CompanyInfo
{ "companyId": 1, "businessNumber": "123-45-*****", "companyName": "○○물류", "ceoName": "홍길동",
  "address": "서울 강서구 …", "region": "서울", "phone": "02-…", "businessType": "화물운송주선",
  "status": "ACTIVE", "createdAt": "2026-09-24T…" }

// CompanyStats — 위 표

// CompanySummary (검색 결과 한 칸)
{ "companyId", "businessNumber", "companyName", "ceoName", "region", "businessType", "status",
  "stats": CompanyStats /* 전체 기간 */ }

// CompanyDetail
{ "company": CompanyInfo,
  "stats": CompanyStats,              // 요청한 period
  "periods": [CompanyStats, …],       // 30 · 90 · 180 · 365 · 전체, 다섯 개 고정
  "recentTransactions": [PublicTransaction],  // 최근 20건
  "recentUnpaid": [PublicTransaction],        // 최근 UNPAID 5건
  "myTransactionCount": 3 }           // 내가 이 회사와 등록한 거래 수

// PublicTransaction — 누가 등록했는지는 싣지 않는다
{ "transactionId", "transportDate", "originRegion", "destinationRegion", "transportType",
  "amount", "expectedPaymentDate", "actualPaymentDate", "paymentStatus", "delayDays" }
  // originRegion = origin 의 첫 낱말(「경기 평택시 …」→「경기」)

// PublicReview
{ "reviewId", "transportDate", "amount", "paymentStatus", "content", "createdAt" }

// MyTransaction
{ "transactionId", "companyId", "companyName", "businessNumber", "transportDate", "origin",
  "destination", "transportType", "dispatchChannel", "amount", "paidAmount", "outstanding",
  "expectedPaymentDate", "actualPaymentDate", "paymentStatus", "delayDays", "overdueDays",
  "memo", "reviewStatus", "hasReview", "createdAt", "updatedAt" }
  // outstanding = amount − paidAmount (PAID·DELAYED 면 0)
  // overdueDays = 아직 안 받았고 예정일이 지났으면 오늘 − 예정일, 아니면 null

// TransactionSaveRequest
{ "companyId", "transportDate", "origin", "destination", "transportType", "dispatchChannel",
  "amount", "expectedPaymentDate", "memo" }

// PaymentRequest
{ "paidDate": "2026-10-02" | null, "paidAmount": 450000, "result": null | "UNPAID" | "DISPUTE", "memo" }

// PaymentRecord
{ "paymentId", "paidDate", "paidAmount", "resultStatus", "delayDays", "memo", "createdAt" }

// TransactionDetail
{ "transaction": MyTransaction, "payments": [PaymentRecord], "review": Review | null,
  "disputes": [Dispute] }

// Review
{ "reviewId", "transactionId", "content", "status", "createdAt", "updatedAt" }

// ReceivableSummary
{ "total", "scheduled", "delayed", "unpaid", "partial", "dispute", "count" }   // 금액(원)
// ReceivableItem
{ "transactionId", "companyId", "companyName", "transportDate", "amount", "paidAmount",
  "outstanding", "expectedPaymentDate", "overdueDays", "paymentStatus", "bucket" }
  // bucket: SCHEDULED(예정일 전) · DELAYED(예정일 지남) · UNPAID · PARTIAL · DISPUTE
// Receivables
{ "summary": ReceivableSummary, "items": [ReceivableItem] }

// Me
{ "userId", "externalUserId", "displayName", "userType", "companyId", "companyName", "status",
  "isAdmin", "transactionCount", "createdAt" }

// Home
{ "me": Me, "receivable": ReceivableSummary, "recentCompanies": [CompanySummary] /* 최근 본 10 */ }

// Dispute
{ "disputeId", "transactionId", "companyId", "companyName", "transportDate", "amount",
  "reason", "content", "status", "resolution", "createdAt", "resolvedAt" }
// DisputeRequest
{ "transactionId", "reason", "content" }

// Report
{ "reportId", "targetType", "targetId", "targetSummary", "reason", "content", "status",
  "resolution", "createdAt", "resolvedAt" }
// ReportRequest
{ "targetType", "targetId", "reason", "content" }

// CompanyCreateRequest (관리자 수정은 AdminCompanySave 로 status·adminMemo 가 더해진다)
{ "businessNumber", "companyName", "ceoName", "address", "region", "phone", "businessType" }
```

## 사용자 엔드포인트

| 메서드 | 경로 | 받는 것 → 주는 것 | 규칙 |
|---|---|---|---|
| GET | `/me` | → `Me` | |
| GET | `/home` | → `Home` | |
| GET | `/companies/search?q=&field=` | → `[CompanySummary]` (≤50) | `field`: `all`(기본)·`name`·`bizno`·`ceo`·`phone`·`address`. `q` 가 숫자 10자리면 사업자번호 정확 일치를 맨 앞에. 이름은 `ILIKE %q%`(pg_trgm). `HIDDEN` 회사는 관리자에게만 |
| GET | `/companies/by-number/{bizno}` | → `CompanyInfo` 또는 빈 결과 | 등록 전 중복 확인 |
| GET | `/companies/{id}?period=90` | → `CompanyDetail` | `period`: 30·90·180·365·`all`. 부를 때 `company_view` 갱신 |
| GET | `/companies/{id}/reviews` | → `[PublicReview]` | `VISIBLE` 만, 최신순 |
| POST | `/companies` | `CompanyCreateRequest` → `CompanyInfo` | 번호 검증, 이미 있으면 **409** + 메시지 「이미 등록된 사업자번호입니다」 |
| GET | `/transactions?status=&from=&to=&companyId=` | → `[MyTransaction]` | **내 것만** |
| GET | `/transactions/{id}` | → `TransactionDetail` | 등록자 또는 관리자 |
| POST | `/transactions` | `TransactionSaveRequest` → `MyTransaction` | 아래 「거래 등록 검사」 |
| PUT | `/transactions/{id}` | `TransactionSaveRequest` → `MyTransaction` | 등록자만. 바뀌기 전·후를 `audit_log` |
| DELETE | `/transactions/{id}` | → 없음 | 등록자만, 논리 삭제, `audit_log` |
| POST | `/transactions/{id}/payment` | `PaymentRequest` → `MyTransaction` | 아래 「결제 판정」 |
| POST | `/transactions/{id}/review` | `{content}` → `Review` | 등록자만. 있으면 고친다(거래당 하나) |
| GET | `/receivables` | → `Receivables` | 내 거래 중 `SCHEDULED·PARTIAL·UNPAID·DISPUTE` |
| POST | `/reports` | `ReportRequest` → `Report` | 같은 사람이 같은 대상에 **처리 안 된 신고**가 있으면 409 |
| GET | `/reports/mine` | → `[Report]` | |
| GET | `/reports/{id}` | → `Report` | 신고자 또는 관리자 |
| GET | `/company-transactions` | → `[PublicTransaction]` | `CARRIER` 이고 `company_id` 가 있는 사람만 — **자기 회사에 관한 거래**(이의제기 고르기용) |
| POST | `/disputes` | `DisputeRequest` → `Dispute` | 그 거래의 회사에 연결된 `CARRIER` 또는 관리자만. 같은 거래에 처리 안 된 이의제기가 있으면 409 |
| GET | `/disputes/mine` | → `[Dispute]` | |
| GET | `/disputes/{id}` | → `Dispute` | 신청자 · 관리자 · 그 거래의 등록자 |

### 거래 등록 검사 (설계안 13.2)

1. 차단된 사용자 → 403
2. 회사가 없거나 `HIDDEN` → 400
3. `amount > 0`, `expectedPaymentDate` 가 있으면 `transportDate` 이후
4. **중복** — 같은 사용자 · 같은 회사 · 같은 운송일 · 같은 금액 · 같은 출발지·도착지(삭제 안 된 것)가
   있으면 409 「같은 거래가 이미 등록돼 있습니다」
5. **비정상 패턴** — 같은 사용자가 같은 회사에 오늘 등록한 거래가 10건을 넘거나, 운송일이
   오늘보다 뒤면 거절하지 않고 `review_status = FLAGGED` + `flag_reason`

### 결제 판정

- `result = UNPAID` → `UNPAID` (받은 금액·날짜는 그대로)
- `result = DISPUTE` → `DISPUTE`
- 그 밖에는 `paidAmount > 0` 과 `paidDate` 가 있어야 한다. 받은 금액은 **쌓인다**
  (`paid_amount += paidAmount`), `actual_payment_date = paidDate`.
  - 누적 ≥ 운송료 → 예정일이 없거나 `paidDate ≤ 예정일` 이면 `PAID`, 아니면 `DELAYED`
  - 누적 < 운송료 → `PARTIAL`
- `delay_days = max(0, paidDate − 예정일)`
- 매번 `payment_record` 한 줄을 남긴다.

## 관리자 엔드포인트 (`/admin/*`)

모든 변경은 `audit_log` 에 전·후를 남긴다(설계안 30). 관리자 응답에서는 사업자번호를 가리지 않는다.

| 메서드 | 경로 | 받는 것 → 주는 것 |
|---|---|---|
| GET | `/admin/dashboard` | → `AdminDashboard` |
| GET | `/admin/companies?q=&status=` | → `[AdminCompany]` |
| POST | `/admin/companies` | `AdminCompanySave` → `AdminCompany` |
| PUT | `/admin/companies/{id}` | `AdminCompanySave` → `AdminCompany` |
| GET | `/admin/transactions?q=&paymentStatus=&reviewStatus=&from=&to=` | → `[AdminTransaction]` (≤500, 최신순) |
| PUT | `/admin/transactions/{id}` | `AdminTransactionUpdate` → `AdminTransaction` |
| GET | `/admin/payments?from=&to=` | → `[AdminPayment]` |
| GET | `/admin/reviews?status=` | → `[AdminReview]` |
| PUT | `/admin/reviews/{id}` | `{status}` → `AdminReview` |
| GET | `/admin/reports?status=` | → `[AdminReport]` |
| PUT | `/admin/reports/{id}` | `AdminResolve` → `AdminReport` |
| GET | `/admin/disputes?status=` | → `[AdminDispute]` |
| PUT | `/admin/disputes/{id}` | `AdminResolve` → `AdminDispute` |
| GET | `/admin/users?q=&userType=` | → `[AdminUser]` |
| PUT | `/admin/users/{id}` | `AdminUserUpdate` → `AdminUser` |
| GET | `/admin/statistics?months=12` | → `AdminStatistics` |
| GET | `/admin/audit?targetType=&from=&to=` | → `[AuditEntry]` (≤500) |

```jsonc
// AdminDashboard
{ "companyCount", "userCount", "transactionCount", "transactionsLast30", "flaggedCount",
  "openReports", "openDisputes", "totalAmount", "outstandingAmount",
  "statusCounts": [{ "status": "PAID", "count": 12 }],
  "recentAudit": [AuditEntry] /* 10 */ }

// AdminCompany = CompanyInfo(번호 안 가림) + { "adminMemo", "transactionCount", "updatedAt" }
// AdminCompanySave = CompanyCreateRequest + { "status", "adminMemo" }

// AdminTransaction = MyTransaction + { "userId", "userName", "externalUserId", "flagReason",
//                                      "adminMemo", "isDeleted", "reportCount", "disputeCount" }
// AdminTransactionUpdate
{ "reviewStatus", "paymentStatus" /* null 이면 안 바꾼다 */, "adminMemo" }

// AdminPayment = PaymentRecord + { "transactionId", "companyName", "userName", "amount" }
// AdminReview = Review + { "companyId", "companyName", "userName", "transportDate", "reportCount" }

// AdminReport = Report + { "reporterName", "reporterExternalId" }
// AdminDispute = Dispute + { "requesterName", "transactionOwnerName" }
// AdminResolve
{ "status", "resolution", "hideTarget": false }
  // hideTarget = true 면: 신고는 대상(거래→review_status HIDDEN · 후기→HIDDEN · 회사→HIDDEN)을,
  //                      이의제기(ACCEPTED 일 때)는 그 거래를 HIDDEN 으로 만든다.
  // 상태가 접수·검토중이 아니면 resolved_at · resolved_by 를 채운다.

// AdminUser
{ "userId", "externalUserId", "displayName", "userType", "companyId", "companyName", "status",
  "adminMemo", "transactionCount", "reportCount", "createdAt", "lastSeenAt" }
// AdminUserUpdate
{ "userType", "companyId", "status", "adminMemo" }

// AdminStatistics
{ "monthly": [{ "month": "2026-09", "count", "amount", "normalCount", "delayedCount",
                "partialCount", "unpaidCount", "disputeCount" }],
  "topDelayCompanies": [{ "companyId", "companyName", "businessNumber", "totalCount",
                         "delayedCount", "unpaidCount", "averageDelayDays" }],   // 10
  "flaggedUsers": [{ "userId", "displayName", "externalUserId", "flaggedCount", "transactionCount" }] }

// AuditEntry
{ "auditId", "actorUserId", "actorName", "action", "targetType", "targetId",
  "beforeData" /* JSON 문자열 */, "afterData", "createdAt" }
```
