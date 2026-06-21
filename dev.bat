@echo off
chcp 65001 > nul
setlocal enabledelayedexpansion

:: 프로젝트 루트 경로 설정 (스크립트가 위치한 디렉토리)
set "ROOT_DIR=%~dp0"
set "FRONTEND_DIR=%ROOT_DIR%fronts"
set "GATEWAY_DIR=%ROOT_DIR%ApiGateway"
set "AUTH_SERVER_DIR=%ROOT_DIR%microservices\AuthServer"
set "MICROSERVICE_DIR=%ROOT_DIR%microservices\funeralv2Api"
set "AI_AGENT_DIR=%ROOT_DIR%microservices\AIAgentServer"

echo ====================================================
echo    Funeral V2 시스템 초기화 및 시작 (MS Architecture)
echo ====================================================

:: [단계 0] 기존 실행 중인 서비스 종료 (Cleanup)
:: taskkill을 사용하여 이전에 열린 서버 창들을 종료합니다.
echo ^> [0/3] 기존 실행 중인 서비스 종료 중...

taskkill /F /FI "WINDOWTITLE eq Auth Server*" /T > nul 2>&1
taskkill /F /FI "WINDOWTITLE eq Microservice*" /T > nul 2>&1
taskkill /F /FI "WINDOWTITLE eq API Gateway*" /T > nul 2>&1
taskkill /F /FI "WINDOWTITLE eq AI Agent Server*" /T > nul 2>&1
taskkill /F /FI "WINDOWTITLE eq Frontend*" /T > nul 2>&1

echo [SUCCESS] 기존 프로세스 정리 완료.

:: [단계 1] 빌드 확인 (Build Phase)
echo ^> [1/3] 백엔드 서비스 빌드 중...

echo 1. Auth Server 빌드...
pushd "%AUTH_SERVER_DIR%"
dotnet build
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Auth Server 빌드 실패! 실행을 중단합니다.
    popd
    pause
    exit /b 1
)
popd

echo 2. Microservice 빌드...
pushd "%MICROSERVICE_DIR%"
dotnet build
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Microservice 빌드 실패! 실행을 중단합니다.
    popd
    pause
    exit /b 1
)
popd

echo 3. AI Agent Server 빌드...
pushd "%AI_AGENT_DIR%"
dotnet build
if %ERRORLEVEL% neq 0 (
    echo [ERROR] AI Agent Server 빌드 실패! 실행을 중단합니다.
    popd
    pause
    exit /b 1
)
popd

echo 4. API Gateway 빌드...
pushd "%GATEWAY_DIR%"
dotnet build
if %ERRORLEVEL% neq 0 (
    echo [ERROR] API Gateway 빌드 실패! 실행을 중단합니다.
    popd
    pause
    exit /b 1
)
popd

echo [SUCCESS] 모든 백엔드 빌드 성공!

:: [단계 2] 백엔드 서비스 실행 (Execution Phase)
echo ^> [2/3] 서비스 실행 중... (각 서비스는 별도 창에서 실행됩니다)

:: Auth Server 실행
start "Auth Server" cmd /k "cd /d %AUTH_SERVER_DIR% && set SERVER_NAME=AUTH && dotnet run --no-build"
timeout /t 2 /nobreak > nul

:: Microservice 실행
start "Microservice" cmd /k "cd /d %MICROSERVICE_DIR% && set SERVER_NAME=FUNERALV2 && dotnet run --no-build"
timeout /t 2 /nobreak > nul

:: AI Agent Server 실행
start "AI Agent Server" cmd /k "cd /d %AI_AGENT_DIR% && set SERVER_NAME=AI_AGENT && dotnet run --no-build"
timeout /t 2 /nobreak > nul

:: API Gateway 실행
start "API Gateway" cmd /k "cd /d %GATEWAY_DIR% && set SERVER_NAME=GATEWAY && dotnet run --no-build"
timeout /t 3 /nobreak > nul

:: [단계 3] 프론트엔드 실행 (필요 시 주석 해제)
:: echo ^> [3/3] 프론트엔드 (Vben Admin) 실행 중...
start "Frontend" cmd /k "cd /d %FRONTEND_DIR% && pnpm dev"

echo ====================================================
echo 모든 서비스가 시작되었습니다. 
echo 각 서비스의 로그는 새로 열린 창에서 확인하세요.
echo 서비스를 재시작하려면 이 창에서 아무 키나 누른 후 
echo dev.bat를 다시 실행하거나 각 창을 직접 닫으세요.
echo ====================================================
pause
