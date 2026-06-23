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

# [단계 0] 기존 프로세스 정리
echo ">>> [0/4] 기존에 실행 중인 개발 서버를 종료합니다..."
# 'dotnet watch run'을 포함하는 모든 백엔드 프로세스를 종료합니다.
# 프로세스가 종료되면 해당 터미널 창도 자동으로 닫힙니다.
pkill -f "dotnet watch run" && echo "기존 백엔드 프로세스를 종료했습니다." || echo "실행 중인 백엔드 프로세스가 없습니다."
# 'pnpm dev'를 포함하는 프론트엔드 프로세스를 종료합니다.
pkill -f "pnpm dev" && echo "기존 프론트엔드 프로세스를 종료했습니다." || echo "실행 중인 프론트엔드 프로세스가 없습니다."
sleep 1 # 프로세스가 완전히 종료될 시간을 잠시 줍니다.
echo "✅ 기존 프로세스 정리 완료."

# 종료 시 모든 프로세스 정리 함수
cleanup() {
   echo ""
   echo ">>> 모든 서비스를 종료하는 중입니다..."
   kill 0
} 
# trap cleanup EXIT

# [단계 1] 빌드 확인 (Build Phase) - 단계 번호 수정
echo ">>> [1/4] 백엔드 서비스 빌드 중..."

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

# [단계 2] 백엔드 서비스 실행 (Execution Phase) - 단계 번호 수정
echo ">>> [2/4] 서비스 실행 중..."

cd "$ROOT_DIR"

# 범용적인 xdg-terminal-exec를 사용하여 시스템 기본 터미널에서 서비스를 실행합니다.
# 명령어 마지막의 '; exec bash'를 제거하여, 프로세스 종료 시 터미널 창이 자동으로 닫히도록 합니다.
# 각 명령 끝에 '&'를 추가하여 백그라운드에서 실행하고, 스크립트가 다음 명령으로 즉시 진행하도록 합니다.
xdg-terminal-exec bash -c "cd $AUTH_SERVER_DIR && SERVER_NAME=AUTH DOTNET_WATCH_HOT_RELOAD=0 dotnet watch run --no-hot-reload" &
xdg-terminal-exec bash -c "cd $MICROSERVICE_DIR && SERVER_NAME=FUNERALV2 DOTNET_WATCH_HOT_RELOAD=0 dotnet watch run --no-hot-reload" &
xdg-terminal-exec bash -c "cd $AI_AGENT_DIR && SERVER_NAME=AI_AGENT DOTNET_WATCH_HOT_RELOAD=0 dotnet watch run --no-hot-reload" &
xdg-terminal-exec bash -c "cd $GATEWAY_DIR && SERVER_NAME=GATEWAY DOTNET_WATCH_HOT_RELOAD=0 dotnet watch run --no-hot-reload" &

# [단계 3] 프론트엔드 실행 - 단계 번호 수정
echo ">>> [3/4] 프론트엔드 (Vben Admin) 실행 중..."

# cd "$AUTH_SERVER_DIR" && dotnet run --no-build &
# sleep 1

# cd "$MICROSERVICE_DIR" && dotnet run --no-build &
# sleep 1

# cd "$GATEWAY_DIR" && dotnet run --no-build &
# sleep 1

# [단계 4] 프론트엔드 실행

# nvm 환경을 로드하고 프론트엔드를 실행합니다.
# 새로운 터미널(non-login, non-interactive shell)은 .bashrc를 자동으로 로드하지 않으므로,
# nvm 스크립트를 수동으로 source하여 node와 pnpm 경로를 설정해줍니다.
NVM_INIT_COMMAND="export NVM_DIR=\"\$HOME/.nvm\"; [ -s \"\$NVM_DIR/nvm.sh\" ] && \. \"\$NVM_DIR/nvm.sh\";"
xdg-terminal-exec bash -c "trap ':' INT; ${NVM_INIT_COMMAND} cd $FRONTEND_DIR && pnpm dev" &


#echo ">>> [3/3] 프론트엔드 (Vben Admin) 실행 중..."
#cd "$FRONTEND_DIR" && pnpm dev

# wait
