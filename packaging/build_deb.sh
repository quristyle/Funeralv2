#!/usr/bin/env bash
#
# funeralv2_player 의 Linux 빌드 산출물을 배포 패키지로 만든다.
#
#   사용법: bash packaging/build_deb.sh <버전>
#   예:     bash packaging/build_deb.sh 1.0.0
#
# 선행 조건: funeralv2_player 에서 `flutter build linux --release` 가 완료되어 있을 것.
#
# 산출물 (dist/):
#   funeralv2-player_<버전>_<배포판>_<arch>.deb          ← 현장 배포 주력
#   funeralv2_player-<버전>-<배포판>-<arch>.tar.gz       ← deb 를 쓸 수 없는 환경용
#
#
# ── 왜 파일 이름에 배포판이 들어가나 ────────────────────────
#
# Flutter 리눅스 빌드는 **빌드한 곳의 glibc 를 그대로 요구한다.**
# Debian 13 trixie(glibc 2.41)에서 빌드한 바이너리는 Ubuntu 24.04(glibc 2.39)에서
# 실행되지 않는다 — 그 반대는 된다. 그래서 배포판마다 따로 빌드해야 하고,
# 산출물이 섞이지 않게 이름으로 구분한다.
#
# 의존 패키지 이름도 배포판마다 다르다(jammy 는 libmpv1·libgtk-3-0,
# noble/trixie 는 libmpv2·libgtk-3-0t64). 그래서 `/etc/os-release` 를 보고 고른다.
# 빌드하는 컨테이너·러너가 곧 대상 환경이므로 이 판단이 맞다.
#
# 환경변수로 덮어쓸 수 있다.
#   PLAYER_DISTRO_TAG    파일 이름에 넣을 배포판 태그 (debian13 · ubuntu24 …)
#   PLAYER_DEB_DEPENDS   Depends 줄 전체
#   PLAYER_DEB_ARCH      dpkg 아키텍처 (기본: dpkg --print-architecture)
#   PLAYER_OS_RELEASE    배포판을 읽을 파일 (기본: /etc/os-release)
#   DEB_MAINTAINER       패키지 관리자 주소
#
# 뒤의 둘은 판별 결과를 확인해 볼 수 있게 열어 둔 것이다.
#   PLAYER_DEB_ARCH=arm64 PLAYER_OS_RELEASE=/tmp/os bash packaging/build_deb.sh 1.0.0
#
set -euo pipefail

VERSION="${1:?사용법: bash packaging/build_deb.sh <버전>}"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP_DIR="$ROOT/funeralv2_player"
DIST="$ROOT/dist"

PKG_NAME="funeralv2-player"
ARCH="${PLAYER_DEB_ARCH:-$(dpkg --print-architecture)}"

# Flutter 의 리눅스 출력 폴더는 **dpkg 아키텍처 이름과 다르다.**
#   dpkg   : amd64 / arm64
#   Flutter: x64   / arm64      ← x86_64 만 이름이 갈린다
# 예전에는 $ARCH 를 그대로 경로에 썼다가 amd64 에서 산출물을 못 찾고 죽었다.
case "$ARCH" in
  amd64) FLUTTER_ARCH='x64' ;;
  *)     FLUTTER_ARCH="$ARCH" ;;
esac
BUNDLE="$APP_DIR/build/linux/$FLUTTER_ARCH/release/bundle"

# 패키지 관리자 주소. 필요하면 DEB_MAINTAINER 환경변수로 덮어쓴다.
MAINTAINER="${DEB_MAINTAINER:-quristyle <quristyle@users.noreply.github.com>}"

# ---------------------------------------------------------------------------
# 대상 배포판 판별
# ---------------------------------------------------------------------------
#
# 여기서 정하는 것은 둘이다.
#   DISTRO_TAG   파일 이름에 넣을 짧은 이름
#   DEPENDS      .deb 의 Depends 줄
#
# 모르는 배포판이면 noble/trixie 기준(t64 이후)을 쓴다. 요즘 배포판은 대부분
# 그쪽이고, 틀리면 설치할 때 apt 가 없는 패키지를 알려 주므로 조용히 깨지지 않는다.
detect_target() {
  local id='' ver=''
  local os_release="${PLAYER_OS_RELEASE:-/etc/os-release}"

  # `/etc/os-release` 는 **VERSION 이라는 이름을 쓴다**
  # (예: VERSION="24.04.4 LTS (Noble Numbat)"). 아래에서 그것을 읽어 들이면
  # 이 스크립트가 인자로 받은 **패키지 버전이 통째로 덮인다.**
  # 그러면 control 파일의 Version 이 '24.04.4 LTS (Noble Numbat)' 이 되어
  # dpkg-deb 가 "version string has embedded spaces" 로 거절한다.
  # 그래서 함수 안에서 local 로 가려 두고, 읽은 뒤에는 쓰지 않는다.
  local VERSION VERSION_CODENAME PRETTY_NAME NAME ID_LIKE

  if [ -r "$os_release" ]; then
    # shellcheck disable=SC1091
    . "$os_release"
    id="${ID:-}"
    ver="${VERSION_ID:-}"
  fi

  # t64(64비트 time_t) 전환 이후 이름. Debian 13+ · Ubuntu 24.04+ 가 여기에 든다.
  local deps_t64='libmpv2, libgtk-3-0t64, libepoxy0, libsqlite3-0, cage, wlr-randr'
  # 전환 이전 이름. Ubuntu 22.04 · Debian 12 가 여기에 든다.
  local deps_pre='libmpv1, libgtk-3-0, libepoxy0, libsqlite3-0, cage, wlr-randr'

  case "$id:$ver" in
    debian:12*)  DISTRO_TAG='debian12'; DEPENDS="$deps_pre" ;;
    debian:13*)  DISTRO_TAG='debian13'; DEPENDS="$deps_t64" ;;
    debian:*)    DISTRO_TAG="debian${ver%%.*}"; DEPENDS="$deps_t64" ;;
    ubuntu:22*)  DISTRO_TAG='ubuntu22'; DEPENDS="$deps_pre" ;;
    ubuntu:24*)  DISTRO_TAG='ubuntu24'; DEPENDS="$deps_t64" ;;
    ubuntu:*)    DISTRO_TAG="ubuntu${ver%%.*}"; DEPENDS="$deps_t64" ;;
    *)           DISTRO_TAG='linux';    DEPENDS="$deps_t64" ;;
  esac

  # 명시로 준 값이 있으면 그것을 쓴다.
  DISTRO_TAG="${PLAYER_DISTRO_TAG:-$DISTRO_TAG}"
  DEPENDS="${PLAYER_DEB_DEPENDS:-$DEPENDS}"
}

detect_target
echo "대상: $DISTRO_TAG / $ARCH"
echo "의존: $DEPENDS"

# tar.gz 의 README 에 넣을 apt 설치 줄 (Depends 를 그대로 쓴다)
APT_LINE="sudo apt install -y $(echo "$DEPENDS" | sed 's/,//g')"

if [ ! -x "$BUNDLE/funeralv2_player" ]; then
  echo "오류: 빌드 산출물이 없다 → $BUNDLE" >&2
  echo "      먼저 'flutter build linux --release' 를 실행할 것." >&2
  exit 1
fi

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
mkdir -p "$DIST"

# ---------------------------------------------------------------------------
# 공통 구성요소: cage 런처 스크립트와 systemd 유닛
# ---------------------------------------------------------------------------

# cage(Wayland 키오스크 컴포지터) 안에서 실행되는 런처.
# wlroots 계열 컴포지터는 커널의 video= 파라미터를 무시하고 EDID 선호 모드를 쓰기 때문에,
# 출력 해상도는 이렇게 컴포지터가 뜬 뒤 wlr-randr 로 잡아야 한다.
# (4K 패널이 3840x2160@30Hz 로 잡히면 사이니지 재생이 끊긴다)
write_launcher() {
  cat > "$1" <<'LAUNCHER'
#!/bin/sh
# cage 내부에서 실행된다. 출력 모드를 고정한 뒤 플레이어를 exec 한다.
set -eu

[ -r /etc/default/funeralv2-player ] && . /etc/default/funeralv2-player

PLAYER_OUTPUT="${PLAYER_OUTPUT:-HDMI-A-1}"
PLAYER_OUTPUT_MODE="${PLAYER_OUTPUT_MODE:-1920x1080}"

if [ "$PLAYER_OUTPUT_MODE" != "none" ] && command -v wlr-randr >/dev/null 2>&1; then
  wlr-randr --output "$PLAYER_OUTPUT" --mode "$PLAYER_OUTPUT_MODE" >/dev/null 2>&1 || \
    echo "경고: 출력 모드 설정 실패 ($PLAYER_OUTPUT $PLAYER_OUTPUT_MODE). 기본 모드로 계속한다." >&2
fi

exec /opt/funeralv2-player/funeralv2_player "$@"
LAUNCHER
  chmod 755 "$1"
}

write_unit() {
  cat > "$1" <<'UNIT'
[Unit]
Description=Funeral Signage Player (cage kiosk)
Documentation=https://github.com/quristyle/Funeralv2
After=systemd-user-sessions.service getty@tty1.service network-online.target
Wants=network-online.target
# tty1 의 로그인 콘솔 대신 플레이어가 화면을 점유한다.
Conflicts=getty@tty1.service

[Service]
Type=simple
# 실제 실행 계정은 설치 시 postinst 가 10-user.conf 드롭인으로 덮어쓴다.
User=quri
# PAMName=login 이 있어야 logind 세션이 만들어지고 seat0(DRM/입력) 접근이 가능하다.
PAMName=login
TTYPath=/dev/tty1
TTYReset=yes
TTYVHangup=yes
StandardInput=tty-fail
StandardOutput=journal
StandardError=journal
EnvironmentFile=-/etc/default/funeralv2-player
Environment=XDG_RUNTIME_DIR=/run/user/1000
ExecStart=/usr/bin/cage -- /usr/bin/funeralv2-player-session
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
UNIT
  chmod 644 "$1"
}

write_defaults() {
  cat > "$1" <<'DEFAULTS'
# funeralv2-player 실행 설정
#
# PLAYER_OUTPUT       출력 커넥터 이름. `wlr-randr` 로 확인한다. (예: HDMI-A-1, HDMI-A-2)
# PLAYER_OUTPUT_MODE  고정할 해상도(예: 1920x1080). "none" 이면 모드를 건드리지 않는다.
#                     주사율은 붙이지 않는다 - 패널마다 59.9 등으로 달라 정확히 맞지 않으면 거부된다.
#
PLAYER_OUTPUT=HDMI-A-1
PLAYER_OUTPUT_MODE=1920x1080
DEFAULTS
  chmod 644 "$1"
}

# ---------------------------------------------------------------------------
# .deb
# ---------------------------------------------------------------------------
DEB_ROOT="$STAGE/deb"
mkdir -p "$DEB_ROOT/DEBIAN" \
         "$DEB_ROOT/opt/$PKG_NAME" \
         "$DEB_ROOT/usr/bin" \
         "$DEB_ROOT/lib/systemd/system" \
         "$DEB_ROOT/etc/default"

cp -a "$BUNDLE/." "$DEB_ROOT/opt/$PKG_NAME/"
write_launcher "$DEB_ROOT/usr/bin/funeralv2-player-session"
write_unit     "$DEB_ROOT/lib/systemd/system/funeral-player.service"
write_defaults "$DEB_ROOT/etc/default/$PKG_NAME"

INSTALLED_SIZE="$(du -sk "$DEB_ROOT" | cut -f1)"

cat > "$DEB_ROOT/DEBIAN/control" <<CONTROL
Package: $PKG_NAME
Version: $VERSION
Section: video
Priority: optional
Architecture: $ARCH
Maintainer: $MAINTAINER
Installed-Size: $INSTALLED_SIZE
Depends: $DEPENDS
Description: 장례식장 사이니지 플레이어 ($DISTRO_TAG)
 funeralv2 사이니지 플레이어. cage(Wayland 키오스크 컴포지터) 위에서
 전체화면으로 동작하며, systemd 로 부팅 시 자동 실행된다.
 한글 폰트는 앱에 번들되어 있어 별도 설치가 필요 없다.
 .
 이 패키지는 $DISTRO_TAG 에서 빌드했다. Flutter 리눅스 빌드는 빌드한 곳의
 glibc 를 요구하므로 더 낮은 버전의 배포판에서는 실행되지 않는다.
CONTROL

echo "/etc/default/$PKG_NAME" > "$DEB_ROOT/DEBIAN/conffiles"

cat > "$DEB_ROOT/DEBIAN/postinst" <<'POSTINST'
#!/bin/sh
set -e

case "$1" in
  configure)
    # 실행 계정을 uid 1000(라즈베리파이 OS 의 기본 사용자)으로 맞춘다.
    # 유닛 파일을 직접 고치지 않고 드롭인으로 덮어써서 패키지 업그레이드에도 살아남게 한다.
    user_name="$(getent passwd 1000 | cut -d: -f1 || true)"
    if [ -n "$user_name" ]; then
      mkdir -p /etc/systemd/system/funeral-player.service.d
      cat > /etc/systemd/system/funeral-player.service.d/10-user.conf <<EOF
[Service]
User=$user_name
Environment=XDG_RUNTIME_DIR=/run/user/1000
EOF
    else
      echo "경고: uid 1000 사용자를 찾지 못했다. funeral-player.service 의 User= 를 직접 지정할 것." >&2
    fi

    systemctl daemon-reload >/dev/null 2>&1 || true
    systemctl enable funeral-player.service >/dev/null 2>&1 || true

    # 업그레이드로 이미 돌고 있던 경우에만 즉시 재시작한다.
    # 신규 설치 때는 tty1 을 갑자기 가로채지 않도록 재부팅(또는 수동 start)을 기다린다.
    if systemctl is-active --quiet funeral-player.service; then
      systemctl restart funeral-player.service >/dev/null 2>&1 || true
    else
      echo "설치 완료. 재부팅하거나 'sudo systemctl start funeral-player.service' 로 시작한다."
    fi
    ;;
esac

exit 0
POSTINST

cat > "$DEB_ROOT/DEBIAN/prerm" <<'PRERM'
#!/bin/sh
set -e

case "$1" in
  remove|deconfigure)
    systemctl disable --now funeral-player.service >/dev/null 2>&1 || true
    ;;
esac

exit 0
PRERM

cat > "$DEB_ROOT/DEBIAN/postrm" <<'POSTRM'
#!/bin/sh
set -e

case "$1" in
  purge)
    rm -rf /etc/systemd/system/funeral-player.service.d
    ;;
esac

systemctl daemon-reload >/dev/null 2>&1 || true
exit 0
POSTRM

chmod 755 "$DEB_ROOT/DEBIAN/postinst" "$DEB_ROOT/DEBIAN/prerm" "$DEB_ROOT/DEBIAN/postrm"

DEB_FILE="$DIST/${PKG_NAME}_${VERSION}_${DISTRO_TAG}_${ARCH}.deb"
dpkg-deb --root-owner-group --build "$DEB_ROOT" "$DEB_FILE" >/dev/null
echo "생성: $DEB_FILE"

# ---------------------------------------------------------------------------
# .tar.gz (deb 를 쓸 수 없는 환경용)
# ---------------------------------------------------------------------------
TAR_NAME="funeralv2_player-${VERSION}-${DISTRO_TAG}-${ARCH}"
TAR_ROOT="$STAGE/$TAR_NAME"
mkdir -p "$TAR_ROOT/bundle" "$TAR_ROOT/systemd" "$TAR_ROOT/bin"

cp -a "$BUNDLE/." "$TAR_ROOT/bundle/"
write_launcher "$TAR_ROOT/bin/funeralv2-player-session"
write_unit     "$TAR_ROOT/systemd/funeral-player.service"
write_defaults "$TAR_ROOT/funeralv2-player.default"

cat > "$TAR_ROOT/README.txt" <<README
funeralv2_player $VERSION ($DISTRO_TAG/$ARCH)

이 빌드는 $DISTRO_TAG 에서 만들었다. Flutter 리눅스 빌드는 빌드한 곳의 glibc 를
요구하므로 **더 낮은 버전의 배포판에서는 실행되지 않는다.**
(trixie 빌드 → Ubuntu 24.04 에서 실행 안 됨. 반대는 된다.)

가능하면 .deb 를 쓸 것. 이 tar.gz 는 의존성 설치와 배치를 직접 해야 한다.

1) 런타임 의존성 설치
   $APT_LINE

2) 배치
   sudo cp -a bundle /opt/funeralv2-player
   sudo install -m 755 bin/funeralv2-player-session /usr/bin/
   sudo install -m 644 systemd/funeral-player.service /lib/systemd/system/
   sudo install -m 644 funeralv2-player.default /etc/default/funeralv2-player

3) 실행 계정 지정 (기본 유닛은 User=quri 로 되어 있다)
   sudo mkdir -p /etc/systemd/system/funeral-player.service.d
   printf '[Service]\nUser=%s\n' "\$(id -un 1000)" | \\
     sudo tee /etc/systemd/system/funeral-player.service.d/10-user.conf

4) 활성화
   sudo systemctl daemon-reload
   sudo systemctl enable --now funeral-player.service

해상도는 /etc/default/funeralv2-player 에서 조정한다.

주의 — 데스크톱 배포판(Ubuntu Desktop 등)
  이 서비스는 tty1 을 가져가 화면을 점유한다(Conflicts=getty@tty1.service).
  GDM 같은 디스플레이 매니저가 이미 화면을 쓰고 있으면 서로 부딪힌다.
  키오스크로 쓸 기기라면 서버/최소 설치를 쓰거나 디스플레이 매니저를 끈다.

    sudo systemctl disable --now gdm3    # 또는 lightdm · sddm
    sudo systemctl set-default multi-user.target
README

TAR_FILE="$DIST/${TAR_NAME}.tar.gz"
tar -czf "$TAR_FILE" -C "$STAGE" "$TAR_NAME"
echo "생성: $TAR_FILE"

ls -lh "$DIST"
