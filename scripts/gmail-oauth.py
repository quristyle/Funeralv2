#!/usr/bin/env python3
"""Gmail 리프레시 토큰을 한 번 받아 scripts/secrets.env 에 적는다.

받은 토큰을 **화면에 찍지 않는다** — 터미널 기록과 스크롤에 비밀이 남지 않게,
곧바로 secrets.env(git 제외)에 써 넣고 "적었다"만 알린다.

쓰는 법:
    python3 scripts/gmail-oauth.py --account imrbiz

ClientId·ClientSecret 은 secrets.env 의 Gmail__OAuth__* 에서 읽는다. 없으면 물어본다.
자세한 것은 docs/gmail-archive.md.
"""

import argparse
import http.server
import io
import json
import os
import secrets
import threading
import urllib.parse
import urllib.request
import webbrowser

AUTH_URL = "https://accounts.google.com/o/oauth2/v2/auth"
TOKEN_URL = "https://oauth2.googleapis.com/token"
SCOPE = "https://www.googleapis.com/auth/gmail.readonly"

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SECRETS = os.path.join(ROOT, "scripts", "secrets.env")


def read_secrets():
    values = {}
    if os.path.exists(SECRETS):
        with io.open(SECRETS, encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith("#") and "=" in line:
                    k, v = line.split("=", 1)
                    values[k.strip()] = v.strip()
    return values


def write_secret(key, value):
    """있으면 고치고 없으면 덧붙인다. 다른 줄은 건드리지 않는다."""
    lines = []
    if os.path.exists(SECRETS):
        with io.open(SECRETS, encoding="utf-8") as f:
            lines = f.read().split("\n")

    for i, line in enumerate(lines):
        if line.strip().startswith(key + "="):
            lines[i] = "%s=%s" % (key, value)
            break
    else:
        if lines and lines[-1].strip():
            lines.append("")
        lines.append("%s=%s" % (key, value))
        lines.append("")

    with io.open(SECRETS, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    os.chmod(SECRETS, 0o600)


class Catcher(http.server.BaseHTTPRequestHandler):
    """구글이 되돌려 보내는 code 를 받는 한 번짜리 서버."""

    code = None
    state = None
    error = None

    def do_GET(self):
        query = urllib.parse.parse_qs(urllib.parse.urlparse(self.path).query)
        Catcher.code = (query.get("code") or [None])[0]
        Catcher.state = (query.get("state") or [None])[0]
        Catcher.error = (query.get("error") or [None])[0]

        self.send_response(200)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.end_headers()
        done = "받았습니다. 터미널로 돌아가세요." if Catcher.code else "실패했습니다."
        self.wfile.write(("<html><body style='font-family:sans-serif;padding:3rem'>"
                          "<h2>%s</h2></body></html>" % done).encode("utf-8"))

    def log_message(self, *args):
        pass        # 주소에 code 가 들어 있다 — 콘솔에 찍지 않는다


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--account", required=True,
                    help="scom.gmail_accounts.id (예: imrbiz)")
    ap.add_argument("--port", type=int, default=8765)
    args = ap.parse_args()

    env = read_secrets()
    client_id = env.get("Gmail__OAuth__ClientId") or input("ClientId: ").strip()
    client_secret = env.get("Gmail__OAuth__ClientSecret") or input("ClientSecret: ").strip()
    if not client_id or not client_secret:
        raise SystemExit("ClientId·ClientSecret 이 필요합니다.")

    redirect = "http://localhost:%d" % args.port
    state = secrets.token_urlsafe(16)

    # access_type=offline 과 prompt=consent 가 **둘 다** 있어야 리프레시 토큰이 온다.
    # 한 번 동의한 계정은 prompt 없이는 access_token 만 주고 조용히 넘어간다.
    url = AUTH_URL + "?" + urllib.parse.urlencode({
        "client_id": client_id,
        "redirect_uri": redirect,
        "response_type": "code",
        "scope": SCOPE,
        "access_type": "offline",
        "prompt": "consent",
        "state": state,
    })

    server = http.server.HTTPServer(("localhost", args.port), Catcher)
    threading.Thread(target=server.handle_request, daemon=True).start()

    print("브라우저에서 동의해 주세요. 안 열리면 아래 주소를 직접 여세요:\n")
    print(url + "\n")
    try:
        webbrowser.open(url)
    except Exception:
        pass

    print("기다리는 중…")
    server.server_close()

    if Catcher.error:
        raise SystemExit("거절되었습니다: %s" % Catcher.error)
    if not Catcher.code:
        raise SystemExit("code 를 못 받았습니다.")
    if Catcher.state != state:
        raise SystemExit("state 가 다릅니다. 처음부터 다시 하세요.")

    body = urllib.parse.urlencode({
        "code": Catcher.code,
        "client_id": client_id,
        "client_secret": client_secret,
        "redirect_uri": redirect,
        "grant_type": "authorization_code",
    }).encode("utf-8")

    with urllib.request.urlopen(urllib.request.Request(TOKEN_URL, data=body)) as res:
        token = json.load(res)

    refresh = token.get("refresh_token")
    if not refresh:
        raise SystemExit(
            "refresh_token 이 안 왔습니다. 이미 동의한 계정이면 "
            "https://myaccount.google.com/permissions 에서 이 앱을 지우고 다시 하세요.")

    key = "Gmail__Credentials__%s__RefreshToken" % args.account
    write_secret("Gmail__OAuth__ClientId", client_id)
    write_secret("Gmail__OAuth__ClientSecret", client_secret)
    write_secret(key, refresh)

    print("\n%s 에 적었습니다 (값은 찍지 않습니다)." % os.path.relpath(SECRETS, ROOT))
    print("  %s" % key)
    print("\n다음: dev.bat ai / ./backend_run_ubuntu.sh ai 로 다시 띄우고")
    print("  POST /gmail/sync  {\"accountId\":\"%s\",\"maxMessages\":10}" % args.account)


if __name__ == "__main__":
    main()
