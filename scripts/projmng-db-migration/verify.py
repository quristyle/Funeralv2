#!/usr/bin/env python
"""ASIS 와 TOBE 를 나란히 놓고 대조한다. 양쪽 다 읽기만 한다.

  python verify.py           목록·행수 대조
  python verify.py --deep    테이블 내용까지 해시로 대조 (느리지만 확실하다)
"""
import argparse
import hashlib
import sys

import psycopg2

import dsn

ASIS = dsn.asis()
TOBE = dsn.tobe()
SCHEMA = dsn.SCHEMA

# 확장(hypopg·pg_stat_statements)이 만든 객체는 업무와 무관하므로 뺀다.
TABLES = """
select c.relname from pg_class c join pg_namespace n on n.oid=c.relnamespace
 where n.nspname=%s and c.relkind='r'
   and c.oid not in (select objid from pg_depend where deptype='e')
 order by 1"""

ROUTINES = """
select p.proname||'('||pg_get_function_identity_arguments(p.oid)||')'
  from pg_proc p join pg_namespace n on n.oid=p.pronamespace
 where n.nspname=%s and p.oid not in (select objid from pg_depend where deptype='e')
 order by 1"""

CONSTRAINTS = """
select c.relname||'.'||con.conname||' '||pg_get_constraintdef(con.oid)
  from pg_constraint con join pg_class c on c.oid=con.conrelid
  join pg_namespace n on n.oid=c.relnamespace
 where n.nspname=%s order by 1"""

COLUMNS = """
select c.relname||'.'||a.attname||' '||format_type(a.atttypid,a.atttypmod)||
       case when a.attnotnull then ' NOT NULL' else '' end
  from pg_attribute a join pg_class c on c.oid=a.attrelid
  join pg_namespace n on n.oid=c.relnamespace
 where n.nspname=%s and c.relkind='r' and a.attnum>0 and not a.attisdropped
   and c.oid not in (select objid from pg_depend where deptype='e')
 order by 1"""

# 본문까지 대조한다. 이름·인자가 같아도 안이 다를 수 있다.
BODIES = """
select p.proname||'('||pg_get_function_identity_arguments(p.oid)||')'||E'\\n'
       ||pg_get_functiondef(p.oid)
  from pg_proc p join pg_namespace n on n.oid=p.pronamespace
 where n.nspname=%s and p.oid not in (select objid from pg_depend where deptype='e')
 order by 1"""


def fetch(cur, sql):
    cur.execute(sql, (SCHEMA,))
    return [r[0] for r in cur.fetchall()]


def compare(label, a, b):
    sa, sb = set(a), set(b)
    only_a, only_b = sorted(sa - sb), sorted(sb - sa)
    ok = not only_a and not only_b
    print(f"{'OK ' if ok else '!! '}{label:12} ASIS {len(sa):>4}  TOBE {len(sb):>4}")
    # 루틴 본문은 길다. 어느 것이 다른지만 보이면 되므로 첫 줄만 찍는다.
    for x in only_a[:20]:
        print(f"      ASIS 에만: {x.splitlines()[0][:110]}")
    for x in only_b[:20]:
        print(f"      TOBE 에만: {x.splitlines()[0][:110]}")
    return ok


def digest(cur, table, cols):
    """행 순서에 흔들리지 않게 행별 해시를 XOR 로 합친다."""
    acc = 0
    cur.execute(f'select {cols} from {SCHEMA}."{table}"')
    n = 0
    for row in cur:
        h = hashlib.md5(repr(row).encode("utf-8")).digest()
        acc ^= int.from_bytes(h, "big")
        n += 1
    return n, f"{acc:032x}"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--deep", action="store_true")
    args = ap.parse_args()

    src = psycopg2.connect(**ASIS); src.set_session(readonly=True)
    dst = psycopg2.connect(**TOBE); dst.set_session(readonly=True)
    a, b = src.cursor(), dst.cursor()

    # 양쪽 search_path 를 같게 맞춘다. pg_get_constraintdef · pg_get_functiondef 는
    # search_path 에 든 스키마를 접두어 없이 내놓기 때문에, 이걸 맞추지 않으면
    # 같은 객체가 'projmng.dev_menu' 와 'dev_menu' 로 갈려 다른 것처럼 보인다.
    for cur in (a, b):
        cur.execute(f"SET search_path = {SCHEMA}, public")

    ok = True
    ok &= compare("테이블", fetch(a, TABLES), fetch(b, TABLES))
    ok &= compare("컬럼", fetch(a, COLUMNS), fetch(b, COLUMNS))
    ok &= compare("루틴", fetch(a, ROUTINES), fetch(b, ROUTINES))
    ok &= compare("루틴본문", fetch(a, BODIES), fetch(b, BODIES))
    ok &= compare("제약", fetch(a, CONSTRAINTS), fetch(b, CONSTRAINTS))

    print("\n행수" + ("  · 내용 해시" if args.deep else ""))
    for t in fetch(a, TABLES):
        a.execute(f'select count(*) from {SCHEMA}."{t}"'); ca = a.fetchone()[0]
        try:
            b.execute(f'select count(*) from {SCHEMA}."{t}"'); cb = b.fetchone()[0]
        except psycopg2.Error:
            dst.rollback(); print(f"!! {t:26} TOBE 에 없다"); ok = False; continue

        line = f"{t:26} {ca:>6,} / {cb:>6,}"
        good = ca == cb
        if args.deep and good and ca:
            a.execute(
                """select string_agg(quote_ident(attname), ', ' order by attnum)
                     from pg_attribute where attrelid=%s::regclass
                      and attnum>0 and not attisdropped""", (f"{SCHEMA}.{t}",))
            cols = a.fetchone()[0]
            na, ha = digest(a, t, cols)
            nb, hb = digest(b, t, cols)
            good = ha == hb
            line += f"  {ha[:12]} / {hb[:12]}"
        print(("OK " if good else "!! ") + line)
        ok &= good

    print("\n" + ("모두 일치한다." if ok else "차이가 있다. 위를 보라."))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
