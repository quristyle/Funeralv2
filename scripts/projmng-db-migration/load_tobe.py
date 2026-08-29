#!/usr/bin/env python
"""뽑아 둔 구조를 TOBE(jin114.co.kr:31015 / projmng)에 세우고 자료를 옮긴다.

  python load_tobe.py --dry-run   무엇을 할지만 보여 준다 (TOBE 를 건드리지 않는다)
  python load_tobe.py             실제로 옮긴다
  python load_tobe.py --replace   기존 projmng 스키마를 지우고 다시 만든다

ASIS 는 읽기 전용 트랜잭션으로만 연다. 이 스크립트는 ASIS 에 어떤 쓰기도 하지 않는다.

자료는 COPY 로 흘려보낸다. 전체가 5MB 남짓이라 테이블 하나씩 메모리에 담아도 된다.
전체를 트랜잭션 하나로 묶으므로, 중간에 실패하면 TOBE 는 손대기 전 상태로 돌아간다.
"""
import argparse
import io
import json
import os
import sys

import psycopg2

import dsn

ASIS = dsn.asis()
TOBE = dsn.tobe()
SCHEMA = dsn.SCHEMA
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")


def q(ident):
    return '"' + ident.replace('"', '""') + '"'


def read(name):
    # newline="" 로 읽는다. 기본 모드는 '\r\n' 을 '\n' 으로 바꿔 버리는데,
    # 함수 본문에 CRLF 를 쓰는 프로시저가 있어 그러면 원본과 달라진다.
    with open(os.path.join(OUT, name), encoding="utf-8", newline="") as f:
        return f.read()


def columns_of(cur, table):
    cur.execute(
        """select a.attname from pg_attribute a
            where a.attrelid = %s::regclass and a.attnum > 0
              and not a.attisdropped
            order by a.attnum""",
        (f"{SCHEMA}.{table}",),
    )
    return [r[0] for r in cur.fetchall()]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--replace", action="store_true",
                    help="TOBE 의 기존 projmng 스키마를 지우고 다시 만든다")
    args = ap.parse_args()

    manifest = json.loads(read("manifest.json"))
    tables = list(manifest["tables"])

    # ---- ASIS: 읽기 전용
    src = psycopg2.connect(**ASIS)
    src.set_session(readonly=True, autocommit=False)
    scur = src.cursor()

    print(f"ASIS  {ASIS['host']}:{ASIS['port']}/{ASIS['dbname']} (읽기 전용)")
    print(f"TOBE  {TOBE['host']}:{TOBE['port']}/{TOBE['dbname']}")
    print(f"대상  테이블 {len(tables)}개 · 루틴 {len(manifest['routines'])}개 · "
          f"제약 {len(manifest['constraints'])}개 · 주석 {manifest['comments']}개\n")

    if args.dry_run:
        for t in tables:
            print(f"  {t:26} {manifest['tables'][t]:>6,}행")
        print("\n--dry-run 이라 여기서 멈춘다.")
        return 0

    dst = psycopg2.connect(**TOBE)
    dst.autocommit = False
    dcur = dst.cursor()

    # ---- 안전장치: 이미 무언가 서 있으면 덮어쓰지 않는다
    dcur.execute(
        """select count(*) from pg_class c join pg_namespace n
             on n.oid = c.relnamespace
            where n.nspname = %s and c.relkind in ('r','v','S','p')""",
        (SCHEMA,),
    )
    existing = dcur.fetchone()[0]
    if existing and not args.replace:
        sys.exit(
            f"중단: TOBE 의 {SCHEMA} 스키마에 이미 객체가 {existing}개 있다.\n"
            f"       덮어쓰려면 --replace 를 준다."
        )

    try:
        if args.replace and existing:
            print(f"기존 {SCHEMA} 스키마를 지운다 ({existing}개 객체)")
            dcur.execute(f"DROP SCHEMA {q(SCHEMA)} CASCADE")

        print("① 구조 (스키마 · 테이블 · 루틴)")
        dcur.execute(read("01_schema.sql"))

        print("② 자료")
        total = 0
        for t in tables:
            cols = columns_of(scur, t)
            collist = ", ".join(q(c) for c in cols)
            buf = io.BytesIO()
            scur.copy_expert(
                f"COPY {SCHEMA}.{q(t)} ({collist}) TO STDOUT "
                f"WITH (FORMAT text, ENCODING 'UTF8')",
                buf,
            )
            buf.seek(0)
            dcur.copy_expert(
                f"COPY {SCHEMA}.{q(t)} ({collist}) FROM STDIN "
                f"WITH (FORMAT text, ENCODING 'UTF8')",
                buf,
            )
            dcur.execute(f"select count(*) from {SCHEMA}.{q(t)}")
            got = dcur.fetchone()[0]
            want = manifest["tables"][t]
            mark = "OK " if got == want else "!! "
            print(f"   {mark}{t:26} {got:>6,} / {want:,}")
            if got != want:
                raise RuntimeError(f"{t}: 행수가 맞지 않는다 ({got} != {want})")
            total += got

        print("③ 제약 · 인덱스 · 주석")
        dcur.execute(read("02_constraints.sql"))

        dst.commit()
        print(f"\n커밋했다. 총 {total:,}행.")
    except Exception:
        dst.rollback()
        print("\n실패해서 되돌렸다. TOBE 는 손대기 전 상태다.", file=sys.stderr)
        raise
    finally:
        src.close()
        dst.close()

    return 0


if __name__ == "__main__":
    sys.exit(main())
