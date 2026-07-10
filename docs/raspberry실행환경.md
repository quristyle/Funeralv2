실행버전 설치 옵션

sudo apt update
sudo apt full-upgrade -y
sudo reboot

2. 실행에 필요한 최소 라이브러리 설치

sudo apt install -y \
libgtk-3-0 \
libblkid1 \
liblzma5 \
libstdc++6 \
libglu1-mesa \
libegl1 \
libgles2 \
libxkbcommon0 \
libwayland-client0 \
libwayland-cursor0 \
libwayland-egl1 \
libdbus-1-3 \
libfontconfig1 \
libfreetype6 \
libasound2 \
libmpv2 \
mpv \
ffmpeg \
libegl1 \
libgles2 \
libgl1 \
libwayland-client0 \
libwayland-cursor0 \
libwayland-egl1 \
libgtk-3-0 \
libxkbcommon0 \
fonts-noto-cjk \
curl \
wget \
unzip \
rsync \
sqlite3 \
alsa-utils \
wayland-protocols \
wayland-utils \
weston \
labwc \
seatd \
xwayland 


시간 동기화 - SSL과 SignalR을 위해 중요합니다.

sudo timedatectl set-timezone Asia/Seoul

확인

timedatectl



4. seatd 활성화
sudo systemctl enable seatd
sudo systemctl start seatd

사용자를 seat 그룹에 추가

sudo usermod -aG video,input,render pi

재부팅

sudo reboot





10. 실행 폴더

다음 구조를 권장.

/opt/player
    player          ← 실행파일
    data/
    lib/
    media/
        image/
        video/
        audio/
    database/
    cache/
    logs/

11. 자동 실행(systemd)
sudo vi /etc/systemd/system/player.service

예시

[Unit]
Description=Flutter Player
After=network.target

[Service]
User=pi
WorkingDirectory=/opt/player
ExecStart=/opt/player/player
Restart=always
RestartSec=3

Environment=HOME=/home/pi

[Install]
WantedBy=multi-user.target

활성화

sudo systemctl daemon-reload
sudo systemctl enable player

하지만 여기서 중요한 점이 하나 있습니다.


최종 목표
전원 ON

↓

Linux 부팅

↓

자동 로그인

↓

Wayland 실행

↓

Labwc 실행

↓

Flutter Player 실행

↓

전체화면

↓

SignalR 연결

↓

서비스 시작

이 구조입니다.

전체 구성
Raspberry Pi OS Lite
↓
systemd
↓
seatd
↓
Wayland
↓
Labwc
↓
Flutter
메모리도 적게 사용하고 매우 안정적입니다.



5. 자동 로그인

Lite에서는

sudo raspi-config

선택

System Options

↓

Boot / Auto Login

↓

Console Autologin

이렇게 하면

부팅 후

pi 로그인

이 자동으로 됩니다.

6. Flutter 폴더

예를 들어

/opt/player
player

data

lib

media

database

복사해 둡니다.

7. 실행 스크립트 작성
mkdir -p ~/.config/labwc
vi ~/.config/labwc/autostart

내용

#!/bin/sh

export GDK_BACKEND=wayland

export XDG_SESSION_TYPE=wayland

export WAYLAND_DISPLAY=wayland-1

/opt/player/player

권한

chmod +x ~/.config/labwc/autostart
8. Wayland 시작
vi ~/.bash_profile

추가

if [ -z "$WAYLAND_DISPLAY" ] && [ "$(tty)" = "/dev/tty1" ]; then
    exec labwc
fi

이제 tty1 로그인하면

자동으로

labwc

↓

Flutter

가 실행됩니다.

9. Flutter 전체화면

Flutter에서는

windowManager.setFullScreen(true);

또는

windowManager.waitUntilReadyToShow(
  const WindowOptions(
    fullScreen: true,
  ),
  () async {
    await windowManager.show();
    await windowManager.focus();
  },
);

를 사용합니다.

window_manager 패키지를 추천합니다.

10. 화면 꺼짐 방지
sudo vi /boot/firmware/cmdline.txt

끝에 추가

consoleblank=0
11. 절전 해제
sudo vi /etc/systemd/logind.conf

변경

HandleLidSwitch=ignore

IdleAction=ignore
12. HDMI 절전 해제
sudo vi /boot/firmware/config.txt

추가

hdmi_force_hotplug=1

disable_splash=1
13. 자동 재시작

player.service

Restart=always

RestartSec=3

Flutter가 죽으면

3초 후

자동 실행됩니다.

14. 로그
journalctl -u player.service -f

실시간 확인

부팅 과정
BIOS

↓

Linux

↓

systemd

↓

Console Auto Login

↓

labwc

↓

Flutter

↓

전체화면

부팅 후 약 10~15초 내에 Flutter가 전체화면으로 실행되는 구성이 가능합니다.

제가 실제로 추천하는 구조

다만 여기서 한 가지를 더 개선하면 더 안정적인 시스템이 됩니다.

현재 설명한 방법은 .bash_profile에서 labwc를 실행합니다.

상용 장비에서는 로그인 셸에 의존하기보다 systemd가 Wayland와 Flutter를 직접 관리하는 방식을 더 많이 사용합니다.

그 구조는 다음과 같습니다.

systemd
    │
    ├── seatd
    ├── labwc.service
    └── flutter-player.service

이 방식의 장점은 다음과 같습니다.

로그인 셸이 없어도 자동 실행
앱이 종료되면 systemd가 자동 재시작
부팅 시점 제어가 쉬움
로그를 journalctl에서 일괄 관리
24시간 무인 운영에 적합

귀하의 장례식장 DID 시스템처럼 항상 켜져 있는 장비라면 이 방식이 가장 안정적입니다.

저는 이후 설명은 상용 제품 수준의 systemd 기반 키오스크 구성을 기준으로 진행하는 것을 권장드립니다. 이렇게 하면 Raspberry Pi뿐 아니라 Orange Pi와 Intel Mini PC까지 거의 동일한 방식으로 운영할 수 있습니다.










