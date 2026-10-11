#!/bin/bash
# JSini 개발 서버 제어판을 백그라운드로 띄우고 브라우저를 연다.
#
#   ./devui.sh                   http://127.0.0.1:5600 으로 연다
#   ./devui_stop.sh              끈다
#   DEVUI_PORT=5700 ./devui.sh   다른 포트로 연다
#
# 제어판은 이 터미널을 붙잡지 않는다. setsid 로 떼어 내므로 부른 터미널을 닫아도
# 계속 돌고, 출력은 logs/devui.log 에 쌓인다. 끄는 것은 Ctrl+C 가 아니라
# ./devui_stop.sh 다.
#
# 띄우는 브라우저도 마찬가지로 떼어 낸다 — 터미널을 닫아도 창이 함께 닫히지
# 않는다(open_browser 주석 참고).
#
# 제어판에서 띄운 서비스도 터미널 창 없이 백그라운드로 돈다(DEV_BACKGROUND=1).
# 각 서비스의 출력은 logs/<이름>.log 에 쌓이고, 화면의 ☰ 단추로 본다.
# 그래서 DISPLAY 없이(SSH 등) 띄워 두고 다른 기기에서 봐도 된다 —
# 다만 브라우저를 여는 부분만 조용히 넘어간다.

# 어느 디렉터리에서 부르든 프로젝트 루트에서 돈다.
cd "$(dirname "$0")" || exit 1

PORT="${DEVUI_PORT:-5600}"
LOG="logs/devui.log"
PIDFILE=".devui.pid"

# 브라우저도 이 터미널에서 떼어 낸다.
#
# 제어판 자신은 setsid 로 떼어 두었는데 브라우저는 그렇지 않았다. xdg-open 이
# 브라우저를 **처음** 띄우는 경우 그 브라우저는 이 셸의 자식으로 남고, 터미널을
# 닫을 때 프로세스 그룹에 날아가는 SIGHUP 을 같이 맞는다 — 제어판은 멀쩡히
# 도는데 창만 사라지던 까닭이다. 이미 떠 있는 브라우저에 새 탭만 여는 경우에는
# 안 겪으므로 "될 때도 있고 안 될 때도 있는" 것처럼 보였다.
#
# setsid 로 세션을 가르고 nohup 으로 한 번 더 막는다. 표준 입출력을 모두
# 끊어야 터미널이 사라진 뒤 쓰기에서 걸리지 않는다.
open_browser() {
    command -v xdg-open >/dev/null 2>&1 || return 0
    # 화면이 없는 자리(SSH 등)에서는 조용히 넘어간다.
    [ -n "$DISPLAY$WAYLAND_DISPLAY" ] || return 0

    if command -v setsid >/dev/null 2>&1; then
        setsid nohup xdg-open "http://127.0.0.1:$PORT" >/dev/null 2>&1 </dev/null &
    else
        nohup xdg-open "http://127.0.0.1:$PORT" >/dev/null 2>&1 </dev/null &
    fi
}

if ss -ltn 2>/dev/null | grep -q ":$PORT "; then
    echo "제어판이 이미 :$PORT 에서 돌고 있습니다. 브라우저만 엽니다."
    open_browser
    exit 0
fi

mkdir -p logs

# setsid 로 세션을 떼어 낸다. 이렇게 해야 이 터미널을 닫아도(SIGHUP) 제어판이
# 살아 남는다. </dev/null 이 없으면 백그라운드에서 입력을 읽으려다 멈춘다.
setsid python3 scripts/devui/server.py >"$LOG" 2>&1 </dev/null &
echo $! > "$PIDFILE"

# 포트가 열릴 때까지 기다린다. 열리지 않으면 로그를 보여 주고 실패로 끝낸다 —
# 예전처럼 브라우저만 열어 두면 빈 오류 화면의 까닭을 알 길이 없다.
for _ in $(seq 1 100); do
    ss -ltn 2>/dev/null | grep -q ":$PORT " && break
    sleep 0.1
done

if ! ss -ltn 2>/dev/null | grep -q ":$PORT "; then
    echo "❌ 제어판이 :$PORT 에서 뜨지 않았습니다. $LOG 를 보세요:"
    echo
    tail -20 "$LOG" 2>/dev/null | sed 's/^/   /'
    rm -f "$PIDFILE"
    exit 1
fi

# 실제로 포트를 쥔 프로세스를 PID 로 적어 둔다. setsid 가 한 번 더 fork 하는
# 경우가 있어 $! 가 어긋날 수 있는데, 포트로 찾은 것은 어긋나지 않는다.
real="$(ss -ltnp 2>/dev/null | grep ":$PORT " | grep -oP 'pid=\K[0-9]+' | head -1)"
[ -n "$real" ] && echo "$real" > "$PIDFILE"

echo "JSini 개발 서버 제어판  →  http://127.0.0.1:$PORT  (pid $(cat "$PIDFILE"))"
echo "  로그: $LOG"
echo "  끄기: ./devui_stop.sh"
open_browser
