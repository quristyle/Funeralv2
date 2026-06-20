@echo off
setlocal

:: CMD 콘솔의 코드페이지를 UTF-8(65001)로 강제 지정
chcp 65001 >nul

:: Define the rules directory and files
set "RULES_DIR=docs\prompts"
set "RULES_FILES=coding_agent_system.md coding_agent_typescript.md coding_agent_vue3_script.md 3.AI.md 5.dev.md"

:: Check if all rule files exist
for %%F in (%RULES_FILES%) do (
    if not exist "%RULES_DIR%\%%F" (
        echo [X] Rules file not found: %RULES_DIR%\%%F
        exit /b 1
    )
)

:: .agents 폴더 생성
if not exist ".agents" mkdir ".agents"

echo [OK] All rule files found. Merging into .agents\AGENTS.md ...

:: PowerShell을 사용하여 인코딩 깨짐 없이 .agents\AGENTS.md 파일로 규칙 병합 및 저장
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Console]::InputEncoding = [Console]::OutputEncoding = $OutputEncoding = [System.Text.Encoding]::UTF8; $files = '%RULES_FILES%'.Split(' '); $rules = foreach ($f in $files) { Get-Content (Join-Path '%RULES_DIR%' $f) -Encoding utf8 -Raw }; $combined = '아래 규칙은 이 세션 전체에 대해 **최우선으로 적용**된다.`n모든 응답은 반드시 이 규칙을 준수해야 한다.`n규칙을 위반하려는 경우, 응답을 중단하고 규칙을 재적용한다.`n`n====================`n' + ($rules -join '`n') + '`n====================`n`n# REQUIREMENTS`n- 모든 코드에는 주석을 달아야 한다.`n`n이제부터 사용자 입력을 기다린다.'; [System.IO.File]::WriteAllText('.agents\AGENTS.md', $combined, [System.Text.Encoding]::UTF8)"

echo [OK] Rules successfully loaded into Workspace Customization (.agents/AGENTS.md).
echo.
echo ==================================================
echo agy 실행 방식을 선택하세요:
echo [1] 현재 창에서 실행 (기본값)
echo [2] 새로운 창에서 실행
echo ==================================================
set "CHOICE=1"
set /p CHOICE="선택 (1 또는 2): "

if "%CHOICE%"=="2" (
    echo [OK] Starting agy in a new window...
    start "agy" agy --dangerously-skip-permissions
) else (
    echo [OK] Starting agy in the current window...
    agy --dangerously-skip-permissions
)

