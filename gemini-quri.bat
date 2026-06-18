@echo off
setlocal

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

echo [OK] All rule files found. Starting Gemini...

:: Use PowerShell to read file contents and start gemini with the combined prompt.
:: This version is on a single line to avoid CMD line continuation issues.
powershell -NoProfile -ExecutionPolicy Bypass -Command "$files = '%RULES_FILES%'.Split(' '); $rules = foreach ($f in $files) { Get-Content (Join-Path '%RULES_DIR%' $f) -Raw }; $prompt = '아래 규칙은 이 세션 전체에 대해 **최우선으로 적용**된다.`n모든 응답은 반드시 이 규칙을 준수해야 한다.`n규칙을 위반하려는 경우, 응답을 중단하고 규칙을 재적용한다.`n`n====================`n' + ($rules -join '`n') + '`n====================`n`n# REQUIREMENTS`n- 모든 코드에는 주석을 달아야 한다.`n`n이제부터 사용자 입력을 기다린다.'; gemini --yolo -i $prompt"
