#!/bin/bash

# 프로젝트 루트 경로 설정
ROOT_DIR=$(pwd)
FRONTEND_DIR="$ROOT_DIR/fronts"
GATEWAY_DIR="$ROOT_DIR/ApiGateway"
AUTH_SERVER_DIR="$ROOT_DIR/microservices/AuthServer"
MICROSERVICE_DIR="$ROOT_DIR/microservices/funeralv2Api"
AI_AGENT_DIR="$ROOT_DIR/microservices/AIAgentServer"

echo "===================================================="
echo "   Funeral V2 시스템 빌드 및 시작 (MS Architecture)"
echo "===================================================="

# 종료 시 모든 프로세스 정리 함수
cleanup() {
   echo ""
   echo ">>> 모든 서비스를 종료하는 중입니다..."
   kill 0
} 
# trap cleanup EXIT

# [단계 1] 빌드 확인 (Build Phase)
echo ">>> [1/3] 백엔드 서비스 빌드 중..."

echo "1. Auth Server 빌드..."
if ! (cd "$AUTH_SERVER_DIR" && dotnet build); then
    echo "❌ Auth Server 빌드 실패! 실행을 중단합니다."
    exit 1
fi

echo "2. Microservice 빌드..."
if ! (cd "$MICROSERVICE_DIR" && dotnet build); then
    echo "❌ Microservice 빌드 실패! 실행을 중단합니다."
    exit 1
fi

echo "3. AI Agent Server 빌드..."
if ! (cd "$AI_AGENT_DIR" && dotnet build); then
    echo "❌ AI Agent Server 빌드 실패! 실행을 중단합니다."
    exit 1
fi

echo "4. API Gateway 빌드..."
if ! (cd "$GATEWAY_DIR" && dotnet build); then
    echo "❌ API Gateway 빌드 실패! 실행을 중단합니다."
    exit 1
fi

echo "5. 프론트엔드 빌드..."
cd "$ROOT_DIR"


cd "$FRONTEND_DIR" && pnpm install


echo "✅ 모든 백엔드 빌드 성공!"

# [단계 2] 백엔드 서비스 실행 (Execution Phase)
echo ">>> [2/3] 서비스 실행 중..."

cd "$ROOT_DIR"

TERMINAL=$(readlink -f /etc/alternatives/x-terminal-emulator)

echo "$TERMINAL" ">>> 으로 새창 실행..."


xdg-terminal-exec bash -c "trap ':' INT; cd $AUTH_SERVER_DIR && SERVER_NAME=AUTH DOTNET_WATCH_HOT_RELOAD=0 dotnet watch run --no-hot-reload; exec bash"
xdg-terminal-exec bash -c "trap ':' INT; cd $MICROSERVICE_DIR && SERVER_NAME=FUNERALV2 DOTNET_WATCH_HOT_RELOAD=0 dotnet watch run --no-hot-reload; exec bash"
xdg-terminal-exec bash -c "trap ':' INT; cd $AI_AGENT_DIR && SERVER_NAME=AI_AGENT DOTNET_WATCH_HOT_RELOAD=0 dotnet watch run --no-hot-reload; exec bash"
xdg-terminal-exec bash -c "trap ':' INT; cd $GATEWAY_DIR && SERVER_NAME=GATEWAY DOTNET_WATCH_HOT_RELOAD=0 dotnet watch run --no-hot-reload; exec bash"



# cd "$AUTH_SERVER_DIR" && dotnet run --no-build &
# sleep 1

# cd "$MICROSERVICE_DIR" && dotnet run --no-build &
# sleep 1

# cd "$GATEWAY_DIR" && dotnet run --no-build &
# sleep 1

# [단계 3] 프론트엔드 실행

xdg-terminal-exec bash -c "trap ':' INT; cd $FRONTEND_DIR && pnpm dev; exec bash"


#echo ">>> [3/3] 프론트엔드 (Vben Admin) 실행 중..."
#cd "$FRONTEND_DIR" && pnpm dev

# wait
