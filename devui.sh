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
# 제어판에서 띄운 서비스도 터미널 창 없이 백그라운드로 돈다(DEV_BACKGROUND=1).
# 각 서비스의 출력은 logs/<이름>.log 에 쌓이고, 화면의 ☰ 단추로 본다.
# 그래서 DISPLAY 없이(SSH 등) 띄워 두고 다른 기기에서 봐도 된다 —
# 다만 브라우저를 여는 부분만 조용히 넘어간다.

# 어느 디렉터리에서 부르든 프로젝트 루트에서 돈다.
cd "$(dirname "$0")" || exit 1

PORT="${DEVUI_PORT:-5600}"
LOG="logs/devui.log"
PIDFILE=".devui.pid"

open_browser() {
    command -v xdg-open >/dev/null 2>&1 || return 0
    xdg-open "http://127.0.0.1:$PORT" >/dev/null 2>&1 &
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
