#!/usr/bin/env python3
"""
JSini 개발 서버 제어판 — backend_run_ubuntu.sh 를 감싸는 로컬 웹 UI.

이 서버는 서비스 목록을 스스로 갖지 않는다. backend_run_ubuntu.sh 의 SERVICES 표를
읽어서 쓴다. 표가 이 파일과 스크립트 양쪽에 있으면 반드시 어긋나기 때문이다.
기동·중지도 전부 그 스크립트에 넘긴다. 이 파일은 버튼과 상태 표시만 한다.

작업은 **서비스마다 따로** 돈다. 하나를 재기동하는 동안에도 다른 것을 만질 수 있다.
겹치는 것을 막는 규칙은 Runner 주석에 적었다.

서비스는 DEV_BACKGROUND=1 로 띄운다 — 터미널 창이 뜨지 않고, 출력은
logs/<이름>.log 에 쌓여 이 화면의 "로그" 단추로 본다.

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
from urllib.parse import parse_qs, urlparse

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "backend_run_ubuntu.sh"
LOG_DIR = ROOT / "logs"
PORT = int(os.environ.get("DEVUI_PORT", "5600"))
MAX_LINES = 4000          # 작업 하나당 보관할 출력 줄 수
MAX_JOBS = 30             # 끝난 작업을 이만큼만 남긴다
TAIL_BYTES = 64 * 1024    # 서비스 로그를 처음 열 때 거슬러 올라가는 양


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


def read_tail(path, pos):
    """서비스 로그 파일을 pos 바이트부터 읽는다. (내용, 다음 위치, 파일 있음)

    재기동하면 스크립트가 로그를 새로 쓰기 때문에(`>` 로 자른다) 파일이 줄어든다.
    그때는 pos 를 0 으로 되돌려야 한다 — 안 그러면 새 출력이 영영 안 보인다.
    """
    try:
        size = path.stat().st_size
    except OSError:
        return "", 0, False
    if pos > size:
        pos = 0
    if pos <= 0 and size > TAIL_BYTES:
        pos = size - TAIL_BYTES     # 처음 열 때 전체를 다 보내지 않는다
    if pos >= size:
        return "", size, True
    try:
        with open(path, "rb") as f:
            f.seek(pos)
            data = f.read(size - pos)
    except OSError:
        return "", pos, True
    return data.decode("utf-8", "replace"), size, True


# ---------------------------------------------------------------- 작업 실행

class Job:
    """명령 한 번 실행. 출력은 줄 단위로 모아 두고 프런트가 오프셋으로 받아 간다.

    보통은 backend_run_ubuntu.sh 를 부르지만(args 만 주면 된다), 소스 받기처럼
    스크립트 밖의 명령도 돈다 — 그때는 cmd 로 통째로 준다. 화면에 보여 줄 줄은
    shown 으로 따로 받는다. cmd 를 그대로 찍으면 bash -c 의 따옴표까지 나와
    읽기 어렵기 때문이다.
    """

    def __init__(self, jid, key, title, args, cmd=None, shown=None):
        self.id = jid
        self.key = key            # 대상 서비스. 전체 작업(all·allstop·pull)이면 None
        self.title = title
        self.args = args
        self.cmd = cmd or [str(SCRIPT), *args]
        self.shown = shown or f"./{SCRIPT.name} {' '.join(args)}"
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
        self._add(f"$ {self.shown}")
        env = os.environ.copy()
        # 서비스마다 터미널 창을 여는 대신 백그라운드로 띄우게 한다.
        # 제어판에서 열두 개를 만지는데 창이 열두 개 뜨면 화면을 덮는다.
        env["DEV_BACKGROUND"] = "1"
        try:
            self._proc = subprocess.Popen(
                self.cmd,
                cwd=str(ROOT),
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                bufsize=1,
                env=env,
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
    """작업을 서비스마다 따로 돌린다.

    예전에는 한 번에 하나만 돌렸다. 빌드가 겹치면 서로 느려지고 포트 경합이
    난다는 이유였는데, 그 대가로 auth 를 재기동하는 3 분 동안 화면 전체가
    잠겨 아무것도 못 눌렀다. 겹쳐서 곤란한 것은 사실 둘뿐이라 그것만 막는다.

      · 같은 서비스에 두 작업   — 한쪽이 방금 띄운 것을 다른 쪽이 죽인다
      · 전체 작업과 다른 작업   — all/allstop 은 모든 서비스를 건드린다

    포트는 서비스마다 다르므로 서로 다른 서비스끼리는 경합하지 않는다.
    빌드가 겹치는 문제는 backend_run_ubuntu.sh 의 flock 이 맡는다 — 함께 쓰는
    프로젝트(microservices/Common)를 두 빌드가 동시에 건드리지 못하게 한다.
    """

    def __init__(self):
        self.jobs = {}          # id -> Job (끝난 것도 로그를 보려고 남겨 둔다)
        self._seq = 0
        self._lock = threading.Lock()

    def _live(self):
        return [j for j in self.jobs.values() if j.running]

    def start(self, key, title, args, cmd=None, shown=None):
        with self._lock:
            live = self._live()
            glob = next((j for j in live if j.key is None), None)
            if glob:
                return None, f"'{glob.title}' 작업이 끝나야 합니다. 전체 작업 중에는 다른 작업을 받지 않습니다."
            if key is None:
                if live:
                    names = ", ".join(j.title for j in live)
                    return None, f"실행 중인 작업이 있습니다: {names}"
            else:
                same = next((j for j in live if j.key == key), None)
                if same:
                    return None, f"'{same.title}' 작업이 아직 실행 중입니다."

            self._seq += 1
            jid = str(self._seq)
            job = Job(jid, key, title, args, cmd, shown)
            self.jobs[jid] = job

            # 끝난 작업이 쌓이지 않게 오래된 것부터 버린다.
            done = sorted((j for j in self.jobs.values() if not j.running),
                          key=lambda j: int(j.id))
            for old in done[:max(0, len(self.jobs) - MAX_JOBS)]:
                self.jobs.pop(old.id, None)
            return job, None

    def state(self):
        with self._lock:
            jobs = sorted(self.jobs.values(), key=lambda j: int(j.id))
        return [{"id": j.id, "key": j.key, "title": j.title,
                 "running": j.running, "exit": j.exit} for j in jobs]

    def get(self, jid):
        return self.jobs.get(jid)


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
      --up:#17974e;--upline:#9cd9b6;--down:#c2c7cd;--work:#c98a17;--workline:#e8c98a;
      --btn:#fff;--btnline:#cfd4da;--accent:#1f6feb;--logbg:#0f1216;--logfg:#d7dde4;
      --sbtrack:#eef0f3;--sbthumb:#c3c9d0;}
/* 어두운 쪽 값. 아무것도 고르지 않았으면 시스템 설정을 따르고(앞),
   머리글의 ☀/☾ 로 고르면 그때부터 data-theme 이 이긴다(뒤). 값은 같다. */
@media (prefers-color-scheme:dark){:root:not([data-theme=light]){
      --bg:#14171a;--card:#1c2025;--fg:#e6e9ed;--muted:#9aa4b0;--line:#2b3138;--subtle:#181c21;
      --up:#4ec98a;--upline:#2f6446;--down:#4a525b;--work:#e0a83c;--workline:#6b5324;
      --btn:#252a31;--btnline:#3a424b;--accent:#589bff;--logbg:#0b0e11;--logfg:#cfd6dd;
      --sbtrack:#1a1e23;--sbthumb:#39424c;}}
:root[data-theme=dark]{
      --bg:#14171a;--card:#1c2025;--fg:#e6e9ed;--muted:#9aa4b0;--line:#2b3138;--subtle:#181c21;
      --up:#4ec98a;--upline:#2f6446;--down:#4a525b;--work:#e0a83c;--workline:#6b5324;
      --btn:#252a31;--btnline:#3a424b;--accent:#589bff;--logbg:#0b0e11;--logfg:#cfd6dd;
      --sbtrack:#1a1e23;--sbthumb:#39424c;}
*{box-sizing:border-box}

/* 스크롤 막대 — 3px 가는 띠. 바탕과 손잡이 색이 테마를 따라간다.
   크롬·사파리는 ::-webkit-* 로 폭을 px 로 줄 수 있고, 파이어폭스는 못 주므로
   scrollbar-width:thin 으로 만족한다. 둘을 같이 쓰면 안 된다 —
   크롬은 scrollbar-width 가 있으면 ::-webkit-* 를 통째로 버리고 제 기본 폭(10px)을
   쓴다. 그래서 표준 속성은 ::-webkit-scrollbar 를 모르는 쪽에만 준다. */
@supports not selector(::-webkit-scrollbar){
  *{scrollbar-width:thin;scrollbar-color:var(--sbthumb) var(--sbtrack)}}
::-webkit-scrollbar{width:3px;height:3px}
::-webkit-scrollbar-track{background:var(--sbtrack)}
::-webkit-scrollbar-thumb{background:var(--sbthumb);border-radius:3px}
::-webkit-scrollbar-thumb:hover{background:var(--muted)}
::-webkit-scrollbar-corner{background:var(--sbtrack)}
body{margin:0;padding:16px 18px 18px;background:var(--bg);color:var(--fg);
     font:14px/1.5 system-ui,-apple-system,"Noto Sans KR",sans-serif;
     height:100vh;overflow:hidden;display:flex;flex-direction:column}
/* 폭을 묶지 않는다 — 넓은 화면에서는 카드가 한 줄에 더 많이 들어가고
   로그 칸도 그만큼 넓어진다. */
.wrap{max-width:none;margin:0;flex:1;min-height:0;display:flex;flex-direction:column}

/* 두 칸 — 카드는 왼쪽, 로그는 오른쪽. 화면 높이를 나눠 쓰기 때문에 카드가
   많아져도 로그는 늘 같은 자리에 있고, 굴리는 것은 카드 칸 안쪽뿐이다.
   예전처럼 로그가 카드 아래에 있으면 서비스를 재기동할 때마다 맨 아래까지
   내려가야 출력이 보였다. */
.cols{flex:1;min-height:0;display:grid;gap:16px;
      grid-template-columns:minmax(0,1fr) minmax(380px,34%)}
.cols>.left{min-width:0;min-height:0;overflow:auto;padding-right:3px}
.cols>.right{min-width:0;min-height:0;display:flex;flex-direction:column}

/* 머리글 — 제목·요약·전체 버튼을 한 줄에 둬서 세로 공간을 아낀다. */
header{display:flex;align-items:baseline;gap:12px;flex-wrap:wrap;margin-bottom:4px}
h1{font-size:17px;margin:0}
header .sum{color:var(--muted);font-size:13px}
header .grow{flex:1}
.hint{color:var(--muted);font-size:12px;margin-bottom:14px}

h2{font-size:12px;margin:0 0 8px;color:var(--muted);font-weight:600;letter-spacing:.06em;
   text-transform:uppercase}
h2 .count{font-weight:400;text-transform:none;letter-spacing:0;margin-left:6px}
section{margin-bottom:16px}
/* 마지막 묶음의 아래 여백은 뺀다 — 왼쪽 칸이 그만큼만 넘쳐서 쓸데없는
   스크롤 막대가 생긴다. */
.cols>.left>section:last-child{margin-bottom:0}

/* 카드 격자 — 13개가 한 화면에 들어오도록 폭을 좁게 잡는다. 칸 수는 고정하지
   않고 들어가는 만큼 채운다. 최소 폭은 이름·포트 한 줄과 단추 셋이 들어갈
   만큼인 190px 으로, 1280px 화면에서도 왼쪽 칸에 넉 줄씩 들어간다. */
.grid{display:grid;gap:9px;grid-template-columns:repeat(auto-fill,minmax(190px,1fr))}
.svc{background:var(--card);border:1px solid var(--line);border-radius:9px;padding:10px 11px 9px}
.svc.on{border-color:var(--upline)}
.svc.work{border-color:var(--workline)}
.svc .top{display:flex;align-items:center;gap:7px}
.dot{width:8px;height:8px;border-radius:99px;background:var(--down);flex:none}
.svc.on .dot{background:var(--up)}
.svc.work .dot{background:var(--work);animation:pulse 1.1s ease-in-out infinite}
@keyframes pulse{50%{opacity:.25}}
.svc .name{font-family:ui-monospace,monospace;font-weight:600;font-size:13px}
.svc .port{margin-left:auto;font-family:ui-monospace,monospace;font-size:12px;color:var(--muted)}
.svc .label{color:var(--muted);font-size:12px;margin:2px 0 4px;
            white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.svc.work .label{color:var(--work)}
/* 프론트 카드의 "브라우저로 열기". 모양은 .ico 를 그대로 쓰고, 이름·포트 줄
   끝에 붙으므로 조금 작게 둔다. 꺼져 있으면 누를 수 없다(.off) —
   안 뜬 주소를 열면 빈 오류 탭만 남는다. */
.svc .open{width:22px;height:22px;font-size:13px;margin-left:4px}
.svc .btns{display:flex;gap:2px;margin-left:-5px}

button{font:inherit;padding:5px 11px;border:1px solid var(--btnline);background:var(--btn);
       color:var(--fg);border-radius:6px;cursor:pointer}
button:hover:not(:disabled){border-color:var(--accent);color:var(--accent)}
button:disabled{opacity:.42;cursor:not-allowed}
.danger:hover:not(:disabled){border-color:#d9534f;color:#d9534f}

/* 아이콘 단추 — 글자 대신 기호 하나만 둔다. 무엇을 하는 단추인지는 title 이
   말해 주고, 되돌릴 수 없는 전체 작업은 누른 뒤 확인 창이 다시 한번 말해 준다.
   기호는 일부러 흔한 것만 골랐다(⟳ ■ ☰ ↗) — 이모지나 최신 기호는 글꼴에
   따라 네모(두부)로 나온다.

   평소에는 테두리도 바탕도 없이 기호만 두고, 마우스를 올렸을 때만 단추 모양이
   나온다. 카드 열셋에 단추 마흔 개가 늘 상자로 깔려 있으면 정작 봐야 할 것
   — 서비스 이름과 켜짐/꺼짐 점 — 이 묻힌다.

   크기는 머리글의 전체 단추와 카드 단추가 같다. 기호 하나가 들어갈 만큼만 잡아,
   글자 단추였을 때처럼 카드 폭을 삼등분해 늘어나지 않는다. */
.ico{display:inline-flex;align-items:center;justify-content:center;flex:none;
     width:28px;height:28px;padding:0;font-size:16px;line-height:1;
     border:1px solid transparent;background:none;color:var(--muted);
     border-radius:7px;cursor:pointer;text-decoration:none;
     font-family:"DejaVu Sans","Noto Sans Symbols2",system-ui,sans-serif;
     transition:background .12s,border-color .12s,color .12s}
.ico:hover:not(:disabled):not(.off){border-color:var(--btnline);background:var(--btn);
                                    color:var(--accent)}
.ico:disabled{opacity:.28;cursor:not-allowed}
.ico.danger:hover:not(:disabled){border-color:#d9534f;background:var(--btn);color:#d9534f}
.ico.off{opacity:.28;pointer-events:none}

/* MFE 는 셸 카드 옆이 아니라 아래 띠로 뺀다. 카드 안에 넣으면 그 카드만
   길어져서 격자가 어긋난다. */
.mfe{margin-top:9px;background:var(--subtle);border:1px solid var(--line);
     border-radius:9px;padding:9px 11px 6px}
.mfe-note{color:var(--muted);font-size:12px;margin-bottom:7px}
.chip{display:inline-block;border:1px solid var(--line);border-radius:6px;
      padding:2px 8px;margin:0 5px 5px 0;font-size:12px;background:var(--card)}
.chip code{color:var(--muted);font-family:ui-monospace,monospace;margin-left:5px}

details{border:1px solid var(--line);border-radius:9px;background:var(--card);overflow:hidden}
/* 펼쳤을 때만 남은 높이를 다 가져간다. 접었으면 제목 줄만큼만 차지한다 —
   안 그러면 빈 상자가 오른쪽 칸을 통째로 덮는다. */
#logbox{display:flex;flex-direction:column;min-height:0;flex:1}
#logbox:not([open]){flex:0 0 auto}
/* 크롬은 details 안쪽을 ::details-content 라는 익명 상자로 한 번 더 감싼다.
   그래서 #log 에 준 flex:1 이 details 에 닿지 않고 로그 칸이 두 줄로 쪼그라든다.
   그 상자에도 똑같이 걸어 줘야 남은 높이를 물려받는다. 이 가짜 선택자가 없는
   브라우저(파이어폭스·사파리)는 감싸는 상자 자체가 없어 규칙 없이도 맞는다. */
#logbox::details-content{display:flex;flex-direction:column;min-height:0;flex:1}
summary{cursor:pointer;padding:8px 12px;font-size:13px;color:var(--muted);user-select:none}
summary::marker{color:var(--muted)}

/* 로그 탭 — 작업이 여럿 동시에 돌므로 어느 것을 보는지 고를 수 있어야 한다. */
.tabs{display:flex;gap:6px;flex-wrap:wrap;padding:0 10px 9px;border-bottom:1px solid var(--line)}
.tabs:empty{display:none}
.tab{border:1px solid var(--btnline);background:var(--btn);color:var(--fg);border-radius:6px;
     padding:3px 9px;font-size:12px;cursor:pointer;display:flex;align-items:center;gap:6px}
.tab.sel{border-color:var(--accent);color:var(--accent)}
.tab .st{width:7px;height:7px;border-radius:99px;background:var(--down);flex:none}
.tab.run .st{background:var(--work);animation:pulse 1.1s ease-in-out infinite}
.tab.fail .st{background:#d9534f}
.tab.done .st{background:var(--up)}
.tab.off .st{background:var(--down)}
.tab .x{color:var(--muted);font-size:13px;line-height:1}
#log{background:var(--logbg);color:var(--logfg);padding:12px;
     font:12px/1.5 ui-monospace,monospace;white-space:pre-wrap;word-break:break-all;
     flex:1;min-height:0;overflow:auto}
@supports not selector(::-webkit-scrollbar){#log{scrollbar-color:#39424c #0b0e11}}
#log::-webkit-scrollbar-track{background:#0b0e11}
#log::-webkit-scrollbar-thumb{background:#39424c}
#log:empty::before{content:"작업을 실행하면 출력이 여기에 표시됩니다.";color:var(--muted)}

/* 좁은 화면에서는 두 칸을 나란히 둘 자리가 없다. 위아래로 포개고 높이 고정도
   푼다 — 칸 안쪽을 따로 굴리는 것은 넓은 화면에서나 쓸모가 있다. */
@media (max-width:1000px){
  body{height:auto;overflow:visible;display:block;padding:16px 14px 20px}
  .wrap{display:block}
  .cols{display:block}
  .cols>.left{overflow:visible;padding-right:0}
  .cols>.right{display:block;margin-top:16px}
  #logbox{display:block}
  #logbox::details-content{display:block;flex:none}
  #log{height:260px;flex:none}
}
</style>
<script>try{var t=localStorage.getItem("devui-theme");
  if(t==="dark"||t==="light")document.documentElement.dataset.theme=t}catch(e){}</script>
</head><body><div class="wrap">

<header>
  <h1>JSini 개발 서버 제어판</h1>
  <span class="sum" id="summary">…</span>
  <span class="grow"></span>
  <button id="theme" class="ico" title="밝기" aria-label="밝기 전환">◐</button>
  <button id="pull" class="ico" title="git pull — 소스 받기"
          aria-label="소스 받기">↓</button>
  <button id="allstop" class="ico danger" title="전체 중지" aria-label="전체 중지">■</button>
  <button id="all" class="ico" title="전체 재기동" aria-label="전체 재기동">⟳</button>
</header>
<div class="hint">backend_run_ubuntu.sh 를 그대로 호출합니다.
  <b>⟳</b> 재기동 · <b>■</b> 중지 · <b>☰</b> 로그 · <b>↗</b> 브라우저로 열기 ·
  <b>↓</b> 소스 받기(git pull) — 단추에 마우스를 올리면 무엇인지 나옵니다.
  서비스는 터미널 창 없이 백그라운드로 뜨고, 서비스마다 작업이 따로 돌기 때문에
  하나를 재기동하는 동안에도 다른 것을 만질 수 있습니다.</div>

<div class="cols">
  <div class="left">
    <section id="sec-back"></section>
    <section id="sec-front"></section>
  </div>
  <div class="right">
    <details id="logbox" open><summary>로그</summary>
      <div class="tabs" id="tabs"></div>
      <div id="log"></div>
    </details>
  </div>
</div>

</div><script>
// view: 지금 보고 있는 로그. {kind:"job",id} 또는 {kind:"svc",key}
let view = null, pos = 0, jobs = [], services = [], svcTabs = [], globalBusy = false;
const logEl = document.getElementById("log");
const logBox = document.getElementById("logbox");
const tabsEl = document.getElementById("tabs");
const esc = s => String(s).replace(/[&<>"]/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;"}[c]));
const sleep = ms => new Promise(r => setTimeout(r, ms));
const viewKey = v => v ? v.kind + ":" + (v.id || v.key) : "";

function card(s) {
  const busy = s.busy || globalBusy;
  const cls = s.busy ? "work" : (s.up ? "on" : "");
  const label = s.busy ? (s.jobTitle || "작업 중…") : s.label;
  // 화면이 있는 것(프론트)만 열기 아이콘을 준다. 백엔드는 열어 봐야 API 라
  // 볼 것이 없고, 아이콘이 있으면 눌러 보게 된다.
  const open = s.url
    ? `<a class="ico open ${s.up ? "" : "off"}" href="${esc(s.url)}" target="_blank" rel="noopener"
         aria-label="브라우저로 열기"
         title="${s.up ? esc(s.url) + " 를 새 탭에서 엽니다" : "기동한 뒤에 열 수 있습니다"}">↗</a>`
    : "";
  return `<div class="svc ${cls}">
    <div class="top"><span class="dot"></span>
      <span class="name">${esc(s.key)}</span><span class="port">${s.port}</span>${open}</div>
    <div class="label" title="${esc(label)}">${esc(label)}</div>
    <div class="btns">
      <button class="ico" data-a="restart" data-s="${esc(s.key)}" ${busy ? "disabled" : ""}
              title="${esc(s.key)} 재기동" aria-label="재기동">⟳</button>
      <button class="ico" data-a="stop" data-s="${esc(s.key)}" ${busy || !s.up ? "disabled" : ""}
              title="${esc(s.key)} 중지" aria-label="중지">■</button>
      <button class="ico" data-a="log" data-s="${esc(s.key)}"
              title="${esc(s.key)} 로그 보기" aria-label="로그 보기">☰</button>
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

// 탭의 점이 뜻하는 것.
//
// 예전에는 작업이 0 으로 끝나면 초록이었다. 그런데 재기동 작업은 `dotnet watch` 를
// **띄우기만 하고** 끝나므로, 서버가 실제로 포트를 열기까지 20~60초가 더 걸린다.
// 그 사이 카드의 점은 회색인데 탭의 점만 초록이라 같은 화면에서 서로 다른 말을
// 했다. 그래서 작업이 끝난 뒤의 점은 **카드와 똑같이 지금 포트가 열려 있는지**로
// 판정한다. 작업 자체의 성패는 실패했을 때(빨강)만 남긴다.
function tabState(key, job) {
  if (job && job.running) return {cls: "run", why: "작업 중"};
  if (job && job.exit !== 0) return {cls: "fail", why: `작업 실패 (종료 코드 ${job.exit})`};
  if (!key) return {cls: "done", why: "작업 완료"};       // all·allstop 은 한 서비스가 아니다
  const s = services.find(x => x.key === key);
  if (!s) return {cls: "", why: ""};
  return s.up ? {cls: "done", why: `${key} 실행 중 (포트 ${s.port})`}
              : {cls: "off", why: `${key} 내려가 있음`};
}

function renderTabs() {
  // 작업 탭은 최근 것이 앞에 오도록 뒤집어 놓는다. 끝난 작업도 로그를 볼 수 있게 남긴다.
  const jt = jobs.slice().reverse().slice(0, 8).map(j => {
    const st = tabState(j.key, j);
    const sel = view && view.kind === "job" && view.id === j.id ? "sel" : "";
    return `<button class="tab ${st.cls} ${sel}" data-t="job" data-v="${esc(j.id)}"
              title="${esc(j.title)} — ${esc(st.why)}">
      <span class="st"></span>${esc(j.title)}</button>`;
  }).join("");
  const sv = svcTabs.map(k => {
    const st = tabState(k, null);
    const sel = view && view.kind === "svc" && view.key === k ? "sel" : "";
    return `<button class="tab ${st.cls} ${sel}" data-t="svc" data-v="${esc(k)}"
              title="${esc(k)} 서비스 로그 — ${esc(st.why)}">
      <span class="st"></span>${esc(k)} 로그<span class="x" data-close="${esc(k)}"
        title="이 탭 닫기">×</span></button>`;
  }).join("");
  tabsEl.innerHTML = jt + sv;
}

function show(v) {
  if (viewKey(v) === viewKey(view)) return;
  view = v; pos = 0; logEl.textContent = "";
  logBox.open = true;
  renderTabs();
}

async function refresh() {
  const r = await fetch("/api/state");
  const d = await r.json();
  services = d.services; jobs = d.jobs; globalBusy = d.globalBusy;

  section(document.getElementById("sec-back"), "백엔드",
          services.filter(s => s.group === "back"), "");
  section(document.getElementById("sec-front"), "프론트",
          services.filter(s => s.group === "front"),
          d.modules.length ? mfeStrip(d.modules) : "");

  const n = services.filter(s => s.up).length;
  const run = jobs.filter(j => j.running).length;
  document.getElementById("summary").textContent =
    `${n}/${services.length} 실행 중` + (run ? ` · 작업 ${run}개 진행 중` : "");

  const anyBusy = run > 0 || globalBusy;
  document.getElementById("all").disabled = anyBusy;
  document.getElementById("allstop").disabled = anyBusy;
  document.getElementById("pull").disabled = anyBusy;

  // 보고 있던 작업 탭이 밀려 사라졌으면 선택을 푼다.
  if (view && view.kind === "job" && !jobs.some(j => j.id === view.id)) {
    view = null; logEl.textContent = "";
  }
  renderTabs();
}

async function pullLog() {
  if (!view) return;
  if (view.kind === "job") {
    const r = await fetch("/api/log?job=" + encodeURIComponent(view.id) + "&offset=" + pos);
    const d = await r.json();
    if (d.lines && d.lines.length) { append(d.lines.join("\\n") + "\\n"); pos = d.offset; }
  } else {
    const r = await fetch("/api/svclog?svc=" + encodeURIComponent(view.key) + "&pos=" + pos);
    const d = await r.json();
    if (!d.exists && pos === 0 && !logEl.textContent) {
      logEl.textContent = "아직 로그가 없습니다 — 이 서비스를 제어판에서 한 번 기동하면 생깁니다.";
      return;
    }
    if (d.text) { append(d.text); }
    pos = d.pos;
  }
}

function append(text) {
  const stick = logEl.scrollTop + logEl.clientHeight >= logEl.scrollHeight - 30;
  logEl.textContent += text;
  if (stick) logEl.scrollTop = logEl.scrollHeight;
}

async function run(action, svc, confirmMsg) {
  if (confirmMsg && !confirm(confirmMsg)) return;
  const r = await fetch("/api/run", {
    method: "POST", headers: {"Content-Type": "application/json"},
    body: JSON.stringify({action, svc})
  });
  const d = await r.json();
  if (d.error) { alert(d.error); return; }
  show({kind: "job", id: d.job});
  refresh();
}

document.querySelector(".wrap").addEventListener("click", e => {
  const close = e.target.closest("[data-close]");
  if (close) {
    e.stopPropagation();
    const k = close.dataset.close;
    svcTabs = svcTabs.filter(x => x !== k);
    if (view && view.kind === "svc" && view.key === k) { view = null; logEl.textContent = ""; }
    renderTabs();
    return;
  }
  const tab = e.target.closest(".tab");
  if (tab) {
    show(tab.dataset.t === "job" ? {kind: "job", id: tab.dataset.v}
                                 : {kind: "svc", key: tab.dataset.v});
    pullLog();
    return;
  }
  const b = e.target.closest("button[data-a]");
  if (!b || b.disabled) return;
  if (b.dataset.a === "log") {
    if (!svcTabs.includes(b.dataset.s)) svcTabs.push(b.dataset.s);
    show({kind: "svc", key: b.dataset.s});
    pullLog();
  } else {
    run(b.dataset.a, b.dataset.s);
  }
});
// 밝기 — 시스템 → 어두움 → 밝음 을 돌아가며 고른다. 시스템은 OS 설정을 그대로
// 따르고(설정을 바꾸면 화면도 따라 바뀐다), 나머지 둘은 거기에 상관없이 고정한다.
// 단추의 기호는 "지금 무엇인지"를 가리킨다. 고른 값은 localStorage 에 남는다.
const THEMES = [
  {id: "system", icon: "\u25d0", label: "시스템 설정"},
  {id: "dark",   icon: "\u263e", label: "어두움"},
  {id: "light",  icon: "\u2600", label: "밝음"},
];
const themeBtn = document.getElementById("theme");
const darkQuery = matchMedia("(prefers-color-scheme:dark)");
function themeMode() {
  let t = null;
  try { t = localStorage.getItem("devui-theme"); } catch (e) {}
  return THEMES.find(x => x.id === t) || THEMES[0];
}
function paintTheme() {
  const cur = themeMode();
  const next = THEMES[(THEMES.indexOf(cur) + 1) % THEMES.length];
  const dark = cur.id === "system" ? darkQuery.matches : cur.id === "dark";
  const applied = dark ? "dark" : "light";
  const title = `밝기: ${cur.label} — 눌러 ${next.label}(으)로`;
  // 같은 값이면 쓰지 않는다. 괜히 쓰면 브라우저가 화면을 다시 그린다.
  if (document.documentElement.dataset.theme !== applied)
    document.documentElement.dataset.theme = applied;
  if (themeBtn.textContent !== cur.icon) themeBtn.textContent = cur.icon;
  if (themeBtn.title !== title) themeBtn.title = title;
}
themeBtn.onclick = () => {
  const next = THEMES[(THEMES.indexOf(themeMode()) + 1) % THEMES.length];
  try { localStorage.setItem("devui-theme", next.id); } catch (e) {}
  paintTheme();
};
darkQuery.addEventListener("change", paintTheme);
// 탭이 가려져 있는 동안에는 아래 폴링 바퀴가 쉬므로, 돌아온 그 순간에 한 번 맞춘다.
document.addEventListener("visibilitychange", () => { if (!document.hidden) paintTheme(); });
paintTheme();

document.getElementById("pull").onclick =
  () => run("pull", null,
            "git pull --ff-only 로 소스를 받습니다.\\n\\n" +
            "갈라져 있거나 고치다 만 파일이 있으면 받지 않고 그대로 멈춥니다. " +
            "계속할까요?");
document.getElementById("allstop").onclick =
  () => run("allstop", null, "백엔드와 프론트를 전부 내립니다. 계속할까요?");
document.getElementById("all").onclick =
  () => run("all", null, "전체를 중지하고 다시 빌드합니다. 몇 분 걸립니다. 계속할까요?");

// 한 바퀴에 상태와 로그를 함께 받아 온다. 돌고 있는 작업이 있으면 더 자주 돈다.
// 탭이 보이지 않을 때는 쉰다 (노트북 배터리) — 다만 첫 바퀴는 무조건 돈다.
// 배경 탭으로 열어 두면 화면이 빈 채로 남기 때문이다.
(async function loop() {
  for (let first = true; ; first = false) {
    if (first || !document.hidden) {
      // 시스템 설정을 따르는 중이면 바뀐 것이 없는지 여기서도 본다.
      // matchMedia 의 change 만 믿지 않는 까닭 — 못 받는 브라우저가 있다.
      paintTheme();
      try { await refresh(); await pullLog(); } catch (e) { /* 서버가 잠깐 없을 수 있다 */ }
    }
    await sleep(jobs.some(j => j.running) ? 800 : 2500);
  }
})();
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

    def _query(self):
        return parse_qs(urlparse(self.path).query)

    def do_GET(self):
        path = urlparse(self.path).path
        if path == "/":
            self._send(200, PAGE, "text/html; charset=utf-8")
        elif path == "/api/state":
            open_ports = listening_ports()
            jobs = RUNNER.state()
            # 서비스마다 "지금 이 서비스를 건드리는 작업" 을 붙여 준다.
            busy_of = {j["key"]: j["title"] for j in jobs if j["running"] and j["key"]}
            global_busy = any(j["running"] and j["key"] is None for j in jobs)
            self._json({
                "services": [{**{k: s[k] for k in ("key", "label", "port", "group")},
                              "up": s["port"] in open_ports,
                              "busy": s["key"] in busy_of or global_busy,
                              "jobTitle": busy_of.get(s["key"]),
                              # 화면이 있는 것만 주소를 준다 (프론트). 백엔드는 API 라 열 것이 없다.
                              "url": f"http://localhost:{s['port']}" if s["group"] == "front" else None}
                             for s in SERVICES],
                "modules": MODULES,
                "moduleHost": MODULE_HOST,
                "jobs": jobs,
                "globalBusy": global_busy,
            })
        elif path == "/api/log":
            q = self._query()
            job = RUNNER.get(q.get("job", [""])[0])
            if not job:
                self._json({"lines": [], "offset": 0, "running": False})
                return
            try:
                offset = int(q.get("offset", ["0"])[0])
            except ValueError:
                offset = 0
            lines, total = job.snapshot(offset)
            self._json({"lines": lines, "offset": total,
                        "running": job.running, "exit": job.exit})
        elif path == "/api/svclog":
            q = self._query()
            key = q.get("svc", [""])[0]
            if key not in BY_KEY:
                self._json({"error": f"알 수 없는 서비스: {key}"}, 400)
                return
            try:
                pos = int(q.get("pos", ["0"])[0])
            except ValueError:
                pos = 0
            text, nxt, exists = read_tail(LOG_DIR / f"{key}.log", pos)
            self._json({"text": text, "pos": nxt, "exists": exists})
        else:
            self._send(404, "not found", "text/plain; charset=utf-8")

    def do_POST(self):
        if urlparse(self.path).path != "/api/run":
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
            key, title, args = svc, f"{svc} 재기동", [svc]
        elif action == "stop":
            key, title, args = svc, f"{svc} 중지", ["stop", svc]
        elif action == "allstop":
            key, title, args = None, "전체 중지", ["allstop"]
        elif action == "all":
            key, title, args = None, "전체 재기동", ["all"]
        elif action == "pull":
            # 전체 작업으로 둔다(key=None). 소스를 갈아 끼우는 일이라 어느 서비스
            # 하나의 일이 아니고, 빌드가 도는 중에 파일이 바뀌면 그 빌드가 무엇을
            # 만든 것인지 알 수 없게 된다.
            #
            # --ff-only 인 까닭: 단추 한 번에 머지 커밋이 생기는 것보다, 갈라져
            # 있으면 그냥 거절당하고 터미널에서 손으로 푸는 편이 낫다.
            key, title, args = None, "소스 받기", []
            return self._start(key, title, args,
                               cmd=["bash", "-c",
                                    'echo "브랜치: $(git rev-parse --abbrev-ref HEAD)"; '
                                    "echo; git pull --ff-only"],
                               shown="git pull --ff-only")
        else:
            self._json({"error": f"알 수 없는 동작: {action}"}, 400)
            return

        self._start(key, title, args)

    def _start(self, key, title, args, cmd=None, shown=None):
        job, err = RUNNER.start(key, title, args, cmd, shown)
        if err:
            self._json({"error": err}, 409)
        else:
            self._json({"job": job.id, "title": job.title})


def main():
    if not os.access(SCRIPT, os.X_OK):
        sys.exit(f"{SCRIPT} 를 실행할 수 없습니다.")
    srv = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    print(f"JSini 개발 서버 제어판  →  http://127.0.0.1:{PORT}", flush=True)
    print(f"  서비스 {len(SERVICES)}개를 {SCRIPT.name} 에서 읽었습니다.")
    print(f"  서비스는 창 없이 백그라운드로 띄웁니다 — 출력은 logs/<이름>.log.")
    print("  Ctrl+C 로 종료합니다.")
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        print("\n종료합니다.")


if __name__ == "__main__":
    main()
