@echo off
chcp 65001 > nul
setlocal enabledelayedexpansion
:: ============================================================
:: Stop the JSini dev control panel started by devui.cmd
:: (Windows counterpart of devui_stop.sh). KEEP THIS FILE PURE ASCII.
::
::   devui_stop.cmd                            stop the panel on :5600
::   set DEVUI_PORT=5700 ^& devui_stop.cmd     another port
::
:: Only the panel stops. Services it started keep running - to stop them
:: too: dev.bat allstop
:: ============================================================

cd /d "%~dp0" || exit /b 1

if not defined DEVUI_PORT set "DEVUI_PORT=5600"
set "PORT=%DEVUI_PORT%"
set "PIDFILE=.devui.pid"

:: The process holding the port is the most reliable. The PID file is the
:: fallback for when the port lookup finds nothing.
call :find_pid
set "PID=%PORT_PID%"
if not defined PID if exist "%PIDFILE%" (
    set /p PID=< "%PIDFILE%"
    if defined PID (
        tasklist /fi "PID eq !PID!" /fo csv /nh | findstr /c:"\"!PID!\"" > nul || set "PID="
    )
)

if not defined PID (
    echo The panel is not running on :%PORT%.
    del "%PIDFILE%" 2> nul
    exit /b 0
)

:: No /T: the services the panel started must survive.
taskkill /pid %PID% /f > nul 2>&1

:: Judge by the port, not by taskkill. A port still held would make the next
:: devui.cmd think the panel is already running.
for /l %%i in (1,1,10) do (
    call :find_pid
    if not defined PORT_PID goto stopped
    ping -n 2 127.0.0.1 > nul
)
echo [ERROR] :%PORT% is still open ^(pid %PORT_PID%^).
exit /b 1

:stopped
del "%PIDFILE%" 2> nul
echo Stopped the panel ^(:%PORT%, pid %PID%^).
echo   Services it started keep running - to stop them: dev.bat allstop
exit /b 0

:: Same lookup as devui.cmd.
:find_pid
set "PORT_PID="
for /f "tokens=1,2,3,*" %%a in ('netstat -ano -p TCP') do (
    if "%%a"=="TCP" if "%%c"=="0.0.0.0:0" if "%%b"=="127.0.0.1:%PORT%" (
        for %%p in (%%d) do set "PORT_PID=%%p"
    )
)
exit /b 0
