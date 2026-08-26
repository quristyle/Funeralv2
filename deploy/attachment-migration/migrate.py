#!/usr/bin/env python3
"""헬프데스크 첨부파일 37건을 FileServer 로 옮긴다 (결정 D5-B).

왜 이 도구가 배포 장비에서 돌아야 하나
--------------------------------------
옮길 파일의 **바이트가 배포 장비의 디스크에만** 있다(``/home/lee/jinAttachment`` ·
``/home/quri/jinAttachment``). 개발 PC 에서는 DB 는 보이지만 파일은 보이지 않는다.
그래서 스키마 변경(``docs/sql/attachment_to_fileserver.sql``)과 코드 변경은 미리 해 두고,
바이트를 옮기는 일만 이 도구로 분리했다.

무엇을 하나
-----------
``jsini.attachment`` 에서 ``fileid`` 가 비어 있는 행을 찾아, 그 파일을 FileServer 에
올리고 돌려받은 파일 아이디를 그 행에 적는다.

* **행마다 ``filepath`` 를 읽는다.** 저장 경로가 하나가 아니다(35건 /home/lee,
  2건 /home/quri). 디렉터리를 가정하면 2건을 놓친다.
* **반복 실행해도 안전하다.** 이미 ``fileid`` 가 있는 행은 건너뛴다. 중간에 끊겨도
  다시 돌리면 남은 것만 이어서 옮긴다.
* **원본을 지우지 않는다.** ``filepath``·``storedfilename`` 도 그대로 둔다.
  되돌릴 근거이고, 원본 삭제는 사람이 확인한 뒤 할 일이다.
* **없는 파일을 조용히 넘기지 않는다.** DB 행은 있는데 디스크에 파일이 없으면
  따로 모아 마지막에 보고한다. 그 행은 옮겨지지 않은 채로 남는다.

사용법
------
먼저 반드시 확인만 해 본다(아무것도 바꾸지 않는다)::

    export HD_DB="host=localhost port=5432 dbname=jinrecept user=jsini password=..."
    export FILE_UPLOAD_URL="http://localhost:5265/api/file/upload"
    export JSINI_TOKEN="eyJ..."          # 포털에서 로그인해 받은 JWT
    python3 migrate.py --dry-run

실제로 옮긴다::

    python3 migrate.py

토큰은 어디서 얻나
------------------
FileServer 업로드는 익명으로 열려 있지 않다(예전에는 아무나 올릴 수 있었고 그 구멍은
닫혔다). 포털에 관리자로 로그인한 뒤 브라우저 개발자도구에서 ``Authorization`` 헤더의
Bearer 값을 복사하거나, ``POST /api/auth/login`` 응답의 토큰을 쓴다.

필요한 것: python3 · psycopg2 (``pip3 install psycopg2-binary``)
"""

import argparse
import json
import mimetypes
import os
import posixpath
import sys
import urllib.error
import urllib.request
import uuid


try:
    import psycopg2
except ImportError:
    sys.exit("psycopg2 가 필요하다: pip3 install psycopg2-binary")

DB_DSN = os.environ.get("HD_DB", "")
UPLOAD_URL = os.environ.get(
    "FILE_UPLOAD_URL", "http://localhost:5265/api/file/upload"
)
TOKEN = os.environ.get("JSINI_TOKEN", "")

# FileServer 에서 이 첨부들을 구분할 이름. 나중에 무엇이 어디서 왔는지 알 수 있다.
BIZ_TYPE = os.environ.get("FILE_BIZ_TYPE", "helpdesk-improvement")


def build_multipart(field_name, filename, content_type, data, biz_type):
    """multipart/form-data 본문을 손으로 만든다 (requests 의존을 피한다)."""
    boundary = f"----jsini{uuid.uuid4().hex}"
    pre, post = [], []

    def enc(s):
        return s.encode("utf-8")

    # bizType 필드
    pre.append(enc(f"--{boundary}\r\n"))
    pre.append(enc('Content-Disposition: form-data; name="bizType"\r\n\r\n'))
    pre.append(enc(f"{biz_type}\r\n"))

    # 파일 필드. 파일명은 UTF-8 그대로 넣는다 (한글 파일명이 있다).
    pre.append(enc(f"--{boundary}\r\n"))
    pre.append(
        enc(
            f'Content-Disposition: form-data; name="{field_name}"; '
            f'filename="{filename}"\r\n'
        )
    )
    pre.append(enc(f"Content-Type: {content_type}\r\n\r\n"))
    post.append(enc(f"\r\n--{boundary}--\r\n"))

    body = b"".join(pre) + data + b"".join(post)
    return boundary, body


def upload(path: str, original_name: str, content_type: str) -> str:
    """FileServer 에 올리고 발급된 파일 아이디를 돌려준다."""
    with open(path, "rb") as fh:
        data = fh.read()

    if not content_type:
        content_type = (
            mimetypes.guess_type(original_name)[0] or "application/octet-stream"
        )

    boundary, body = build_multipart(
        "file", original_name, content_type, data, BIZ_TYPE
    )

    req = urllib.request.Request(UPLOAD_URL, method="POST", data=body)
    req.add_header("Content-Type", f"multipart/form-data; boundary={boundary}")
    if TOKEN:
        req.add_header("Authorization", f"Bearer {TOKEN}")

    with urllib.request.urlopen(req, timeout=180) as r:
        payload = json.loads(r.read().decode("utf-8"))

    # 봉투: { success, code, message, data: { result: [ {...} ], page } }
    result = (payload.get("data") or {}).get("result")
    item = result[0] if isinstance(result, list) and result else result
    if not isinstance(item, dict) or not item.get("id"):
        raise RuntimeError(f"업로드 응답에서 파일 아이디를 찾지 못했다: {payload}")
    return item["id"]


def main():
    ap = argparse.ArgumentParser(description="헬프데스크 첨부를 FileServer 로 옮긴다")
    ap.add_argument("--dry-run", action="store_true",
                    help="아무것도 바꾸지 않고 무엇을 옮길지만 보여 준다")
    ap.add_argument("--limit", type=int, default=0,
                    help="이 건수만 옮긴다 (0 이면 전부). 처음엔 1 로 해 보는 것을 권한다")
    args = ap.parse_args()

    if not DB_DSN:
        sys.exit("HD_DB 환경변수에 헬프데스크 DB 접속 문자열을 넣어야 한다.")
    if not args.dry_run and not TOKEN:
        sys.exit(
            "JSINI_TOKEN 이 비어 있다. FileServer 업로드는 익명으로 열려 있지 않다.\n"
            "확인만 하려면 --dry-run 을 쓴다."
        )

    conn = psycopg2.connect(DB_DSN)
    conn.autocommit = False
    cur = conn.cursor()

    cur.execute("""
        SELECT id, filepath, storedfilename, originalfilename, filetype, filesize
        FROM jsini.attachment
        WHERE fileid IS NULL
        ORDER BY id
    """)
    rows = cur.fetchall()

    cur.execute("SELECT count(*), count(fileid) FROM jsini.attachment")
    total, done = cur.fetchone()
    print(f"전체 {total}건 · 이미 옮김 {done}건 · 남음 {len(rows)}건")
    if args.dry_run:
        print("(확인만 한다 — 아무것도 바꾸지 않는다)\n")

    missing, moved, failed = [], 0, []

    for (aid, filepath, stored, original, ftype, fsize) in rows:
        # 경로를 행마다 조립한다. 디렉터리를 가정하지 않는 것이 핵심이다.
        #
        # posixpath 로 이어 붙이고 문자열로 둔다. DB 의 filepath 는 항상 리눅스
        # 경로인데 pathlib.Path 로 다루면 실행하는 OS 의 구분자로 바뀌어,
        # 윈도우에서 확인만 해 볼 때 `\home\lee\...` 로 보여 경로가 틀린 것으로 오해하게 된다.
        src = posixpath.join(filepath or "", stored or "")

        if not os.path.isfile(src):
            missing.append((aid, src, original))
            print(f"  [없음] id={aid} {src}")
            continue

        on_disk = os.path.getsize(src)
        size_note = "" if on_disk == fsize else f" (DB {fsize} ≠ 디스크 {on_disk})"

        if args.dry_run:
            print(f"  [옮길것] id={aid} {original} {on_disk}B{size_note}")
            continue

        try:
            file_id = upload(src, original or posixpath.basename(src), ftype or "")
        except (urllib.error.HTTPError, urllib.error.URLError, RuntimeError, OSError) as e:
            detail = e
            if isinstance(e, urllib.error.HTTPError):
                detail = f"HTTP {e.code} {e.read()[:200]!r}"
            failed.append((aid, str(detail)))
            print(f"  [실패] id={aid} {original}: {detail}")
            continue

        # 한 건씩 커밋한다. 중간에 끊겨도 여기까지는 남는다.
        cur.execute(
            "UPDATE jsini.attachment SET fileid = %s, migratedat = now() WHERE id = %s",
            (file_id, aid),
        )
        conn.commit()
        moved += 1
        print(f"  [옮김] id={aid} {original} → {file_id}{size_note}")

        if args.limit and moved >= args.limit:
            print(f"  (--limit {args.limit} 에 도달해 멈춘다)")
            break

    print("\n" + "=" * 56)
    if args.dry_run:
        print(f"옮길 수 있는 것 {len(rows) - len(missing)}건 · 파일이 없는 것 {len(missing)}건")
    else:
        print(f"옮김 {moved}건 · 실패 {len(failed)}건 · 파일 없음 {len(missing)}건")

    if missing:
        print("\n디스크에 파일이 없는 행 (옮기지 않았다):")
        for aid, path, original in missing:
            print(f"  id={aid} {original}\n      {path}")
        print("  → DB 행만 남은 것이다. 지울지는 사람이 판단할 일이라 손대지 않았다.")

    if failed:
        print("\n업로드가 실패한 행:")
        for aid, why in failed:
            print(f"  id={aid}: {why}")

    cur.close()
    conn.close()

    # 남은 것이 있으면 0 이 아닌 코드로 끝낸다. 스크립트로 엮을 때 알 수 있어야 한다.
    return 1 if (failed or missing) else 0


if __name__ == "__main__":
    sys.exit(main())
