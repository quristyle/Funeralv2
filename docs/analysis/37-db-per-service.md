# 서비스별 DB 로 나눴다 (2026-08-29)

## 요약

DB 두 개를 새로 만들고 스키마를 옮겼다. 이제 **서비스마다 DB 하나**다.

| DB | 스키마 | 쓰는 서비스 | 언제 |
|---|---|---|---|
| `jsiniportal` | `scom` | AuthServer · FileServer · NotificationServer | 2026-08-29 신설 |
| `funeralv2` | `smfr` | funeralv2Api | 원래 있던 것 (scom 을 덜어냄) |
| `jsinisite` | `site` | SiteServer | 2026-08-29 신설 |
| `jinrecept` | `jsini` | HelpDeskServer | 원래부터 별도 |
| `projmng` | `projmng` | ProjMngServer | 원래부터 별도 |

절차는 SQL 파일에 있다 (실행한 것 그대로).

- [docs/sql/site_schema_move_to_own_db.sql](../sql/site_schema_move_to_own_db.sql)
- [docs/sql/scom_schema_move_to_own_db.sql](../sql/scom_schema_move_to_own_db.sql)

---

## 1. 왜 나눴나 — 이유가 둘 다 다르다

두 번 옮겼는데 이유가 같지 않다. 섞어 읽으면 안 된다.

### `site` → `jsinisite` : **경계가 필요해서**

SiteServer 만 **로그인하지 않은 사람의 입력을 받는다**(문의 접수).
익명 쓰기가 닿는 표와 업무 표가 같은 DB 에 있으면, 둘을 가르는 것이
DB 권한이 아니라 코드 안의 약속뿐이다. 나누면 그 경계가 물리적으로 생긴다.

### `scom` → `jsiniportal` : **이름이 거짓말을 해서**

`funeralv2` 라는 이름이 두 가지를 뜻하고 있었다.
장례식장 시스템(`smfr`)이면서, 동시에 포털 전체가 사는 DB(`scom`)였다.
장례식장은 포털에 붙은 여러 업무 중 하나일 뿐인데 이름이 그렇게 읽히지 않는다.

새로 온 사람이 `funeralv2` 에 접속해서 `scom.accounts` 를 보면
"장례식장 계정" 으로 읽는다. 실제로는 포털 전체의 계정이다.
이름을 실제에 맞췄다.

---

## 2. 왜 셋을 한 DB 에 그대로 뒀나

`jsiniportal` 하나를 **세 서비스가 함께** 쓴다. 서비스별 DB 라면서 셋이 하나다.
일부러다.

AuthServer(계정 · 메뉴 · 권한 · 공지) · FileServer(파일) · NotificationServer(구독)는
포털이라는 한 덩어리를 이루고, 서로를 실제로 참조한다.

- 공지가 첨부의 공개 여부를 맞출 때 `scom.notice_files` ↔ `scom.filemetadatas` 를
  한 문장 UPDATE 로 처리한다 ([AuthServer/Services/PublicFileSyncService.cs](../../microservices/AuthServer/Services/PublicFileSyncService.cs))
- 구독은 `scom.accounts` 의 계정에 달린다

DB 를 갈라 놓으면 이 참조가 **코드 안의 약속으로만** 남는다.
지금 나눠서 얻을 것(독립 배포 · 장애 격리)이 없는데 잃을 것(트랜잭션)은 확실하다.

SiteServer 를 갈라낸 이유(익명 입력)가 이 셋에는 해당하지 않는다.
**같은 원칙에서 나온 다른 답이다.**

값은 치렀다 — 소개 사이트의 자료 · 대표 이미지는 FileServer 파일을 가리키는데
그것이 이제 다른 DB 라, 공지가 쓰는 한 문장 UPDATE 를 소개 사이트에서는 못 쓴다.
필요해지면 `PUT /api/file/public/{id}` 를 거쳐야 한다.

---

## 3. 어떻게 옮겼나 — 두 번 다 방법이 달랐다

| | `site` → `jsinisite` | `scom` → `jsiniportal` |
|---|---|---|
| 방법 | 새 DB 에 스키마를 세우고 행만 복사 | `CREATE DATABASE ... TEMPLATE` 로 통째 복제 |
| 표 | 5개 · 외래키 없음 | 29개 · 인덱스 · 제약 · 시퀀스 |
| 중단 | 없음 | **있음** (4개 서비스) |

왜 달랐나. `pg_dump` 도 `psql` 도 이 저장소에 없다.
표 다섯 개는 스키마 SQL 이 이미 있어 행만 옮기면 됐지만,
표 29개의 컬럼 기본값 · 인덱스 · 제약 · 시퀀스 현재값을 손으로 다시 만들면 어딘가 틀린다.
`TEMPLATE` 는 통째로 복제하므로 그럴 일이 없다.

대가는 **템플릿으로 쓰는 DB 에 다른 접속이 하나도 없어야 한다**는 것이다.
그래서 서비스 넷을 내리고 했다. 실제로 걸린 것은 서비스가 아니라
DBeaver 가 idle 로 잡고 있던 접속 6개였다.

복제하면 새 DB 에 `scom` · `smfr` 이 **둘 다** 들어온다.
새 쪽에서 `smfr` 을, 옛 쪽에서 `scom` 을 각각 걷어내야 짝이 맞는다.

### 옮기기 전에 확인한 것

통째로 잘라 낼 수 있는지부터 봤다. 하나라도 걸렸으면
그 참조를 API 호출로 바꾸는 일이 먼저였다.

| 확인 | 결과 |
|---|---|
| 스키마를 넘는 외래키 (`scom` ↔ `smfr`) | 0건 |
| `scom` 안의 뷰 · 함수 · 프로시저 | 0건 |
| funeralv2Api 가 `scom` 을 읽나 | 아니오 — 코드에 `scom` 이 한 번도 안 나온다 |
| HelpDesk · ProjMng 의 `scom.` 언급 | 전부 주석이다. 그 DB 에 붙지 않는다 |

---

## 4. 다 됐다고 어떻게 확인했나

**"서비스가 떠 있다" 로는 부족하다.** 옛 DB 가 그대로 있으니
연결 문자열이 안 바뀌었어도 똑같이 잘 뜬다.

새 DB 에 **쓰기가 실제로 닿는지**를 봤다 — 로그인을 몇 번 하고 양쪽을 비교했다.

```
funeralv2.scom.account_login_logs    120건   마지막 11:40  (복제 시점에 멈춤)
jsiniportal.scom.account_login_logs  132건   마지막 12:10  (새 로그인이 여기에만)
```

행수가 갈라지는 것을 본 뒤에 옛 `scom` 을 지웠다.

그 밖에 확인한 것:

| | 결과 |
|---|---|
| `scripts/smoke-test.sh` | 31 통과 · 0 실패 |
| 로그인 | 200 · 계정 · 회사 · 비밀번호 만료일까지 정상 |
| 메뉴 `/api/auth/menu/all` | 최상위 9개 (System · 회사 관리 · Dashboard · 개발영역 · ProjMng · HelpDesk · 장례식장 관리시스템 · 설정 …) |
| 공지 `/api/auth/notices` | 2건 |
| 파일 읽기 (쿠키 경유) | 302 — FileServer 가 새 DB 의 `filemetadatas` 를 읽는다 |
| 장례식장 `/api/funeral/building/room/list` | 200 — `smfr` 은 안 건드려졌다 |
| 표 29개 행수 대조 (원본 ↔ 복제본) | 전부 일치 |

포털 화면(:5555)은 로그인 화면까지만 봤다. 브라우저 창이 0×0 으로 잡혀
로그인 뒤 메뉴가 그려지는 것은 **화면으로 확인하지 못했다.**
API 는 위처럼 다 확인했고 프론트는 그 API 를 그대로 받아 쓰지만,
화면을 직접 본 것은 아니다.

---

## 5. 남은 것

### D2 의 방향이 바뀌었다

[12-decisions-pending.md](12-decisions-pending.md) 의 **D2** 는 원래
"헬프데스크만 혼자 떨어져 있으니 모으자" 였다. 이제 반대다 —
**떨어져 있는 쪽이 표준이고 헬프데스크는 예외가 아니다.**

남은 문제는 "DB 가 나뉘어 있다" 가 아니라 **"DB 계정 하나를 여럿이 쓴다"** 이다.
`funeralv2` 계정 하나가 `jsiniportal` · `funeralv2` · `jsinisite` 를 다 연다.
지금의 경계는 이름뿐이고 권한으로는 막혀 있지 않다.

### 맨바닥에서 세우는 방법이 없다

`.gitignore` 가 `Migrations/` 를 제외하는데 FileServer 만 `Database.Migrate()` 로
표를 만든다. 즉 **다른 장비에서는 이 DB 들이 저절로 서지 않는다.**
`docs/sql` 의 이동 SQL 은 *이미 있는* DB 를 옮기는 절차이지,
없는 DB 를 세우는 절차가 아니다. 새 장비 · 운영 반영 때 걸린다.

### 백업 · 복구 단위가 다섯이 됐다

나눈 값이다. 지금은 전부 한 PostgreSQL 인스턴스라 실무상 차이가 없지만,
백업 스크립트가 있다면 DB 목록을 늘려야 한다.
