#!/usr/bin/env python
"""ASIS 와 TOBE 를 나란히 놓고 대조한다. 양쪽 다 읽기만 한다.

  python verify.py           목록·행수 대조
  python verify.py --deep    테이블 내용까지 해시로 대조 (느리지만 확실하다)
  python verify.py --raw     아래 '일부러 다른 것' 을 감안하지 않고 있는 그대로 비교

**둘은 이제 일부러 다르다.** 5단계에서 인증·사용자·메뉴를 TOBE 에서 걷어냈고
(docs/analysis/36-projmng-tobe-feature-cleanup.md), 3단계에서 프로시저 하나를 더했다.
기본 동작은 그 차이를 알고 있으므로, 그것 말고 다른 차이가 생기면 그때 잡힌다.
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


# ── 일부러 다른 것 ───────────────────────────────────────────
# 5단계에서 TOBE 에서만 걷어낸 것. ASIS 에는 그대로 있다.
REMOVED_TABLES = {
    "dev_user", "dev_user_prop", "dev_user_grp", "dev_user_grp_map",
    "dev_grp_menu_map", "dev_menu", "dev_menu_favorites",
}
REMOVED_ROUTINES = {
    "sp_proj_login", "sp_dev_user_exec", "sp_dev_user_exec_all",
    "sp_dev_user_prop_exec", "sp_dev_user_grp_exec", "sp_dev_user_grp_map_exec",
    "sp_dev_menu_exec", "sp_dev_menu_auth", "sp_dev_grp_menu_map_exec",
    "sp_dev_program_exec",
}
# 3단계에서 TOBE 에만 더한 것.
ADDED_ROUTINES = {"sp_proj_user_map_list"}
# 5단계에서 TOBE 쪽만 본문이 바뀐 것 (user·family 분기 제거).
CHANGED_ROUTINES = {"sp_projcommon"}
# 6단계에서 TOBE 쪽만 내용이 바뀐 표. 행수는 같아야 하지만 내용 해시는 다르다.
#   devdbinfo — 개발 도구가 볼 DB 목록. `jsini` 행을 ASIS 에서 TOBE 로 돌렸다.
CHANGED_TABLES = {"devdbinfo"}


def fetch(cur, sql):
    cur.execute(sql, (SCHEMA,))
    return [r[0] for r in cur.fetchall()]


def prune(label, rows, side, raw):
    """`--raw` 가 아니면 '일부러 다른 것' 을 양쪽에서 걷어내고 비교한다.

    각 대조의 행 모양이 달라 이름을 뽑는 방법도 다르다.
      테이블   `dev_menu`
      컬럼     `dev_menu.mnu_id character varying(10)`      → 첫 '.' 앞이 표 이름
      제약     `dev_menu.dev_menu_pk PRIMARY KEY (...)`     → 첫 '.' 앞이 표 이름
      루틴     `sp_dev_menu_exec(IN p_srch ...)`            → 첫 '(' 앞이 루틴 이름
      루틴본문 위와 같은 첫 줄 + 정의
    """
    if raw:
        return rows

    if label == "테이블":
        drop = REMOVED_TABLES if side == "ASIS" else set()
        return [r for r in rows if r not in drop]

    if label in ("컬럼", "제약"):
        if side != "ASIS":
            return rows
        return [r for r in rows if r.split(".", 1)[0] not in REMOVED_TABLES]

    if label in ("루틴", "루틴본문"):
        skip = CHANGED_ROUTINES | (
            REMOVED_ROUTINES if side == "ASIS" else ADDED_ROUTINES
        )
        return [r for r in rows if r.splitlines()[0].split("(", 1)[0] not in skip]

    return rows


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
    ap.add_argument("--raw", action="store_true",
                    help="'일부러 다른 것' 을 감안하지 않고 있는 그대로 비교한다")
    args = ap.parse_args()

    src = psycopg2.connect(**ASIS); src.set_session(readonly=True)
    dst = psycopg2.connect(**TOBE); dst.set_session(readonly=True)
    a, b = src.cursor(), dst.cursor()

    # 양쪽 search_path 를 같게 맞춘다. pg_get_constraintdef · pg_get_functiondef 는
    # search_path 에 든 스키마를 접두어 없이 내놓기 때문에, 이걸 맞추지 않으면
    # 같은 객체가 'projmng.dev_menu' 와 'dev_menu' 로 갈려 다른 것처럼 보인다.
    for cur in (a, b):
        cur.execute(f"SET search_path = {SCHEMA}, public")

    if not args.raw:
        print(f"* 일부러 다른 것은 빼고 본다 — TOBE 에서 걷어낸 표 {len(REMOVED_TABLES)}개 ·"
              f" 루틴 {len(REMOVED_ROUTINES)}개, 더한 루틴 {len(ADDED_ROUTINES)}개,"
              f" 본문이 바뀐 루틴 {len(CHANGED_ROUTINES)}개,"
              f" 내용이 바뀐 표 {len(CHANGED_TABLES)}개. (--raw 로 그대로 보기)\n")

    ok = True
    for label, sql in (("테이블", TABLES), ("컬럼", COLUMNS), ("루틴", ROUTINES),
                       ("루틴본문", BODIES), ("제약", CONSTRAINTS)):
        ok &= compare(label,
                      prune(label, fetch(a, sql), "ASIS", args.raw),
                      prune(label, fetch(b, sql), "TOBE", args.raw))

    print("\n행수" + ("  · 내용 해시" if args.deep else ""))
    for t in fetch(a, TABLES):
        if not args.raw and t in REMOVED_TABLES:
            continue
        a.execute(f'select count(*) from {SCHEMA}."{t}"'); ca = a.fetchone()[0]
        try:
            b.execute(f'select count(*) from {SCHEMA}."{t}"'); cb = b.fetchone()[0]
        except psycopg2.Error:
            dst.rollback(); print(f"!! {t:26} TOBE 에 없다"); ok = False; continue

        line = f"{t:26} {ca:>6,} / {cb:>6,}"
        good = ca == cb
        # 내용이 일부러 다른 표는 행수만 본다.
        if not args.raw and t in CHANGED_TABLES:
            print(("OK " if good else "!! ") + line + "  (내용은 일부러 다르다)")
            ok &= good
            continue
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
