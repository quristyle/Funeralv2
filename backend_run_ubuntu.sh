

#!/bin/bash

#############################################
# 프로젝트 루트 경로 (스크립트 위치 기준)
#############################################
ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"

FRONTEND_DIR="$ROOT_DIR/fronts"
GATEWAY_DIR="$ROOT_DIR/ApiGateway"
AUTH_SERVER_DIR="$ROOT_DIR/microservices/AuthServer"
MICROSERVICE_DIR="$ROOT_DIR/microservices/funeralv2Api"
AI_AGENT_DIR="$ROOT_DIR/microservices/AIAgentServer"
FILE_SERVER_DIR="$ROOT_DIR/microservices/FileServer"

#############################################
# 시스템에서 사용 가능한 터미널 자동 선택
#############################################
run_terminal() {
    local cmd="$1"

    if command -v xdg-terminal-exec >/dev/null 2>&1; then
        xdg-terminal-exec bash -lc "$cmd"
        return
    fi

    if command -v gnome-terminal >/dev/null 2>&1; then
        gnome-terminal -- bash -lc "$cmd"
        return
    fi

    if command -v ptyxis >/dev/null 2>&1; then
        ptyxis -- bash -lc "$cmd"
        return
    fi

    if command -v kgx >/dev/null 2>&1; then
        kgx bash -lc "$cmd"
        return
    fi

    if command -v konsole >/dev/null 2>&1; then
        konsole -e bash -lc "$cmd"
        return
    fi

    if command -v xfce4-terminal >/dev/null 2>&1; then
        xfce4-terminal --command="bash -lc '$cmd'"
        return
    fi

    if command -v mate-terminal >/dev/null 2>&1; then
        mate-terminal -- bash -lc "$cmd"
        return
    fi

    if command -v qterminal >/dev/null 2>&1; then
        qterminal -e "bash -lc '$cmd'"
        return
    fi

    if command -v lxterminal >/dev/null 2>&1; then
        lxterminal -e "bash -lc '$cmd'"
        return
    fi

    if command -v xterm >/dev/null 2>&1; then
        xterm -e "bash -lc '$cmd'"
        return
    fi

    echo "❌ 실행 가능한 터미널을 찾을 수 없습니다."
    exit 1
}

#############################################
# DotNet 서비스 실행
#############################################
start_dotnet_service() {
    local name="$1"
    local dir="$2"

    run_terminal "cd \"$dir\" && SERVER_NAME=$name DOTNET_WATCH_HOT_RELOAD=0 dotnet watch run --no-hot-reload; exec bash" &
}

#############################################

echo "===================================================="
echo "   Funeral V2 시스템 빌드 및 시작 (MS Architecture)"
echo "===================================================="

#############################################
# [0/4] 기존 프로세스 종료
#############################################

echo ">>> [0/4] 기존에 실행 중인 개발 서버를 종료합니다..."

pkill -f "dotnet watch run" \
    && echo "기존 백엔드 프로세스를 종료했습니다." \
    || echo "실행 중인 백엔드 프로세스가 없습니다."

pkill -f "pnpm dev" \
    && echo "기존 프론트엔드 프로세스를 종료했습니다." \
    || echo "실행 중인 프론트엔드 프로세스가 없습니다."

sleep 1

echo "✅ 기존 프로세스 정리 완료."

cleanup() {
    echo ""
    echo ">>> 모든 서비스를 종료하는 중입니다..."
    kill 0
}

#trap cleanup EXIT

#############################################
# [1/4] 빌드
#############################################

echo ">>> [1/4] 백엔드 서비스 빌드 중..."

echo "1. Auth Server 빌드..."
if ! (cd "$AUTH_SERVER_DIR" && dotnet build); then
    exit 1
fi

echo "2. Microservice 빌드..."
if ! (cd "$MICROSERVICE_DIR" && dotnet build); then
    exit 1
fi

echo "3. AI Agent Server 빌드..."
if ! (cd "$AI_AGENT_DIR" && dotnet build); then
    exit 1
fi

echo "4. File Server 빌드..."
if ! (cd "$FILE_SERVER_DIR" && dotnet build); then
    exit 1
fi

echo "5. API Gateway 빌드..."
if ! (cd "$GATEWAY_DIR" && dotnet build); then
    exit 1
fi

echo "6. Frontend 의존성 설치..."

cd "$FRONTEND_DIR" || exit 1
pnpm install

echo "✅ 모든 서비스 빌드 성공!"

#############################################
# [2/4] 서비스 실행
#############################################

echo ">>> [2/4] 서비스 실행 중..."

start_dotnet_service GATEWAY "$GATEWAY_DIR"

start_dotnet_service AUTH "$AUTH_SERVER_DIR"

start_dotnet_service FUNERALV2 "$MICROSERVICE_DIR"

start_dotnet_service AI_AGENT "$AI_AGENT_DIR"

start_dotnet_service FILE_API "$FILE_SERVER_DIR"

#############################################
# Frontend 실행
#############################################

NVM_INIT_COMMAND='export NVM_DIR="$HOME/.nvm"; [ -s "$NVM_DIR/nvm.sh" ] && . "$NVM_DIR/nvm.sh";'

run_terminal "trap ':' INT; ${NVM_INIT_COMMAND} cd \"$FRONTEND_DIR\" && pnpm dev" &

#############################################

echo ">>> [3/4] Frontend 실행 완료"

echo ""
echo "===================================================="
echo "모든 서비스가 시작되었습니다."
echo "===================================================="

# wait