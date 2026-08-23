@echo off
chcp 65001 > nul
setlocal enabledelayedexpansion
:: ============================================================
:: JSini 관리 포털 — 개발 서버 기동 스크립트 (Windows)
:: ============================================================
::
:: 사용법 (backend_run_ubuntu.sh / backend_run_mac.sh 와 동일하다)
::   dev.bat                 전체 재기동 (중지 -^> 빌드 -^> 기동)
::   dev.bat auth            AuthServer 만 재기동
::   dev.bat auth file       여러 개 지정도 된다
::   dev.bat stop auth       AuthServer 만 중지
::   dev.bat allstop         전체 중지
::   dev.bat status          지금 무엇이 떠 있는지 확인
::   dev.bat help            사용법
::
:: 한 서비스만 재기동할 때는 그 서비스만 빌드한다.
::
:: ── 리눅스/맥판과 다른 점 ───────────────────────────────────
::   · 서비스를 고를 때 작업 디렉터리(cwd) 대신 포트와 창 제목으로 찾는다.
::     윈도우에는 /proc 이 없고, 각 서비스가 자기 제목을 가진 창에서 돌기 때문이다.
::   · 기동은 dotnet run --no-build 다. 앞 단계에서 이미 빌드했으므로 다시 빌드하지 않는다.
::     리눅스/맥처럼 파일 변경 감지를 쓰려면 아래 START_CMD 를
::     dotnet watch run --no-hot-reload 로 바꾸면 된다.
:: ============================================================

set "EXITCODE=0"

set "ROOT_DIR=%~dp0"
if "%ROOT_DIR:~-1%"=="\" set "ROOT_DIR=%ROOT_DIR:~0,-1%"

set "SECRETS_FILE=%ROOT_DIR%\scripts\secrets.env"
set "FRONTEND_DIR=%ROOT_DIR%\fronts"

:: 서비스 기동 명령. 파일 변경 감지를 원하면 여기를 바꾼다.
set "START_CMD=dotnet run --no-build"

:: ------------------------------------------------------------
:: scripts\secrets.env 가 있으면 이 스크립트의 환경에 실어 둔다.
:: start 로 띄우는 창들이 이 환경을 물려받으므로 서비스마다 따로 읽을 필요가 없다.
:: (ASP.NET Core 는 Jwt__Key 같은 환경변수를 Jwt:Key 설정으로 읽고,
::  환경변수가 appsettings 보다 우선한다.)
::
:: 주의: 배치에서는 지연확장이 켜져 있어 값에 든 `!` 가 사라진다.
::       비밀번호 등에 `!` 가 있으면 secrets.env 에서 `^^!` 로 적어야 한다.
::       (리눅스/맥 스크립트에는 이 제약이 없다)
:: ------------------------------------------------------------
if exist "%SECRETS_FILE%" (
    for /f "usebackq eol=# tokens=1,* delims==" %%a in ("%SECRETS_FILE%") do (
        if not "%%a"=="" set "%%a=%%b"
    )
    echo [INFO] scripts\secrets.env 를 환경변수로 실었습니다.
)

:: ============================================================
:: 서비스 목록
:: ============================================================
::
:: 형식: 표시이름(창 제목)^|상대경로^|포트^|SERVER_NAME
::
:: 서비스를 추가하려면 SVC_KEYS 에 이름을 넣고 SVC_^<이름^> 을 한 줄 더하면 된다.
:: 빌드·기동·중지·상태 확인이 모두 이 표를 읽는다.
:: 기동 순서는 SVC_KEYS 의 순서를 따른다.
set "SVC_KEYS=gateway auth funeral ai file helpdesk projmng front"

set "SVC_gateway=API Gateway|ApiGateway|5265|GATEWAY"
set "SVC_auth=Auth Server|microservices\AuthServer|5264|AUTH"
set "SVC_funeral=funeralv2 API|microservices\funeralv2Api|5320|FUNERALV2"
set "SVC_ai=AI Agent Server|microservices\AIAgentServer|5029|AI_AGENT"
set "SVC_file=File Server|microservices\FileServer|5350|FILE_API"
set "SVC_helpdesk=HelpDesk Server|microservices\HelpDeskServer|5400|HELPDESK"
set "SVC_projmng=ProjMng Server|microservices\ProjMngServer|5450|PROJMNG"
set "SVC_front=Frontend|fronts|5555|-"

:: ============================================================
:: 인자 해석
:: ============================================================
set "CMD=%~1"
if "%CMD%"=="" set "CMD=all"

if /i "%CMD%"=="help"    goto cmd_help
if /i "%CMD%"=="-h"      goto cmd_help
if /i "%CMD%"=="--help"  goto cmd_help
if /i "%CMD%"=="/?"      goto cmd_help
if /i "%CMD%"=="list"    goto cmd_list
if /i "%CMD%"=="status"  goto cmd_status
if /i "%CMD%"=="allstop" goto cmd_allstop
if /i "%CMD%"=="stop"    goto cmd_stop
if /i "%CMD%"=="all"     goto cmd_all
goto cmd_restart

:: ------------------------------------------------------------
:cmd_help
call :print_usage
goto end

:: ------------------------------------------------------------
:cmd_list
for %%k in (%SVC_KEYS%) do echo %%k
goto end

:: ------------------------------------------------------------
:cmd_status
call :print_status
goto end

:: ------------------------------------------------------------
:cmd_allstop
echo ====================================================
echo    전체 중지
echo ====================================================
for %%k in (%SVC_KEYS%) do call :stop_service %%k
echo.
echo [SUCCESS] 전체 중지 완료.
goto end

:: ------------------------------------------------------------
:cmd_stop
shift
if "%~1"=="" (
    echo [ERROR] 중지할 서비스를 지정하세요. 전체를 내리려면 allstop 입니다.
    echo.
    call :print_usage
    set "EXITCODE=1"
    goto end
)

:: 이름을 먼저 모두 검사한다. 하나라도 틀리면 아무것도 건드리지 않는다.
set "TARGETS="
:stop_collect
if "%~1"=="" goto stop_collected
call :svc_exists %~1
if errorlevel 1 (
    echo [ERROR] 알 수 없는 서비스: %~1
    echo         사용 가능: %SVC_KEYS%
    set "EXITCODE=1"
    goto end
)
set "TARGETS=!TARGETS! %~1"
shift
goto stop_collect

:stop_collected
echo ====================================================
echo    중지:!TARGETS!
echo ====================================================
for %%k in (!TARGETS!) do call :stop_service %%k
echo.
echo [SUCCESS] 중지 완료.
goto end

:: ------------------------------------------------------------
:cmd_all
if not "%~2"=="" (
    echo [ERROR] all 은 다른 이름과 함께 쓸 수 없습니다.
    set "EXITCODE=1"
    goto end
)
echo ====================================================
echo    JSini 관리 포털 — 전체 빌드 및 시작
echo ====================================================
set "TARGETS=%SVC_KEYS%"
call :restart_services
goto end

:: ------------------------------------------------------------
:cmd_restart
set "TARGETS="
:restart_collect
if "%~1"=="" goto restart_collected
call :svc_exists %~1
if errorlevel 1 (
    echo [ERROR] 알 수 없는 서비스: %~1
    echo.
    call :print_usage
    set "EXITCODE=1"
    goto end
)
set "TARGETS=!TARGETS! %~1"
shift
goto restart_collect

:restart_collected
echo ====================================================
echo    재기동:!TARGETS!
echo ====================================================
call :restart_services
goto end

:: ============================================================
:: 서비스 표 조회
:: ============================================================

:: 호출하면 SVC_LABEL / SVC_DIR / SVC_PORT / SVC_NAME 를 채운다.
:svc_get
set "SVC_LABEL="
set "SVC_DIR="
set "SVC_PORT="
set "SVC_NAME="
for /f "tokens=1-4 delims=|" %%a in ("!SVC_%~1!") do (
    set "SVC_LABEL=%%a"
    set "SVC_DIR=%ROOT_DIR%\%%b"
    set "SVC_PORT=%%c"
    set "SVC_NAME=%%d"
)
exit /b 0

:: 이름이 표에 있는지 확인한다. 없으면 errorlevel 1.
:svc_exists
if not defined SVC_%~1 exit /b 1
exit /b 0

:: ============================================================
:: 프로세스 찾기 / 중지
:: ============================================================
::
:: 서비스 하나만 골라 죽여야 한다. 윈도우에서는 두 가지로 찾는다.
::   1) 포트   — netstat 로 그 포트를 LISTENING 중인 PID 를 찾아 종료한다.
::               스크립트 밖에서 띄운 서버도 잡히므로 이것이 기본이다.
::   2) 창 제목 — 서비스마다 자기 제목을 가진 창에서 돌기 때문에,
::               포트를 잡지 못한 채 떠 있는 창(기동 실패 등)도 정리할 수 있다.
:stop_service
call :svc_get %~1
set "STOPPED="

:: 1) 포트를 잡고 있는 프로세스
for /f "tokens=5" %%p in ('netstat -ano ^| findstr /r /c:"LISTENING" ^| findstr ":!SVC_PORT! "') do (
    if not "%%p"=="0" (
        taskkill /F /PID %%p /T > nul 2>&1
        if not errorlevel 1 set "STOPPED=1"
    )
)

:: 2) 그 서비스의 창
taskkill /F /FI "WINDOWTITLE eq !SVC_LABEL!*" /T > nul 2>&1
if not errorlevel 1 set "STOPPED=1"

if defined STOPPED (
    echo    [OK] !SVC_LABEL! 종료
) else (
    echo    [--] !SVC_LABEL! - 실행 중이 아님
)
exit /b 0

:: ============================================================
:: 빌드 / 기동
:: ============================================================
:build_service
call :svc_get %~1

if /i "%~1"=="front" (
    echo    - !SVC_LABEL! 의존성 설치...
    pushd "%FRONTEND_DIR%"
    call pnpm install
    if errorlevel 1 (
        popd
        exit /b 1
    )
    popd
    exit /b 0
)

echo    - !SVC_LABEL! 빌드...
pushd "!SVC_DIR!"
dotnet build
if errorlevel 1 (
    popd
    exit /b 1
)
popd
exit /b 0

:start_service
call :svc_get %~1

:: 두 가지를 문자열에 끼워 넣지 않는다. 배치에서 따옴표가 겹치면 깨지기 쉽다.
::   · 작업 디렉터리 → start 의 /D 로 넘긴다 (경로에 공백이 있어도 안전하다)
::   · 환경변수     → 여기서 set 하면 start 로 띄운 창이 물려받는다
if /i "%~1"=="front" (
    start "!SVC_LABEL!" /D "%FRONTEND_DIR%" cmd /k pnpm dev
    echo    [OK] !SVC_LABEL! 기동 ^(포트 !SVC_PORT!^)
    exit /b 0
)

set "SERVER_NAME=!SVC_NAME!"
set "DOTNET_WATCH_HOT_RELOAD=0"
start "!SVC_LABEL!" /D "!SVC_DIR!" cmd /k %START_CMD%
echo    [OK] !SVC_LABEL! 기동 ^(포트 !SVC_PORT!^)
timeout /t 2 /nobreak > nul
exit /b 0

:: ============================================================
:: 재기동 (TARGETS 에 담긴 서비스들)
:: ============================================================
:restart_services
echo.
echo ^>^>^> [1/3] 중지
for %%k in (!TARGETS!) do call :stop_service %%k

echo.
echo ^>^>^> [2/3] 빌드
for %%k in (!TARGETS!) do (
    call :build_service %%k
    if errorlevel 1 (
        echo [ERROR] %%k 빌드 실패. 기동하지 않습니다.
        set "EXITCODE=1"
        exit /b 1
    )
)

echo.
echo ^>^>^> [3/3] 기동
for %%k in (!TARGETS!) do call :start_service %%k

echo.
echo ====================================================
echo 완료:!TARGETS!
echo 각 서비스의 로그는 새로 열린 창에서 확인하세요.
echo ====================================================
exit /b 0

:: ============================================================
:: 출력
:: ============================================================
:print_usage
echo 사용법: dev.bat [명령 ^| 서비스이름...]
echo.
echo   ^(없음^)              전체 재기동 - 중지, 빌드, 기동
echo   all                 위와 같음
echo   ^<서비스^> [^<서비스^>] 지정한 서비스만 재기동 - 그 서비스만 빌드한다
echo   stop ^<서비스^>...    지정한 서비스만 중지
echo   allstop             전체 중지
echo   status              지금 무엇이 떠 있는지 확인
echo   list                서비스 이름 목록
echo   help                이 도움말
echo.
echo 서비스 이름
for %%k in (%SVC_KEYS%) do (
    call :svc_get %%k
    call :pad10 %%k
    echo   !PADDED! !SVC_LABEL! - 포트 !SVC_PORT!
)
echo.
echo 예시
echo   dev.bat auth              AuthServer 만 다시 띄운다
echo   dev.bat projmng front     ProjMng 와 프론트를 다시 띄운다
echo   dev.bat stop helpdesk     헬프데스크만 내린다
echo   dev.bat allstop           전부 내린다
exit /b 0

:print_status
echo ====================================================
echo    서비스 상태
echo ====================================================
echo   name       port   state  service
echo   ---------- ----   -----  -------
for %%k in (%SVC_KEYS%) do (
    call :svc_get %%k
    set "STATE=DOWN  "
    netstat -ano | findstr /r /c:"LISTENING" | findstr ":!SVC_PORT! " > nul 2>&1
    if not errorlevel 1 set "STATE=UP    "
    call :pad10 %%k
    call :pad6 "!SVC_PORT!"
    echo   !PADDED! !PADDED6! !STATE! !SVC_LABEL!
)
echo.
exit /b 0

:: 자릿수 맞추기. 한글은 폭이 달라 이 칸에는 ASCII 만 쓴다.
:pad10
set "PADDED=%~1          "
set "PADDED=!PADDED:~0,10!"
exit /b 0

:pad6
set "PADDED6=%~1      "
set "PADDED6=!PADDED6:~0,6!"
exit /b 0

:: ============================================================
:end
endlocal & exit /b %EXITCODE%
