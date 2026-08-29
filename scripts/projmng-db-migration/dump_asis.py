#!/usr/bin/env python
"""ASIS(jsini.co.kr:15432 / jsini / projmng) 구조를 SQL 파일로 뽑는다.

ASIS 는 **읽기만 한다.** 세션을 read-only 트랜잭션으로 열어 실수로도 쓰지 못하게 한다.
pg_dump 가 이 장비에 없어 pg_catalog 를 직접 읽어 DDL 을 만든다.

만드는 파일 (out/ 아래):
  01_schema.sql     스키마 + 루틴(함수·프로시저) + 테이블
  02_constraints.sql PK·UK·FK·CHECK + 인덱스 + 주석
  manifest.json     대조용 목록 (객체 이름과 건수)

데이터는 파일로 뽑지 않는다. load_tobe.py 가 COPY 로 직접 흘려보낸다.
"""
import json
import os
import sys

import psycopg2

import dsn

ASIS = dsn.asis()
SCHEMA = dsn.SCHEMA
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")


def connect():
    c = psycopg2.connect(**ASIS)
    c.set_session(readonly=True, autocommit=False)
    return c


# ---------------------------------------------------------------- 루틴

ROUTINES = """
select p.proname,
       pg_get_function_identity_arguments(p.oid) as args,
       pg_get_functiondef(p.oid)                 as ddl
  from pg_proc p
  join pg_namespace n on n.oid = p.pronamespace
 where n.nspname = %s
   and p.oid not in (select objid from pg_depend where deptype = 'e')
 order by p.prokind desc, p.proname, args
"""

# ---------------------------------------------------------------- 테이블

TABLES = """
select c.relname
  from pg_class c
  join pg_namespace n on n.oid = c.relnamespace
 where n.nspname = %s and c.relkind = 'r'
   and c.oid not in (select objid from pg_depend where deptype = 'e')
 order by c.relname
"""

COLUMNS = """
select a.attname,
       format_type(a.atttypid, a.atttypmod)             as coltype,
       a.attnotnull,
       pg_get_expr(d.adbin, d.adrelid)                  as coldefault,
       a.attidentity,
       a.attgenerated,
       coll.collname
  from pg_attribute a
  left join pg_attrdef d  on d.adrelid = a.attrelid and d.adnum = a.attnum
  left join pg_collation coll on coll.oid = a.attcollation
                             and coll.collname <> 'default'
 where a.attrelid = %s::regclass and a.attnum > 0 and not a.attisdropped
 order by a.attnum
"""

# 제약. PK·UK 를 FK 보다 먼저 걸어야 FK 가 참조할 대상이 선다.
CONSTRAINTS = """
select c.relname, con.conname, pg_get_constraintdef(con.oid), con.contype
  from pg_constraint con
  join pg_class c     on c.oid = con.conrelid
  join pg_namespace n on n.oid = c.relnamespace
 where n.nspname = %s
 order by case con.contype when 'p' then 0 when 'u' then 1
                           when 'c' then 2 else 3 end,
          c.relname, con.conname
"""

# 제약이 만들어 주는 인덱스는 뺀다. 두 번 만들면 이름이 부딪힌다.
INDEXES = """
select i.relname, pg_get_indexdef(i.oid)
  from pg_index x
  join pg_class i     on i.oid = x.indexrelid
  join pg_class t     on t.oid = x.indrelid
  join pg_namespace n on n.oid = t.relnamespace
 where n.nspname = %s
   and not exists (select 1 from pg_constraint con where con.conindid = i.oid)
 order by t.relname, i.relname
"""

COMMENTS = """
select 'table' as kind, c.relname, null::text as colname,
       obj_description(c.oid, 'pg_class') as descr
  from pg_class c join pg_namespace n on n.oid = c.relnamespace
 where n.nspname = %(s)s and c.relkind = 'r'
   and obj_description(c.oid, 'pg_class') is not null
union all
select 'column', c.relname, a.attname, col_description(c.oid, a.attnum)
  from pg_class c
  join pg_namespace n on n.oid = c.relnamespace
  join pg_attribute a on a.attrelid = c.oid and a.attnum > 0
                     and not a.attisdropped
 where n.nspname = %(s)s and c.relkind = 'r'
   and col_description(c.oid, a.attnum) is not null
union all
select 'routine', p.proname || '(' ||
       pg_get_function_identity_arguments(p.oid) || ')', null,
       obj_description(p.oid, 'pg_proc')
  from pg_proc p join pg_namespace n on n.oid = p.pronamespace
 where n.nspname = %(s)s
   and p.oid not in (select objid from pg_depend where deptype = 'e')
   and obj_description(p.oid, 'pg_proc') is not null
order by 1, 2, 3
"""

SEQUENCES = """
select c.relname from pg_class c join pg_namespace n on n.oid = c.relnamespace
 where n.nspname = %s and c.relkind = 'S'
   and c.oid not in (select objid from pg_depend where deptype = 'e')
 order by 1
"""


def q(ident):
    """식별자를 큰따옴표로 감싼다. 대문자·예약어가 섞인 이름을 지키려는 것이다."""
    return '"' + ident.replace('"', '""') + '"'


def lit(text):
    return "'" + text.replace("'", "''") + "'"


def table_ddl(cur, table):
    cur.execute(COLUMNS, (f"{SCHEMA}.{table}",))
    parts = []
    for name, coltype, notnull, default, identity, generated, collname in cur:
        piece = f"  {q(name)} {coltype}"
        if collname:
            piece += f" COLLATE {q(collname)}"
        if identity:
            always = "ALWAYS" if identity == "a" else "BY DEFAULT"
            piece += f" GENERATED {always} AS IDENTITY"
        elif generated == "s":
            piece += f" GENERATED ALWAYS AS ({default}) STORED"
        elif default is not None:
            piece += f" DEFAULT {default}"
        if notnull:
            piece += " NOT NULL"
        parts.append(piece)
    body = ",\n".join(parts)
    return f"CREATE TABLE IF NOT EXISTS {SCHEMA}.{q(table)} (\n{body}\n);"


def main():
    os.makedirs(OUT, exist_ok=True)
    manifest = {"schema": SCHEMA, "source": f"{ASIS['host']}:{ASIS['port']}/{ASIS['dbname']}"}

    with connect() as conn, conn.cursor() as cur:
        cur.execute(SEQUENCES, (SCHEMA,))
        sequences = [r[0] for r in cur.fetchall()]

        cur.execute(ROUTINES, (SCHEMA,))
        routines = cur.fetchall()

        cur.execute(TABLES, (SCHEMA,))
        tables = [r[0] for r in cur.fetchall()]

        ddl = [table_ddl(cur, t) for t in tables]

        cur.execute(CONSTRAINTS, (SCHEMA,))
        constraints = cur.fetchall()

        cur.execute(INDEXES, (SCHEMA,))
        indexes = cur.fetchall()

        cur.execute(COMMENTS, {"s": SCHEMA})
        comments = cur.fetchall()

        counts = {}
        for t in tables:
            cur.execute(f"select count(*) from {SCHEMA}.{q(t)}")
            counts[t] = cur.fetchone()[0]

    # ---- 01_schema.sql
    with open(os.path.join(OUT, "01_schema.sql"), "w", encoding="utf-8", newline="") as f:
        f.write(f"-- ASIS {manifest['source']} 의 {SCHEMA} 스키마 구조\n")
        f.write("-- dump_asis.py 가 만든 것이다. 손으로 고치지 말고 다시 뽑는다.\n\n")
        f.write("SET client_min_messages = warning;\n")
        # 루틴 본문 안에서 참조하는 테이블이 아직 없어도 넘어가게 한다.
        f.write("SET check_function_bodies = off;\n\n")
        f.write(f"CREATE SCHEMA IF NOT EXISTS {q(SCHEMA)};\n")
        f.write(f"SET search_path = {q(SCHEMA)}, public;\n\n")

        if sequences:
            f.write("-- ===== 시퀀스 =====\n")
            for s in sequences:
                f.write(f"CREATE SEQUENCE IF NOT EXISTS {SCHEMA}.{q(s)};\n")
            f.write("\n")

        f.write(f"-- ===== 테이블 {len(tables)}개 =====\n\n")
        for stmt in ddl:
            f.write(stmt + "\n\n")

        f.write(f"-- ===== 함수·프로시저 {len(routines)}개 =====\n")
        f.write("-- nvl 처럼 이름이 같고 인자가 다른 것이 있어 인자까지 적어 둔다.\n\n")
        for name, args, body in routines:
            f.write(f"-- {name}({args})\n{body.rstrip()};\n\n")

    # ---- 02_constraints.sql
    with open(os.path.join(OUT, "02_constraints.sql"), "w", encoding="utf-8", newline="") as f:
        f.write("-- 제약 · 인덱스 · 주석. 자료를 넣은 뒤에 건다.\n")
        f.write("SET client_min_messages = warning;\n")
        f.write(f"SET search_path = {q(SCHEMA)}, public;\n\n")

        f.write(f"-- ===== 제약 {len(constraints)}개 =====\n")
        for table, name, definition, _ in constraints:
            f.write(
                f"ALTER TABLE {SCHEMA}.{q(table)} "
                f"ADD CONSTRAINT {q(name)} {definition};\n"
            )

        f.write(f"\n-- ===== 인덱스 {len(indexes)}개 =====\n")
        for _, definition in indexes:
            # pg_get_indexdef 는 CREATE INDEX 로만 내놓는다. 재실행 대비로 바꿔 준다.
            f.write(definition.replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ", 1)
                              .replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ", 1)
                    + ";\n")

        f.write(f"\n-- ===== 주석 {len(comments)}개 =====\n")
        for kind, name, colname, descr in comments:
            if kind == "table":
                f.write(f"COMMENT ON TABLE {SCHEMA}.{q(name)} IS {lit(descr)};\n")
            elif kind == "column":
                f.write(f"COMMENT ON COLUMN {SCHEMA}.{q(name)}.{q(colname)} IS {lit(descr)};\n")
            else:
                f.write(f"COMMENT ON ROUTINE {SCHEMA}.{name} IS {lit(descr)};\n")

    # ---- manifest.json
    manifest.update(
        tables={t: counts[t] for t in tables},
        sequences=sequences,
        routines=[f"{n}({a})" for n, a, _ in routines],
        constraints=[f"{t}.{n}" for t, n, _, _ in constraints],
        indexes=[n for n, _ in indexes],
        comments=len(comments),
    )
    with open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)

    print(f"테이블   {len(tables):3}개  (총 {sum(counts.values()):,}행)")
    print(f"루틴     {len(routines):3}개")
    print(f"제약     {len(constraints):3}개")
    print(f"인덱스   {len(indexes):3}개")
    print(f"주석     {len(comments):3}개")
    print(f"시퀀스   {len(sequences):3}개")
    print(f"\n-> {OUT}")


if __name__ == "__main__":
    sys.exit(main())
