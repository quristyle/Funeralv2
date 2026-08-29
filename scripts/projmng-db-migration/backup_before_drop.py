#!/usr/bin/env python
r"""5단계(제거) 직전의 TOBE 상태를 되돌릴 수 있게 파일로 남긴다.

ASIS 가 살아 있으니 최후의 정본은 있지만, TOBE 에는 그 뒤에 더한 것
(`sp_proj_user_map_list`)이 있어 ASIS 만으로는 그대로 복구되지 않는다.
그래서 **지우기 직전의 TOBE 를** 뽑아 둔다.

  python backup_before_drop.py

만드는 파일 (out/backup-step5/):
  tables.sql    지울 테이블 7개의 CREATE + INSERT (93행)
  routines.sql  지울 루틴 10개의 정의
  projcommon.sql 손대기 전 sp_projcommon 정의

되돌리려면 routines.sql → tables.sql → projcommon.sql 순으로 실행한다.
"""
import os
import sys

import psycopg2

import dsn

SCHEMA = dsn.SCHEMA
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "backup-step5")

TABLES = [
    "dev_user", "dev_user_prop", "dev_user_grp", "dev_user_grp_map",
    "dev_grp_menu_map", "dev_menu", "dev_menu_favorites",
]

ROUTINES = [
    "sp_proj_login", "sp_dev_user_exec", "sp_dev_user_exec_all",
    "sp_dev_user_prop_exec", "sp_dev_user_grp_exec", "sp_dev_user_grp_map_exec",
    "sp_dev_menu_exec", "sp_dev_menu_auth", "sp_dev_grp_menu_map_exec",
    "sp_dev_program_exec",
]


def q(ident):
    return '"' + ident.replace('"', '""') + '"'


def table_ddl(cur, table):
    cur.execute(
        """select a.attname, format_type(a.atttypid, a.atttypmod), a.attnotnull,
                  pg_get_expr(d.adbin, d.adrelid)
             from pg_attribute a
             left join pg_attrdef d on d.adrelid = a.attrelid and d.adnum = a.attnum
            where a.attrelid = %s::regclass and a.attnum > 0 and not a.attisdropped
            order by a.attnum""",
        (f"{SCHEMA}.{table}",),
    )
    parts = []
    for name, coltype, notnull, default in cur.fetchall():
        piece = f"  {q(name)} {coltype}"
        if default is not None:
            piece += f" DEFAULT {default}"
        if notnull:
            piece += " NOT NULL"
        parts.append(piece)
    return f"CREATE TABLE IF NOT EXISTS {SCHEMA}.{q(table)} (\n" + ",\n".join(parts) + "\n);"


def main():
    os.makedirs(OUT, exist_ok=True)
    conn = psycopg2.connect(**dsn.tobe())
    conn.set_session(readonly=True)
    cur = conn.cursor()
    cur.execute(f"SET search_path = {SCHEMA}, public")

    # ---- 테이블 구조 + 자료
    total = 0
    with open(os.path.join(OUT, "tables.sql"), "w", encoding="utf-8", newline="") as f:
        f.write("-- 5단계에서 지운 테이블 7개. 되돌릴 때 이 파일을 실행한다.\n")
        f.write(f"SET search_path = {q(SCHEMA)}, public;\n\n")
        for t in TABLES:
            f.write(table_ddl(cur, t) + "\n")
            cur.execute(f"select * from {SCHEMA}.{q(t)}")
            cols = [d[0] for d in cur.description]
            rows = cur.fetchall()
            total += len(rows)
            for row in rows:
                vals = ", ".join(
                    "NULL" if v is None else
                    ("TRUE" if v is True else "FALSE" if v is False else
                     "'" + str(v).replace("'", "''") + "'")
                    for v in row
                )
                collist = ", ".join(q(c) for c in cols)
                f.write(f"INSERT INTO {SCHEMA}.{q(t)} ({collist}) VALUES ({vals});\n")
            f.write(f"-- {t}: {len(rows)}행\n\n")

    # ---- 지울 루틴
    with open(os.path.join(OUT, "routines.sql"), "w", encoding="utf-8", newline="") as f:
        f.write("-- 5단계에서 지운 루틴 10개. 되돌릴 때 테이블보다 먼저 실행한다.\n")
        f.write("SET check_function_bodies = off;\n")
        f.write(f"SET search_path = {q(SCHEMA)}, public;\n\n")
        found = 0
        for name in ROUTINES:
            cur.execute(
                """select pg_get_functiondef(p.oid) from pg_proc p
                     join pg_namespace n on n.oid = p.pronamespace
                    where n.nspname = %s and p.proname = %s""",
                (SCHEMA, name),
            )
            for (body,) in cur.fetchall():
                f.write(f"-- {name}\n{body.rstrip()};\n\n")
                found += 1

    # ---- 손대기 전 sp_projcommon
    cur.execute(
        """select pg_get_functiondef(p.oid) from pg_proc p
             join pg_namespace n on n.oid = p.pronamespace
            where n.nspname = %s and p.proname = 'sp_projcommon'""",
        (SCHEMA,),
    )
    with open(os.path.join(OUT, "projcommon.sql"), "w", encoding="utf-8", newline="") as f:
        f.write("-- 손대기 전 sp_projcommon. user·family 코드 분기가 살아 있는 판이다.\n")
        f.write(f"SET search_path = {q(SCHEMA)}, public;\n\n")
        f.write(cur.fetchone()[0].rstrip() + ";\n")

    conn.close()
    print(f"테이블 {len(TABLES)}개 · {total}행")
    print(f"루틴   {found}개")
    print(f"-> {OUT}")


if __name__ == "__main__":
    sys.exit(main())
