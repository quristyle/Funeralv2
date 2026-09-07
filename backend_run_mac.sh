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
# `front`·`portal`·`mfe` 는 blazor 의 옛 이름이라 그대로 받는다.
# `watch <서비스>` 는 일반 기동과 같다 — 모든 기동이 watch 기동이다.
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
  # 알림(푸시·이메일). 포털·장례식장·헬프데스크가 함께 쓴다 (결정 D8-A).
  "notify|Notification Server|microservices/NotificationServer|5460|NOTIFY"
  # 생활과환경(기상·생일). GHUB(SK가스 지허브)에서 이식했다.
  "life|LifeEnv Server|microservices/LifeEnvServer|5490|LIFEENV"
  # ── 프론트 ────────────────────────────────────────────────
  # 이제 프론트도 .NET 이다. Vue/pnpm 포털을 걷어내면서 pnpm 전용 처리(FRONTS 표 ·
  # 의존성 설치 · vite 기동)가 통째로 사라졌고, 두 프론트가 나머지 서비스와
  # 똑같이 이 표에서 다뤄진다.
  #
  # 업무 포털 셸 :5557 — 업무 MFE 여섯이 이 한 프로세스 안에 실린다.
  "blazor|Blazor 업무 포털|web/src/Shell/JSini.Web.Shell|5557|PORTAL_SHELL"
  # 회사 소개 사이트 :5556 — 정적 SSR 전용. 포털과 무관하고 인증도 없다.
  "web|회사 소개 사이트|web/src/Site/JSini.PublicSite|5556|PUBLIC_SITE"
)


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

# 옛 이름을 지금 이름으로 바꾼다. dev.bat 의 :svc_exists 가 가진 표와 같은 것이다.
#   front, portal → blazor   Blazor 셸이 Vue 포털을 대체했다
#   mfe           → blazor   업무 앱이 저마다 프로세스였을 때의 묶음 이름
# 루트 CLAUDE.md 가 "그대로 받아 준다" 고 적어 둔 이름들이다. 받지 않으면
# 이 스크립트 자신의 도움말에 실린 `projmng portal` 예시부터 실패한다.
svc_alias() {   # svc_alias <입력이름>  → 실제 이름
    case "$1" in
        front|portal|mfe) echo "blazor" ;;
        *)                echo "$1" ;;
    esac
}

svc_exists() {
    svc_field "$(svc_alias "$1")" 1 >/dev/null 2>&1
}

#############################################
# 터미널 실행 (iTerm 우선, 없으면 Terminal.app)
#############################################
#
# 리눅스판과 같은 이름의 have_terminal 을 둔다 — restart_services 가 기동 전에
# 이것을 불러 "터미널이 없는데 전부 기동됐다" 는 거짓 보고를 막는다.
# macOS 는 osascript 로 창을 띄우므로 그것만 있으면 된다.
have_terminal() {
    command -v osascript >/dev/null 2>&1
}

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
    for pid in $(pgrep -f 'dotnet' 2>/dev/null); do
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


    stop_dir "$(svc_dir "$key")" "$(svc_label "$key")"

    # 디렉터리로 못 찾은 경우를 위한 보루. 포트를 잡고 있으면 그것도 정리한다.
    port="$(svc_port "$key")"
    pid="$(pid_on_port "$port")"
    if [ -n "$pid" ]; then
        echo "     (포트 $port 를 잡고 있던 $pid 도 정리)"
        kill "$pid" 2>/dev/null
        sleep 1
        if [ -n "$(pid_on_port "$port")" ]; then
            kill -9 "$pid" 2>/dev/null
            sleep 1
        fi
    fi

    # 최종 판정은 **포트가 풀렸는지**로 한다. 프로세스를 못 찾았더라도 포트가
    # 물려 있으면 내려간 것이 아니다.
    #
    # 예전에는 이 함수가 늘 0 을 돌려줬다. 그래서 옛 프로세스가 포트를 쥔 채로
    # 남아 있어도 그 위에 새로 띄웠고, 새 쪽이 조용히 죽는 동안 화면에는
    # "✓ 기동" 이 찍혔다. dev.bat 이 STOP_FAILED 를 보는 이유가 이것이다.
    if port_is_open "$port"; then
        echo "   ✗ $(svc_label "$key") — 포트 $port 가 아직 열려 있다"
        return 1
    fi
    return 0
}

#############################################
# 빌드 / 기동
#############################################
build_service() {   # build_service <이름>
    local key="$1"


    echo "   · $(svc_label "$key") 빌드..."
    (cd "$(svc_dir "$key")" && dotnet build) || return 1
}

start_service() {   # start_service <이름>
    local key="$1"


    # scripts/secrets.env 가 있으면 환경변수로 실어 준다.
    # 없으면 아무 일도 하지 않고 appsettings.json 값이 그대로 쓰인다.
    # (ASP.NET Core 는 Jwt__Key 같은 환경변수를 Jwt:Key 설정으로 읽고,
    #  환경변수가 appsettings 보다 우선한다.)
    # 기동 명령에 대해 알아 둘 것 넷.
    #
    #  · `cd` 가 실패하면 그 자리에서 멈춘다. 예전에는 `cd ... && { ... }; dotnet ...`
    #    이라 `;` 뒤가 무조건 실행돼, 경로가 틀리면 엉뚱한 디렉터리에서 dotnet 이 돌았다.
    #  · `set +a` 는 `;` 로 떼어 둔다. `&& set +a` 였을 때는 secrets.env 의 마지막 줄이
    #    0 이 아닌 값을 남기면 export 가 켜진 채로 남았다.
    #  · hot reload 를 켠다. dev.bat 이 "절대 끄지 말라" 고 적어 둔 그 설정이다 —
    #    0 으로 박아 두면 watch 로 띄워도 고친 것이 반영되지 않는다.
    #  · rude edit(형식 추가·서명 변경)은 물어보지 않고 재기동한다. 물어보면
    #    누군가 그 창을 볼 때까지 서비스가 멈춰 있다.
    run_terminal "cd \\\"$(svc_dir "$key")\\\" || exit 1; { [ -f \\\"$SECRETS_FILE\\\" ] && set -a && . \\\"$SECRETS_FILE\\\"; set +a; }; SERVER_NAME=$(svc_name "$key") DOTNET_WATCH_HOT_RELOAD=1 DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1 dotnet watch run"
    echo "   ✓ $(svc_label "$key") 기동 (포트 $(svc_port "$key") — watch, 고친 것이 바로 반영된다)"

    # dev.bat 과 같이 2초씩 벌린다. 열두 개를 한꺼번에 던지면 dotnet watch 들이
    # 동시에 복원·빌드에 들어가 서로 느려진다.
    sleep 2
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
  watch <서비스>...   위와 같음 — 모든 기동이 watch 기동이다
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
    cat <<EOF
  front·portal·mfe 는 blazor 의 옛 이름이라 그대로 받는다.

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
    for key in $(svc_keys); do
            port="$(svc_port "$key")"

        if port_is_open "$port"; then
            state="$(printf '%s' "${C_UP}UP${C_OFF}")"
        else
            state="$(printf '%s' "${C_DOWN}DOWN${C_OFF}")"
        fi

            print_status_row "$key" "$port" "$state" "$(svc_label "$key")"
    done
    echo
}

# 지정한 서비스들을 재기동한다.
restart_services() {   # restart_services <이름>...
    local targets=("$@") key failed=""

    echo ">>> [1/3] 중지"
    # 하나라도 못 내렸으면 여기서 멈춘다. 포트를 쥔 프로세스 위에 새로 띄우면
    # 새 쪽이 조용히 죽어서 "기동 완료" 가 사실과 달라진다.
    for key in "${targets[@]}"; do
        stop_service "$key" || failed=1
    done
    if [ -n "$failed" ]; then
        echo
        echo "❌ 내려가지 않은 서비스가 있어 아무것도 기동하지 않았습니다."
        echo "   남은 프로세스를 정리한 뒤 다시 실행하세요 ($(basename "$0") status)."
        exit 1
    fi

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
    # 기동 직전에 터미널을 한 번 확인한다. start_service 안에서는 늦다 —
    # 거기서 run_terminal 은 백그라운드로 떨어져 실패가 전달되지 않는다.
    if ! have_terminal; then
        echo "❌ 실행 가능한 터미널을 찾을 수 없어 기동하지 못했습니다."
        echo "   빌드는 끝났습니다. 터미널을 쓸 수 있는 환경에서 다시 실행하세요."
        exit 1
    fi
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
# `watch` 는 지금은 일반 기동과 같다. 예전에는 watch 모드가 따로 있었지만 이제
# 모든 기동이 watch 기동이라 구분할 것이 없다. 손이 기억하는 이름이라 그대로
# 받아 준다 (`watch blazor` = `blazor`, `watch` 혼자 = 전체).
# dev.bat 의 :cmd_watch 와 같은 처리다.
[ "${1:-}" = "watch" ] && shift

COMMAND="${1:-all}"

case "$COMMAND" in
    help|-h|--help)
        print_usage
        exit 0
        ;;

    list)
        svc_keys
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
        stop_failed=""
        for key in $(svc_keys); do
            stop_service "$key" || stop_failed=1
        done
        echo
        if [ -n "$stop_failed" ]; then
            echo "❌ 아직 떠 있는 서비스가 있습니다. $(basename "$0") status 로 확인하세요."
            exit 1
        fi
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
        # 이름을 먼저 전부 검사한다. 하나라도 틀리면 아무것도 건드리지 않는다.
        targets=()
        for key in "$@"; do
            if ! svc_exists "$key"; then
                echo "❌ 알 수 없는 서비스: $key   (사용 가능: $(svc_keys | tr '\n' ' '))"
                exit 1
            fi
            targets+=("$(svc_alias "$key")")
        done
        echo "===================================================="
        echo "   중지: ${targets[*]}"
        echo "===================================================="
        stop_failed=""
        for key in "${targets[@]}"; do
            stop_service "$key" || stop_failed=1
        done
        echo
        if [ -n "$stop_failed" ]; then
            echo "❌ 아직 떠 있는 서비스가 있습니다. $(basename "$0") status 로 확인하세요."
            exit 1
        fi
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
        restart_services $(svc_keys)
        exit 0
        ;;

    *)
        # 서비스 이름들로 본다. 옛 이름은 svc_alias 가 지금 이름으로 바꿔 준다.
        targets=()
        for key in "$@"; do
            if ! svc_exists "$key"; then
                echo "❌ 알 수 없는 서비스: $key"
                echo
                print_usage
                exit 1
            fi
            targets+=("$(svc_alias "$key")")
        done
        echo "===================================================="
        echo "   재기동: ${targets[*]}"
        echo "===================================================="
        restart_services "${targets[@]}"
        exit 0
        ;;
esac
