# 테이블 계통도 (`/projmng/design/table-lineage`)

프로젝트관리 › 설계 › 「테이블 계통도」. 고른 **DB 접속 하나(= DB 하나 · 스키마 하나)** 의
표들이 어떤 차례로 자료를 주고받는지 칸과 선으로 그리고 고친다. 옛 WBS 도구의
`#/tables2`(`docs/wbs_참고소스/wbs-source-*.zip` 의 `web/src/views/Tables2.vue`)를 옮긴 것이다.

## 옛 도구와 달라진 것

| | 옛 도구 | 여기 |
|---|---|---|
| 도면의 단위 | 스키마 둘(M10 · M20)을 코드에 박았다 | **DB 접속마다 한 장** — `/projmng/db/list` 에 단 접속이 곧 도면이다 |
| 담는 곳 | 서버 파일(`api/data/vueflow/tables-*.json`) | `projmng.dev_db_prop` 세 줄 (아래) |
| 표 찾기 | 수빅 Oracle 에 붙은 자바 데몬 | 등록 질의 `objectlist` — 표·뷰·프로시저·함수 (접속의 DB 종류별) |
| 덮어쓰기 막기 | 파일 수정 시각 | 속성 줄의 `mod_dt` — 저장 직전에 다시 읽어 견준다 |
| 그리기 | Vue Flow | `wwwroot/js/table-lineage.js` (의존 없음) |

도면 글은 **Vue Flow JSON 그대로**다(`{lanes, flows, nodes, edges}`). 번역하지 않아서
옮겨 온 도면이 한 글자도 안 바뀌고 열린다.

## 담는 곳 — 접속 하나에 네 줄

| `db_pkey` | 무엇 |
|---|---|
| `table_lineage` | 도면 |
| `table_lineage_items` | 업무항목 `{v, items:[{id,name,tables[]}]}` |
| `table_lineage_seed` | 처음 모습. 「기본 배치로」가 이것을 캔버스에 올린다(저장해야 남는다) |
| `table_lineage_catalog` | 대상 DB 에서 모아 둔 객체와 연결 `{v, at, source, objects[], relations[]}` (아래) |

넷 다 `db_ptype = 'lineage'` 다. ERD 화면의 「그림」 고르개는 `entities` 가 있는 줄만
고르므로 이 줄들이 거기 섞이지 않는다. 업무항목을 도면과 가르는 까닭은 옛 도구와 같다 —
배치를 처음으로 되돌려도 사람이 모아 둔 업무항목은 남아야 한다.

## 업무항목을 고르면 그 항목의 표만 그린다

M20 은 칸 543 · 선 1995 라 전부를 DOM 에 둔 채 흐리게만 하면 끌기·확대가 버벅인다.
업무항목을 고르면 캔버스가 **그 항목의 칸과 그 칸끼리 잇는 선만** 만든다
(실측: 543/1995 → 「현지POR 인터페이스데이터」 15/18). 아무것도 안 고르면 전부 그린다.

- 그리지 않는 것일 뿐 도면에서 빠지는 것이 아니다 — 저장은 늘 전부를 담는다.
- **「칸을 눌러 담기」 동안은 전부 그린다.** 담을 칸이 보여야 한다. 끄면 보던 자리 그대로 다시 좁힌다.
- 표 찾기도 고른 항목 안에서만 찾고, 항목 밖의 표로 「찾아가기」 하면 그렇다고 알린다.
- 옛 「이 업무만 그리기」 체크는 걷었다 — 그것이 이제 기본이다.

## 「객체 놓기」 — 표만이 아니라 뷰·프로시저·함수도

화면이 대상 DB 에 묻는 것은 객체 목록 하나다 — 등록 질의 **`objectlist`**
(`deploy/sql/projmng-devsqlresp-objectlist-2026-11-01.sql`). DB 종류와 상관없이 같은 세 칸을 준다.

| 칸 | 값 |
|---|---|
| `ObjectType` | `TABLE` · `VIEW` · `MVIEW` · `FOREIGN` · `PROCEDURE` · `FUNCTION` |
| `ObjectName` | 이름. 겹이름 함수는 한 줄 — 도면의 칸은 이름이 열쇠다 |
| `Description` | 코멘트 |

POSTGRESQL · EDB · MSSQL 셋에만 있다(실행기 `DevService` 가 붙는 종류가 그 셋이다).
**그 DB 종류에 `objectlist` 가 없으면 옛 `tablelist`(표만)로 내려간다.** 확장이 깐 것
(`pg_stat_statements` · pgcrypto 함수)은 뺀다 — 사람이 만든 것이 아니다.

놓기 창에서 종류 칩(전체 · 표 · 뷰 · 프로시저 · 함수)으로 거르고, 「DB 에서 다시 읽기」로
그 사이 만든 객체를 받아 온다. 「놓을 종류」의 기본은 **자동**이다.

| 객체 | 칸 종류 | 생김 |
|---|---|---|
| 표 · 외부 표 | 원장(`ledger`) — 기준인지 파생인지는 DB 가 모르니 사람이 고친다 | 왼쪽 띠 |
| 뷰 · 구체화 뷰 | **뷰(`view`)** | 왼쪽 띠가 겹줄 |
| 프로시저 | 프로시저(`proc`) | 둥근 칸 |
| 함수 | **함수(`func`)** | 둥근 칸(보라) |

`view` · `func` 는 이번에 늘었다 — C# `LineageGraph.NodeKinds` 와 캔버스 `NODE_KINDS` 가 같은 차례다.
점선(대상 DB 에 없는 이름) 판정에도 둘이 든다(`DB_KINDS`). 프로시저(`proc`)는 옛 도면에서
프로그램 이름으로도 써서 판정에서 뺀다.

## 대상 DB 에 못 붙어도 열린다

붙으면 **도면에 없는 객체를 골라 놓고**, **대상 DB 에 없는 칸을 점선으로** 가르고,
빈 설명을 코멘트로 채울 수 있다. 못 붙으면 그 사실을 안내 띠로 말하고 —

- `table_lineage_catalog` 가 있으면 **모아 둔 그 목록으로 고르고 견준다**(띠에 모은 때를 적는다).
- 없으면 저장된 도면만 그린다.

HHIP_MM 의 두 접속(`hhip-m10` · `hhip-m20`, Oracle `10.103.13.10:1521/ORAHHIP`)은
**사내 폐쇄망이라 늘 이쪽**이다. ORACLE 에는 등록 질의도 없어서 서버가
「dbtype ORACLE 의 objectlist 가 정의 되지 않았습니다」로 돌려준다.

## 대상 DB 에서 모아 보관한다 — `scripts/projmng-table-lineage-harvest.py`

프로젝트에 등록된 접속(PostgreSQL)마다 그 스키마의 객체와 **연결**을 모아 세 줄에 담는다.

| 연결 | 선 | 글귀 |
|---|---|---|
| 외래키 | 부모 표 → 자식 표 · 「읽기」(굵게) | 칸 이름 |
| 뷰가 읽는 것 (`pg_depend`) | 표 → 뷰 · 「읽기」 | 뷰 |
| 트리거 | 표 → 함수 · 「트리거」 | 트리거 이름 |
| 함수 본문의 표 이름 | 함수 → 표 · 「처리」 / 표 → 함수 · 「읽기」 | 쓰기 / 읽기 |

마지막 줄은 **글자로 찾은 어림**이다 — PL/pgSQL 은 의존을 DB 가 적어 두지 않는다(주석은 걷고 본다).
배치는 외래키의 깊이로 단을 나눈다: 기준 표(참조받기만) → 1단 · 2단 · 3단 이상 참조 → 외래키 없는 표 → 뷰 → 함수 · 프로시저.
한 단이 22칸을 넘으면 옆 칸으로 넘긴다.

- `table_lineage_catalog` · `table_lineage_seed` 는 **늘 새로 쓴다.**
- `table_lineage`(도면)는 **없을 때만** 넣는다 — 사람이 고친 배치가 거기 있다. 덮으려면 `--force`.
- 출처(호스트 · DB · 스키마 · 계정 · 서버 판)는 카탈로그와 도면 머리에 남고 **비밀번호는 안 담는다.**
  접속 비밀번호는 `devdbinfo` 에서 읽어 psql 환경변수로만 넘긴다.

```
PGPASSWORD=… python3 -I scripts/projmng-table-lineage-harvest.py              # 프로젝트 1 (jsini통합포탈) 전부
PGPASSWORD=… python3 -I scripts/projmng-table-lineage-harvest.py --db jsini-comm --force
PGPASSWORD=… python3 -I scripts/projmng-table-lineage-harvest.py --prj 3 --dry-run
```

### jsini통합포탈(prj_rid 1)에 넣은 것 (2026-11-01)

| 접속 | DB · 스키마 | 객체 | 칸 · 선 |
|---|---|---|---|
| `funeralfr` (3) | funeralfr.jsini.co.kr `funeral` · smfr | 표 30 · 프로시저 96 · 함수 15 | 141 · 275 |
| `jsini-comm` (13) | `jsiniportal` · scom | 표 57 | 57 · 31 |
| `jsini-funeral` (14) | `funeralv2` · smfr | 표 18 | 18 · 10 |
| `jsini-helpdesk` (15) | `helpdesk` · helpdesk | 표 31 | 31 · 20 |
| `jsini-projmng` (16) | `projmng` · projmng | 표 41 · 뷰 4 · 함수 4 | 49 · 36 |
| `jsini-site` (17) | `jsinisite` · site | 표 5 | 5 · 0 |
| `jsini-lifeenv` (18) | `ghub` · ghub | 표 15 | 15 · 9 |
| `jsini-cargo` (19) | `cargotrust` · cargotrust | 표 14 | 14 · 18 |

## 옮겨 넣은 자료 (2026-10-31)

| 접속 | 도면 | 업무항목 |
|---|---|---|
| `hhip-m10` (db_rid 20) | 칸 48 · 선 71 | 0 |
| `hhip-m20` (db_rid 21) | 칸 543 · 선 1995 | 140 |

```
psql … -v pw_hhip=… -f deploy/sql/devdbinfo-hhip-mm-2026-10-31.sql      # 접속 두 줄 + DB 종류 ORACLE
psql … -f deploy/sql/portal-menu-table-lineage-2026-10-31.sql            # 메뉴 (권한은 ERD 에서 베낀다)
PGPASSWORD=… python3 -I scripts/projmng-table-lineage-import.py          # zip 의 도면·업무항목·처음 모습
```

스크립트는 같은 열쇠의 줄을 덮어쓰므로 다시 돌려도 줄이 늘지 않는다 —
**대신 화면에서 고친 것이 원본으로 돌아간다.**
