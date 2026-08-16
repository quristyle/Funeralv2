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
#   funeralv2-player_<버전>_<arch>.deb            ← 현장 배포 주력
#   funeralv2_player-<버전>-linux-<arch>.tar.gz   ← deb 를 쓸 수 없는 환경용
#
set -euo pipefail

VERSION="${1:?사용법: bash packaging/build_deb.sh <버전>}"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP_DIR="$ROOT/funeralv2_player"
DIST="$ROOT/dist"

PKG_NAME="funeralv2-player"
ARCH="$(dpkg --print-architecture)"
BUNDLE="$APP_DIR/build/linux/$ARCH/release/bundle"

# 패키지 관리자 주소. 필요하면 DEB_MAINTAINER 환경변수로 덮어쓴다.
MAINTAINER="${DEB_MAINTAINER:-quristyle <quristyle@users.noreply.github.com>}"

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
PLAYER_OUTPUT_MODE="${PLAYER_OUTPUT_MODE:-1920x1080@60}"

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
# PLAYER_OUTPUT_MODE  고정할 해상도@주사율. "none" 이면 모드를 건드리지 않는다.
#
PLAYER_OUTPUT=HDMI-A-1
PLAYER_OUTPUT_MODE=1920x1080@60
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
Depends: libmpv2, libgtk-3-0t64, libepoxy0, libsqlite3-0, cage, wlr-randr
Description: 장례식장 사이니지 플레이어
 funeralv2 사이니지 플레이어. cage(Wayland 키오스크 컴포지터) 위에서
 전체화면으로 동작하며, systemd 로 부팅 시 자동 실행된다.
 한글 폰트는 앱에 번들되어 있어 별도 설치가 필요 없다.
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

DEB_FILE="$DIST/${PKG_NAME}_${VERSION}_${ARCH}.deb"
dpkg-deb --root-owner-group --build "$DEB_ROOT" "$DEB_FILE" >/dev/null
echo "생성: $DEB_FILE"

# ---------------------------------------------------------------------------
# .tar.gz (deb 를 쓸 수 없는 환경용)
# ---------------------------------------------------------------------------
TAR_NAME="funeralv2_player-${VERSION}-linux-${ARCH}"
TAR_ROOT="$STAGE/$TAR_NAME"
mkdir -p "$TAR_ROOT/bundle" "$TAR_ROOT/systemd" "$TAR_ROOT/bin"

cp -a "$BUNDLE/." "$TAR_ROOT/bundle/"
write_launcher "$TAR_ROOT/bin/funeralv2-player-session"
write_unit     "$TAR_ROOT/systemd/funeral-player.service"
write_defaults "$TAR_ROOT/funeralv2-player.default"

cat > "$TAR_ROOT/README.txt" <<README
funeralv2_player $VERSION (linux/$ARCH)

가능하면 .deb 를 쓸 것. 이 tar.gz 는 의존성 설치와 배치를 직접 해야 한다.

1) 런타임 의존성 설치
   sudo apt install -y libmpv2 libgtk-3-0t64 libepoxy0 libsqlite3-0 cage wlr-randr

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
README

TAR_FILE="$DIST/${TAR_NAME}.tar.gz"
tar -czf "$TAR_FILE" -C "$STAGE" "$TAR_NAME"
echo "생성: $TAR_FILE"

ls -lh "$DIST"
