# 프로젝트관리 DB 이관 — ASIS → TOBE

작성: 2026-08-29

## 1. 무엇을 했나

프로젝트관리(`projmng` 스키마)의 **구조와 자료 전부**를 새 데이터베이스로 옮겼다.

| | ASIS (원본) | TOBE (대상) |
|---|---|---|
| 서버 | `jsini.co.kr:15432` | `jin114.co.kr:31015` |
| DB · 스키마 | `jsini` / `projmng` | `projmng` / `projmng` |
| 버전 | PostgreSQL 14.24 | PostgreSQL 16.15 |
| 인코딩 · 정렬 | UTF8 · `C.UTF-8` | UTF8 · `C.UTF-8` (맞췄다) |

**ASIS 는 건드리지 않았다.** 계속 돌아간다. 스크립트가 ASIS 를 열 때 항상
read-only 트랜잭션으로 열기 때문에, 실수로 쓰기를 시도해도 **서버가 거부한다.**
사람의 주의에 기대지 않는다.

옮긴 것: **테이블 21개 · 4,736행 · 함수와 프로시저 40개 · 제약 11개 · 주석 111개.**

## 2. pg_dump 를 쓰지 않았다

이 장비에 PostgreSQL 클라이언트가 설치되어 있지 않다(`psql` · `pg_dump` 둘 다 없다).
그래서 `pg_catalog` 를 직접 읽어 DDL 을 만드는 스크립트를 짰다.

`pg_get_functiondef` · `pg_get_constraintdef` · `pg_get_indexdef` 가 정의를 그대로
내주므로, 손으로 조립해야 하는 것은 테이블의 컬럼 목록뿐이다.
자료는 파일을 거치지 않고 `COPY ... TO STDOUT` → `COPY ... FROM STDIN` 으로 바로 흘려보낸다.

## 3. 옮기지 않은 것과 그 이유

ASIS 의 `projmng` 스키마에는 확장이 만든 객체가 섞여 있다.

| 객체 | 출처 | 판단 |
|---|---|---|
| `hypopg_list_indexes` | `hypopg` 확장 | 제외 — 가상 인덱스 실험용 진단 도구 |
| `pg_stat_statements`, `pg_stat_statements_info` | `pg_stat_statements` 확장 | 제외 — 쿼리 통계 진단 도구 |

**업무 기능과 무관하다는 것을 확인하고 뺐다.** 함수·프로시저 40개의 본문 67,414자를
전부 훑어 `dblink` · `uuid-ossp` · `hypopg` · `pg_stat_statements` 를 참조하는 곳이
하나도 없음을 확인했다. 설치에 슈퍼유저가 필요하기도 하다.
나중에 진단이 필요하면 TOBE 에서 `CREATE EXTENSION` 으로 따로 켜면 된다.

시퀀스 · 트리거 · 사용자 정의 타입 · 업무용 뷰는 ASIS 에 **하나도 없다.**
구조가 단순해서 걸릴 것이 없었다.

## 4. 걸렸던 것

### TOBE 계정에 생성 권한이 없었다

`projmng` 계정은 `rolcreatedb = false` 이고 기존 DB 어디에도 `CREATE` 권한이 없어
데이터베이스는커녕 스키마도 만들 수 없었다. 슈퍼유저(`funeral`)로 한 줄만 실행했다.

```sql
CREATE DATABASE projmng OWNER projmng ENCODING 'UTF8'
  TEMPLATE template0 LC_COLLATE 'C.UTF-8' LC_CTYPE 'C.UTF-8';
```

**슈퍼유저는 이 한 줄에만 썼다.** 스키마·테이블·자료는 전부 `projmng` 계정으로 넣었다.
실제 운영이 쓸 계정으로 만들어야 소유권과 권한이 어긋나지 않는다.

### 함수 본문의 CRLF 가 망가졌다 (고침)

프로시저 몇 개는 본문에 CRLF 줄바꿈이 들어 있다. 처음에 SQL 파일을 윈도우 기본
텍스트 모드로 썼더니 파이썬이 `\n` 을 `\r\n` 으로 바꿔, 이미 CRLF 인 자리가
`\r\r\n` 이 되었다. 동작에는 영향이 없는 공백이지만 **원본과 다른 것은 다른 것이다.**

읽고 쓸 때 모두 `newline=""` 으로 바꿔 바이트가 그대로 오가게 했고,
다시 올린 뒤 루틴 40개의 본문이 한 글자도 다르지 않음을 확인했다.

발견한 방법은 대조 스크립트다. 이름과 인자만 맞춰 보고 넘어갔으면 못 잡았다.

## 5. 확인한 것

### 구조·자료 대조 (`verify.py --deep`)

```
OK 테이블    ASIS  21  TOBE  21
OK 컬럼     ASIS 205  TOBE 205      (이름 · 타입 · NOT NULL 까지)
OK 루틴     ASIS  40  TOBE  40
OK 루틴본문  ASIS  40  TOBE  40      (pg_get_functiondef 전문 비교)
OK 제약     ASIS  11  TOBE  11      (정의문 전문 비교)
행수 · 내용 해시 — 21개 테이블 전부 일치
```

내용 해시는 행별 MD5 를 XOR 로 합친 값이다. 행 순서가 달라도 흔들리지 않으므로
`COPY` 로 옮긴 자료가 한 건도 어긋나지 않았음을 보인다.

> 처음 대조에서 FK 하나가 다르게 보였는데 실제 차이가 아니었다.
> `pg_get_constraintdef` 는 `search_path` 에 든 스키마를 접두어 없이 내놓는다.
> ASIS 는 접속 계정이 `jsini` 라 `projmng` 가 경로에 없었고 TOBE 는 있었다.
> 양쪽 `search_path` 를 같게 맞추니 사라졌다. 대조 스크립트에 그 처리를 넣어 두었다.

### 프로시저 실행 (`smoke_tobe.py`)

TOBE 에서 프로시저 24개를 실제로 불러 **통과 24 · 실패 0.**
`ProjMngServer` 와 같은 방식으로 부른다 — 인자를 채우고 `CALL` 한 뒤 `INOUT refcursor`
에서 `FETCH` 한다. 조회만 하고 롤백하므로 TOBE 자료도 바뀌지 않는다.

`sp_projCommon` 은 ASIS·TOBE 양쪽에 같은 인자로 넣어 결과 건수를 맞춰 봤다.
`projlist` 는 사용자에 따라 0건 또는 7건인데 **양쪽이 똑같이 그렇다** —
`dev_proj_user_map` 으로 걸러지는 자료 특성이지 이관 문제가 아니다.

## 6. 도구

[scripts/projmng-db-migration/](../../scripts/projmng-db-migration/) 에 있다. 전부 다시 실행해도 안전하다.

| 파일 | 하는 일 |
|---|---|
| `dsn.py` · `dsn.env.example` | 접속 정보. **암호는 스크립트에 없다** — `dsn.env`(.gitignore 대상)나 환경변수에서 읽는다 |
| `dump_asis.py` | ASIS 구조를 `out/*.sql` 로 뽑는다 |
| `load_tobe.py` | 구조를 세우고 자료를 옮긴다. `--dry-run` · `--replace` |
| `verify.py` | ASIS·TOBE 대조. `--deep` 은 내용 해시까지 |
| `smoke_tobe.py` | TOBE 프로시저 24개를 실제로 불러 본다 |
| `pmq.py` | ASIS 읽기 전용 조회 도구 (쓰기 문장은 실행 전에 거른다) |

`load_tobe.py` 는 **전체가 트랜잭션 하나다.** 테이블마다 행수를 즉시 대조하고,
하나라도 어긋나면 통째로 롤백해 TOBE 를 손대기 전 상태로 되돌린다.
이미 객체가 서 있으면 `--replace` 없이는 덮어쓰지 않는다.

## 7. 남은 일

### 🟠 아직 아무것도 TOBE 를 바라보지 않는다

`microservices/ProjMngServer/appsettings.Local.json` 의 `ConnectionStrings:jsini` 는
**여전히 ASIS 를 가리킨다.** 자료만 복제한 상태이고 전환은 하지 않았다.
운영 대상을 바꾸는 일이라 지시를 기다린다.

바꿀 때 주의할 점 하나 — `SearchPath=projmng` 는 **공백 없이** 붙여 쓴다.
`ProjService.cs:78` 이 접속 문자열을 손으로 파싱해 `SearchPath` 키를 문자 그대로
찾기 때문에, Npgsql 정식 표기인 `Search Path=`(공백)로 쓰면 스키마를 못 읽어
프로시저를 `.sp_xxx` 로 불러 전부 실패한다.
([13-projmng-migration.md](13-projmng-migration.md) 8절과 같은 함정이다)

### 🟠 전환 시점의 자료 차이

ASIS 가 계속 돌고 있으므로, 지금 이후 ASIS 에 쌓이는 자료는 TOBE 에 없다.
전환할 때 `load_tobe.py --replace` 로 한 번 더 옮기고 `verify.py --deep` 으로
확인하면 된다. 4,736행 규모라 몇 초면 끝난다.

### ⚪ 참고

- 소유권·권한(GRANT)은 옮기지 않았다. TOBE 는 `projmng` 계정이 DB 소유자이고
  모든 객체를 그 계정으로 만들었으므로 단일 계정으로 쓰는 한 문제가 없다.
- ASIS `dev_srcinfo.src_path` 가 전부 윈도우 경로라는 문제는 이관과 무관하게
  그대로 남아 있다([13-projmng-migration.md](13-projmng-migration.md) 8절).
