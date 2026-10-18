@echo off
chcp 65001 > nul
setlocal enabledelayedexpansion
:: ============================================================
:: JSini dev control panel - Windows counterpart of devui.sh
:: ============================================================
::
:: KEEP THIS FILE PURE ASCII (same reason as dev.bat - cmd reads batch files
:: by byte offset, Korean text makes it execute the tail of comment lines).
::
::   devui.cmd                     open http://127.0.0.1:5600
::   devui_stop.cmd                stop it
::   set DEVUI_PORT=5700 ^& devui.cmd   use another port
::
:: The panel does not hold this window. server.py --detach starts it with
:: its own hidden console, so closing this window leaves it running; its
:: output goes to logs\devui.log. Stop it with devui_stop.cmd, not Ctrl+C.
::
:: Services started from the panel run without windows too (dev.bat with
:: DEV_BACKGROUND=1). Their output is in logs\^<name^>.log - the panel's
:: log button shows it.
:: ============================================================

cd /d "%~dp0" || exit /b 1

if not defined DEVUI_PORT set "DEVUI_PORT=5600"
set "PORT=%DEVUI_PORT%"
set "LOG=logs\devui.log"
set "PIDFILE=.devui.pid"
set "URL=http://127.0.0.1:%PORT%"

call :find_pid
if defined PORT_PID (
    echo The panel is already running on :%PORT%. Opening the browser only.
    start "" "%URL%"
    exit /b 0
)

:: py launcher first (it skips the Microsoft Store stub), then python.
set "PY="
where py > nul 2>&1 && set "PY=py -3"
if not defined PY (
    where python > nul 2>&1 && set "PY=python"
)
if not defined PY (
    echo [ERROR] Python 3 was not found. Install it from https://www.python.org/
    exit /b 1
)

if not exist logs mkdir logs
%PY% scripts\devui\server.py --detach > "%PIDFILE%"
if errorlevel 1 (
    echo [ERROR] could not start the panel.
    del "%PIDFILE%" 2> nul
    exit /b 1
)

:: Wait for the port. If it never opens, show the log instead of opening an
:: empty error page in the browser.
for /l %%i in (1,1,30) do (
    call :find_pid
    if defined PORT_PID goto started
    ping -n 2 127.0.0.1 > nul
)

echo [ERROR] the panel did not come up on :%PORT%. Last lines of %LOG%:
echo.
powershell -NoProfile -Command "Get-Content -Encoding UTF8 -Tail 20 '%LOG%' | ForEach-Object { '   ' + $_ }"
del "%PIDFILE%" 2> nul
exit /b 1

:started
:: Record the process that actually holds the port (the same as devui.sh).
> "%PIDFILE%" echo !PORT_PID!
echo JSini dev control panel  -^>  %URL%  ^(pid !PORT_PID!^)
echo   log : %LOG%
echo   stop: devui_stop.cmd
start "" "%URL%"
exit /b 0

:: PID listening on 127.0.0.1:PORT, empty when none. Decided by the foreign
:: address 0.0.0.0:0, not the LISTENING column, which may be localized -
:: so the PID is taken as the last word of the line.
:find_pid
set "PORT_PID="
for /f "tokens=1,2,3,*" %%a in ('netstat -ano -p TCP') do (
    if "%%a"=="TCP" if "%%c"=="0.0.0.0:0" if "%%b"=="127.0.0.1:%PORT%" (
        for %%p in (%%d) do set "PORT_PID=%%p"
    )
)
exit /b 0
