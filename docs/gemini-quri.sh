#!/usr/bin/env bash

set -e

RULES_FILE="prompts/coding_agent_system.md"

if [ ! -f "$RULES_FILE" ]; then
  echo "❌ 규칙 파일을 찾을 수 없습니다: $RULES_FILE"
  exit 1
fi


RULES_FILE2="prompts/coding_agent_vue3_script.md"
if [ ! -f "$RULES_FILE2" ]; then
  echo "❌ 규칙 파일을 찾을 수 없습니다: $RULES_FILE2"
  exit 1
fi

RULES="$(cat "$RULES_FILE")"
RULES2="$(cat "$RULES_FILE2")"

gemini -i "
아래 규칙은 이 세션 전체에 대해 **최우선으로 적용**된다.
모든 응답은 반드시 이 규칙을 준수해야 한다.
규칙을 위반하려는 경우, 응답을 중단하고 규칙을 재적용한다.


====================
$RULES
$RULES2
====================

# REQUIREMENTS
- 백엔드: .NET 8
- 프론트엔드: Vue3, praimevue4.4, tailwindcss3.4
- DB: PostgreSQL
- ORM: EF Core
- 모든 코드에는 주석을 달아야 한다.
- mkdir, npm, dotnet 등등의 명령이 필요함에 권한 획득은 이번 session에 한하여 모두 허용한다. 사용자의 action 이 필요하면 시작과 동시에 권한을 요청하라.

이제부터 사용자 입력을 기다린다.
"
