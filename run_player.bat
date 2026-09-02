@echo off
title JSINI Player Bootstrapper

echo ==================================================
echo [1/4] Flutter Environment Configuration
echo ==================================================
set "PATH=C:\dev\flutter\bin;%PATH%"
cd /d "%~dp0"
if exist "funeralv2_player" (
    cd "funeralv2_player"
)

echo.
echo ==================================================
echo [2/4] Detecting Android Emulator / Device
echo ==================================================
set "PATH=C:\Users\jjstyle\AppData\Local\Android\Sdk\platform-tools;C:\Users\jjstyle\AppData\Local\Android\Sdk\emulator;%PATH%"

rem Check if there is already an active running emulator or device
adb devices > temp_devices.txt
findstr /v /i "List" temp_devices.txt | findstr /i "device" > nul
if %errorlevel% equ 0 goto RE_CHECK

echo [INFO] No active devices detected. Checking Android Studio Emulator...

rem List avds to temp file
emulator -list-avds > temp_avds.txt 2>nul
if errorlevel 1 goto NO_AVD

rem Check if temp file is empty
for %%A in (temp_avds.txt) do if %%~zA==0 goto NO_AVD

rem Read the first avd name
set /p AVD_NAME=<temp_avds.txt
if "%AVD_NAME%"=="" goto NO_AVD

echo [INFO] Launching Emulator: %AVD_NAME%
start "" emulator -avd "%AVD_NAME%"

echo [INFO] Waiting for emulator to establish ADB connection...
adb wait-for-device

echo [INFO] Emulator ADB connected. Waiting for Android OS boot to complete...
set /a BOOT_WAIT_SEC=0

:BOOT_LOOP
if %BOOT_WAIT_SEC% gtr 60 goto BOOT_TIMEOUT
adb shell getprop sys.boot_completed | findstr "1" > nul
if %errorlevel% equ 0 goto BOOT_SUCCESS
timeout /t 2 /nobreak > nul
set /a BOOT_WAIT_SEC+=2
goto BOOT_LOOP

:BOOT_TIMEOUT
echo [WARNING] Emulator boot verification timeout. Proceeding to execution...
goto RE_CHECK

:BOOT_SUCCESS
echo [INFO] Emulator boot completed successfully.
goto RE_CHECK

:NO_AVD
echo --------------------------------------------------
echo [WARNING] No Android Virtual Device (AVD) found.
echo [HELP] Please open Android Studio and create a Virtual Device first.
echo --------------------------------------------------
if exist temp_avds.txt del temp_avds.txt
if exist temp_devices.txt del temp_devices.txt
pause
exit /b

:RE_CHECK
if exist temp_devices.txt del temp_devices.txt
if exist temp_avds.txt del temp_avds.txt

echo.
echo ==================================================
echo [3/4] Resolving Flutter Dependencies
echo ==================================================
call flutter pub get

echo.
echo ==================================================
echo [4/4] Launching Funeral Player on Emulator
echo ==================================================
echo [INFO] Building and starting app. Please wait...
call flutter run

pause