#!/usr/bin/env bash

set -e

RULES_FILE="prompts/coding_agent_system.md"

if [ ! -f "$RULES_FILE" ]; then
  echo "❌ 규칙 파일을 찾을 수 없습니다: $RULES_FILE"
  exit 1
fi


RULES_FILE2="prompts/coding_agent_typescript.md"
if [ ! -f "$RULES_FILE2" ]; then
  echo "❌ 규칙 파일을 찾을 수 없습니다: $RULES_FILE2"
  exit 1
fi


RULES_FILE3="prompts/coding_agent_vue3_script.md"
if [ ! -f "$RULES_FILE3" ]; then
  echo "❌ 규칙 파일을 찾을 수 없습니다: $RULES_FILE3"
  exit 1
fi


RULES_FILE4="prompts/3.AI.md"
if [ ! -f "$RULES_FILE4" ]; then
  echo "❌ 규칙 파일을 찾을 수 없습니다: $RULES_FILE4"
  exit 1
fi


RULES_FILE5="prompts/5.dev.md"
if [ ! -f "$RULES_FILE5" ]; then
  echo "❌ 규칙 파일을 찾을 수 없습니다: $RULES_FILE5"
  exit 1
fi



RULES="$(cat "$RULES_FILE")"
RULES2="$(cat "$RULES_FILE2")"
RULES3="$(cat "$RULES_FILE3")"
RULES4="$(cat "$RULES_FILE4")"
RULES5="$(cat "$RULES_FILE5")"

gemini --yolo -i "
아래 규칙은 이 세션 전체에 대해 **최우선으로 적용**된다.
모든 응답은 반드시 이 규칙을 준수해야 한다.
규칙을 위반하려는 경우, 응답을 중단하고 규칙을 재적용한다.


====================
$RULES
$RULES2
$RULES3
$RULES4
$RULES5
====================

# REQUIREMENTS
- 모든 코드에는 주석을 달아야 한다.

이제부터 사용자 입력을 기다린다.
"
