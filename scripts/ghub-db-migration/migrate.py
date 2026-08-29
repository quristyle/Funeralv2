#!/usr/bin/env python
"""GHUB(생활과환경) 자료 이관 — ASIS(45750/ghub) → TOBE(31015/ghub).

  python migrate.py --dry-run   무엇을 옮길지만 보여 준다 (TOBE 를 건드리지 않는다)
  python migrate.py             빈 테이블만 채운다 (이미 자료가 있으면 건너뛴다)
  python migrate.py --replace   TOBE 표를 비우고 다시 채운다

ASIS 는 읽기 전용 세션(default_transaction_read_only=on)으로만 연다.
이 스크립트는 ASIS 에 어떤 쓰기도 하지 않는다.

스키마는 docs/sql/ghub_schema.sql 이 만든다 — 이 스크립트는 자료만 나른다.
전량이 20만 행 미만이라 테이블 하나씩 COPY 로 흘려보낸다.
TOBE 쪽 전체를 트랜잭션 하나로 묶으므로 중간에 실패하면 손대기 전으로 돌아간다.
"""
import argparse
import io
import os
import sys

import psycopg2


def load_env():
    here = os.path.dirname(os.path.abspath(__file__))
    path = os.path.join(here, "dsn.env")
    if not os.path.exists(path):
        return
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            k, v = line.split("=", 1)
            os.environ.setdefault(k.strip(), v.strip())


load_env()


def cfg(prefix):
    def get(name, default=None):
        v = os.environ.get(f"GHUB_{prefix}_{name}", default)
        if v is None or v == "":
            print(f"GHUB_{prefix}_{name} 가 비어 있다 — dsn.env 를 채워라", file=sys.stderr)
            sys.exit(1)
        return v
    return dict(
        host=get("HOST"), port=int(get("PORT")), dbname=get("DB"),
        user=get("USER"), password=get("PASSWORD"),
    )


# 그대로 복사하는 표 (ASIS 이름 = TOBE 이름, 컬럼 교집합만 나른다)
# 순서가 곧 적재 순서다 — FK 부모가 먼저 온다.
COPY_TABLES = [
    "weather_locations",
    "weather_standards",
    "weather_responses",
    "grid_coordinates",
    "weather_warning_zones",
    "weather_infos",
    "weather_event_records",
    "weather_warnings",
    "weather_location_warnings",
    "weather_warning_msgs",
    "weather_warning_msg_sentences",
    "weather_warning_statuses",
    "weather_mid_term_forecasts",
    "weather_short_term_forecasts",
    "weather_ultra_srt_forecasts",
    "birthday_messages",
]

# birthday_profiles 는 ASIS user_profiles 에서 생일 관련 필드만 추린다
BIRTHDAY_PROFILE_SELECT = """
  SELECT user_id, full_name, department, company_code, thumbnail_url,
         birth_date, is_lunar, is_celebrated, is_active,
         created_by, created_at, modified_by, modified_at, is_deleted
  FROM ghub.user_profiles
  ORDER BY id
"""
BIRTHDAY_PROFILE_COLS = (
    "user_id, full_name, department, company_code, thumbnail_url, "
    "birth_date, is_lunar, is_celebrated, is_active, "
    "created_by, created_at, modified_by, modified_at, is_deleted"
)


def q(ident):
    return '"' + ident.replace('"', '""') + '"'


def columns_of(cur, table):
    cur.execute(
        """SELECT column_name FROM information_schema.columns
           WHERE table_schema = 'ghub' AND table_name = %s
           ORDER BY ordinal_position""",
        (table,),
    )
    return [r[0] for r in cur.fetchall()]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--replace", action="store_true")
    args = ap.parse_args()

    asis = psycopg2.connect(options="-c default_transaction_read_only=on", **cfg("ASIS"))
    tobe = psycopg2.connect(**cfg("TOBE"))
    ac, tc = asis.cursor(), tobe.cursor()

    try:
        for table in COPY_TABLES + ["birthday_profiles"]:
            special = table == "birthday_profiles"
            src_table = "user_profiles" if special else table

            ac.execute(f"SELECT count(*) FROM ghub.{q(src_table)}")
            src_cnt = ac.fetchone()[0]
            tc.execute(f"SELECT count(*) FROM ghub.{q(table)}")
            dst_cnt = tc.fetchone()[0]

            if args.dry_run:
                print(f"{table:<36} ASIS {src_cnt:>7} → TOBE(현재 {dst_cnt})")
                continue

            if dst_cnt > 0:
                if args.replace:
                    tc.execute(f"TRUNCATE ghub.{q(table)} CASCADE")
                    print(f"{table:<36} truncate")
                else:
                    print(f"{table:<36} 이미 {dst_cnt}행 — 건너뜀")
                    continue

            buf = io.StringIO()
            if special:
                ac.copy_expert(
                    f"COPY ({BIRTHDAY_PROFILE_SELECT}) TO STDOUT", buf)
                cols = BIRTHDAY_PROFILE_COLS
            else:
                # 컬럼 교집합만 나른다 (TOBE 에 없는 ASIS 컬럼은 버린다)
                src_cols = columns_of(ac, src_table)
                dst_cols = set(columns_of(tc, table))
                cols_list = [c for c in src_cols if c in dst_cols]
                cols = ", ".join(q(c) for c in cols_list)
                ac.copy_expert(
                    f"COPY (SELECT {cols} FROM ghub.{q(src_table)}) TO STDOUT", buf)
            buf.seek(0)
            tc.copy_expert(f"COPY ghub.{q(table)} ({cols}) FROM STDIN", buf)
            print(f"{table:<36} {src_cnt}행 복사")

            # identity 시퀀스를 max(id) 뒤로 보낸다 (id 를 그대로 복사했으므로)
            tc.execute(
                """SELECT column_name FROM information_schema.columns
                   WHERE table_schema='ghub' AND table_name=%s
                     AND column_name='id' AND is_identity='YES'""",
                (table,),
            )
            if tc.fetchone():
                tc.execute(
                    f"SELECT setval(pg_get_serial_sequence('ghub.{table}', 'id'), "
                    f"COALESCE((SELECT max(id) FROM ghub.{q(table)}), 0) + 1, false)")

        if not args.dry_run:
            tobe.commit()
            print("커밋 완료")
    except Exception:
        tobe.rollback()
        raise
    finally:
        asis.close()
        tobe.close()


if __name__ == "__main__":
    main()
