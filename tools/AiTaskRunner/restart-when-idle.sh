#!/bin/bash
# ============================================================
# 실행기 재기동 감시 — 한가해지면 내린다
# ============================================================
#
# 새 바이너리를 `/home/lee/ai-task-runner` 에 올린 뒤 그것을 반영하려면
# 실행기를 내려야 하는데, **재기동은 그때 돌던 AI 작업을 같이 죽인다**
# (docs/ai-task-runner.md 14.2). 그리고 이 배포를 하는 것은 대개
# **실행기 안에서 도는 AI 자신**이라, 그냥 내리면 자기 작업을 자기가 죽인다.
# 그래서 이 감시가 「실행 중」이 0 이 될 때까지 기다렸다가 내린다.
#
#   rsync -a --delete --exclude=appsettings.Local.json <스테이지>/ /home/lee/ai-task-runner/
#   setsid tools/AiTaskRunner/restart-when-idle.sh cgroup &
#   XDG_RUNTIME_DIR=/run/user/$(id -u) \
#     DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus \
#     systemd-run --user --unit=jsini-runner-restart --collect \
#       tools/AiTaskRunner/restart-when-idle.sh unit
#
# `sudo systemctl restart` 는 쓸 수 없다 — 유닛의 NoNewPrivileges 가
# setuid 를 무력화한다. 주 프로세스에 TERM 을 보내고 Restart=always 에 맡긴다.
#
# 둘이 함께 돈다(유닛 안 · 사용자 관리자 밑). 유닛 안의 것은 재기동과 함께
# 죽으므로 뒤처리를 못 하고, 사용자 관리자 밑의 것은 살아남아 확인까지 한다.
# **둘이 같이 내리지 않게** mkdir 로 자리를 하나만 잡는다.

LOG=/home/lee/ai-task-runner-restart.log
TAG=${1:-?}
LOCK=/tmp/jsini-runner-restart.lock
API=http://127.0.0.1:5450

say() { printf '[%s] %-6s %s\n' "$(date '+%F %T')" "$TAG" "$*" >> "$LOG"; }

# **「지금 돌고 있는 실행이 있나」를 서버에 묻는다.** cgroup 에 CLI 가
# 보이나로 세면 안 된다 — CLI 가 끝난 뒤에도 실행기는 게이트(빌드·테스트)를
# 돌리고 push 하고 완료를 보고한다. 그 사이에 내리면 **다 해 놓고 보고만
# 못 한 채 죽는다.** 실행이 「실행 중」에서 빠지는 시점이 곧 그것이 다 끝난
# 시점이다. 못 물어봤으면 바쁜 것으로 친다(모르면 내리지 않는다).
running_now() {
  curl -s --max-time 10 "$API/api/ai-dashboard?days=1" \
    | python3 -c 'import sys,json
try:
    d=json.load(sys.stdin)
    print(d["data"]["result"][0]["summary"]["runningNow"])
except Exception:
    print(-1)' 2>/dev/null || echo -1
}

say "감시 시작 — 돌고 있는 실행이 없어지면 내린다."

idle=0
for _ in $(seq 1 1440); do          # 10초 × 1440 = 네 시간
  n=$(running_now)
  if [ "$n" = "0" ]; then idle=$((idle + 1)); else idle=0; fi
  [ "$idle" -ge 3 ] && break        # 30초 내리 비어 있어야 인정한다
  sleep 10
done

if [ "$idle" -lt 3 ]; then
  say "네 시간을 기다렸는데 계속 돌고 있습니다. 포기합니다 — 사람이 내려 주십시오."
  exit 1
fi

if ! mkdir "$LOCK" 2>/dev/null; then
  say "다른 감시가 이미 맡았습니다. 물러납니다."
  exit 0
fi

OLD=$(systemctl show ai-task-runner.service -p MainPID --value)
say "한가해졌습니다. 주 프로세스 $OLD 에 TERM 을 보냅니다."
kill -TERM "$OLD" 2>/dev/null || say "TERM 을 보내지 못했습니다."

# ── 여기부터는 살아남은 쪽만 지나간다 ────────────────────────
NEW=0
for _ in $(seq 1 60); do
  sleep 5
  NEW=$(systemctl show ai-task-runner.service -p MainPID --value)
  [ "$NEW" != "0" ] && [ "$NEW" != "$OLD" ] && break
done

if [ "$NEW" = "0" ] || [ "$NEW" = "$OLD" ]; then
  say "새 주 프로세스를 못 봤습니다 (지금 $NEW). systemctl status 를 보십시오."
  exit 1
fi

say "새 주 프로세스 $NEW 로 떴습니다 (예전 $OLD)."
say "저널: $(journalctl -u ai-task-runner.service -n 40 --no-pager 2>/dev/null | grep '실행기 .* 시작' | tail -1)"

# 첫 사용량 보고는 기동 10초 뒤에 간다. 넉넉히 기다렸다가 한 줄을 되읽는다.
sleep 120
row=$(curl -s -X POST http://127.0.0.1:5450/api/dev/sql \
  -H 'Content-Type: application/json' \
  -d '{"db_nick":"jsini","query":"select runner_nm, runner_kind, bucket_nm, ok, session_pct, week_pct, month_pct, to_char(observed_at,'"'"'MM-DD HH24:MI:SS'"'"') as observed from ai_usage_snapshot order by runner_kind, bucket_nm"}' \
  | head -c 1200)
# **칸 이름을 함께 읽는다.** 세 CLI 를 붙인 뒤로는 줄이 하나가 아니라
# 여럿이고, 「몇 줄이 들어왔나」가 곧 어느 CLI 를 못 읽었나다.
say "사용량: $row"
say "끝."
