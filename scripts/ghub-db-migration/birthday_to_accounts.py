#!/usr/bin/env python
"""생일 명단을 포털 계정으로 옮긴다 — ghub.birthday_profiles → scom.accounts.

  python birthday_to_accounts.py            매칭 결과만 보여 준다 (아무것도 안 바꾼다)
  python birthday_to_accounts.py --apply    scom.accounts 의 생일 컬럼을 채운다
  python birthday_to_accounts.py --drop     (apply 후) ghub.birthday_profiles 를 지운다

user_id 문자열로 매칭한다. 포털에 없는 사용자의 생일은 옮길 곳이 없다 —
그 목록을 보여 주고 버린다 (지허브 임직원 전원이 포털 사용자는 아니다).

대상 컬럼은 docs/sql/account_birthday.sql 이 만든
birth_date · birth_date_is_lunar · birthday_celebrated 다.
이미 생일이 있는 계정은 덮지 않는다 — 포털에서 입력한 값이 정본이다.
"""
import argparse
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

GHUB = dict(host=os.environ.get("GHUB_TOBE_HOST", "jin114.co.kr"),
            port=int(os.environ.get("GHUB_TOBE_PORT", "31015")),
            dbname="ghub",
            user=os.environ.get("GHUB_TOBE_USER", ""),
            password=os.environ.get("GHUB_TOBE_PASSWORD", ""))

# 포털(scom)은 dsn.env 에 없다 — 접속 정보는 환경변수나 인자로 준다.
PORTAL = dict(host=os.environ.get("PORTAL_HOST", "jin114.co.kr"),
              port=int(os.environ.get("PORTAL_PORT", "31015")),
              dbname=os.environ.get("PORTAL_DB", "jsiniportal"),
              user=os.environ.get("PORTAL_USER", "funeralv2"),
              password=os.environ.get("PORTAL_PASSWORD", "funeralv2"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--drop", action="store_true")
    args = ap.parse_args()

    ghub = psycopg2.connect(**GHUB)
    portal = psycopg2.connect(**PORTAL)
    gc, pc = ghub.cursor(), portal.cursor()

    gc.execute("""SELECT to_regclass('ghub.birthday_profiles')""")
    if gc.fetchone()[0] is None:
        print("ghub.birthday_profiles 가 이미 없다 — 할 일 없음")
        return

    gc.execute("""SELECT user_id, full_name, birth_date, is_lunar, is_celebrated
                  FROM ghub.birthday_profiles
                  WHERE NOT is_deleted AND birth_date IS NOT NULL""")
    rows = gc.fetchall()

    matched, skipped_has, unmatched = [], [], []
    for user_id, name, birth, lunar, celebrated in rows:
        pc.execute("""SELECT id, birth_date FROM scom.accounts
                      WHERE user_id = %s AND is_deleted = false""", (user_id,))
        acc = pc.fetchone()
        if acc is None:
            unmatched.append((user_id, name))
        elif acc[1] is not None:
            skipped_has.append((user_id, name))
        else:
            matched.append((acc[0], user_id, name, birth, lunar, celebrated))

    print(f"명단 {len(rows)}명: 옮김 {len(matched)} · 이미 생일 있음 {len(skipped_has)} · 포털에 없음 {len(unmatched)}")
    for u, n in unmatched:
        print(f"  포털에 없음: {u} ({n})")

    if args.apply or args.drop:
        for acc_id, user_id, name, birth, lunar, celebrated in matched:
            pc.execute("""UPDATE scom.accounts
                          SET birth_date = %s, birth_date_is_lunar = %s,
                              birthday_celebrated = %s,
                              updated_at = now(), updated_by = 'birthday-migration'
                          WHERE id = %s""",
                       (birth, lunar, celebrated, acc_id))
        portal.commit()
        print(f"scom.accounts {len(matched)}건 갱신 완료")

    if args.drop:
        gc.execute("DROP TABLE ghub.birthday_profiles")
        ghub.commit()
        print("ghub.birthday_profiles 삭제 완료")

    ghub.close()
    portal.close()


if __name__ == "__main__":
    main()
