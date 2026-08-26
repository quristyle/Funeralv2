#!/bin/bash
# ============================================================
# JSini 관리 포털 — 개발 서버 기동 스크립트 (macOS)
# ============================================================
#
# 사용법 (backend_run_ubuntu.sh 와 동일하다)
#   ./backend_run_mac.sh                 전체 재기동 (중지 → 빌드 → 기동)
#   ./backend_run_mac.sh auth            AuthServer 만 재기동
#   ./backend_run_mac.sh auth file       여러 개 지정도 된다
#   ./backend_run_mac.sh stop auth       AuthServer 만 중지
#   ./backend_run_mac.sh allstop         전체 중지
#   ./backend_run_mac.sh status          지금 무엇이 떠 있는지 확인
#   ./backend_run_mac.sh help            사용법
#
# 한 서비스만 재기동할 때는 그 서비스만 빌드한다.
#
# ── 리눅스판과 다른 점 ──────────────────────────────────────
#   · 터미널은 iTerm 을 쓰고, 없으면 기본 Terminal.app 으로 넘어간다
#   · macOS 에는 /proc 이 없어 프로세스의 작업 디렉터리를 lsof 로 읽는다
#   · 포트 확인은 ss 대신 lsof 를 쓴다
# ============================================================

#############################################
# 프로젝트 루트 경로 (스크립트 위치 기준)
#############################################
ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"

SECRETS_FILE="$ROOT_DIR/scripts/secrets.env"   # 있으면 서비스에 환경변수로 실어 준다 (git 제외)
FRONTEND_DIR="$ROOT_DIR/fronts"

#############################################
# 서비스 목록
#############################################
#
# 형식: 이름|표시이름|상대경로|포트|SERVER_NAME
#
# 서비스를 추가하려면 이 표에 한 줄만 더하면 된다.
# 빌드·기동·중지·상태 확인이 모두 이 표를 읽는다.
SERVICES=(
  "gateway|API Gateway|ApiGateway|5265|GATEWAY"
  "auth|Auth Server|microservices/AuthServer|5264|AUTH"
  "funeral|funeralv2 API|microservices/funeralv2Api|5320|FUNERALV2"
  "ai|AI Agent Server|microservices/AIAgentServer|5029|AI_AGENT"
  "file|File Server|microservices/FileServer|5350|FILE_API"
  "helpdesk|HelpDesk Server|microservices/HelpDeskServer|5400|HELPDESK"
  "projmng|ProjMng Server|microservices/ProjMngServer|5450|PROJMNG"
  "site|Site Server|microservices/SiteServer|5480|SITE_API"
)

# 프론트엔드는 dotnet 서비스가 아니라 따로 다룬다.
# 형식: 이름|표시이름|상대경로|포트|pnpm 필터
#
# 프론트가 둘이 되면서 변수 하나(FRONT_KEY)로는 표현이 안 됐다. 표로 바꿨다.
# 셋째가 붙어도 여기에 한 줄만 더하면 된다.
#
# 이름이 바뀌었다: 예전 `front` 는 이제 `portal` 이다. `web` 이 회사 소개 사이트다.
FRONTS=(
  "portal|Portal Frontend (vite)|fronts|5555|@vben/jsini-portal"
  "web|Site Frontend (vite)|fronts|5556|@jsini/site"
)

front_keys()   { printf '%s
' "${FRONTS[@]}" | cut -d'|' -f1; }
front_field()  {   # front_field <이름> <필드번호>
    local key="$1" idx="$2" row
    for row in "${FRONTS[@]}"; do
        if [ "${row%%|*}" = "$key" ]; then
            printf '%s
' "$row" | cut -d'|' -f"$idx"
            return 0
        fi
    done
    return 1
}
is_front()     { front_field "$1" 1 >/dev/null 2>&1; }
front_label()  { front_field "$1" 2; }
front_dir()    { echo "$ROOT_DIR/$(front_field "$1" 3)"; }
front_port()   { front_field "$1" 4; }
front_filter() { front_field "$1" 5; }

#############################################
# 서비스 표 조회 도우미
#############################################
svc_field() {   # svc_field <이름> <필드번호>
    local key="$1" idx="$2" row
    for row in "${SERVICES[@]}"; do
        [ "${row%%|*}" = "$key" ] && { echo "$row" | cut -d'|' -f"$idx"; return 0; }
    done
    return 1
}

svc_label() { svc_field "$1" 2; }
svc_dir()   { echo "$ROOT_DIR/$(svc_field "$1" 3)"; }
svc_port()  { svc_field "$1" 4; }
svc_name()  { svc_field "$1" 5; }

svc_keys() {
    local row
    for row in "${SERVICES[@]}"; do echo "${row%%|*}"; done
}

svc_exists() {
    is_front "$1" && return 0
    svc_field "$1" 1 >/dev/null 2>&1
}

#############################################
# 터미널 실행 (iTerm 우선, 없으면 Terminal.app)
#############################################
run_terminal() {
    local cmd="$1"

    if [ -d "/Applications/iTerm.app" ] || osascript -e 'id of application "iTerm"' >/dev/null 2>&1; then
        osascript <<EOF
tell application "iTerm"
    activate
    if (count of windows) = 0 then
        create window with default profile
        tell current session of current window
            write text "$cmd"
        end tell
    else
        tell current window
            create tab with default profile
            tell current session
                write text "$cmd"
            end tell
        end tell
    end if
end tell
EOF
        return
    fi

    # iTerm 이 없으면 기본 터미널을 쓴다.
    osascript <<EOF
tell application "Terminal"
    activate
    do script "$cmd"
end tell
EOF
}

#############################################
# 프로세스 찾기 / 중지
#############################################
#
# 서비스 하나만 골라 죽여야 하므로 `pkill -f "dotnet watch run"` 은 쓸 수 없다.
# 그 명령줄은 모든 서비스가 똑같이 갖고 있어 구분이 안 된다.
#
# 대신 **작업 디렉터리(cwd)** 로 찾는다. 한 서비스를 띄우면
#   터미널 → 셸(cd 서비스디렉터리) → dotnet watch → dotnet run → 서비스
# 이렇게 겹쳐 뜨는데 이 넷이 모두 같은 cwd 를 갖는다.
#
# 리눅스는 /proc/<pid>/cwd 를 읽으면 되지만 macOS 에는 /proc 이 없다.
# 그래서 lsof 로 읽는다 — 후보를 먼저 좁힌 뒤 하나씩 확인해 느려지지 않게 한다.

# 이 스크립트 자신과 조상 프로세스는 절대 죽이지 않는다.
self_chain() {
    local pid=$$
    while [ -n "$pid" ] && [ "$pid" != "0" ] && [ "$pid" != "1" ]; do
        echo "$pid"
        pid="$(ps -o ppid= -p "$pid" 2>/dev/null | tr -d ' ')"
    done
}

pid_cwd() {   # pid_cwd <pid>
    lsof -a -p "$1" -d cwd -Fn 2>/dev/null | awk '/^n/ {print substr($0,2); exit}'
}

pids_in_dir() {   # pids_in_dir <절대경로>
    local target="$1" pid cwd protected

    # 심볼릭 링크(/tmp → /private/tmp 등)를 풀어 비교 기준을 맞춘다.
    target="$(cd "$target" 2>/dev/null && pwd -P)" || return 0

    protected=" $(self_chain | tr '\n' ' ') "

    # 개발 서버로 볼 수 있는 것만 후보로 삼는다. 그 디렉터리에서 열어 둔
    # 편집기나 셸까지 죽이면 안 된다.
    for pid in $(pgrep -f 'dotnet|pnpm|vite|node' 2>/dev/null); do
        case "$protected" in *" $pid "*) continue ;; esac

        cwd="$(pid_cwd "$pid")"
        [ -n "$cwd" ] || continue
        cwd="$(cd "$cwd" 2>/dev/null && pwd -P)" || continue
        [ "$cwd" = "$target" ] && echo "$pid"
    done
}

pid_on_port() {   # pid_on_port <포트>
    lsof -nP -iTCP:"$1" -sTCP:LISTEN -t 2>/dev/null | head -1
}

port_is_open() {
    [ -n "$(pid_on_port "$1")" ]
}

# 디렉터리로 찾은 프로세스를 정리한다.
# 부모(dotnet watch)를 먼저 보내지 않으면 자식을 죽여도 watch 가 다시 띄운다.
# 그래서 PID 가 큰 것(자식)부터가 아니라 **작은 것(부모)부터** 보낸다.
stop_dir() {   # stop_dir <절대경로> <표시이름>
    local dir="$1" label="$2" pids left

    pids="$(pids_in_dir "$dir" | sort -n)"
    if [ -z "$pids" ]; then
        echo "   · $label — 실행 중이 아님"
        return 0
    fi

    # shellcheck disable=SC2086
    kill $pids 2>/dev/null
    sleep 2

    left="$(pids_in_dir "$dir" | sort -n)"
    if [ -n "$left" ]; then
        # shellcheck disable=SC2086
        kill -9 $left 2>/dev/null
        sleep 1
    fi

    echo "   ✓ $label 종료"
}

stop_service() {   # stop_service <이름>
    local key="$1" port pid

    if is_front "$key"; then
        # **디렉터리로 찾지 않는다.** 프론트가 둘 다 `fronts` 아래에서 돌기 때문에
        # 작업 디렉터리로 고르면 한쪽을 내릴 때 다른 쪽까지 죽는다. 포트로만 고른다.
        FRONT_STOP_LABEL="$(front_label "$key")"
        # vite 는 fronts/apps/... 아래에서 돌 수도 있다. 포트로 한 번 더 확인한다.
        pid="$(pid_on_port "$(front_port "$key")")"
        if [ -n "$pid" ]; then
            kill "$pid" 2>/dev/null && sleep 1
            kill -9 "$pid" 2>/dev/null
            echo "   ✓ $FRONT_STOP_LABEL 종료"
        else
            echo "   · $FRONT_STOP_LABEL 은 실행 중이 아님"
        fi
        return 0
    fi

    stop_dir "$(svc_dir "$key")" "$(svc_label "$key")"

    # 디렉터리로 못 찾은 경우를 위한 보루. 포트를 잡고 있으면 그것도 정리한다.
    port="$(svc_port "$key")"
    pid="$(pid_on_port "$port")"
    if [ -n "$pid" ]; then
        echo "     (포트 $port 를 잡고 있던 $pid 도 정리)"
        kill "$pid" 2>/dev/null && sleep 1
        kill -9 "$pid" 2>/dev/null
    fi
}

#############################################
# 빌드 / 기동
#############################################
build_service() {   # build_service <이름>
    local key="$1"

    if is_front "$key"; then
        # 프론트는 워크스페이스 하나를 공유한다. 여러 개를 함께 띄워도 설치는 한 번이면 된다.
        if [ -n "${PNPM_INSTALLED:-}" ]; then
            echo "   · $(front_label "$key") 의존성 설치 (이미 했음)"
            return 0
        fi
        echo "   · $(front_label "$key") 의존성 설치..."
        (cd "$FRONTEND_DIR" && pnpm install) || return 1
        PNPM_INSTALLED=1
        return 0
    fi

    echo "   · $(svc_label "$key") 빌드..."
    (cd "$(svc_dir "$key")" && dotnet build) || return 1
}

start_service() {   # start_service <이름>
    local key="$1"

    if is_front "$key"; then
        local nvm_init='export NVM_DIR=\"$HOME/.nvm\"; [ -s \"$NVM_DIR/nvm.sh\" ] && . \"$NVM_DIR/nvm.sh\";'
        local dir filter
        dir="$(front_dir "$key")"
        filter="$(front_filter "$key")"
        run_terminal "${nvm_init} cd \\\"$dir\\\" && pnpm --filter $filter dev"
        echo "   ✓ $(front_label "$key") 기동"
        return 0
    fi

    # scripts/secrets.env 가 있으면 환경변수로 실어 준다.
    # 없으면 아무 일도 하지 않고 appsettings.json 값이 그대로 쓰인다.
    # (ASP.NET Core 는 Jwt__Key 같은 환경변수를 Jwt:Key 설정으로 읽고,
    #  환경변수가 appsettings 보다 우선한다.)
    run_terminal "cd \\\"$(svc_dir "$key")\\\" && { [ -f \\\"$SECRETS_FILE\\\" ] && set -a && . \\\"$SECRETS_FILE\\\" && set +a; }; SERVER_NAME=$(svc_name "$key") DOTNET_WATCH_HOT_RELOAD=0 dotnet watch run --no-hot-reload"
    echo "   ✓ $(svc_label "$key") 기동 (포트 $(svc_port "$key"))"
}

#############################################
# 명령
#############################################
print_usage() {
    cat <<EOF
사용법: $(basename "$0") [명령 | 서비스이름...]

  (없음)              전체 재기동 — 중지 → 빌드 → 기동
  all                 위와 같음
  <서비스> [<서비스>] 지정한 서비스만 재기동 (그 서비스만 빌드한다)
  stop <서비스>...    지정한 서비스만 중지
  allstop             전체 중지
  status              지금 무엇이 떠 있는지 확인
  list                서비스 이름 목록
  help                이 도움말

서비스 이름
EOF
    local key
    for key in $(svc_keys); do
        printf "  %-10s %s (포트 %s)\n" "$key" "$(svc_label "$key")" "$(svc_port "$key")"
    done
    for key in $(front_keys); do
        printf "  %-10s %s (포트 %s)\n" "$key" "$(front_label "$key")" "$(front_port "$key")"
    done
    cat <<EOF

예시
  $(basename "$0") auth              AuthServer 만 다시 띄운다
  $(basename "$0") site web          소개 사이트 백엔드와 프론트를 다시 띄운다
  $(basename "$0") projmng portal    ProjMng 와 업무 포털을 다시 띄운다
  $(basename "$0") stop helpdesk     헬프데스크만 내린다
  $(basename "$0") allstop           전부 내린다
EOF
}

# 색 코드는 자릿수에 잡히므로 상태 칸의 폭은 색을 뺀 글자수로 맞춘다.
if [ -t 1 ]; then
    C_UP=$'\033[32m'; C_DOWN=$'\033[90m'; C_OFF=$'\033[0m'
else
    C_UP=""; C_DOWN=""; C_OFF=""
fi

print_status_row() {   # print_status_row <이름> <포트> <상태(색포함)> <설명>
    local plain pad
    plain="$(printf '%s' "$3" | sed 's/\x1b\[[0-9;]*m//g')"
    pad=$((6 - ${#plain}))
    [ "$pad" -lt 0 ] && pad=0
    printf "  %-10s %-6s %s%*s %s\n" "$1" "$2" "$3" "$pad" "" "$4"
}

print_status() {
    # 한글은 한 글자가 2칸을 차지하는데 printf 는 바이트로 세므로,
    # 자릿수를 맞춰야 하는 칸에는 ASCII 만 쓴다.
    echo "===================================================="
    echo "   서비스 상태"
    echo "===================================================="
    printf "  %-10s %-6s %-6s %s\n" "name" "port" "state" "service"
    printf "  %-10s %-6s %-6s %s\n" "----------" "----" "-----" "-------"

    local key port state
    for key in $(svc_keys) $(front_keys); do
        if is_front "$key"; then
            port="$(front_port "$key")"
        else
            port="$(svc_port "$key")"
        fi

        if port_is_open "$port"; then
            state="$(printf '%s' "${C_UP}UP${C_OFF}")"
        else
            state="$(printf '%s' "${C_DOWN}DOWN${C_OFF}")"
        fi

        if is_front "$key"; then
            print_status_row "$key" "$port" "$state" "$(front_label "$key")"
        else
            print_status_row "$key" "$port" "$state" "$(svc_label "$key")"
        fi
    done
    echo
}

# 지정한 서비스들을 재기동한다.
restart_services() {   # restart_services <이름>...
    local targets=("$@") key

    echo ">>> [1/3] 중지"
    for key in "${targets[@]}"; do
        stop_service "$key"
    done

    echo
    echo ">>> [2/3] 빌드"
    for key in "${targets[@]}"; do
        if ! build_service "$key"; then
            echo "❌ $(svc_label "$key" 2>/dev/null || echo "$key") 빌드 실패. 기동하지 않습니다."
            exit 1
        fi
    done

    echo
    echo ">>> [3/3] 기동"
    for key in "${targets[@]}"; do
        start_service "$key"
    done

    echo
    echo "===================================================="
    echo "완료: ${targets[*]}"
    echo "===================================================="
}

#############################################
# 인자 해석
#############################################
COMMAND="${1:-all}"

case "$COMMAND" in
    help|-h|--help)
        print_usage
        exit 0
        ;;

    list)
        svc_keys
        front_keys
        exit 0
        ;;

    status)
        print_status
        exit 0
        ;;

    allstop)
        echo "===================================================="
        echo "   전체 중지"
        echo "===================================================="
        # 게이트웨이를 먼저 내려 외부 요청을 끊고 나머지를 정리한다.
        for key in $(svc_keys) $(front_keys); do
            stop_service "$key"
        done
        echo
        echo "✅ 전체 중지 완료."
        exit 0
        ;;

    stop)
        shift
        if [ $# -eq 0 ]; then
            echo "❌ 중지할 서비스를 지정하세요. 전체를 내리려면 allstop 입니다."
            echo
            print_usage
            exit 1
        fi
        for key in "$@"; do
            if ! svc_exists "$key"; then
                echo "❌ 알 수 없는 서비스: $key   (사용 가능: $(svc_keys | tr '\n' ' ')$(front_keys | tr '\n' ' '))"
                exit 1
            fi
        done
        echo "===================================================="
        echo "   중지: $*"
        echo "===================================================="
        for key in "$@"; do
            stop_service "$key"
        done
        echo
        echo "✅ 중지 완료."
        exit 0
        ;;

    all)
        # 인자가 없거나 all 이면 기존 동작 그대로 — 전체 중지 후 전체 빌드·기동.
        if [ $# -gt 1 ]; then
            echo "❌ all 은 다른 이름과 함께 쓸 수 없습니다."
            exit 1
        fi
        echo "===================================================="
        echo "   JSini 관리 포털 — 전체 빌드 및 시작"
        echo "===================================================="
        # shellcheck disable=SC2046
        restart_services $(svc_keys) $(front_keys)
        exit 0
        ;;

    *)
        # 서비스 이름들로 본다.
        for key in "$@"; do
            if ! svc_exists "$key"; then
                echo "❌ 알 수 없는 서비스: $key"
                echo
                print_usage
                exit 1
            fi
        done
        echo "===================================================="
        echo "   재기동: $*"
        echo "===================================================="
        restart_services "$@"
        exit 0
        ;;
esac
