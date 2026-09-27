#!/bin/bash
# JSini 개발 서버 제어판을 띄우고 브라우저를 연다.
#
#   ./devui.sh              http://127.0.0.1:5600 으로 연다
#   DEVUI_PORT=5700 ./devui.sh   다른 포트로 연다
#
# 이 스크립트는 반드시 데스크톱 세션 안에서 실행해야 한다.
# backend_run_ubuntu.sh 가 서비스마다 gnome-terminal 을 띄우므로,
# DISPLAY 가 없으면 기동이 조용히 실패한다.

# 어느 디렉터리에서 부르든 프로젝트 루트에서 돈다.
cd "$(dirname "$0")" || exit 1

PORT="${DEVUI_PORT:-5600}"

if ss -ltn 2>/dev/null | grep -q ":$PORT "; then
    echo "제어판이 이미 :$PORT 에서 돌고 있습니다. 브라우저만 엽니다."
    xdg-open "http://127.0.0.1:$PORT" >/dev/null 2>&1
    exit 0
fi

# 서버가 응답하면 브라우저를 연다.
( until ss -ltn 2>/dev/null | grep -q ":$PORT "; do sleep 0.2; done
  xdg-open "http://127.0.0.1:$PORT" >/dev/null 2>&1 ) &

# 서버는 이 터미널에 붙잡아 둔다 — Ctrl+C 로 끄는 것이 자연스럽다.
exec python3 scripts/devui/server.py
