#!/usr/bin/env bash

# 스크립트 실행 중 오류가 발생하면 즉시 중단합니다.
set -e

# --- 1. 규칙 파일 정의 ---
# .bat 파일과 동일하게, 규칙 파일들을 배열로 관리하여 유지보수성을 높입니다.
RULES_DIR="docs/prompts"
RULES_FILES=(
  "coding_agent_system.md"
  "coding_agent_typescript.md"
  "coding_agent_vue3_script.md"
  "3.AI.md"
  "5.dev.md"
)

# --- 2. 규칙 파일 존재 여부 확인 ---
echo "[OK] 모든 규칙 파일의 존재 여부를 확인합니다..."
for file in "${RULES_FILES[@]}"; do
  if [ ! -f "$RULES_DIR/$file" ]; then
    echo "[X] 규칙 파일을 찾을 수 없습니다: $RULES_DIR/$file"
    exit 1
  fi
done

# --- 3. .agents 디렉토리 생성 ---
# .bat 파일과 동일하게, .agents 디렉토리가 없으면 생성합니다.
mkdir -p ".agents"

echo "[OK] 모든 규칙 파일을 찾았습니다. .agents/AGENTS.md 파일로 병합합니다..."

# --- 4. 규칙 내용 병합 및 파일 저장 ---
# .bat 파일의 PowerShell 로직과 동일하게 동작하도록 구현합니다.
# 모든 규칙 파일의 내용을 한 번에 읽어옵니다.
RULES_CONTENT=$(cat "${RULES_FILES[@]/#/$RULES_DIR/}")

# 헤더, 병합된 규칙, 푸터를 포함한 전체 컨텍스트를 구성합니다.
CONTEXT="아래 규칙은 이 세션 전체에 대해 **최우선으로 적용**된다.
모든 응답은 반드시 이 규칙을 준수해야 한다.
규칙을 위반하려는 경우, 응답을 중단하고 규칙을 재적용한다.

====================
${RULES_CONTENT}
====================

# REQUIREMENTS
- 모든 코드에는 주석을 달아야 한다.

이제부터 사용자 입력을 기다린다."

# 구성된 전체 컨텍스트를 .agents/AGENTS.md 파일에 덮어씁니다.
echo "$CONTEXT" > ".agents/AGENTS.md"

echo "[OK] 규칙이 Workspace Customization (.agents/AGENTS.md)에 성공적으로 로드되었습니다."
echo ""

# --- 5. 실행 방식 선택 ---
echo "=================================================="
echo "gemini 실행 방식을 선택하세요:"
echo "[1] 현재 창에서 실행 (기본값)"
echo "[2] 백그라운드에서 실행"
echo "=================================================="
read -p "선택 (1 또는 2): " CHOICE
CHOICE=${CHOICE:-1} # 사용자가 아무것도 입력하지 않으면 기본값으로 1을 사용합니다.

# --- 6. Gemini 실행 ---
if [ "$CHOICE" = "1" ]; then
  # echo "[OK] 백그라운드에서 gemini를 시작합니다..."
  # .bat 파일과 동일하게 agy를 실행합니다.
  # agy는 .agents/AGENTS.md 파일을 자동으로 읽으므로 -i 옵션이 필요 없습니다.
  agy --dangerously-skip-permissions
  # echo "백그라운드 프로세스 ID: $!"
else
  echo "[OK] 새창에서 gemini를 시작합니다..."
  # .bat 파일과 동일하게 agy를 실행합니다.
  xdg-terminal-exec bash -c agy --dangerously-skip-permissions
fi
