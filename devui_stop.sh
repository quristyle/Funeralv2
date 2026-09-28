#!/bin/bash
# JSini 개발 서버 제어판(./devui.sh 로 띄운 것)을 끈다.
#
#   ./devui_stop.sh                   :5600 의 제어판을 끈다
#   DEVUI_PORT=5700 ./devui_stop.sh   다른 포트의 것을 끈다
#
# 제어판만 끈다. 제어판이 띄운 개발 서버들은 setsid 로 떼어져 있어 그대로 돈다 —
# 그것까지 내리려면 ./backend_run_ubuntu.sh allstop 이다.

cd "$(dirname "$0")" || exit 1

PORT="${DEVUI_PORT:-5600}"
PIDFILE=".devui.pid"

# 포트를 쥔 프로세스가 제일 믿을 만하다. PID 파일은 손으로 띄웠거나 파일이
# 어긋났을 때를 위한 보루다.
pid="$(ss -ltnp 2>/dev/null | grep ":$PORT " | grep -oP 'pid=\K[0-9]+' | head -1)"

if [ -z "$pid" ] && [ -f "$PIDFILE" ]; then
    p="$(cat "$PIDFILE" 2>/dev/null)"
    if [ -n "$p" ] && kill -0 "$p" 2>/dev/null; then
        pid="$p"
    fi
fi

if [ -z "$pid" ]; then
    echo "제어판이 :$PORT 에서 돌고 있지 않습니다."
    rm -f "$PIDFILE"
    exit 0
fi

kill "$pid" 2>/dev/null

# 포트가 풀렸는지로 판정한다. 프로세스가 사라져도 포트가 물려 있으면
# 다음 ./devui.sh 가 "이미 돌고 있습니다" 로 잘못 빠진다.
for _ in $(seq 1 50); do
    ss -ltn 2>/dev/null | grep -q ":$PORT " || break
    sleep 0.1
done

if ss -ltn 2>/dev/null | grep -q ":$PORT "; then
    kill -9 "$pid" 2>/dev/null
    sleep 1
fi

rm -f "$PIDFILE"

if ss -ltn 2>/dev/null | grep -q ":$PORT "; then
    echo "❌ :$PORT 가 아직 열려 있습니다 (pid $pid)."
    exit 1
fi

echo "제어판(:$PORT, pid $pid)을 껐습니다."
echo "  제어판이 띄운 개발 서버들은 그대로 돕니다 — 내리려면 ./backend_run_ubuntu.sh allstop"
