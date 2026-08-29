#!/usr/bin/env python
r"""프로젝트관리 역할 시드를 포털 DB(scom)에 적용하고 전후를 비교한다.

  python apply_role_seed.py --dry-run   지금 상태만 보여 준다
  python apply_role_seed.py             docs/sql/projmng_role_seed.sql 을 실행한다

시드 자체가 반복 실행에 안전하므로 여러 번 돌려도 된다.
접속 정보는 AuthServer 의 appsettings.Local.json 에서 읽는다 — 여기 적지 않는다.
"""
import argparse
import io
import json
import os
import sys

import psycopg2

AUTH_SETTINGS = r"C:\Funeralv2\microservices\AuthServer\appsettings.Local.json"
SEED = r"C:\Funeralv2\docs\sql\projmng_role_seed.sql"

USERS = ('bmkim', 'hsstyle', 'jjstyle', 'jskim', 'kggmvp',
         'kspark', 'quristyle', 'sglee', 'yws')

# 사람별로 실제 보이게 되는 프로젝트관리 화면 수.
# LIKE 'PM\_%%' 의 역슬래시는 밑줄을 한 글자로 묶기 위한 것이다(와일드카드 방지).
# 이 문장은 파라미터를 받으므로 psycopg2 가 %를 자리표시자로 읽는다 — %% 로 적는다.
SNAPSHOT = r"""
select a.user_id,
       coalesce(string_agg(distinct ra.role_id, ', '), '(없음)') as roles,
       count(distinct m.id) as screens
  from scom.accounts a
  left join scom.role_accounts ra on ra.account_id = a.id and not ra.is_deleted
  left join scom.role_menus rm    on rm.role_id = ra.role_id and not rm.is_deleted
  left join scom.system_menus m   on m.id = rm.menu_id and m.type = 'MENU'
                                 and m.id like 'PM\_%%' and not m.is_deleted
 where a.user_id = any(%s)
 group by 1 order by 1
"""


def portal_dsn():
    cfg = json.load(io.open(AUTH_SETTINGS, encoding="utf-8-sig"))
    kv = dict(p.split("=", 1) for p in cfg["ConnectionStrings"]["jsinicore"].split(";") if "=" in p)
    return dict(host=kv["Host"], port=int(kv["Port"]), dbname=kv["Database"],
                user=kv["Username"], password=kv["Password"], connect_timeout=15)


def snapshot(cur):
    cur.execute(SNAPSHOT, (list(USERS),))
    return {r[0]: (r[1], r[2]) for r in cur.fetchall()}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    if not os.path.exists(SEED):
        sys.exit(f"시드 파일이 없다: {SEED}")

    # 시드 파일이 BEGIN/COMMIT 을 직접 들고 있으므로 autocommit 으로 연다.
    conn = psycopg2.connect(**portal_dsn())
    conn.autocommit = True
    cur = conn.cursor()

    before = snapshot(cur)
    if args.dry_run:
        print("지금 상태 (--dry-run)")
        for uid, (roles, n) in sorted(before.items()):
            print(f"  {uid:12} {n:>3}화면   {roles}")
        return 0

    cur.execute(io.open(SEED, encoding="utf-8").read())
    after = snapshot(cur)

    print(f"{'사용자':12} {'전':>4} {'후':>5}   역할")
    print("-" * 78)
    for uid in sorted(after):
        b = before.get(uid, ("", 0))[1]
        a_roles, a_n = after[uid]
        arrow = "->" if b != a_n else "  "
        print(f"{uid:14} {b:>3} {arrow}{a_n:>3}   {a_roles}")

    cur.execute(r"""select rm.role_id, count(*)
                      from scom.role_menus rm join scom.system_menus m on m.id = rm.menu_id
                     where rm.role_id like 'PROJMNG\_%' and not rm.is_deleted and m.type = 'MENU'
                     group by 1 order by 1""")
    print("\n새 역할이 가진 화면 수")
    for role, n in cur.fetchall():
        print(f"  {role:20} {n:>3}")

    conn.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
