#!/bin/sh
# =============================================================
# release-run.sh — 배포 스크립트를 감싸고 진행 상황을 되돌려 보고한다
# =============================================================
#
#   사용법: release-run.sh <runId> <token> <callbackUrl> <script> [args...]
#
# JSini 포털의 배포 도구(/portal/release)가 큐에 넣은 요청을 배포 장비의 큐
# 소비자가 집어가 이 스크립트를 실행한다.
#
# ── 왜 필요한가 ──────────────────────────────────────────────
#
# 예전에는 서버가 큐에 넣고 잊었다. 진행 상황을 알 길이 없으니 화면이 단계를
# 스스로 만들어 내 setTimeout 으로 초록색 [SUCCESS] 를 찍었다. 그래서 이 장비에서
# 스크립트가 실패해도 화면은 전부 초록이었다.
#
# 이 래퍼는 실제 배포 스크립트의 stdout/stderr 를 한 줄씩 포털로 보내고,
# 끝나면 종료 코드를 보고한다. 화면에 보이는 줄은 전부 실제로 일어난 일이 된다.
#
# ── 소비자를 고치지 않아도 된다 ──────────────────────────────
#
# 포털의 Release:Targets[].WrapperPath 에 이 파일의 경로를 넣으면, 포털이 큐
# 메시지의 script 자리에 이 래퍼를 넣고 args 앞에 run 정보를 끼워 보낸다.
# 소비자는 예전과 똑같이 "script 를 args 와 함께 실행" 하는데 그 script 가
# 이 래퍼가 된다. 소비자 코드는 한 줄도 바뀌지 않는다.
#
# ── 지켜야 할 것 ─────────────────────────────────────────────
#
# **보고에 실패해도 배포는 계속한다.** 포털이 내려가 있다고 배포가 멈추면
# 도구가 원래 하려던 일보다 더 큰 문제를 만든다. 모든 curl 은 실패를 삼킨다.
#
# 필요한 것: sh · curl · awk. jq 는 쓰지 않는다(배포 장비에 없을 수 있다).
#
# ── 단계 표시 ────────────────────────────────────────────────
#
# 배포 스크립트가 아래처럼 찍으면 화면이 그 줄을 단계로 강조한다.
#
#     echo "##STEP front build"
#
# 안 찍어도 된다. 그때는 stdout 이 그대로 로그가 된다 — 그것만으로도
# 예전의 가짜 단계보다 정확하다.

RUN_ID="$1"
TOKEN="$2"
CALLBACK_URL="$3"
SCRIPT="$4"
[ $# -ge 4 ] && shift 4

# ── 설정 ─────────────────────────────────────────────────────

# 몇 줄이 모이면 보낼지. 줄마다 보내면 로그가 긴 배포에서 요청이 수천 건이 된다.
FLUSH_LINES="${RELEASE_FLUSH_LINES:-20}"
# 줄이 적어도 이 시간이 지나면 보낸다. 화면이 멈춘 것처럼 보이지 않게 한다.
FLUSH_SECONDS="${RELEASE_FLUSH_SECONDS:-2}"
# 한 줄의 길이 상한(바이트). 서버도 자르지만 여기서 줄이면 트래픽이 줄어든다.
MAX_LINE="${RELEASE_MAX_LINE:-4000}"
CURL_TIMEOUT="${RELEASE_CURL_TIMEOUT:-10}"
CURL_CONNECT_TIMEOUT="${RELEASE_CURL_CONNECT_TIMEOUT:-3}"
# 연달아 이만큼 실패하면 보고를 포기한다.
#
# 포기하지 않으면 포털이 내려가 있는 동안 보고마다 타임아웃을 기다리게 되어
# **배포 자체가 느려진다.** 보고를 못 하는 것보다 배포가 늦어지는 것이 큰 문제다.
# 포기하면 포털 쪽 run 은 제한 시간이 지나 '중단' 으로 남는다 — 연락이 끊긴 것이
# 사실이므로 그렇게 보이는 것이 맞다.
MAX_FAILS="${RELEASE_MAX_FAILS:-5}"

WORK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/release-run.XXXXXX")" || exit 1
BUFFER="$WORK_DIR/buffer"
COUNT_FILE="$WORK_DIR/count"
CLOCK_FILE="$WORK_DIR/clock"
RC_FILE="$WORK_DIR/rc"
STOP_FILE="$WORK_DIR/stop"
BODY_FILE="$WORK_DIR/body"
FAIL_FILE="$WORK_DIR/fails"

: >"$BUFFER"
echo 0 >"$COUNT_FILE"
echo 0 >"$FAIL_FILE"
date +%s >"$CLOCK_FILE"

cleanup() { rm -rf "$WORK_DIR"; }
trap cleanup EXIT

log_local() { echo "[release-run] $*" >&2; }

# ── 버퍼에 한 줄 넣기 ────────────────────────────────────────
#
# **여기서는 아무것도 가공하지 않는다.** 원문 그대로 `수준 <탭> 단계 <탭> 내용`
# 으로 적어 두고, 보낼 때 awk 가 한 번에 JSON 으로 바꾼다.
#
# 예전에는 줄마다 sed 를 여섯 번 걸었다. 줄마다 프로세스를 띄우는 것도 문제였지만
# 더 큰 문제는 **sed 구현에 따라 결과가 달랐다**는 것이다. 역슬래시 이스케이프가
# 어떤 sed 에서는 먹지 않아 JSON 이 깨졌고, 한글이 다른 인코딩으로 바뀌기도 했다.
buffer_add() {
    printf '%s\t%s\t%s\n' "$1" "$2" "$3" >>"$BUFFER"
    _n=$(($(cat "$COUNT_FILE") + 1))
    echo "$_n" >"$COUNT_FILE"
}

# ── 버퍼를 JSON 배열로 바꾸기 ────────────────────────────────
#
# LC_ALL=C 로 돌린다. 그래야 awk 가 바이트 단위로 움직여
#   * UTF-8 한글이 바이트 그대로 지나가고 (인코딩이 바뀌지 않는다)
#   * ASCII 제어문자만 정확히 골라낼 수 있다.
#
# 이스케이프는 gsub 대신 한 바이트씩 이어 붙여 만든다. gsub 의 치환문에서
# `\\` 와 `&` 가 특별하게 해석되는 것을 아예 피하려는 것이다.
buffer_to_json() {
    LC_ALL=C awk -v max="$MAX_LINE" '
        # 잘라야 하면 자르고, 자른 자리가 UTF-8 문자 가운데면 그 문자를 버린다.
        # 반 토막 난 문자를 보내면 본문 전체가 잘못된 UTF-8 이 되어
        # 그 묶음의 로그를 통째로 잃는다.
        function clip(s,    t, n) {
            if (length(s) <= max) return s
            t = substr(s, 1, max)
            n = length(t)
            while (n > 0 && substr(t, n, 1) >= "\200") n--
            return substr(t, 1, n) " ...(잘림)"
        }

        function esc(s,    out, i, n, c) {
            gsub(/\033\[[0-9;]*[A-Za-z]/, "", s)   # ANSI 색상코드를 지운다
            s = clip(s)
            out = ""; n = length(s)
            for (i = 1; i <= n; i++) {
                c = substr(s, i, 1)
                if (c == "\\")      out = out "\\\\"
                else if (c == "\"") out = out "\\\""
                else if (c == "\t") out = out "\\t"
                else if (c < " ")   continue       # 남은 제어문자는 버린다
                else                out = out c
            }
            return out
        }

        BEGIN { FS = "\t"; body = "" }
        {
            lvl = $1
            stp = $2
            msg = $0
            # 내용은 세 번째 칸부터 끝까지다. 내용 안에 탭이 있어도 그대로 살린다.
            sub(/^[^\t]*\t[^\t]*\t/, "", msg)

            if (body != "") body = body ","
            body = body "{\"level\":\"" esc(lvl) "\""
            if (stp != "") body = body ",\"step\":\"" esc(stp) "\""
            body = body ",\"message\":\"" esc(msg) "\"}"
        }
        END { printf "%s", body }
    ' "$BUFFER"
}

# ── 보내기 ───────────────────────────────────────────────────
#
# final 이 참이면 exitCode 를 함께 보낸다. 서버가 그 값으로 성공/실패를 정한다.
flush() {
    _final="$1"
    _code="$2"

    _n="$(cat "$COUNT_FILE" 2>/dev/null || echo 0)"

    # 보낼 것도 없고 마지막 보고도 아니면 그만둔다.
    if [ "$_n" -eq 0 ] && [ "$_final" != "true" ]; then
        return 0
    fi

    # 서버가 그만 보내라고 했으면 조용히 버린다 (끝난 run · 로그 한도 초과).
    if [ -f "$STOP_FILE" ]; then
        : >"$BUFFER"
        echo 0 >"$COUNT_FILE"
        return 0
    fi

    _events="$(buffer_to_json)"

    if [ "$_final" = "true" ]; then
        printf '{"events":[%s],"final":true,"exitCode":%s}' "$_events" "$_code" >"$BODY_FILE"
    else
        printf '{"events":[%s],"final":false}' "$_events" >"$BODY_FILE"
    fi

    # 버퍼는 보내기 전에 비운다. 실패해도 같은 줄을 두 번 보내지 않는다 —
    # 놓친 줄보다 중복된 줄이 로그를 더 헷갈리게 만든다.
    : >"$BUFFER"
    echo 0 >"$COUNT_FILE"
    date +%s >"$CLOCK_FILE"

    _status="$(
        curl -sS -o /dev/null -w '%{http_code}' \
            --connect-timeout "$CURL_CONNECT_TIMEOUT" \
            -m "$CURL_TIMEOUT" \
            -X POST "$CALLBACK_URL" \
            -H 'Content-Type: application/json; charset=utf-8' \
            -H "X-Release-Token: $TOKEN" \
            --data-binary "@$BODY_FILE" 2>/dev/null
    )" || _status="000"

    case "$_status" in
        2*)
            echo 0 >"$FAIL_FILE"
            ;;
        403 | 404)
            # 토큰이 무효거나 없는 run 이다. 더 보내도 소용없다.
            # **배포는 계속한다** — 보고가 안 되는 것과 배포가 실패하는 것은 다른 일이다.
            log_local "보고가 거절됐습니다 (HTTP $_status). 이후 보고를 멈추고 배포는 계속합니다."
            : >"$STOP_FILE"
            ;;
        *)
            _f=$(($(cat "$FAIL_FILE" 2>/dev/null || echo 0) + 1))
            echo "$_f" >"$FAIL_FILE"
            log_local "보고를 보내지 못했습니다 (HTTP $_status, 연속 $_f 회). 배포는 계속합니다."

            if [ "$_f" -ge "$MAX_FAILS" ]; then
                # 계속 기다리면 배포가 느려진다. 보고를 포기하고 배포에 집중한다.
                log_local "연속 $_f 회 실패로 보고를 포기합니다. 배포는 끝까지 진행합니다."
                : >"$STOP_FILE"
            fi
            ;;
    esac

    return 0
}

# 마지막 보고까지 마쳤는지. 두 번 보내지 않도록 표시해 둔다.
FINAL_SENT=""

report_final() {
    [ -n "$FINAL_SENT" ] && return 0
    FINAL_SENT="yes"
    flush true "$1"
}

# 스크립트가 신호를 받아 죽어도 결과를 남긴다.
# 이것이 없으면 run 이 running 에 남아 다음 배포를 막는다.
on_signal() {
    buffer_add "error" "" "[중단] 래퍼가 신호를 받아 멈췄습니다."
    report_final 143
    cleanup
    exit 143
}
trap on_signal HUP INT TERM

# ── 인자 확인 ────────────────────────────────────────────────

if [ -z "$RUN_ID" ] || [ -z "$CALLBACK_URL" ] || [ -z "$SCRIPT" ]; then
    log_local "사용법: release-run.sh <runId> <token> <callbackUrl> <script> [args...]"
    exit 2
fi

if [ ! -f "$SCRIPT" ]; then
    buffer_add "error" "" "[오류] 배포 스크립트가 없습니다: $SCRIPT"
    report_final 127
    exit 127
fi

# ── 실행 ─────────────────────────────────────────────────────

buffer_add "info" "" "[시작] $SCRIPT $*"
flush false ""

# stdout·stderr 를 함께 읽는다. 배포 스크립트가 오류를 stderr 로만 내는 일이 흔하다.
#
# 파이프 뒤의 while 은 서브셸에서 돌기 때문에 스크립트의 종료 코드를 변수로
# 받을 수 없다($PIPESTATUS 는 bash 전용이다). 그래서 파일에 적어 둔다.
{
    "$SCRIPT" "$@" 2>&1
    echo "$?" >"$RC_FILE"
} | {
    lines_since_clock=0

    while IFS= read -r line; do
        # '##STEP <이름>' 은 단계 표시로 올린다. 화면이 그 줄을 강조한다.
        case "$line" in
            '##STEP '*)
                buffer_add "step" "${line#\#\#STEP }" "$line"
                ;;
            *)
                buffer_add "stdout" "" "$line"
                ;;
        esac

        if [ "$(cat "$COUNT_FILE")" -ge "$FLUSH_LINES" ]; then
            flush false ""
            lines_since_clock=0
            continue
        fi

        # 시간으로도 흘려보낸다. 줄이 드문 배포에서 화면이 멈춘 것처럼 보이지 않게.
        # date 는 fork 라서 매 줄 부르지 않는다 — 다섯 줄에 한 번만 본다.
        lines_since_clock=$((lines_since_clock + 1))
        if [ "$lines_since_clock" -ge 5 ]; then
            lines_since_clock=0
            now="$(date +%s)"
            was="$(cat "$CLOCK_FILE" 2>/dev/null || echo "$now")"
            if [ "$((now - was))" -ge "$FLUSH_SECONDS" ]; then
                flush false ""
            fi
        fi
    done

    # 루프가 서브셸이라 남은 줄은 여기서 보낸다.
    flush false ""
}

# ── 마무리 ───────────────────────────────────────────────────

rc="$(cat "$RC_FILE" 2>/dev/null || echo 1)"
case "$rc" in
    '' | *[!0-9]*) rc=1 ;;
esac

report_final "$rc"
exit "$rc"
