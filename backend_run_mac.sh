#!/bin/bash

# 프로젝트 루트 경로 설정
ROOT_DIR=$(pwd)
FRONTEND_DIR="$ROOT_DIR/fronts"
GATEWAY_DIR="$ROOT_DIR/ApiGateway"
AUTH_SERVER_DIR="$ROOT_DIR/microservices/AuthServer"
MICROSERVICE_DIR="$ROOT_DIR/microservices/funeralv2Api"
AI_AGENT_DIR="$ROOT_DIR/microservices/AIAgentServer"
FILE_SERVER_DIR="$ROOT_DIR/microservices/FileServer"

echo "===================================================="
echo "   Funeral V2 시스템 빌드 및 시작 (MS Architecture)"
echo "===================================================="

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

echo "4. File Server 빌드..."
if ! (cd "$FILE_SERVER_DIR" && dotnet build); then
    echo "❌ File Server 빌드 실패! 실행을 중단합니다."
    exit 1
fi

echo "5. API Gateway 빌드..."
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


run_tab() {
  osascript <<EOF
tell application "iTerm"
    activate
    
    if (count of windows) = 0 then
        create window with default profile
        tell current session of current window
            write text "cd $1 && DOTNET_WATCH_HOT_RELOAD=0 dotnet watch run --no-hot-reload"
        end tell
    else
        tell current window
            create tab with default profile
            tell current session
                write text "cd $1 && DOTNET_WATCH_HOT_RELOAD=0 dotnet watch run --no-hot-reload"
            end tell
        end tell
    end if
end tell
EOF
}

run_tab $MICROSERVICE_DIR
run_tab $AUTH_SERVER_DIR
run_tab $AI_AGENT_DIR
run_tab $FILE_SERVER_DIR
run_tab $GATEWAY_DIR


# cd "$AUTH_SERVER_DIR" && dotnet run --no-build &
# sleep 1

# cd "$MICROSERVICE_DIR" && dotnet run --no-build &
# sleep 1

# cd "$GATEWAY_DIR" && dotnet run --no-build &
# sleep 1

# [단계 3] 프론트엔드 실행

run_iterm() {
  osascript <<EOF
tell application "iTerm"
    create window with default profile
    tell current session of current window
        write text "cd $1 && pnpm dev; exec bash"
    end tell
end tell
EOF
}


run_tab2() {
  osascript <<EOF
tell application "iTerm"
    activate
    
    if (count of windows) = 0 then
        create window with default profile
        tell current session of current window
            write text "cd $1 && pnpm dev; exec bash"
        end tell
    else
        tell current window
            create tab with default profile
            tell current session
                write text "cd $1 && pnpm dev; exec bash"
            end tell
        end tell
    end if
end tell
EOF
}


run_tab2 "$FRONTEND_DIR"
#run_iterm "$FRONTEND_DIR"

#echo ">>> [3/3] 프론트엔드 (Vben Admin) 실행 중..."
#cd "$FRONTEND_DIR" && pnpm dev

# wait
