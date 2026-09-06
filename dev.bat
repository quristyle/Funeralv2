@echo off
chcp 65001 > nul
setlocal enabledelayedexpansion
:: ============================================================
:: JSini portal - dev server launcher (Windows)
:: ============================================================
::
:: KEEP THIS FILE PURE ASCII. Do not write Korean here.
::
:: cmd.exe advances through a batch file by BYTE offset but measures line
:: length in CHARACTERS. With UTF-8 Korean (3 bytes per glyph) the two drift
:: apart and cmd starts executing the TAIL of comment lines:
::
::     C:\Funeralv2^>h file       ...        (from ':: dev.bat auth file ...')
::     'h' is not recognized as an internal or external command
::
:: The drift grows with every multi-byte glyph earlier in the file, so it
:: only shows up once the file passes some size. Neither `chcp 65001` nor a
:: BOM fixes it - a BOM actually breaks `@echo off`. Staying ASCII removes
:: the cause. Korean prose belongs in CLAUDE.md, not in this file.
::
:: ------------------------------------------------------------
:: Usage (mirrors backend_run_ubuntu.sh / backend_run_mac.sh)
::   dev.bat                 restart everything (stop -^> build -^> start)
::   dev.bat auth            restart AuthServer only
::   dev.bat auth file       several names are allowed
::   dev.bat site web        public site only (backend :5480 + front :5556)
::   dev.bat blazor          work portal only (:5557)
::   dev.bat stop auth       stop AuthServer only
::   dev.bat allstop         stop everything
::   dev.bat status          show what is up
::   dev.bat help            this help
::
:: Restarting one service builds only that service.
::
:: ------------------------------------------------------------
:: How this differs from the Linux/macOS scripts
::   - Stopping is delegated to scripts\dev-stop.ps1. Windows has no /proc,
::     so processes cannot be picked by working directory, and batch cannot
::     walk up the parent chain. That script uses port + image path + parent
::     chain to do the same job.
::   - Services start with `dotnet watch run`. EVERY service runs in watch
::     mode, so edits apply without a restart (Hot Reload).
::
:: ------------------------------------------------------------
:: Two things to know about watch mode
::   - A watched service holds its own DLLs, so `dotnet build` from another
::     window fails with MSB3027. Stop that service first
::     (dev.bat stop ^<service^>).
::   - Rude edits (adding a type, changing a signature) restart the service
::     instead of prompting (DOTNET_WATCH_RESTART_ON_RUDE_EDIT).
:: ============================================================

set "EXITCODE=0"

set "ROOT_DIR=%~dp0"
if "%ROOT_DIR:~-1%"=="\" set "ROOT_DIR=%ROOT_DIR:~0,-1%"

set "SECRETS_FILE=%ROOT_DIR%\scripts\secrets.env"

:: Start command for every service.
::
:: NO `--no-build` here. Watch compiles and swaps the assembly itself; if the
:: initial build is skipped it has no baseline to diff against.
::
:: Change this to `dotnet run --no-build` only to reproduce the production
:: launch path (no watching).
set "START_CMD=dotnet watch run"

:: ------------------------------------------------------------
:: Load scripts\secrets.env into this script's environment when present.
:: The windows opened by `start` inherit it, so no service reads it itself.
:: (ASP.NET Core maps Jwt__Key to the Jwt:Key setting, and environment
::  variables win over appsettings.)
::
:: Note: delayed expansion is on, so a `!` inside a value disappears.
::       Escape it as `^^!` in secrets.env if a password contains one.
::       (The Linux/macOS scripts have no such restriction.)
:: ------------------------------------------------------------
if exist "%SECRETS_FILE%" (
    for /f "usebackq eol=# tokens=1,* delims==" %%a in ("%SECRETS_FILE%") do (
        if not "%%a"=="" set "%%a=%%b"
    )
    echo [INFO] loaded scripts\secrets.env into the environment.
)

:: ============================================================
:: Service table
:: ============================================================
::
:: Format: label(window title)^|relative path^|port^|SERVER_NAME^|start command
::
:: To add a service, put its name in SVC_KEYS and add one SVC_^<name^> line.
:: Build, start, stop and status all read this table.
:: Start order follows the order of SVC_KEYS.
::
:: Every service starts with %START_CMD%. The front end is .NET now too -
:: the Vue/pnpm portal is gone and the Blazor shell (:5557) took its place.
::
::   blazor  work portal shell (:5557). Six work MFEs live in this one process.
::   web     public site (:5556). Separate site, unrelated to the portal.
set "SVC_KEYS=gateway auth funeral ai file helpdesk projmng site notify life blazor web"

:: What `dev.bat all` starts = everything.
::
:: Blazor was missing from this list for a while, back when each work app was
:: its own process and starting all of them opened nineteen windows. There is
:: one shell now, and since the Vue portal is gone, leaving it out means the
:: portal does not come up at all.
set "SVC_KEYS_DEFAULT=gateway auth funeral ai file helpdesk projmng site notify life blazor web"

:: Group alias. `mfe` is muscle memory, kept alive - it now means the shell.
set "GROUP_mfe=blazor"

set "SVC_gateway=API Gateway|ApiGateway|5265|GATEWAY|%START_CMD%"
set "SVC_auth=Auth Server|microservices\AuthServer|5264|AUTH|%START_CMD%"
set "SVC_funeral=funeralv2 API|microservices\funeralv2Api|5320|FUNERALV2|%START_CMD%"
set "SVC_ai=AI Agent Server|microservices\AIAgentServer|5029|AI_AGENT|%START_CMD%"
set "SVC_file=File Server|microservices\FileServer|5350|FILE_API|%START_CMD%"
set "SVC_helpdesk=HelpDesk Server|microservices\HelpDeskServer|5400|HELPDESK|%START_CMD%"
set "SVC_projmng=ProjMng Server|microservices\ProjMngServer|5450|PROJMNG|%START_CMD%"
set "SVC_site=Site Server|microservices\SiteServer|5480|SITE_API|%START_CMD%"
:: Notifications (push / email). Portal, funeral and helpdesk share it (D8-A).
set "SVC_notify=Notification Server|microservices\NotificationServer|5460|NOTIFY|%START_CMD%"
:: LifeEnv (weather / birthdays). Ported from GHUB.
set "SVC_life=LifeEnv Server|microservices\LifeEnvServer|5490|LIFEENV|%START_CMD%"

:: ------------------------------------------------------------
:: Front end (web\)
::
:: Work portal shell :5557 - six work MFEs (funeral, helpdesk, admin, site,
:: lifeenv, projmng) live in this one process. Modules are composed at build
:: time (ProjectReference in the shell csproj) and the shell scans the
:: assemblies to register them.
set "SVC_blazor=Blazor Work Portal|web\src\Shell\JSini.Web.Shell|5557|PORTAL_SHELL|%START_CMD%"

:: Public site :5556 - static SSR only, no auth, unrelated to the portal.
:: Replaces the old Vue build (fronts/apps/jsini-site).
set "SVC_web=Public Site|web\src\Site\JSini.PublicSite|5556|PUBLIC_SITE|%START_CMD%"

:: ============================================================
:: Argument parsing
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
if /i "%CMD%"=="watch"   goto cmd_watch
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
echo    Stop all
echo ====================================================
set "STOP_FAILED="
for %%k in (%SVC_KEYS%) do (
    call :stop_service %%k
    if errorlevel 1 set "STOP_FAILED=1"
)
echo.
if defined STOP_FAILED (
    echo [ERROR] some services are still up. Check with: dev.bat status
    set "EXITCODE=1"
) else (
    echo [SUCCESS] all stopped.
)
goto end

:: ------------------------------------------------------------
:cmd_stop
shift
if "%~1"=="" (
    echo [ERROR] name the service to stop. Use allstop to stop everything.
    echo.
    call :print_usage
    set "EXITCODE=1"
    goto end
)

:: Validate every name first. If one is wrong, touch nothing.
set "TARGETS="
:stop_collect
if "%~1"=="" goto stop_collected
call :svc_exists %~1
if errorlevel 1 (
    echo [ERROR] unknown service: %~1
    echo         available: %SVC_KEYS%   ^(front is the old name of portal^)
    set "EXITCODE=1"
    goto end
)
set "TARGETS=!TARGETS! !ALIAS_OUT!"
shift
goto stop_collect

:stop_collected
echo ====================================================
echo    Stop:!TARGETS!
echo ====================================================
set "STOP_FAILED="
for %%k in (!TARGETS!) do (
    call :stop_service %%k
    if errorlevel 1 set "STOP_FAILED=1"
)
echo.
if defined STOP_FAILED (
    echo [ERROR] some services are still up. Check with: dev.bat status
    set "EXITCODE=1"
) else (
    echo [SUCCESS] stopped.
)
goto end

:: ------------------------------------------------------------
:cmd_all
if not "%~2"=="" (
    echo [ERROR] all cannot be combined with service names.
    set "EXITCODE=1"
    goto end
)
echo ====================================================
echo    JSini portal - build and start everything
echo ====================================================
set "TARGETS=%SVC_KEYS_DEFAULT%"
call :restart_services
goto end

:: ------------------------------------------------------------
:: watch - the same thing as a normal start now.
::
:: There used to be a separate watch mode. Every start is a watch start
:: today, so there is nothing to distinguish. The keyword stays because it
:: is muscle memory (`dev.bat watch blazor` = `dev.bat blazor`).
:: ------------------------------------------------------------
:cmd_watch
shift
if "%~1"=="" goto cmd_all
goto cmd_restart

:: ------------------------------------------------------------
:cmd_restart
set "TARGETS="
:restart_collect
if "%~1"=="" goto restart_collected
call :svc_exists %~1
if errorlevel 1 (
    echo [ERROR] unknown service: %~1
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
echo    Restart:!TARGETS!
echo ====================================================
call :restart_services
goto end

:: ============================================================
:: Service table lookup
:: ============================================================

:: Fills SVC_LABEL / SVC_DIR / SVC_PORT / SVC_NAME / SVC_CMD.
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

:: Check the name against the table. errorlevel 1 when unknown.
::
:: Old names are accepted so nobody with muscle memory sees an error.
::   front, portal -^> blazor  (the Blazor shell took over the Vue portal)
::   mfe           -^> blazor  (group name from when each app was a process)
:: The real name comes back in ALIAS_OUT; the caller appends it to TARGETS,
:: so returning several names at once works too.
:svc_exists
set "ALIAS_OUT=%~1"
if /i "%~1"=="front" set "ALIAS_OUT=blazor"
if /i "%~1"=="portal" set "ALIAS_OUT=blazor"
if /i "%~1"=="mfe" (
    set "ALIAS_OUT=%GROUP_mfe%"
    exit /b 0
)
if not defined SVC_!ALIAS_OUT! exit /b 1
exit /b 0

:: ============================================================
:: Finding and stopping processes
:: ============================================================
::
:: scripts\dev-stop.ps1 does the finding and killing, because batch cannot.
::   - A service is a stack of shell -^> launcher -^> process, and the port is
::     held by the bottom child. Batch cannot walk up to the parents, so the
::     upper shells would survive.
::   - taskkill exit codes cannot be trusted. A window-title filter returns 0
::     even when nothing matched, and /T intermittently returns access denied.
:: The details are in that file's header.
::
:: The helper prints one line: NOT_RUNNING ^| STOPPED ^| FAILED pid=...
:: The result lands in STOP_RESULT (restart_services reads it).
:stop_service
call :svc_get %~1
set "STOP_RESULT="
set "STOP_DETAIL="

:: Extra arguments for the stop helper.
set "STOP_ARGS="

for /f "usebackq tokens=1,*" %%a in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT_DIR%\scripts\dev-stop.ps1" -Port !SVC_PORT! -Dir "!SVC_DIR!" !STOP_ARGS!`) do (
    set "STOP_RESULT=%%a"
    set "STOP_DETAIL=%%b"
)

if "!STOP_RESULT!"=="STOPPED" (
    echo    [OK] !SVC_LABEL! stopped
    exit /b 0
)
if "!STOP_RESULT!"=="NOT_RUNNING" (
    echo    [--] !SVC_LABEL! - was not running
    exit /b 0
)
:: Marked [XX] on purpose: delayed expansion is on, so a literal `!` here
:: would eat the following !variable!.
if "!STOP_RESULT!"=="FAILED" (
    echo    [XX] !SVC_LABEL! - could not stop ^(!STOP_DETAIL!^)
    exit /b 1
)

:: The helper itself failed. Better to say so than to report "not running".
echo    [XX] !SVC_LABEL! - state unknown ^(scripts\dev-stop.ps1 failed^)
exit /b 1

:: ============================================================
:: Build / start
:: ============================================================
:build_service
call :svc_get %~1

echo    - building !SVC_LABEL! ...
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

set "SERVER_NAME=!SVC_NAME!"

:: Start in watch mode. Edits apply without a restart.
::
:: DO NOT turn this off. It used to be pinned to 0 here, so switching the
:: start command to `dotnet watch` did not actually enable hot reload -
:: it showed up as "started with watch, why does nothing change".
set "DOTNET_WATCH_HOT_RELOAD=1"

:: Rude edits (new type, changed signature) restart instead of prompting.
:: A prompt would freeze the service until someone looks at the window.
set "DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1"

start "!SVC_LABEL!" /D "!SVC_DIR!" cmd /k !SVC_CMD!
echo    [OK] !SVC_LABEL! started ^(port !SVC_PORT! - watching, edits apply^)
timeout /t 2 /nobreak > nul
exit /b 0

:: ============================================================
:: Restart (the services collected in TARGETS)
:: ============================================================
:restart_services
echo.
echo ^>^>^> [1/3] stop
:: Stop here if anything failed to go down. Starting while the old process
:: still holds the port makes the new one die quietly, so "started" would
:: not match reality.
set "STOP_FAILED="
for %%k in (!TARGETS!) do (
    call :stop_service %%k
    if errorlevel 1 set "STOP_FAILED=1"
)
if defined STOP_FAILED (
    echo.
    echo [ERROR] some services would not stop, so nothing was started.
    echo         Clean up the leftover processes and run again.
    set "EXITCODE=1"
    exit /b 1
)

echo.
echo ^>^>^> [2/3] build
for %%k in (!TARGETS!) do (
    call :build_service %%k
    if errorlevel 1 (
        echo [ERROR] %%k failed to build. Nothing was started.
        set "EXITCODE=1"
        exit /b 1
    )
)

echo.
echo ^>^>^> [3/3] start
for %%k in (!TARGETS!) do call :start_service %%k

echo.
echo ====================================================
echo Done:!TARGETS!
echo Each service logs into its own new window.
echo ====================================================
exit /b 0

:: ============================================================
:: Output
:: ============================================================
:print_usage
echo Usage: dev.bat [command ^| service...]
echo.
echo   ^(none^)              restart everything - stop, build, start
echo   all                 same as above
echo   ^<service^> [^<svc^>]   restart those services - builds only those
echo   watch ^<service^>...  same as above - every start is a watch start
echo   stop ^<service^>...   stop those services
echo   allstop             stop everything
echo   status              show what is up
echo   list                list service names
echo   help                this help
echo.
echo Services
for %%k in (%SVC_KEYS%) do (
    call :svc_get %%k
    call :pad10 %%k
    echo   !PADDED! !SVC_LABEL! - port !SVC_PORT!
)
echo.
echo Examples
echo   dev.bat auth              restart AuthServer only
echo   dev.bat site web          restart the site backend ^(:5480^) and front ^(:5556^)
echo   dev.bat projmng blazor    restart the ProjMng backend and the work portal
echo   dev.bat blazor            restart the work portal only ^(:5557^)
echo   dev.bat stop helpdesk     stop helpdesk only
echo   dev.bat allstop           stop everything
exit /b 0

:print_status
echo ====================================================
echo    Service status
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

:: Column padding. ASCII only in these columns so the widths line up.
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
