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
::   dev.bat site web        회사 소개 사이트만 (백엔드 :5480 + 프론트 :5556)
::   dev.bat mfe             Blazor 포털 전체 (셸 :5557 + 업무 MFE 6개). 이행 중인 새 프론트다.
::                           기본 `dev.bat` 에는 들어 있지 않다 — 창이 열아홉 개가 된다.
::   dev.bat stop auth       AuthServer 만 중지
::   dev.bat allstop         전체 중지
::   dev.bat status          지금 무엇이 떠 있는지 확인
::   dev.bat help            사용법
::
:: 한 서비스만 재기동할 때는 그 서비스만 빌드한다.
::
:: ── 리눅스/맥판과 다른 점 ───────────────────────────────────
::   · 중지는 scripts\dev-stop.ps1 에 맡긴다. 윈도우에는 /proc 이 없어 작업
::     디렉터리(cwd)로 프로세스를 고를 수 없고, 배치에는 부모 프로세스를 따라
::     올라갈 방법도 없다. 포트 + 실행 파일 경로 + 부모 사슬로 같은 일을 한다.
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
:: 형식: 표시이름(창 제목)^|상대경로^|포트^|SERVER_NAME^|기동명령
::
:: 서비스를 추가하려면 SVC_KEYS 에 이름을 넣고 SVC_^<이름^> 을 한 줄 더하면 된다.
:: 빌드·기동·중지·상태 확인이 모두 이 표를 읽는다.
:: 기동 순서는 SVC_KEYS 의 순서를 따른다.
::
:: SERVER_NAME 이 `-` 인 것은 프론트엔드다. 빌드 대신 pnpm install 을 하고
:: SERVER_NAME 환경변수를 넘기지 않는다. 프론트가 둘 이상이어도 install 은 한 번만 한다.
::
:: 기동명령을 칸으로 뺀 이유: 프론트가 둘이 되면서 "front 면 pnpm, 아니면 dotnet" 이라는
:: 분기로는 더 이상 표현이 안 됐다. 세 번째 프론트가 붙어도 표에 한 줄만 더하면 된다.
::
:: 이름이 바뀌었다: 예전 `front` 는 이제 `portal` 이다. 프론트가 둘이라 어느 쪽인지
:: 이름만으로 알 수 있어야 한다. `web` 이 회사 소개 사이트다.
set "SVC_KEYS=gateway auth funeral ai file helpdesk projmng site notify life portal web blazor uifuneral uihelpdesk uiadmin uisite uilife uiprojmng"

:: `dev.bat all` 이 띄우는 기본 묶음. **Blazor 포털은 빠져 있다.**
::
:: 이행하는 동안 Blazor 쪽은 프로세스가 일곱 개다(셸 + 업무 MFE 여섯). 이걸
:: 기본에 넣으면 아무 생각 없이 `dev.bat` 를 쳤을 때 창이 열아홉 개 뜬다.
:: 일상 작업은 아직 Vue 포털이므로 Blazor 는 `dev.bat mfe` 로 따로 띄운다.
:: 컷오버 때 이 줄에서 portal·web 을 빼고 blazor 묶음을 넣는다.
set "SVC_KEYS_DEFAULT=gateway auth funeral ai file helpdesk projmng site notify life portal web"

:: 그룹 별칭. 이름 하나가 여러 서비스로 펼쳐진다.
:: TARGETS 는 문자열을 이어 붙이는 방식이라 여러 개를 한꺼번에 넣어도 그대로 돈다.
set "GROUP_mfe=blazor uifuneral uihelpdesk uiadmin uisite uilife uiprojmng"

set "SVC_gateway=API Gateway|ApiGateway|5265|GATEWAY|%START_CMD%"
set "SVC_auth=Auth Server|microservices\AuthServer|5264|AUTH|%START_CMD%"
set "SVC_funeral=funeralv2 API|microservices\funeralv2Api|5320|FUNERALV2|%START_CMD%"
set "SVC_ai=AI Agent Server|microservices\AIAgentServer|5029|AI_AGENT|%START_CMD%"
set "SVC_file=File Server|microservices\FileServer|5350|FILE_API|%START_CMD%"
set "SVC_helpdesk=HelpDesk Server|microservices\HelpDeskServer|5400|HELPDESK|%START_CMD%"
set "SVC_projmng=ProjMng Server|microservices\ProjMngServer|5450|PROJMNG|%START_CMD%"
set "SVC_site=Site Server|microservices\SiteServer|5480|SITE_API|%START_CMD%"
:: 알림(푸시·이메일). 포털·장례식장·헬프데스크가 함께 쓴다 (결정 D8-A).
set "SVC_notify=Notification Server|microservices\NotificationServer|5460|NOTIFY|%START_CMD%"
:: 생활과환경(기상·생일). GHUB(SK가스 지허브)에서 이식했다.
set "SVC_life=LifeEnv Server|microservices\LifeEnvServer|5490|LIFEENV|%START_CMD%"
set "SVC_portal=Portal Frontend|fronts|5555|-|pnpm --filter @vben/jsini-portal dev"
set "SVC_web=Site Frontend|fronts|5556|-|pnpm --filter @jsini/site dev"
:: Blazor 포털(web\). Vue 포털(portal, 5555)을 대체할 것이지만 이행하는 동안에는
:: 둘을 나란히 띄워 화면을 대조해야 하므로 포트를 나눈다. 컷오버 때 5555 를 넘겨받는다.
::
:: SERVER_NAME 이 `-` 가 아니다 — `-` 는 "pnpm 워크스페이스 프론트" 라는 뜻이라
:: 빌드 대신 pnpm install 을 한다. 이건 .NET 프로젝트라 다른 백엔드와 똑같이
:: dotnet build / dotnet run 이면 된다. PORTAL_WEB 은 셸이 읽지 않지만,
:: 표의 다섯 칸을 채워야 기존 분기가 그대로 통한다.
:: ── Blazor 포털 (web\) ─────────────────────────────────────────
::
:: 셸 하나 + 업무 MFE 여섯. **각자 독립 프로세스다** — 셸이 경로 접두사로
:: 앞에서 프록시한다(/funeral → :5561 …). 사용자는 :5557 만 본다.
::
:: 다 띄우려면 `dev.bat mfe`. 한 업무만 고칠 때는 `dev.bat blazor uiprojmng`
:: 처럼 셸과 그 앱만 띄우면 된다 — 나머지 경로는 502 가 나지만 그 업무는 돈다.
::
:: SERVER_NAME 이 `-` 가 아니다. `-` 는 "pnpm 워크스페이스 프론트" 라는 뜻이라
:: 빌드 대신 pnpm install 을 한다. 이건 .NET 이라 dotnet build/run 이면 된다.
set "SVC_blazor=Blazor Shell|web\src\Shell\JSini.Web.Shell|5557|PORTAL_SHELL|%START_CMD%"
set "SVC_uifuneral=MFE 장례식장|web\src\Apps\JSini.Web.Funeral|5561|UI_FUNERAL|%START_CMD%"
set "SVC_uihelpdesk=MFE 헬프데스크|web\src\Apps\JSini.Web.HelpDesk|5562|UI_HELPDESK|%START_CMD%"
set "SVC_uiadmin=MFE 포털관리|web\src\Apps\JSini.Web.Admin|5563|UI_ADMIN|%START_CMD%"
set "SVC_uisite=MFE 소개사이트|web\src\Apps\JSini.Web.Site|5564|UI_SITE|%START_CMD%"
set "SVC_uilife=MFE 생활과환경|web\src\Apps\JSini.Web.LifeEnv|5565|UI_LIFE|%START_CMD%"
set "SVC_uiprojmng=MFE 프로젝트관리|web\src\Apps\JSini.Web.ProjMng|5566|UI_PROJMNG|%START_CMD%"

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
set "STOP_FAILED="
for %%k in (%SVC_KEYS%) do (
    call :stop_service %%k
    if errorlevel 1 set "STOP_FAILED=1"
)
echo.
if defined STOP_FAILED (
    echo [ERROR] 내리지 못한 서비스가 있습니다. status 로 확인하세요.
    set "EXITCODE=1"
) else (
    echo [SUCCESS] 전체 중지 완료.
)
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
    echo         사용 가능: %SVC_KEYS%   ^(front 는 portal 의 예전 이름^)
    set "EXITCODE=1"
    goto end
)
set "TARGETS=!TARGETS! !ALIAS_OUT!"
shift
goto stop_collect

:stop_collected
echo ====================================================
echo    중지:!TARGETS!
echo ====================================================
set "STOP_FAILED="
for %%k in (!TARGETS!) do (
    call :stop_service %%k
    if errorlevel 1 set "STOP_FAILED=1"
)
echo.
if defined STOP_FAILED (
    echo [ERROR] 내리지 못한 서비스가 있습니다. status 로 확인하세요.
    set "EXITCODE=1"
) else (
    echo [SUCCESS] 중지 완료.
)
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
set "TARGETS=%SVC_KEYS_DEFAULT%"
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
set "TARGETS=!TARGETS! !ALIAS_OUT!"
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

:: 호출하면 SVC_LABEL / SVC_DIR / SVC_PORT / SVC_NAME / SVC_CMD 를 채운다.
:svc_get
set "SVC_LABEL="
set "SVC_DIR="
set "SVC_PORT="
set "SVC_NAME="
set "SVC_CMD="
for /f "tokens=1-5 delims=|" %%a in ("!SVC_%~1!") do (
    set "SVC_LABEL=%%a"
    set "SVC_DIR=%ROOT_DIR%\%%b"
    set "SVC_PORT=%%c"
    set "SVC_NAME=%%d"
    set "SVC_CMD=%%e"
)
exit /b 0

:: 이름이 표에 있는지 확인한다. 없으면 errorlevel 1.
::
:: 예전 이름도 받아 준다. `front` 로 손이 굳은 사람이 오류를 보지 않게 하려는 것이다.
:: ALIAS_OUT 에 진짜 이름을 담아 돌려준다 — 부르는 쪽은 그것을 TARGETS 에 넣는다.
:: 그룹 별칭도 여기서 펼친다 (`mfe` → 셸 + 업무 MFE 여섯).
:: 부르는 쪽이 ALIAS_OUT 을 TARGETS 에 이어 붙이므로 여러 개를 돌려줘도 된다.
:svc_exists
set "ALIAS_OUT=%~1"
if /i "%~1"=="front" set "ALIAS_OUT=portal"
if /i "%~1"=="mfe" (
    set "ALIAS_OUT=%GROUP_mfe%"
    exit /b 0
)
if not defined SVC_!ALIAS_OUT! exit /b 1
exit /b 0

:: ============================================================
:: 프로세스 찾기 / 중지
:: ============================================================
::
:: 실제로 찾아 죽이는 일은 scripts\dev-stop.ps1 이 한다. 배치로는 안 되기 때문이다.
::   · 서비스는 셸 → 런처 → 실제 프로세스로 겹쳐 뜨고 포트를 잡는 것은 맨 아래 자식인데,
::     배치에는 부모를 따라 올라갈 방법이 없어 위쪽 셸이 그대로 남는다.
::   · taskkill 의 종료 코드는 믿을 수 없다. 창 제목 필터는 하나도 맞지 않아도 0 을 주고,
::     /T 는 액세스 거부(5) 를 간헐적으로 낸다.
:: 자세한 내용은 그 파일 머리말에 있다.
::
:: 도우미는 한 줄만 출력한다: NOT_RUNNING | STOPPED | FAILED pid=...
:: 결과는 STOP_RESULT 에 담아 둔다(restart_services 가 본다).
:stop_service
call :svc_get %~1
set "STOP_RESULT="
set "STOP_DETAIL="

:: 프론트는 디렉터리를 공유한다(둘 다 `fronts`). 디렉터리로 고르면 한쪽을 내릴 때
:: 다른 쪽까지 죽으므로, 기동 명령을 넘겨 그것으로 고르게 한다.
set "STOP_ARGS="
if "!SVC_NAME!"=="-" set "STOP_ARGS=-CmdMatch "!SVC_CMD!""

for /f "usebackq tokens=1,*" %%a in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT_DIR%\scripts\dev-stop.ps1" -Port !SVC_PORT! -Dir "!SVC_DIR!" !STOP_ARGS!`) do (
    set "STOP_RESULT=%%a"
    set "STOP_DETAIL=%%b"
)

if "!STOP_RESULT!"=="STOPPED" (
    echo    [OK] !SVC_LABEL! 종료
    exit /b 0
)
if "!STOP_RESULT!"=="NOT_RUNNING" (
    echo    [--] !SVC_LABEL! - 실행 중이 아님
    exit /b 0
)
:: 표시는 [XX] 로 둔다. 지연확장이 켜져 있어 `!` 를 그대로 쓰면 뒤의 !변수! 가 깨진다.
if "!STOP_RESULT!"=="FAILED" (
    echo    [XX] !SVC_LABEL! - 내리지 못했습니다 ^(!STOP_DETAIL!^)
    exit /b 1
)

:: 도우미 자체가 실패한 경우다. 안 떠 있다고 잘못 보고하는 것보다 이렇게 두는 편이 낫다.
echo    [XX] !SVC_LABEL! - 상태를 확인하지 못했습니다 ^(scripts\dev-stop.ps1 실행 실패^)
exit /b 1

:: ============================================================
:: 빌드 / 기동
:: ============================================================
:build_service
call :svc_get %~1

if "!SVC_NAME!"=="-" (
    :: 프론트는 워크스페이스 하나를 공유한다. 여러 개를 함께 띄워도 설치는 한 번이면 된다.
    if defined PNPM_INSTALLED (
        echo    - !SVC_LABEL! 의존성 설치 ^(이미 했음^)
        exit /b 0
    )
    echo    - !SVC_LABEL! 의존성 설치...
    pushd "%FRONTEND_DIR%"
    call pnpm install
    if errorlevel 1 (
        popd
        exit /b 1
    )
    popd
    set "PNPM_INSTALLED=1"
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
if "!SVC_NAME!"=="-" (
    :: 프론트는 SERVER_NAME 을 쓰지 않는다. 기동 뒤 기다리지도 않는다(vite 는 금방 뜬다).
    start "!SVC_LABEL!" /D "!SVC_DIR!" cmd /k !SVC_CMD!
    echo    [OK] !SVC_LABEL! 기동 ^(포트 !SVC_PORT!^)
    exit /b 0
)

set "SERVER_NAME=!SVC_NAME!"
set "DOTNET_WATCH_HOT_RELOAD=0"
start "!SVC_LABEL!" /D "!SVC_DIR!" cmd /k !SVC_CMD!
echo    [OK] !SVC_LABEL! 기동 ^(포트 !SVC_PORT!^)
timeout /t 2 /nobreak > nul
exit /b 0

:: ============================================================
:: 재기동 (TARGETS 에 담긴 서비스들)
:: ============================================================
:restart_services
echo.
echo ^>^>^> [1/3] 중지
:: 못 내린 서비스가 있으면 여기서 멈춘다. 포트를 잡고 있는 채로 기동하면
:: 새 프로세스가 조용히 죽어 "기동했다"는 말과 실제가 어긋난다.
set "STOP_FAILED="
for %%k in (!TARGETS!) do (
    call :stop_service %%k
    if errorlevel 1 set "STOP_FAILED=1"
)
if defined STOP_FAILED (
    echo.
    echo [ERROR] 내리지 못한 서비스가 있어 기동하지 않습니다.
    echo         남은 프로세스를 직접 정리한 뒤 다시 실행하세요.
    set "EXITCODE=1"
    exit /b 1
)

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
echo   dev.bat site web          소개 사이트 백엔드와 프론트를 다시 띄운다
echo   dev.bat projmng portal    ProjMng 와 업무 포털을 다시 띄운다
echo   dev.bat mfe               Blazor 포털 전체 ^(셸 :5557 + 업무 MFE 6개^)
echo   dev.bat blazor uiprojmng  셸과 프로젝트관리 MFE 만 ^(한 업무만 고칠 때^)
echo   dev.bat portal mfe        Vue 포털^(:5555^)과 Blazor 포털을 나란히 띄운다
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
