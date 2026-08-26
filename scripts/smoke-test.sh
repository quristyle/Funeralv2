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
#   5. 로그인한 <img src> 가 파일 읽기 쿠키로 통하는가
#   6. 파일 서비스의 쓰기가 막혀 있는가
#   7. 로그인 시도 제한이 걸리는가
#
# 사용:  ./scripts/smoke-test.sh
# 종료코드: 0 정상 / 1 실패 항목 있음

set -u

GATEWAY_PORT=5265
INTERNAL_PORTS=(5264 5320 5350 5400 5450 5480)
HOST_IP=$(hostname -I 2>/dev/null | awk '{print $1}')

pass=0; fail=0
ok()   { printf "  \033[32m✓\033[0m %s\n" "$1"; pass=$((pass+1)); }
ng()   { printf "  \033[31m✗\033[0m %s\n" "$1"; fail=$((fail+1)); }
head_() { printf "\n\033[1m%s\033[0m\n" "$1"; }

code() { curl -s -m "${3:-8}" -o /dev/null -w "%{http_code}" "$1" ${2:+-H "$2"} 2>/dev/null; }

# ── 1. 서비스 기동 ──────────────────────────────────────────
# 포트가 열렸는지가 아니라 /health 가 200 을 주는지로 본다.
# 예전에는 `ss -ltn` 을 썼는데 윈도우(Git Bash)에는 `ss` 가 없어서
# 서비스가 다 떠 있어도 전부 실패로 나왔다. curl 은 어디서나 있다.
head_ "1. 서비스 기동"
for p in $GATEWAY_PORT "${INTERNAL_PORTS[@]}"; do
  c=$(curl -s -m 4 -o /dev/null -w "%{http_code}" "http://localhost:$p/health" 2>/dev/null)
  if [ "$c" = "200" ]; then ok "포트 $p 응답 (health 200)"
  else ng "포트 $p 죽어 있음 (HTTP $c)"; fi
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

# 소개 사이트. 조회는 익명으로 열려 있고 관리는 막혀 있어야 한다.
# 라우트가 없거나 서비스가 죽었으면 404/502 가 오므로 200/401 과 구분이 된다.
c=$(code "http://localhost:$GATEWAY_PORT/api/site/sections?locale=ko")
[ "$c" = "200" ] && ok "소개 사이트 조회 — 비인증 허용 (200)" \
                 || ng "소개 사이트 조회 (HTTP $c · 200 이어야 함)"

c=$(code "http://localhost:$GATEWAY_PORT/api/site/admin/inquiries")
[ "$c" = "401" ] && ok "소개 사이트 관리 — 인증 요구 (401)" \
                 || ng "소개 사이트 관리 (HTTP $c · 401 이어야 함)"

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
  "/api/projmng/Dev/sql" \
  "/api/site/admin/inquiries" ; do
  c=$(code "http://localhost:$GATEWAY_PORT$path")
  if [ "$c" = "401" ] || [ "$c" = "403" ]; then ok "$path → $c"
  else ng "$path → $c (401/403 이어야 함)"; fi
done

# ── 5. 파일 읽기 쿠키 ───────────────────────────────────────
# 화면은 사진을 <img src="/api/file/thumbnail/{id}"> 로 그린다. 브라우저는 그런 태그에
# Authorization 헤더를 붙이지 않으므로, 로그인 때 같은 토큰을 `jsini_file_at` 쿠키로도 심는다.
# 게이트웨이는 그 쿠키를 **파일 읽기 경로에서만** 신원으로 받는다.
# 쓰기에도 받아 주면 남의 사이트가 우리 주소로 요청을 걸어 파일을 지울 수 있다(CSRF).
#
# 이 절은 로그인을 한 번 한다. 그래서 6절(시도 제한)보다 앞에 둔다 — 뒤에 두면 429 에 막힌다.
head_ "5. 파일 읽기 쿠키 (로그인한 <img src> 가 통하는가)"
NIL_ID=00000000-0000-0000-0000-000000000000
JAR=$(mktemp)
SMOKE_USER=${SMOKE_USER:-administrator}
curl -s -m 15 -o /dev/null -c "$JAR" -X POST \
  "http://localhost:$GATEWAY_PORT/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"$SMOKE_USER\",\"password\":\"x\"}" 2>/dev/null

if grep -q "jsini_file_at" "$JAR" 2>/dev/null; then
  ok "로그인이 jsini_file_at 쿠키를 심는다"

  # 쿠키만으로(Authorization 없이) 읽기 경로가 통해야 한다. 401 이면 <img src> 가 깨진다.
  c=$(curl -s -m 8 -o /dev/null -w "%{http_code}" -b "$JAR" \
      "http://localhost:$GATEWAY_PORT/api/file/download/id/$NIL_ID" 2>/dev/null)
  [ "$c" = "401" ] && ng "쿠키만으로 읽기가 401 — 포털의 <img src> 가 깨진다" \
                   || ok "쿠키만으로 읽기 통과 (HTTP $c)"

  # 반대로 쿠키가 쓰기까지 인증해 주면 안 된다.
  c=$(curl -s -m 8 -o /dev/null -w "%{http_code}" -b "$JAR" -X DELETE \
      "http://localhost:$GATEWAY_PORT/api/file/$NIL_ID" 2>/dev/null)
  [ "$c" = "401" ] && ok "쿠키로는 삭제 안 됨 (401) — CSRF 차단" \
                   || ng "쿠키가 삭제까지 인증한다 (HTTP $c) — CSRF 위험"

  c=$(curl -s -m 8 -o /dev/null -w "%{http_code}" -b "$JAR" \
      "http://localhost:$GATEWAY_PORT/api/file/metadata/$NIL_ID" 2>/dev/null)
  [ "$c" = "401" ] && ok "쿠키로는 메타데이터 안 됨 (401)" \
                   || ng "쿠키가 메타데이터까지 인증한다 (HTTP $c · 읽기 경로만 받아야 함)"
else
  echo "  (로그인이 쿠키를 심지 않았다 — AuthServer·게이트웨이가 예전 바이너리이거나"
  echo "   SMOKE_USER=$SMOKE_USER 로 로그인이 안 되는 상태다. 건너뜀)"
fi
rm -f "$JAR"

# ── 6. 파일 서비스 경계 ─────────────────────────────────────
# 예전에는 `/api/file/{**}` 포괄 라우트가 Anonymous 여서 토큰 없이
# 아무 파일이나 지우고(DELETE) 올릴 수(POST) 있었다. 이제는 막혀야 한다.
head_ "6. 파일 서비스 경계"
verb() { curl -s -m 8 -o /dev/null -w "%{http_code}" -X "$1" "$2" 2>/dev/null; }

c=$(verb DELETE "http://localhost:$GATEWAY_PORT/api/file/$NIL_ID")
[ "$c" = "401" ] && ok "파일 삭제 — 인증 요구 (401)" \
                 || ng "파일 삭제가 토큰 없이 통과한다 (HTTP $c · 401 이어야 함)"

c=$(verb POST "http://localhost:$GATEWAY_PORT/api/file/upload")
[ "$c" = "401" ] && ok "파일 업로드 — 인증 요구 (401)" \
                 || ng "파일 업로드가 토큰 없이 통과한다 (HTTP $c · 401 이어야 함)"

c=$(verb PUT "http://localhost:$GATEWAY_PORT/api/file/public/$NIL_ID?value=true")
[ "$c" = "401" ] && ok "공개여부 변경 — 인증 요구 (401)" \
                 || ng "공개여부 변경이 토큰 없이 통과한다 (HTTP $c · 401 이어야 함)"

# 읽기 라우트는 게이트웨이에서 막지 않는다. 브라우저가 <img src="/api/file/thumbnail/{id}">
# 로 직접 부르므로 401 로 끊으면 화면이 깨진다. 파일 단위 판정은 FileServer 가 한다
# (`Files:RequirePublicFlagForAnonymous`) — is_public 이 아니면 404 를 준다.
# 403 이 아니라 404 인 이유: 403 은 "그 아이디의 파일은 있다" 를 알려 준다.
c=$(code "http://localhost:$GATEWAY_PORT/api/file/download/id/$NIL_ID")
[ "$c" = "401" ] && ng "읽기 라우트가 게이트웨이에서 401 — 포털의 <img src> 가 깨진다" \
                 || ok "읽기 라우트는 익명으로 닿는다, 판정은 FileServer 가 (HTTP $c)"

# ── 7. 레이트 리미팅 ────────────────────────────────────────
# 로그인·비밀번호 초기화는 무차별 대입에 노출되므로 IP 단위로 제한한다.
# 존재하지 않는 계정으로만 시도하므로 실제 계정에는 영향이 없다.
#
# 맨 뒤에 두는 이유: 이 절이 IP 의 시도 횟수를 태워 버린다. 앞에 두면
# 5절의 로그인이 429 에 막혀 쿠키 검사를 못 한다.
head_ "7. 로그인 시도 제한 (11회째부터 막혀야 함)"
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
