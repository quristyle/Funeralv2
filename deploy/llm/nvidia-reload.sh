#!/bin/bash
# ============================================================
# NVIDIA 커널 모듈 다시 올리기 — 재부팅 없이 판 어긋남을 푼다
# ============================================================
#
# [언제 쓰나]
#
# 포털 「LLM 장비 상태」(/admin/status/llm) 가
#   「드라이버 판이 어긋났습니다 — 올라가 있는 커널 모듈 A, 설치된 라이브러리 B」
# 라고 말할 때.
#
# unattended-upgrades 가 nvidia 꾸러미를 올려도 **이미 올라가 있는 커널 모듈은
# 그대로**다. 그래서 NVML(nvidia-smi) 초기화가 막힌다. 반면 llama-server 는 이미
# 잡아 둔 핸들로 계속 돌기 때문에 **추론은 멀쩡한데 GPU 만 안 보인다.**
#
# [무엇을 끊나]
#
# 모듈을 내리려면 GPU 를 쥔 것을 전부 놓아야 한다 — llama-server·llama-fim 과
# gdm(Xorg) 이다. **추론이 1~2분 끊긴다.** 한가한 때 돌린다.
#
# [재부팅과 무엇이 다른가]
#
# 재부팅은 **아직 부팅해 본 적 없는 새 커널로 넘어간다**(보통 하나가 밀려 있다).
# 돌아오지 않으면 장비 앞에 가야 한다. 이쪽은 SSH 가 끊기지 않고, 실패해도
# 서비스만 되살아난 채 끝나므로 그 다음에 재부팅을 고르면 된다.
#
# [쓰는 법]
#
#   systemd-run --unit=nvidia-reload --collect --service-type=oneshot \
#       /bin/bash /usr/local/bin/nvidia-reload
#   journalctl -u nvidia-reload --no-pager -o cat
#
# **SSH 안에서 그냥 돌리지 않는다.** gdm 을 끊는 동안 연결이 흔들리면 한쪽만
# 끝난 채로 남는다. systemd 가 들고 있게 맡긴다.
set -x
exec 2>&1

# 무슨 일이 있어도 서비스는 되살린다. rmmod 가 막혀도 추론은 돌아와야 한다.
restore() {
    systemctl start gdm.service || true
    systemctl start llama-server.service llama-fim.service || true
}
trap restore EXIT

echo "=== 지금 올라가 있는 모듈"
cat /proc/driver/nvidia/version
lsmod | grep -i nvidia

systemctl stop llama-server.service llama-fim.service
systemctl stop gdm.service
sleep 5

echo "=== 아직 GPU 를 쥐고 있는 것 (비어 있어야 한다)"
fuser -v /dev/nvidia* || true
lsmod | grep -i nvidia

modprobe -r nvidia_uvm     || true
modprobe -r nvidia_drm     || true
modprobe -r nvidia_modeset || true
modprobe -r nvidia         || true

echo "=== 내린 뒤"
lsmod | grep -i nvidia || echo "(모듈 전부 내려감)"

modprobe nvidia         || true
modprobe nvidia_modeset || true
modprobe nvidia_drm     || true
modprobe nvidia_uvm     || true

echo "=== 다시 올린 뒤 — 두 판이 같아야 한다"
cat /proc/driver/nvidia/version
nvidia-smi
