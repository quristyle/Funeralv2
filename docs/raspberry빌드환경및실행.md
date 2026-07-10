먼저 빌드환경을 작성하고 이후에 실행용 환경을 따로 작성하자.


sudo apt update
sudo apt full-upgrade -y
sudo reboot

uname -m

aarch64 가 나와야 함.


sudo apt install -y \
git \
curl \
wget \
unzip \
xz-utils \
zip \
clang \
cmake \
ninja-build \
pkg-config \
build-essential \
libgtk-3-dev \
liblzma-dev \
libstdc++-12-dev \
mesa-utils \
libegl1-mesa-dev \
libgles2-mesa-dev \
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
libxkbcommon0


cd /opt

sudo git clone https://github.com/flutter/flutter.git

sudo chown -R quri:quri /opt/flutter


vi ~/.bashrc

export PATH="$PATH:/opt/flutter/bin"

source ./bashrc

flutter --version

이때 dark skd 를 다운로드 한다.
---------------------
Downloading Linux arm64 Dart SDK from Flutter engine ce81ae9fbf2f2f2c9be609ed6e756325f58d7696...
  % Total    % Received % Xferd  Average Speed   Time    Time     Time  Current
                                 Dload  Upload   Total   Spent    Left  Speed
100  225M  100  225M    0     0  2428k      0  0:01:34  0:01:34 --:--:-- 1731k
Building flutter tool...
Resolving dependencies... (1.8s)
Downloading packages... (8:49.9s)
Got dependencies.
Flutter 3.46.0-1.0.pre-478 • channel master • https://github.com/flutter/flutter.git
Framework • revision ab7eb7aff6 (3 hours ago) • 2026-07-08 13:30:13 +0900
Engine • hash ce81ae9fbf2f2f2c9be609ed6e756325f58d7696 (revision ab7eb7aff6) (2 hours ago) • 2026-07-08 04:30:13.000Z
Tools • Dart 3.13.0 (build 3.13.0-279.0.dev) • DevTools 2.59.0
--------------------

flutter config --enable-linux-desktop

flutter config


아래 명령이 보여야 함.
Linux desktop: true



flutter doctor


보통 Android 관련 경고는 무시해도 됩니다.

중요한 것은

Linux toolchain



8단계. 테스트 프로젝트
flutter create hello


빌드

cd hello

flutter build linux --release

성공하면

build/linux/arm64/release/bundle

이 생성됩니다.

9단계. 실행 테스트

Lite에는 GUI가 없으므로

아직은 실행되지 않습니다.

하지만

빌드는 가능합니다.

확인

ls build/linux/arm64/release/bundle

예를 들면

hello
data
lib

가 보이면 성공입니다.

10단계. 배포

이제

build/linux/arm64/release/bundle

전체를 압축합니다.

cd build/linux/arm64/release

tar czf hello-arm64.tar.gz bundle

또는

zip -r hello-arm64.zip bundle

이 파일을 Raspberry Pi 3/4/5에 복사하면 됩니다.







---------------------------------------------------------------------------


실행버전 설치 옵션

sudo apt update
sudo apt full-upgrade -y
sudo reboot



2. 실행에 필요한 최소 라이브러리 설치

Flutter Linux 실행에 필요한 최소 런타임입니다.

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
libxkbcommon0



4. 한글 폰트 설치
sudo apt install -y fonts-noto-cjk

이 정도면 대부분의 Flutter Linux 앱은 실행됩니다.


sudo apt install -y \
curl \
wget \
unzip \
rsync \
sqlite3

6. 시간 동기화

SSL과 SignalR을 위해 중요합니다.

sudo timedatectl set-timezone Asia/Seoul

확인

timedatectl

8. 오디오

귀하의 시스템은 음원을 재생하므로

sudo apt install -y alsa-utils

출력 장치 확인

aplay -l
9. GPU 확인
glxinfo | grep OpenGL

또는

es2_info

둘 다 없어도 실행은 가능하지만 GPU 상태를 확인할 때 유용합니다.

10. 실행 폴더

저는 항상 다음 구조를 권장합니다.

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
sudo nano /etc/systemd/system/player.service

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

현재 설치한 것은 Lite(CLI) 입니다.

CLI에는

X11
Wayland
Labwc

가 없습니다.

즉 지금 상태에서는

/opt/player/player

를 실행하면

Gtk-WARNING: cannot open display

가 발생합니다.

실행 전 반드시 필요한 최소 GUI

Flutter Linux는 GUI 라이브러리만이 아니라 Display Server도 필요합니다.

제가 권장하는 최소 구성은 다음입니다.

Raspberry Pi OS Lite

↓

Wayland

↓

Labwc

↓

Flutter

Desktop 환경(LXDE, 브라우저, 파일관리자 등)은 설치하지 않고도 Flutter를 실행할 수 있습니다.

제가 추천하는 최종 구성

운영 장비에는 다음 정도만 설치하면 충분합니다.

기본 런타임 라이브러리(위 apt install)
GStreamer(동영상 사용 시)
Noto CJK 폰트(한글)
SSH 및 rsync(원격 관리)
SQLite CLI(관리용)
Wayland + Labwc(최소 GUI 환경)

이 구성은 Raspberry Pi 3/4/5에서 메모리 사용량이 적고 안정적이며, 귀하처럼 SignalR + SQLite + 미디어 재생 + 24시간 운영을 하는 Flutter 플레이어에 가장 적합한 방식입니다.

다음 단계에서는 Desktop 환경 없이 Wayland/Labwc만 설치하여 부팅 후 Flutter 앱이 전체 화면으로 자동 실행되는 최소 키오스크 환경을 구성하는 과정을 안내드리겠습니다.









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




2. Wayland 설치
sudo apt install -y \
wayland-protocols \
wayland-utils \
weston \
labwc \
seatd \
xwayland

설명

패키지	용도
wayland-protocols	Wayland 라이브러리
wayland-utils	테스트
weston	Wayland 런타임
labwc	가벼운 Window Manager
seatd	입력장치 관리
xwayland	GTK 호환성

3. GTK 런타임

sudo apt install -y \
libgtk-3-0 \
libegl1 \
libgles2 \
libwayland-client0 \
libwayland-cursor0 \
libwayland-egl1 \
libxkbcommon0

4. seatd 활성화
sudo systemctl enable seatd
sudo systemctl start seatd

사용자를 seat 그룹에 추가

sudo usermod -aG video,input,render pi

재부팅

sudo reboot
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
nano ~/.config/labwc/autostart

내용

#!/bin/sh

export GDK_BACKEND=wayland

export XDG_SESSION_TYPE=wayland

export WAYLAND_DISPLAY=wayland-1

/opt/player/player

권한

chmod +x ~/.config/labwc/autostart
8. Wayland 시작
nano ~/.bash_profile

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
sudo nano /boot/firmware/cmdline.txt

끝에 추가

consoleblank=0
11. 절전 해제
sudo nano /etc/systemd/logind.conf

변경

HandleLidSwitch=ignore

IdleAction=ignore
12. HDMI 절전 해제
sudo nano /boot/firmware/config.txt

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










