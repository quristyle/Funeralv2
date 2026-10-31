# 테이블 계통도 (`/projmng/design/table-lineage`)

프로젝트관리 › 설계 › 「테이블 계통도」. 고른 **DB 접속 하나(= DB 하나 · 스키마 하나)** 의
표들이 어떤 차례로 자료를 주고받는지 칸과 선으로 그리고 고친다. 옛 WBS 도구의
`#/tables2`(`docs/wbs_참고소스/wbs-source-*.zip` 의 `web/src/views/Tables2.vue`)를 옮긴 것이다.

## 옛 도구와 달라진 것

| | 옛 도구 | 여기 |
|---|---|---|
| 도면의 단위 | 스키마 둘(M10 · M20)을 코드에 박았다 | **DB 접속마다 한 장** — `/projmng/db/list` 에 단 접속이 곧 도면이다 |
| 담는 곳 | 서버 파일(`api/data/vueflow/tables-*.json`) | `projmng.dev_db_prop` 세 줄 (아래) |
| 표 찾기 | 수빅 Oracle 에 붙은 자바 데몬 | 등록 질의 `tablelist` (접속의 DB 종류별) |
| 덮어쓰기 막기 | 파일 수정 시각 | 속성 줄의 `mod_dt` — 저장 직전에 다시 읽어 견준다 |
| 그리기 | Vue Flow | `wwwroot/js/table-lineage.js` (의존 없음) |

도면 글은 **Vue Flow JSON 그대로**다(`{lanes, flows, nodes, edges}`). 번역하지 않아서
옮겨 온 도면이 한 글자도 안 바뀌고 열린다.

## 담는 곳 — 접속 하나에 세 줄

| `db_pkey` | 무엇 |
|---|---|
| `table_lineage` | 도면 |
| `table_lineage_items` | 업무항목 `{v, items:[{id,name,tables[]}]}` |
| `table_lineage_seed` | 처음 모습. 「기본 배치로」가 이것을 캔버스에 올린다(저장해야 남는다) |

셋 다 `db_ptype = 'lineage'` 다. ERD 화면의 「그림」 고르개는 `entities` 가 있는 줄만
고르므로 이 줄들이 거기 섞이지 않는다. 업무항목을 도면과 가르는 까닭은 옛 도구와 같다 —
배치를 처음으로 되돌려도 사람이 모아 둔 업무항목은 남아야 한다.

## 대상 DB 에 못 붙어도 열린다

화면이 대상 DB 에 묻는 것은 표 목록 하나다. 붙으면 **도면에 없는 표를 골라 놓고**,
**대상 DB 에 없는 표 칸을 점선으로** 가르고, 빈 설명을 코멘트로 채울 수 있다.
못 붙으면 그 사실을 안내 띠로 말하고 저장된 도면만 그린다.

HHIP_MM 의 두 접속(`hhip-m10` · `hhip-m20`, Oracle `10.103.13.10:1521/ORAHHIP`)은
**사내 폐쇄망이라 늘 이쪽**이다. ORACLE 에는 등록 질의도 없어서 서버가
「dbtype ORACLE 의 tablelist 가 정의 되지 않았습니다」로 돌려준다.

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
