#!/usr/bin/env python3
"""
JSini 개발 서버 제어판 — backend_run_ubuntu.sh 를 감싸는 로컬 웹 UI.

이 서버는 서비스 목록을 스스로 갖지 않는다. backend_run_ubuntu.sh 의 SERVICES 표를
읽어서 쓴다. 표가 이 파일과 스크립트 양쪽에 있으면 반드시 어긋나기 때문이다.
기동·중지도 전부 그 스크립트에 넘긴다. 이 파일은 버튼과 상태 표시만 한다.

실행:  python3 scripts/devui/server.py        (127.0.0.1:5600)
"""
import html
import json
import os
import re
import subprocess
import sys
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "backend_run_ubuntu.sh"
PORT = int(os.environ.get("DEVUI_PORT", "5600"))
MAX_LINES = 4000          # 작업 하나당 보관할 출력 줄 수


# ---------------------------------------------------------------- 서비스 표

def load_services():
    """backend_run_ubuntu.sh 의 SERVICES 배열을 읽는다. 형식: 이름|표시이름|경로|포트|SERVER_NAME

    그룹은 표에 없으므로 경로로 판정한다 — web/ 아래면 프론트, 나머지는 백엔드.
    스크립트 안의 `# ── 프론트 ──` 주석 대신 경로를 쓰는 것은, 주석 문구는
    누가 고쳐도 조용히 어긋나지만 디렉터리 구조는 그렇지 않기 때문이다.
    """
    text = SCRIPT.read_text(encoding="utf-8")
    m = re.search(r"^SERVICES=\((.*?)^\)", text, re.S | re.M)
    if not m:
        sys.exit(f"{SCRIPT} 에서 SERVICES 표를 찾지 못했습니다.")
    out = []
    for key, label, path, port, name in re.findall(
        r'"([^"|]+)\|([^"|]*)\|([^"|]*)\|(\d+)\|([^"]*)"', m.group(1)
    ):
        out.append({"key": key, "label": label, "path": path,
                    "port": int(port), "name": name,
                    "group": "front" if path.startswith("web/") else "back"})
    if not out:
        sys.exit("SERVICES 표가 비어 있습니다.")
    return out


def load_modules():
    """포털 셸의 appsettings.json 에 선언된 업무 MFE 목록.

    이 모듈들은 **별도 프로세스가 아니다.** 셸 csproj 의 ProjectReference 로
    빌드 시점에 합성되어 셸 출력 폴더에 DLL 로 실리고, 기동할 때
    PortalModuleRegistry 가 IPortalModule 을 훑어 한 라우터로 합친다.
    그래서 포트도 프로세스도 셸 것 하나뿐이고, 개별 기동·중지가 없다.

    읽는 곳을 csproj 가 아니라 appsettings 로 잡은 이유: csproj 의 ItemGroup 은
    런타임 파일럿으로 넘어가면 사라진다고 그 파일 주석이 예고하고 있지만,
    PortalApps 표는 진단 화면이 계속 쓰는 목록이라 그 뒤에도 남는다.
    """
    cfg = ROOT / "web/src/Shell/JSini.Web.Shell/appsettings.json"
    try:
        text = cfg.read_text(encoding="utf-8")
    except OSError:
        return []

    # 이 파일은 주석이 섞인 JSONC 라 json.load 로는 못 읽는다. 게다가 값 안에
    # "http://..." 가 있어 `//` 를 그냥 지우면 URL 이 잘린다. PortalApps 배열만
    # 괄호를 세어 잘라낸 뒤 필드를 직접 뽑는다.
    m = re.search(r'"PortalApps"\s*:\s*\[', text)
    if not m:
        return []
    depth, start = 0, m.end() - 1
    block = ""
    for j in range(start, len(text)):
        if text[j] == "[":
            depth += 1
        elif text[j] == "]":
            depth -= 1
            if depth == 0:
                block = text[start:j + 1]
                break

    mods = []
    for entry in re.findall(r"\{[^{}]*\}", block):
        def field(f):
            g = re.search(rf'"{f}"\s*:\s*"([^"]*)"', entry)
            return g.group(1) if g else ""
        key = field("Key")
        if key:
            mods.append({"key": key, "label": field("DisplayName"),
                         "route": field("RoutePrefix")})
    return mods


# ---------------------------------------------------------------- 상태 확인

def listening_ports():
    """LISTEN 중인 포트 집합. ss 를 매번 띄우지 않으려고 /proc 를 직접 읽는다."""
    ports = set()
    for path in ("/proc/net/tcp", "/proc/net/tcp6"):
        try:
            with open(path) as f:
                next(f, None)
                for line in f:
                    parts = line.split()
                    if len(parts) > 3 and parts[3] == "0A":
                        ports.add(int(parts[1].split(":")[1], 16))
        except OSError:
            pass
    return ports


# ---------------------------------------------------------------- 작업 실행

class Job:
    """스크립트 한 번 실행. 출력은 줄 단위로 모아 두고 프런트가 오프셋으로 받아 간다."""

    def __init__(self, title, args):
        self.title = title
        self.args = args
        self.lines = []
        self.running = True
        self.exit = None
        self._lock = threading.Lock()
        self._proc = None
        threading.Thread(target=self._run, daemon=True).start()

    def _add(self, text):
        with self._lock:
            self.lines.append(text)
            if len(self.lines) > MAX_LINES:
                del self.lines[: len(self.lines) - MAX_LINES]

    def _run(self):
        self._add(f"$ ./{SCRIPT.name} {' '.join(self.args)}")
        try:
            self._proc = subprocess.Popen(
                [str(SCRIPT), *self.args],
                cwd=str(ROOT),
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                bufsize=1,
                env=os.environ.copy(),   # DISPLAY·DBUS 를 물려줘야 터미널이 뜬다
            )
        except OSError as e:
            self._add(f"실행 실패: {e}")
            self.exit, self.running = 127, False
            return

        for line in self._proc.stdout:
            self._add(line.rstrip("\n"))
        self._proc.wait()
        self.exit = self._proc.returncode
        self._add("")
        self._add("완료" if self.exit == 0 else f"실패 (종료 코드 {self.exit})")
        self.running = False

    def snapshot(self, offset):
        with self._lock:
            return self.lines[offset:], len(self.lines)


class Runner:
    """한 번에 하나만 돌린다. 빌드가 겹치면 서로 느려지고 포트 경합이 난다."""

    def __init__(self):
        self.job = None
        self._lock = threading.Lock()

    def start(self, title, args):
        with self._lock:
            if self.job and self.job.running:
                return None, f"'{self.job.title}' 작업이 아직 실행 중입니다."
            self.job = Job(title, args)
            return self.job, None


RUNNER = Runner()
SERVICES = load_services()
BY_KEY = {s["key"]: s for s in SERVICES}
MODULES = load_modules()
# MFE 를 품는 서비스. SERVER_NAME 으로 찾는다 — 경로나 순서보다 덜 흔들린다.
MODULE_HOST = next((s["key"] for s in SERVICES if s["name"] == "PORTAL_SHELL"), None)


# ---------------------------------------------------------------- HTTP

PAGE = """<!doctype html>
<html lang="ko"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>JSini 개발 서버 제어판</title>
<style>
:root{--bg:#f6f7f9;--card:#fff;--fg:#1a1d21;--muted:#6b7280;--line:#e3e6ea;--subtle:#fafbfc;
      --up:#17974e;--upline:#9cd9b6;--down:#c2c7cd;
      --btn:#fff;--btnline:#cfd4da;--accent:#1f6feb;--logbg:#0f1216;--logfg:#d7dde4;}
@media (prefers-color-scheme:dark){:root:not([data-theme=light]){
      --bg:#14171a;--card:#1c2025;--fg:#e6e9ed;--muted:#9aa4b0;--line:#2b3138;--subtle:#181c21;
      --up:#4ec98a;--upline:#2f6446;--down:#4a525b;
      --btn:#252a31;--btnline:#3a424b;--accent:#589bff;--logbg:#0b0e11;--logfg:#cfd6dd;}}
*{box-sizing:border-box}
body{margin:0;padding:18px 20px 24px;background:var(--bg);color:var(--fg);
     font:14px/1.5 system-ui,-apple-system,"Noto Sans KR",sans-serif}
.wrap{max-width:1180px;margin:0 auto}

/* 머리글 — 제목·요약·전체 버튼을 한 줄에 둬서 세로 공간을 아낀다. */
header{display:flex;align-items:baseline;gap:12px;flex-wrap:wrap;margin-bottom:4px}
h1{font-size:17px;margin:0}
header .sum{color:var(--muted);font-size:13px}
header .grow{flex:1}
header button{font-size:13px}
.hint{color:var(--muted);font-size:12px;margin-bottom:14px}

h2{font-size:12px;margin:0 0 8px;color:var(--muted);font-weight:600;letter-spacing:.06em;
   text-transform:uppercase}
h2 .count{font-weight:400;text-transform:none;letter-spacing:0;margin-left:6px}
section{margin-bottom:16px}

/* 카드 격자 — 13개가 한 화면에 들어오도록 폭을 좁게 잡는다. */
.grid{display:grid;gap:9px;grid-template-columns:repeat(auto-fill,minmax(196px,1fr))}
.svc{background:var(--card);border:1px solid var(--line);border-radius:9px;padding:10px 11px 9px}
.svc.on{border-color:var(--upline)}
.svc .top{display:flex;align-items:center;gap:7px}
.dot{width:8px;height:8px;border-radius:99px;background:var(--down);flex:none}
.svc.on .dot{background:var(--up)}
.svc .name{font-family:ui-monospace,monospace;font-weight:600;font-size:13px}
.svc .port{margin-left:auto;font-family:ui-monospace,monospace;font-size:12px;color:var(--muted)}
.svc .label{color:var(--muted);font-size:12px;margin:2px 0 9px;
            white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.svc .btns{display:flex;gap:6px}
.svc .btns button{flex:1;padding:4px 0;font-size:12px}

button{font:inherit;padding:5px 11px;border:1px solid var(--btnline);background:var(--btn);
       color:var(--fg);border-radius:6px;cursor:pointer}
button:hover:not(:disabled){border-color:var(--accent);color:var(--accent)}
button:disabled{opacity:.42;cursor:not-allowed}
.danger:hover:not(:disabled){border-color:#d9534f;color:#d9534f}

/* MFE 는 셸 카드 옆이 아니라 아래 띠로 뺀다. 카드 안에 넣으면 그 카드만
   길어져서 격자가 어긋난다. */
.mfe{margin-top:9px;background:var(--subtle);border:1px solid var(--line);
     border-radius:9px;padding:9px 11px 6px}
.mfe-note{color:var(--muted);font-size:12px;margin-bottom:7px}
.chip{display:inline-block;border:1px solid var(--line);border-radius:6px;
      padding:2px 8px;margin:0 5px 5px 0;font-size:12px;background:var(--card)}
.chip code{color:var(--muted);font-family:ui-monospace,monospace;margin-left:5px}

details{border:1px solid var(--line);border-radius:9px;background:var(--card);overflow:hidden}
summary{cursor:pointer;padding:8px 12px;font-size:13px;color:var(--muted);user-select:none}
summary::marker{color:var(--muted)}
#log{background:var(--logbg);color:var(--logfg);padding:12px;
     font:12px/1.5 ui-monospace,monospace;white-space:pre-wrap;word-break:break-all;
     height:240px;overflow:auto}
#log:empty::before{content:"작업을 실행하면 출력이 여기에 표시됩니다.";color:var(--muted)}
.warn{background:#fff4e5;border:1px solid #ffd8a8;color:#8a5200;padding:9px 13px;
      border-radius:8px;margin-bottom:14px;font-size:13px}
@media (prefers-color-scheme:dark){:root:not([data-theme=light]) .warn{
      background:#2e2413;border-color:#5b4620;color:#e8c98a}}
</style></head><body><div class="wrap">

<header>
  <h1>JSini 개발 서버 제어판</h1>
  <span class="sum" id="summary">…</span>
  <span class="grow"></span>
  <button id="allstop" class="danger">전체 중지</button>
  <button id="all">전체 재기동</button>
</header>
<div class="hint">backend_run_ubuntu.sh 를 그대로 호출합니다.</div>
<div id="warn"></div>

<section id="sec-back"></section>
<section id="sec-front"></section>

<details id="logbox"><summary>작업 로그</summary><div id="log"></div></details>

</div><script>
let offset = 0, busy = false, lastJob = null;
const logEl = document.getElementById("log");
const logBox = document.getElementById("logbox");
const esc = s => String(s).replace(/[&<>"]/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;"}[c]));

function card(s) {
  return `<div class="svc ${s.up ? "on" : ""}">
    <div class="top"><span class="dot"></span>
      <span class="name">${esc(s.key)}</span><span class="port">${s.port}</span></div>
    <div class="label" title="${esc(s.label)}">${esc(s.label)}</div>
    <div class="btns">
      <button data-a="restart" data-s="${esc(s.key)}">재기동</button>
      <button data-a="stop" data-s="${esc(s.key)}" ${s.up ? "" : "disabled"}>중지</button>
    </div></div>`;
}

// MFE 는 셸과 같은 프로세스에 실려 있다. 버튼을 주면 개별 기동이 되는 것처럼
// 보이므로 정보로만 보여 준다.
function mfeStrip(mods) {
  const chips = mods.map(m =>
    `<span class="chip">${esc(m.label || m.key)}<code>${esc(m.route)}</code></span>`).join("");
  return `<div class="mfe">
    <div class="mfe-note">이 프로세스 안에 함께 실린 업무 MFE ${mods.length}개 —
      빌드 시점 합성이라 별도 포트도 프로세스도 없고, 개별 기동·중지가 되지 않습니다.</div>
    <div>${chips}</div></div>`;
}

function section(el, title, list, strip) {
  const n = list.filter(s => s.up).length;
  el.innerHTML = `<h2>${title}<span class="count">${n}/${list.length} 실행 중</span></h2>
    <div class="grid">${list.map(card).join("")}</div>${strip || ""}`;
}

async function refresh() {
  const r = await fetch("/api/state");
  const d = await r.json();
  const back = d.services.filter(s => s.group === "back");
  const front = d.services.filter(s => s.group === "front");
  section(document.getElementById("sec-back"), "백엔드", back, "");
  section(document.getElementById("sec-front"), "프론트", front,
          d.modules.length ? mfeStrip(d.modules) : "");

  const n = d.services.filter(s => s.up).length;
  document.getElementById("summary").textContent = `${n}/${d.services.length} 실행 중`;
  document.getElementById("warn").innerHTML = d.display ? "" :
    `<div class="warn">DISPLAY 가 없습니다. 이 서버는 데스크톱 세션 안에서 실행해야 합니다 —
     스크립트가 서비스마다 gnome-terminal 을 띄우므로, 없으면 기동이 조용히 실패합니다.</div>`;

  busy = d.busy;
  if (d.job && d.job !== lastJob) { lastJob = d.job; offset = 0; logEl.textContent = ""; }
  document.querySelectorAll("button[data-a],#all,#allstop")
          .forEach(b => { if (b.dataset.a !== "stop" || !b.disabled) b.disabled = busy; });
  if (busy) pollLog();
}

async function pollLog() {
  const r = await fetch("/api/log?offset=" + offset);
  const d = await r.json();
  if (d.lines.length) {
    const stick = logEl.scrollTop + logEl.clientHeight >= logEl.scrollHeight - 30;
    logEl.textContent += d.lines.join("\\n") + "\\n";
    offset = d.offset;
    if (stick) logEl.scrollTop = logEl.scrollHeight;
  }
  if (d.running) setTimeout(pollLog, 700); else refresh();
}

async function run(action, svc, confirmMsg) {
  if (busy) return;
  if (confirmMsg && !confirm(confirmMsg)) return;
  const r = await fetch("/api/run", {
    method: "POST", headers: {"Content-Type": "application/json"},
    body: JSON.stringify({action, svc})
  });
  const d = await r.json();
  if (d.error) { alert(d.error); return; }
  offset = 0; lastJob = d.job; logEl.textContent = ""; busy = true;
  logBox.open = true;                      // 작업이 시작되면 로그를 펼친다
  refresh(); pollLog();
}

document.querySelector(".wrap").addEventListener("click", e => {
  const b = e.target.closest("button[data-a]");
  if (b) run(b.dataset.a, b.dataset.s);
});
document.getElementById("allstop").onclick =
  () => run("allstop", null, "백엔드와 프론트를 전부 내립니다. 계속할까요?");
document.getElementById("all").onclick =
  () => run("all", null, "전체를 중지하고 다시 빌드합니다. 몇 분 걸립니다. 계속할까요?");

refresh();
// 탭이 보이지 않을 때는 폴링하지 않는다 (노트북 배터리).
setInterval(() => { if (!document.hidden && !busy) refresh(); }, 2500);
document.addEventListener("visibilitychange", () => { if (!document.hidden) refresh(); });
</script></body></html>
"""


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, *a):
        pass                                  # 요청 로그로 터미널을 채우지 않는다

    def _send(self, code, body, ctype):
        data = body.encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def _json(self, obj, code=200):
        self._send(code, json.dumps(obj), "application/json; charset=utf-8")

    def do_GET(self):
        path = self.path.split("?")[0]
        if path == "/":
            self._send(200, PAGE, "text/html; charset=utf-8")
        elif path == "/api/state":
            open_ports = listening_ports()
            job = RUNNER.job
            self._json({
                "services": [{**{k: s[k] for k in ("key", "label", "port", "group")},
                              "up": s["port"] in open_ports} for s in SERVICES],
                "modules": MODULES,
                "moduleHost": MODULE_HOST,
                "busy": bool(job and job.running),
                "job": job.title if job else None,
                "display": bool(os.environ.get("DISPLAY") or os.environ.get("WAYLAND_DISPLAY")),
            })
        elif path == "/api/log":
            job = RUNNER.job
            if not job:
                self._json({"lines": [], "offset": 0, "running": False})
                return
            q = self.path.split("?", 1)[1] if "?" in self.path else ""
            try:
                offset = int(dict(p.split("=", 1) for p in q.split("&") if "=" in p)
                             .get("offset", 0))
            except ValueError:
                offset = 0
            lines, total = job.snapshot(offset)
            self._json({"lines": lines, "offset": total,
                        "running": job.running, "exit": job.exit})
        else:
            self._send(404, "not found", "text/plain; charset=utf-8")

    def do_POST(self):
        if self.path != "/api/run":
            self._send(404, "not found", "text/plain; charset=utf-8")
            return
        try:
            n = int(self.headers.get("Content-Length", 0))
            req = json.loads(self.rfile.read(n) or b"{}")
        except (ValueError, json.JSONDecodeError):
            self._json({"error": "잘못된 요청입니다."}, 400)
            return

        action, svc = req.get("action"), req.get("svc")
        if action in ("restart", "stop") and svc not in BY_KEY:
            self._json({"error": f"알 수 없는 서비스: {svc}"}, 400)
            return

        if action == "restart":
            title, args = f"{svc} 재기동", [svc]
        elif action == "stop":
            title, args = f"{svc} 중지", ["stop", svc]
        elif action == "allstop":
            title, args = "전체 중지", ["allstop"]
        elif action == "all":
            title, args = "전체 재기동", ["all"]
        else:
            self._json({"error": f"알 수 없는 동작: {action}"}, 400)
            return

        job, err = RUNNER.start(title, args)
        if err:
            self._json({"error": err}, 409)
        else:
            self._json({"job": job.title})


def main():
    if not os.access(SCRIPT, os.X_OK):
        sys.exit(f"{SCRIPT} 를 실행할 수 없습니다.")
    srv = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    print(f"JSini 개발 서버 제어판  →  http://127.0.0.1:{PORT}", flush=True)
    print(f"  서비스 {len(SERVICES)}개를 {SCRIPT.name} 에서 읽었습니다.")
    if not (os.environ.get("DISPLAY") or os.environ.get("WAYLAND_DISPLAY")):
        print("  ⚠ DISPLAY 가 없습니다 — 데스크톱 세션 안에서 실행해야 기동이 됩니다.")
    print("  Ctrl+C 로 종료합니다.")
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        print("\n종료합니다.")


if __name__ == "__main__":
    main()
