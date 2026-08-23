#!/bin/bash
# ============================================================
# JSini 관리 포털 — 스모크 테스트
# ============================================================
#
# 배포하거나 설정을 바꾼 뒤 이 스크립트만 돌리면 큰 사고는 걸러진다.
# 확인 항목
#   1. 서비스가 떠 있는가
#   2. 내부 서비스가 외부에 노출되지 않았는가 (게이트웨이 우회 차단)
#   3. 게이트웨이 주요 경로가 살아 있는가
#   4. 인증 경계가 지켜지는가 (토큰 없으면 401)
#
# 사용:  ./scripts/smoke-test.sh
# 종료코드: 0 정상 / 1 실패 항목 있음

set -u

GATEWAY_PORT=5265
INTERNAL_PORTS=(5264 5320 5350 5400 5450)
HOST_IP=$(hostname -I 2>/dev/null | awk '{print $1}')

pass=0; fail=0
ok()   { printf "  \033[32m✓\033[0m %s\n" "$1"; pass=$((pass+1)); }
ng()   { printf "  \033[31m✗\033[0m %s\n" "$1"; fail=$((fail+1)); }
head_() { printf "\n\033[1m%s\033[0m\n" "$1"; }

code() { curl -s -m "${3:-8}" -o /dev/null -w "%{http_code}" "$1" ${2:+-H "$2"} 2>/dev/null; }

# ── 1. 서비스 기동 ──────────────────────────────────────────
head_ "1. 서비스 기동"
for p in $GATEWAY_PORT "${INTERNAL_PORTS[@]}"; do
  if ss -ltn 2>/dev/null | grep -q ":$p "; then ok "포트 $p 응답"; else ng "포트 $p 죽어 있음"; fi
done

# ── 2. 게이트웨이 우회 차단 ─────────────────────────────────
# 내부 서비스는 게이트웨이가 붙여 주는 X-User-* 헤더를 신원으로 믿는다.
# 외부에서 직접 닿으면 헤더 위조로 아무 계정이나 사칭할 수 있으므로
# 루프백에만 열려 있어야 한다.
head_ "2. 내부 서비스가 외부에 노출되지 않았는가"
if [ -z "$HOST_IP" ]; then
  echo "  (이 장비의 외부 IP 를 찾지 못해 건너뜀)"
else
  for p in "${INTERNAL_PORTS[@]}"; do
    c=$(curl -s -m 4 -o /dev/null -w "%{http_code}" \
        -H "X-User-Id: smoke-test" "http://$HOST_IP:$p/health" 2>/dev/null)
    if [ "$c" = "000" ]; then ok "포트 $p 외부 차단됨"
    else ng "포트 $p 가 외부($HOST_IP)에 열려 있다 (HTTP $c) — 인증 우회 위험"; fi
  done
fi

# ── 3. 게이트웨이 경로 ──────────────────────────────────────
head_ "3. 게이트웨이 주요 경로"
c=$(code "http://localhost:$GATEWAY_PORT/health")
[ "$c" = "200" ] && ok "게이트웨이 health (200)" || ng "게이트웨이 health (HTTP $c)"

c=$(code "http://localhost:$GATEWAY_PORT/api/auth/notices/popup/public")
[ "$c" = "200" ] && ok "공개 공지 조회 — 비인증 허용 (200)" || ng "공개 공지 조회 (HTTP $c)"

# 헬프데스크는 인증된 요청만 통과시킨다(D10-A). 라우팅이 살아 있으면 401 이 온다.
# 라우트가 없거나 서비스가 죽었으면 404/502 가 오므로 구분이 된다.
c=$(code "http://localhost:$GATEWAY_PORT/api/helpdesk/schedules")
[ "$c" = "401" ] && ok "헬프데스크 라우팅 — 인증 요구 (401)" \
                 || ng "헬프데스크 라우팅 (HTTP $c · 401 이어야 함)"

# 프로젝트관리도 같다. 익명으로 열어 둔 경로가 없다.
c=$(code "http://localhost:$GATEWAY_PORT/api/projmng/Proj")
[ "$c" = "401" ] && ok "프로젝트관리 라우팅 — 인증 요구 (401)" \
                 || ng "프로젝트관리 라우팅 (HTTP $c · 401 이어야 함)"

# ── 4. 인증 경계 ────────────────────────────────────────────
head_ "4. 인증 경계 (토큰 없이 접근하면 막혀야 함)"
for path in \
  "/api/auth/menu/permissions" \
  "/api/auth/notices" \
  "/api/auth/release/targets" \
  "/api/funeral/building/room/list" \
  "/api/helpdesk/companys" \
  "/api/helpdesk/admins" \
  "/api/projmng/Proj" \
  "/api/projmng/Dev/sql" ; do
  c=$(code "http://localhost:$GATEWAY_PORT$path")
  if [ "$c" = "401" ] || [ "$c" = "403" ]; then ok "$path → $c"
  else ng "$path → $c (401/403 이어야 함)"; fi
done

# ── 5. 레이트 리미팅 ────────────────────────────────────────
# 로그인·비밀번호 초기화는 무차별 대입에 노출되므로 IP 단위로 제한한다.
# 존재하지 않는 계정으로만 시도하므로 실제 계정에는 영향이 없다.
head_ "5. 로그인 시도 제한 (11회째부터 막혀야 함)"
limited=0
for i in $(seq 1 12); do
  c=$(curl -s -m 6 -o /dev/null -w "%{http_code}" -X POST \
      "http://localhost:$GATEWAY_PORT/api/auth/login" \
      -H "Content-Type: application/json" \
      -d '{"username":"__smoke_test_nobody__","password":"x"}' 2>/dev/null)
  [ "$c" = "429" ] && limited=1
done
if [ "$limited" = "1" ]; then ok "로그인 시도 제한 동작 (429 반환)"
else ng "로그인 시도 제한이 걸리지 않는다 — 무차별 대입에 무방비"; fi

# ── 결과 ────────────────────────────────────────────────────
printf "\n\033[1m결과: 통과 %d · 실패 %d\033[0m\n" "$pass" "$fail"
[ "$fail" -eq 0 ] || exit 1
