#!/usr/bin/env python
"""ProjMngServer 를 통해 TOBE 가 실제로 응답하는지 확인한다.

DB 에 직접 붙어 보는 것(`smoke_tobe.py`)과 다르다. 여기서는 **서비스가 들고 있는
접속 문자열로** 도는지를 본다. 화면이 부르는 것과 같은 경로다.

  python smoke_service.py                 # 루프백 :5450 (게이트웨이를 거치지 않는다)
  python smoke_service.py --url http://127.0.0.1:5265/api/projmng   # 게이트웨이 경유

게이트웨이 경유는 JWT 가 필요하므로 --token 을 함께 준다.
"""
import argparse
import json
import sys
import urllib.error
import urllib.request

# (라벨, 경로, 본문) — 조회만 한다.
CASES = [
    ("공통코드 projdb", "/api/Proj",
     {"ProcName": "sp_projCommon", "ProcType": "srch",
      "MainParam": {"code_id": "projdb"}}),
    ("공통코드 projlist", "/api/Proj",
     {"ProcName": "sp_projCommon", "ProcType": "srch",
      "MainParam": {"code_id": "projlist"}}),
    ("공통코드 CODE_TYPE", "/api/Proj",
     {"ProcName": "sp_projCommon", "ProcType": "srch",
      "MainParam": {"code_id": "CODE_TYPE"}}),
    ("프로젝트 목록", "/api/Proj",
     {"ProcName": "sp_dev_proj_exec", "ProcType": "srch",
      "MainParam": {"req_type": "srch"}}),
    ("공통코드 관리", "/api/Proj",
     {"ProcName": "sp_devcomm_exec", "ProcType": "srch",
      "MainParam": {"req_type": "srch"}}),
    # 참여자·사용자·메뉴 조회는 여기 없다. 아래 프로시저들은 5단계에서 지웠다 — 인증·사용자·메뉴는 포털이 맡는다.
    # (docs/analysis/36-projmng-tobe-feature-cleanup.md)
    ("WBS (prj_rid=3)", "/api/Proj",
     {"ProcName": "sp_proj_wbs_exec", "ProcType": "srch",
      "MainParam": {"prj_rid": "3", "req_type": "srch"}}),
    ("할일", "/api/Proj",
     {"ProcName": "sp_home_todo_exec", "ProcType": "srch",
      "MainParam": {"req_type": "srch"}}),
    ("프로젝트 DB 목록", "/api/Proj",
     {"ProcName": "sp_projdblist", "ProcType": "srch", "MainParam": {}}),
    ("DB 로직", "/api/Proj",
     {"ProcName": "sp_projdbrspolist", "ProcType": "srch", "MainParam": {}}),
    ("DB 로직 기준", "/api/Proj",
     {"ProcName": "sp_devsqlresp_base_exec", "ProcType": "srch", "MainParam": {}}),
    ("소스 정보", "/api/Proj",
     {"ProcName": "sp_dev_srcinfo_exec", "ProcType": "srch",
      "MainParam": {"req_type": "srch"}}),
    ("액티비티", "/api/Proj",
     {"ProcName": "sp_dev_activityinfo_exec", "ProcType": "srch",
      "MainParam": {"req_type": "srch"}}),
    # 개발 도구. IsProjDb 를 끄면 devsqlresp 의 시스템 쿼리(DB 종류별)를 쓴다.
    # dbnick 은 devdbinfo 에 등록된 이름이어야 하고, db 는 그 행의 db_type 이다.
    # 대상 DB 는 서비스의 접속 문자열이 아니라 devdbinfo 가 정한다는 점에 주의한다.
    ("개발도구 tablelist", "/api/Dev",
     {"ProcName": "tablelist", "IsProjDb": False,
      "MainParam": {"db": "POSTGRESQL", "dbnick": "jsini", "schema": "projmng"}}),
    ("개발도구 proclist", "/api/Dev",
     {"ProcName": "proclist", "IsProjDb": False,
      "MainParam": {"db": "POSTGRESQL", "dbnick": "jsini", "schema": "projmng"}}),
    ("개발도구 columnsOftable", "/api/Dev",
     {"ProcName": "columnsOftable", "IsProjDb": False,
      "MainParam": {"db": "POSTGRESQL", "dbnick": "jsini", "schema": "projmng",
                    "table": "dev_proj"}}),
]


def post(url, body, token, user):
    data = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(url, data=data, method="POST")
    req.add_header("Content-Type", "application/json")
    req.add_header("X-User-Id", user)
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode("utf-8"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--url", default="http://127.0.0.1:5450")
    ap.add_argument("--token", default="")
    ap.add_argument("--user", default="quristyle")
    args = ap.parse_args()

    print(f"대상 {args.url}  (X-User-Id: {args.user})\n")
    ok = fail = 0
    for label, path, body in CASES:
        try:
            res = post(args.url + path, body, args.token, args.user)
            code = res.get("code")
            rows = res.get("data") or []
            cols = res.get("cols") or {}
            if code is not None and code < 0:
                print(f"!! {label:22} code={code} {res.get('message', '')[:60]}")
                fail += 1
                continue
            print(f"OK {label:22} {len(rows):>5}행 {len(cols):>3}컬럼")
            ok += 1
        except urllib.error.HTTPError as e:
            print(f"!! {label:22} HTTP {e.code} {e.reason}")
            fail += 1
        except Exception as e:  # noqa: BLE001 — 무엇이 나오든 실패로 센다
            print(f"!! {label:22} {type(e).__name__}: {str(e)[:70]}")
            fail += 1

    print(f"\n통과 {ok} · 실패 {fail}")
    return 0 if fail == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
