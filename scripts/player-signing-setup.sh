#!/usr/bin/env bash
#
# 플레이어 안드로이드 **릴리스 서명**을 이 장비에 설치한다.
#
# 이 장비에서 만든 release APK 는 기본적으로 **디버그 키**로 서명된다
# (funeralv2_player/android/app/build.gradle.kts 의 주석 참고).
# 디버그 키는 장비마다 다르므로, 그 APK 를 현장 기기에 업데이트로 넣으면 거부된다
# (INSTALL_FAILED_UPDATE_INCOMPATIBLE). CI 가 쓰는 키를 여기에도 두면 그 문제가 없어진다.
#
# ── 왜 스크립트로 만들었나 ────────────────────────────────────────────────
#
# **지문 대조가 핵심이다.** 엉뚱한 키스토어로 설정해도 빌드는 성공한다.
# 문제는 그 APK 를 현장 기기에 넣을 때가 되어서야 드러난다 — 그때는 이미
# "새 버전을 배포했다" 고 알린 뒤다. 그래서 여기서 먼저 막는다:
# 키스토어의 인증서 지문이 **실제로 배포된 APK 의 지문**과 다르면 아무것도 쓰지 않는다.
#
# ── 쓰는 법 ───────────────────────────────────────────────────────────────
#
#   scripts/player-signing-setup.sh /경로/release.jks
#
# 비밀번호는 **물어본다.** 명령줄 인자로 받지 않는다 — 셸 이력에 남기 때문이다.
#
# 키스토어(release.jks)는 이 저장소에도, 운영서버에도 없다.
# GitHub Actions secret `ANDROID_KEYSTORE_BASE64` 안에만 있고 **secret 값은 되읽을 수
# 없다**(쓰기 전용). 그래서 파일은 사람이 가져와야 한다.
#
set -euo pipefail

# 실제 배포된 APK(v1.0.0)에서 뽑은 서명 인증서 지문.
#   apksigner verify --print-certs funeralv2_player-1.0.0-android-releasesigned.apk
#   → CN=Quristyle, OU=JSini, O=JSini, L=Dong, ST=Ulsan, C=KR / RSA 4096
# **비밀이 아니다.** 배포된 APK 를 가진 사람이면 누구나 계산할 수 있는 공개 값이다.
EXPECTED_SHA256="cfd76065a0dc08b2b002ddf9d61244d20fee0e88586602f07687976ef51a32d5"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ANDROID_DIR="$REPO_ROOT/funeralv2_player/android"
DEST_JKS="$ANDROID_DIR/release.jks"
PROPS="$ANDROID_DIR/key.properties"

die() { echo "[중단] $*" >&2; exit 1; }

# ── keytool 찾기 ──────────────────────────────────────────────────────────
find_keytool() {
  if [ -n "${JAVA_HOME:-}" ] && [ -x "$JAVA_HOME/bin/keytool" ]; then
    echo "$JAVA_HOME/bin/keytool"; return
  fi
  if [ -n "${JAVA_HOME:-}" ] && [ -x "$JAVA_HOME/bin/keytool.exe" ]; then
    echo "$JAVA_HOME/bin/keytool.exe"; return
  fi
  # 윈도우 개발 장비에는 Android Studio 번들 JDK 가 있다 (flutter doctor 가 쓰는 것).
  local jbr="/c/Program Files/Android/Android Studio/jbr/bin/keytool.exe"
  [ -x "$jbr" ] && { echo "$jbr"; return; }
  command -v keytool 2>/dev/null && return
  die "keytool 을 찾지 못했다. JAVA_HOME 을 설정하거나 JDK 를 PATH 에 넣는다."
}

KEYTOOL="$(find_keytool)"

# ── 인자 ──────────────────────────────────────────────────────────────────
[ $# -eq 1 ] || die "쓰는 법: $(basename "$0") /경로/release.jks"
SRC_JKS="$1"
[ -f "$SRC_JKS" ] || die "키스토어 파일이 없다: $SRC_JKS"
[ -d "$ANDROID_DIR" ] || die "플레이어 폴더가 없다: $ANDROID_DIR"

echo "키스토어: $SRC_JKS"
echo "keytool : $KEYTOOL"
echo

# ── 저장소 비밀번호 ───────────────────────────────────────────────────────
printf '키스토어 비밀번호(storePassword): '
read -rs STORE_PASS
echo
[ -n "$STORE_PASS" ] || die "비밀번호가 비어 있다."

# **영어 출력을 강제한다.** 한국어 윈도우의 keytool 은 CP949 로 찍는다.
# 그러면 이 스크립트(UTF-8)의 한글 패턴이 맞지 않아 별칭을 못 읽는다 —
# 실제로 이 장비에서 겪었다. `SHA256:` 처럼 ASCII 인 것만 맞는다.
LIST="$("$KEYTOOL" -J-Duser.language=en -J-Duser.country=US \
  -list -v -keystore "$SRC_JKS" -storepass "$STORE_PASS" 2>&1)" \
  || die "키스토어를 열지 못했다. 비밀번호가 틀렸거나 파일이 손상됐다."

# ── 지문 대조 — 여기서 걸러야 한다 ────────────────────────────────────────
ACTUAL="$(printf '%s' "$LIST" \
  | grep -i 'SHA256:' | head -1 \
  | sed 's/.*SHA256://' | tr -d ' :\r' | tr 'A-Z' 'a-z')"

[ -n "$ACTUAL" ] || die "인증서 지문을 읽지 못했다."

if [ "$ACTUAL" != "$EXPECTED_SHA256" ]; then
  echo "[불일치] 이 키스토어는 배포된 APK 를 서명한 키가 아니다." >&2
  echo "  기대: $EXPECTED_SHA256" >&2
  echo "  실제: $ACTUAL" >&2
  echo >&2
  echo "이 키로 서명하면 빌드는 되지만 **현장 기기가 업데이트를 거부한다.**" >&2
  echo "아무것도 쓰지 않고 멈춘다." >&2
  exit 1
fi
echo "[확인] 인증서 지문이 배포된 APK 와 같다."

# ── 별칭 ──────────────────────────────────────────────────────────────────
ALIASES="$(printf '%s' "$LIST" | grep -i 'Alias name' | sed 's/.*: *//' | tr -d '\r')"
COUNT="$(printf '%s\n' "$ALIASES" | grep -c . || true)"
if [ "$COUNT" = "1" ]; then
  KEY_ALIAS="$ALIASES"
  echo "[확인] 별칭: $KEY_ALIAS (키스토어에 하나뿐이라 그대로 쓴다)"
else
  echo "키스토어에 별칭이 여럿이다:"
  printf '  %s\n' $ALIASES
  printf '쓸 별칭(keyAlias): '
  read -r KEY_ALIAS
fi
[ -n "$KEY_ALIAS" ] || die "별칭이 비어 있다."

# ── 키 비밀번호 ───────────────────────────────────────────────────────────
printf '키 비밀번호(keyPassword, 저장소와 같으면 그냥 Enter): '
read -rs KEY_PASS
echo
[ -n "$KEY_PASS" ] || KEY_PASS="$STORE_PASS"

# 키 비밀번호가 맞는지 실제로 써 본다. 틀리면 빌드가 서명 단계에서 실패하는데,
# 그때 나오는 메시지로는 원인을 알기 어렵다.
"$KEYTOOL" -J-Duser.language=en -J-Duser.country=US \
  -certreq -alias "$KEY_ALIAS" -keystore "$SRC_JKS" \
  -storepass "$STORE_PASS" -keypass "$KEY_PASS" >/dev/null 2>&1 \
  || die "키 비밀번호가 틀렸다(또는 별칭이 없다). 아무것도 쓰지 않았다."
echo "[확인] 키 비밀번호가 맞다."

# ── 쓰기 ──────────────────────────────────────────────────────────────────
STAMP="$(date +%Y%m%d-%H%M%S)"
[ -f "$PROPS" ] && { cp "$PROPS" "$PROPS.bak.$STAMP"; echo "기존 key.properties 를 백업했다."; }
[ -f "$DEST_JKS" ] && { cp "$DEST_JKS" "$DEST_JKS.bak.$STAMP"; echo "기존 release.jks 를 백업했다."; }

cp "$SRC_JKS" "$DEST_JKS"

# CI(.github/workflows/release.yml)가 만드는 것과 **같은 네 줄**이다.
# storeFile 은 android/ 기준 상대 경로다 (build.gradle.kts 가 rootProject.file 로 푼다).
umask 077
cat > "$PROPS" <<EOF
storeFile=release.jks
storePassword=$STORE_PASS
keyAlias=$KEY_ALIAS
keyPassword=$KEY_PASS
EOF
chmod 600 "$PROPS" 2>/dev/null || true

echo
echo "완료. 둘 다 .gitignore 에 있어 커밋되지 않는다."
echo "  $DEST_JKS"
echo "  $PROPS"
echo
echo "이제 이렇게 빌드하면 릴리스 키로 서명된다:"
echo "  cd funeralv2_player && flutter build apk --release"
echo
echo "빌드한 APK 의 서명을 확인하려면:"
echo "  apksigner verify --print-certs build/app/outputs/flutter-apk/app-release.apk"
echo "  → SHA-256 이 $EXPECTED_SHA256 이어야 한다."
