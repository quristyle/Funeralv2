#!/usr/bin/env python
"""TOBE 에 옮긴 프로시저가 실제로 도는지 확인한다.

ProjMngServer 가 하는 것과 같은 방식으로 부른다 —
  ① 인자 개수만큼 NULL 을 채우고 마지막 INOUT refcursor 에 이름을 준다
  ② CALL 뒤에 그 커서에서 FETCH 한다
조회만 하고 트랜잭션을 되돌리므로 TOBE 자료도 바뀌지 않는다.
"""
import sys

import psycopg2

import dsn

TOBE = dsn.tobe()

# (프로시저, 이름 있는 인자 몇 개를 채울지) — 나머지는 NULL 로 둔다.
CASES = [
    # 5단계에서 지운 프로시저(자체 로그인·사용자·그룹·메뉴)는 여기 없다.
    # docs/analysis/36-projmng-tobe-feature-cleanup.md 참조.
    ("sp_projcommon", {"ss_user_id": "admin", "p_code_id": "projlist"}),
    ("sp_projcommon", {"ss_user_id": "admin", "p_code_id": "db"}),
    ("sp_projcommon", {"ss_user_id": "admin", "p_code_id": "CODE_TYPE"}),
    ("sp_dev_proj_exec", {"p_req_type": "srch"}),
    ("sp_devcomm_exec", {"p_req_type": "srch"}),
    ("sp_proj_wbs_exec", {"p_prj_rid": "3", "p_req_type": "srch"}),
    ("sp_home_todo_exec", {"p_req_type": "srch"}),
    ("sp_projdblist", {}),
    ("sp_projdbrspolist", {}),
    ("sp_devsqlresp_base_exec", {}),
    ("sp_dev_db_prop_exec", {"p_req_type": "srch"}),
    ("sp_dev_srcinfo_dtl_exec", {"p_req_type": "srch"}),
    ("sp_dev_activityinfo_exec", {"p_req_type": "srch"}),
    ("sp_dev_excel_exec", {"p_req_type": "srch"}),
    ("sp_dev_proj_prop_exec", {"p_req_type": "srch"}),
    ("sp_dev_proj_user_map_exec", {"p_req_type": "srch"}),
    ("sp_dev_srcinfo_exec", {"p_req_type": "srch"}),
]

SIG = """
select pg_get_function_identity_arguments(p.oid)
  from pg_proc p join pg_namespace n on n.oid = p.pronamespace
 where n.nspname = 'projmng' and p.proname = %s
 order by 1 limit 1
"""


def params(sig):
    """'IN p_srch character varying, INOUT p_cur refcursor' -> ['p_srch', 'p_cur']"""
    out = []
    for piece in sig.split(","):
        words = piece.strip().split()
        out.append(words[1] if words[0] in ("IN", "OUT", "INOUT") else words[0])
    return out


def main():
    conn = psycopg2.connect(**TOBE)
    conn.autocommit = False
    ok = fail = 0

    for proc, given in CASES:
        cur = conn.cursor()
        try:
            cur.execute("SET search_path = projmng, public")
            cur.execute(SIG, (proc,))
            row = cur.fetchone()
            if not row:
                print(f"!! {proc:28} 프로시저가 없다")
                fail += 1
                continue

            names = params(row[0])
            # 마지막 refcursor 자리에는 커서 이름을 준다. 나머지는 준 값 또는 NULL.
            args = [given.get(n) for n in names[:-1]] + ["c1"]
            placeholders = ", ".join(["%s"] * len(args))
            cur.execute(f"CALL projmng.{proc}({placeholders})", args)
            cur.execute('FETCH ALL FROM "c1"')
            rows = cur.fetchall()
            cols = len(cur.description or ())
            label = f"{proc}({given.get('p_code_id') or given.get('p_prj_rid') or ''})".rstrip("()")
            print(f"OK {label:40} {len(rows):>5}행 {cols:>3}컬럼")
            ok += 1
        except psycopg2.Error as e:
            print(f"!! {proc:28} {str(e).strip().splitlines()[0][:90]}")
            fail += 1
        finally:
            conn.rollback()   # 조회만 했지만 흔적을 남기지 않는다
            cur.close()

    conn.close()
    print(f"\n통과 {ok} · 실패 {fail}")
    return 0 if fail == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
