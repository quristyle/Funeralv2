@echo off
title Funeral Signage Player Bootstrapper

echo ==================================================
echo [1/4] Flutter Environment Configuration
echo ==================================================
set "PATH=C:\dev\flutter\bin;%PATH%"
cd /d "%~dp0"
if exist "funeralv2_player" cd "funeralv2_player"

echo.
echo ==================================================
echo [2/4] Detecting Android Emulator / Device
echo ==================================================
set "PATH=C:\Users\jjstyle\AppData\Local\Android\Sdk\platform-tools;C:\Users\jjstyle\AppData\Local\Android\Sdk\emulator;%PATH%"

:: 현재 연결된 실기기나 이미 켜진 에뮬레이터가 있는지 검사
adb devices > temp_devices.txt
findstr /r /c:"emulator-" temp_devices.txt > nul
if %errorlevel% equ 0 goto RE_CHECK
findstr /r /c:"[0-9]" temp_devices.txt > nul
if %errorlevel% equ 0 goto RE_CHECK

echo [INFO] No active devices detected. Checking Android Studio Emulator...

:: 에뮬레이터 목록을 파일로 임시 저장하여 안전하게 검사 (구문 오류 원천 차단)
emulator -list-avds > temp_avds.txt 2>nul
if errorlevel 1 goto NO_AVD

:: 파일 크기가 0인지 확인 (등록된 가상 기기가 없는 경우)
for %%A in (temp_avds.txt) do if %%~zA==0 goto NO_AVD

:: 첫 번째 줄의 에뮬레이터 이름을 안전하게 가져옴
set /p AVD_NAME=<temp_avds.txt
if "%AVD_NAME%"=="" goto NO_AVD

echo [INFO] Launching Emulator: %AVD_NAME%
start "" emulator -avd "%AVD_NAME%"
echo [INFO] Waiting for emulator to boot (20s)...
timeout /t 20 /nobreak
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