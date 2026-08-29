#!/usr/bin/env python
"""ASIS 프로젝트관리 DB(jsini/projmng) 읽기 전용 조회 도구.

  python pmq.py "select * from dev_proj limit 5"
  python pmq.py -f query.sql
  echo "select 1" | python pmq.py

읽기 전용이다. 트랜잭션을 read-only 로 열고, 쓰기 키워드로 시작하는 문장은 거른다.
"""
import argparse
import re
import sys

import psycopg2

import dsn
import psycopg2.extras

DSN = dict(dsn.asis(), options=f"-c search_path={dsn.SCHEMA},public")

WRITE = re.compile(
    r"^\s*(insert|update|delete|drop|truncate|alter|create|grant|revoke|call|do)\b",
    re.IGNORECASE,
)


def run(sql, fmt="table", limit=200):
    if WRITE.match(sql):
        sys.exit("거부: 읽기 전용 도구다 — 쓰기 문장은 실행하지 않는다.")

    with psycopg2.connect(**DSN) as conn:
        conn.set_session(readonly=True, autocommit=False)
        with conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cur:
            cur.execute(sql)
            if cur.description is None:
                print("(결과 집합 없음)")
                return
            rows = cur.fetchmany(limit)
            more = cur.fetchone() is not None

    if not rows:
        print("(0건)")
        return

    cols = list(rows[0].keys())
    if fmt == "csv":
        print(",".join(cols))
        for r in rows:
            print(",".join("" if r[c] is None else str(r[c]).replace(",", " ") for c in cols))
    else:
        width = {c: max(len(c), *(len(str(r[c] if r[c] is not None else "")) for r in rows)) for c in cols}
        width = {c: min(w, 60) for c, w in width.items()}
        print(" | ".join(c.ljust(width[c]) for c in cols))
        print("-+-".join("-" * width[c] for c in cols))
        for r in rows:
            print(" | ".join(str(r[c] if r[c] is not None else "").replace("\n", " ")[: width[c]].ljust(width[c]) for c in cols))
    print(f"\n({len(rows)}건{' 이상 — limit 로 잘림' if more else ''})")


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("sql", nargs="?")
    p.add_argument("-f", "--file")
    p.add_argument("--csv", action="store_true")
    p.add_argument("-n", "--limit", type=int, default=200)
    a = p.parse_args()

    if a.file:
        query = open(a.file, encoding="utf-8").read()
    elif a.sql:
        query = a.sql
    else:
        query = sys.stdin.read()

    run(query, "csv" if a.csv else "table", a.limit)
