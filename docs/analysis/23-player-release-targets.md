# 플레이어 배포 대상 확대 (Ubuntu 추가)

작성: 2026-08-25
대상: `.github/workflows/release.yml` · `packaging/build_deb.sh` ·
`views/funeral/player-download/index.vue`

> 지시: "`/system/player-download` 은 github 에 푸시되면 자동으로 설치용 파일이 만들어 지고
> 그 파일을 다운로드 하도록 하는 화면이다. 우분투 os 에 사용할 항목도 추가해줘.
> 그에 따라 처리 필요한 git runner 처리를 위한 파일도 개선하라. 일반적으로 동작시키는
> 다른 os 도 git runner 로 생성할수 있다면 작성하고 추가 해줘."

---

## 1. 먼저 알아야 할 제약 — 리눅스는 배포판마다 따로 빌드해야 한다

Ubuntu 항목을 "카드 하나 더" 로 끝낼 수 없었던 이유다.

Flutter 리눅스 빌드는 **빌드한 곳의 glibc 를 그대로 요구한다.**

| 배포판 | glibc |
|---|---|
| Debian 13 trixie (Raspberry Pi OS Lite 64-bit) | 2.41 |
| Ubuntu 24.04 LTS | 2.39 |

그래서 지금까지 만들던 trixie 빌드는 **Ubuntu 24.04 에서 실행되지 않는다.**
(반대는 된다 — 낮은 glibc 로 빌드한 것은 높은 곳에서 돈다.)

의존 패키지 이름도 갈린다. 64비트 `time_t` 전환(t64) 전후로 다르다.

| | libmpv | libgtk |
|---|---|---|
| Debian 13 · Ubuntu 24.04 이상 | `libmpv2` | `libgtk-3-0t64` |
| Debian 12 · Ubuntu 22.04 | `libmpv1` | `libgtk-3-0` |

**결론: 대상 환경과 같은 곳에서 빌드하고, 산출물을 이름으로 구분한다.**

## 2. 무엇을 만들게 되었나

| 자산 | 대상 | 상태 |
|---|---|---|
| `funeralv2_player-<ver>-windows-x64.zip` | Windows 10/11 x64 | 기존 |
| `funeralv2-player_<ver>_debian13_arm64.deb` | 라즈베리파이 (Pi OS Lite 64-bit) | 기존 (이름 변경) |
| `funeralv2_player-<ver>-debian13-arm64.tar.gz` | 위의 수동 설치용 | 기존 (이름 변경) |
| `funeralv2-player_<ver>_ubuntu24_amd64.deb` | **Ubuntu 24.04 x64 (미니PC)** | 신규 |
| `funeralv2_player-<ver>-ubuntu24-amd64.tar.gz` | 위의 수동 설치용 | 신규 |
| `funeralv2-player_<ver>_ubuntu24_arm64.deb` | **Ubuntu 24.04 arm64 (Jetson 등)** | 신규 |
| `funeralv2_player-<ver>-ubuntu24-arm64.tar.gz` | 위의 수동 설치용 | 신규 |

Ubuntu x64 는 사이니지에 가장 흔한 구성(미니PC)이라 넣었고,
Ubuntu arm64 는 **trixie 빌드가 거기서 안 돌기 때문에** 따로 필요하다.

### 이름을 바꿔야 했던 이유

예전 이름은 `funeralv2-player_<ver>_arm64.deb` 였고, 다운로드 화면의 라즈베리파이 카드는
`.deb` 로 끝나는 자산을 골랐다. Ubuntu 용 `.deb` 가 생기면 **둘 중 아무 것이나 집어 온다.**
그래서 이름에 배포판을 넣고 화면의 matcher 도 배포판·아키텍처까지 함께 보게 고쳤다.

## 3. 러너 파일 — job 을 어떻게 나눴나

```
windows                Windows x64                     (기존 그대로)
linux-debian13-arm64   라즈베리파이 · debian:trixie 컨테이너  (기존, 이름만 변경)
linux-ubuntu24         Ubuntu x64 · arm64 (매트릭스)      ← 신규
release                자산 모아 체크섬 + 릴리스 첨부       (needs 갱신)
```

### 왜 리눅스 셋을 한 매트릭스로 묶지 않았나

처음에는 셋을 한 매트릭스에 넣고 `container: ${{ matrix.container }}` 에 빈 문자열을 주어
컨테이너를 껐다. 그런데 **빈 문자열로 컨테이너를 끄는 동작이 확실하지 않다.**
여기서는 GitHub Actions 를 돌려 볼 수 없어 확인할 방법도 없다.

이미 잘 돌고 있는 라즈베리파이 job 을 그 불확실성에 걸 이유가 없어서,
컨테이너를 쓰는 job(파이)과 러너에서 바로 도는 job(Ubuntu)으로 나눴다.
Ubuntu 둘은 러너만 다르므로 그 안에서 매트릭스로 돈다.

정리하면서 함께 처리한 것들:

- **`fail-fast: false`** — Ubuntu 한쪽이 깨져도 다른 쪽 설치 파일은 나온다.
  기본값(true)이면 하나 실패에 전부 취소된다.
- **Flutter SDK 는 arm64·x64 모두 `git clone`** 으로 설치한다.
  `subosito/flutter-action` 은 linux-arm64 용 공식 아카이브가 없어 x64 SDK 를 받아와 실패한다.
  x64 는 action 을 쓸 수 있지만 두 경로를 나누지 않는 편이 단순하다.
- **`libstdc++` 버전이 다르다.** trixie 의 기본 GCC 는 14, noble 은 13 이다.
- **`ls -lh dist/` 단계를 넣었다.** 이름 규칙이 어긋나면 다운로드 화면이 자산을 못 찾는데,
  릴리스가 끝난 뒤에야 알게 된다. 로그에 남겨 두면 바로 보인다.

## 4. `build_deb.sh` — 배포판을 스스로 알아본다

`/etc/os-release` 를 읽어 파일 이름 태그와 `Depends` 를 정한다.
빌드하는 컨테이너·러너가 곧 대상 환경이므로 이 판단이 맞다.

```
debian:12*  → debian12 / libmpv1 · libgtk-3-0
debian:13*  → debian13 / libmpv2 · libgtk-3-0t64
ubuntu:22*  → ubuntu22 / libmpv1 · libgtk-3-0
ubuntu:24*  → ubuntu24 / libmpv2 · libgtk-3-0t64
그 밖       → linux    / t64 기준 (요즘 배포판은 대부분 그쪽이다)
```

모르는 배포판을 t64 기준으로 두는 것은, 틀렸을 때 **apt 가 없는 패키지를 알려 주므로
조용히 깨지지 않기** 때문이다.

덮어쓸 수 있는 환경변수: `PLAYER_DISTRO_TAG` · `PLAYER_DEB_DEPENDS` ·
`PLAYER_DEB_ARCH` · `PLAYER_OS_RELEASE` · `DEB_MAINTAINER`.
뒤의 둘은 판별 결과를 확인해 볼 수 있게 열어 둔 것이다(교차 패키징에도 쓸 수 있다).

`.deb` 설명과 `tar.gz` 의 README 에도 "이 빌드는 어느 배포판에서 만들었고
더 낮은 배포판에서는 실행되지 않는다" 를 적어 둔다. 파일만 받아 간 사람도 알 수 있어야 한다.

### 데스크톱 배포판 주의사항을 추가했다

서비스 유닛은 `Conflicts=getty@tty1.service` 로 tty1 을 가져간다.
라즈베리파이 OS Lite 는 문제가 없지만 **Ubuntu Desktop 은 GDM 이 이미 화면을 쓰고 있어 부딪힌다.**
README·릴리스 노트·화면 카드 셋 모두에 끄는 방법을 적었다.

```
sudo systemctl disable --now gdm3
sudo systemctl set-default multi-user.target
```

## 5. 다운로드 화면

카드가 셋 → 여섯이 되었다. matcher 를 배포판·아키텍처까지 보게 고치고,
"리눅스는 배포판에 맞는 파일을 받아야 한다" 는 안내 띠를 위에 두었다.
이걸 모르면 debian13 파일을 Ubuntu 에 넣고 "실행이 안 된다" 로만 보인다.

카드가 두 줄이 되어 화면을 넘치므로, 준수사항 4 에 맞춰 **카드 영역만 안에서 스크롤**한다
(머리글과 안내는 고정). `Spin` 으로 감싸지 않고 겹쳐 띄운다 —
antd 가 안쪽에 감싸개를 하나 더 만들어 `h-full` 사슬이 끊긴다.

## 6. Android TV 박스 (2026-08-25 추가)

> 지시: "Android TV 박스 용 을 만든다면 서명키 없이 만들수는 없는가?
> 서명키 없이 만들어도 설치가 가능하지 않는가?"

### 앞서 쓴 판단이 틀렸다

처음에 "릴리스 APK 는 서명 키가 필요하고, 데스크톱 전용 패키지 때문에 동작 확인이 먼저다" 라고
적었다. **네 가지 중 셋이 사실이 아니었다.** 확인한 내용:

| 걸림돌이라고 적었던 것 | 실제 |
|---|---|
| 릴리스 APK 에 서명 키가 필요하다 | **아니다.** `android/app/build.gradle.kts` 가 Flutter 기본값 그대로 `signingConfig = signingConfigs.getByName("debug")` 다. `flutter build apk --release` 가 **설치 가능한 APK** 를 만든다 |
| `INTERNET` 권한이 없을 것 | **이미 있다.** main 매니페스트에 `INTERNET` · `ACCESS_NETWORK_STATE` · `usesCleartextTraffic="true"` |
| `sqflite_common_ffi` 가 데스크톱 전용이라 깨진다 | **이미 분기돼 있다.** `local_db_service.dart` 가 web → ffi_web, Windows·Linux → ffi, **그 밖(Android) → `sqflite` 본체** |
| `media_kit` Android 설정이 따로 필요하다 | `media_kit_libs_video` 는 전체 플랫폼 번들이라 Android 라이브러리를 포함한다 |

`window_manager` 는 `main.dart` 에서 `Platform.isWindows \|\| Platform.isLinux` 로 감싸져 있어
Android 에서는 건너뛴다.

**안드로이드는 서명되지 않은 APK 를 설치하지 않는다** — 그 부분은 맞다.
다만 "서명"이 곧 "직접 만든 키"를 뜻하지는 않는다. 디버그 키로 서명된 APK 도
알 수 없는 출처 설치를 허용하면 정상 설치된다.

### 그래서 실제로 남은 문제는 둘이었다

**① 디버그 키는 빌드마다 달라진다**

`~/.android/debug.keystore` 는 기기·러너마다 새로 만들어진다. GitHub 러너는 실행마다
새 환경이라 **매 릴리스의 서명이 다르다.** 서명이 다르면 이미 깔린 앱 위에 업데이트가
거부된다(`INSTALL_FAILED_UPDATE_INCOMPATIBLE`). 현장에 설치된 키오스크를 갱신할 때 걸린다.

키가 있으면 그것으로, 없으면 디버그 키로 서명하게 했다.

```
key.properties 있음 → 그 키로 서명 (서명 고정 → 덮어쓰기 업데이트 가능)
없음               → 디버그 키     (설치는 되지만 삭제 후 재설치)
```

CI 는 secrets 네 개(`ANDROID_KEYSTORE_BASE64` · `ANDROID_KEY_ALIAS` ·
`ANDROID_KEY_PASSWORD` · `ANDROID_STORE_PASSWORD`)가 있으면 앞쪽을 탄다.
**넷이 없어도 job 은 성공한다.** 파일 이름에 어느 쪽인지 남긴다 —
`...-android-debugsigned.apk` / `...-android-releasesigned.apk`.
받는 사람이 파일 이름만 보고 덮어쓰기가 되는지 알 수 있어야 한다.

**② TV 런처에 아이콘이 뜨지 않는다**

이게 더 조용한 문제였다. Android TV 런처는 `LAUNCHER` 가 아니라
**`LEANBACK_LAUNCHER`** 카테고리를 본다. 없으면 APK 는 설치되는데 홈 화면에 나타나지 않아
**리모컨으로 실행할 방법이 없다.** 매니페스트에 넣었다.

함께 넣은 것들:

- `<uses-feature android:name="android.hardware.touchscreen" android:required="false"/>`
  — 화면을 만질 수 없는 기기(TV 박스·셋톱)가 설치 대상에서 빠지지 않게 한다.
- `<uses-feature android:name="android.software.leanback" android:required="false"/>`
  — `required="true"` 로 두면 일반 태블릿·PC 에 설치할 수 없게 된다. 둘 다 "쓸 수 있으면 쓴다" 다.
- `android:banner="@drawable/tv_banner"` — TV 런처는 아이콘이 아니라 배너를 쓴다.
  없으면 이름만 있는 빈 칸으로 보인다. 이미지 파일을 새로 만들지 않고
  **XML 레이어 목록**으로 조립했다(어두운 배경 + 기존 런처 아이콘).
  디자인이 정해지면 320x180 PNG 를 `drawable-xhdpi/tv_banner.png` 로 넣으면 그쪽이 쓰인다.

APK 는 ABI 를 나누지 않고 하나에 다 담는다(arm64 · armv7 · x86_64).
현장에서 파일 하나만 넣으면 되는 편이 낫다.

### 아직 남은 것 — 키오스크 동작

설치·실행까지는 된다. 다만 리눅스 쪽에서 `cage` + systemd 가 해 주던 것들이
안드로이드에는 아직 없다.

- **자동 시작** — 부팅 후 저절로 뜨지 않는다. `BOOT_COMPLETED` 리시버나
  홈 런처 대체(HOME 인텐트) 중 하나가 필요하다.
- **전체화면 유지** — immersive 모드, 화면 꺼짐 방지(WAKE_LOCK / `keepScreenOn`).
- **리모컨 조작** — 키오스크 화면이 터치·마우스 기준이면 D-pad 포커스가 동작하지 않는다.
  사이니지(영정·안내)처럼 보여 주기만 하는 화면은 문제가 없다.

지시 범위(설치 파일 만들기)를 넘어서므로 손대지 않았다.

## 7. 넣지 않은 OS

### macOS — 서명 없이는 실행이 막힌다

`macos-latest` 러너로 빌드는 된다. 다만 서명·공증(notarization)이 없으면 Gatekeeper 가
실행을 막고, 받는 사람이 매번 `xattr -dr com.apple.quarantine` 을 해야 한다.
안드로이드와 달리 **"기본 키로 서명" 이라는 우회로가 없다.**
사이니지 기기로 macOS 를 쓰는 경우가 없어 그 비용을 낼 이유가 없다고 판단했다.

넣으려면: Apple Developer 계정 + `codesign`·`notarytool` 용 secrets.

### iOS · Web

플레이어의 역할(HDMI 전체화면 · 자동 실행 · 로컬 캐시)과 맞지 않아 대상으로 보지 않았다.

## 7. 확인한 것 · 못 한 것

```
bash -n packaging/build_deb.sh    문법 오류 없음
배포판 판별                        6가지 경우 전부 기대값 (아래)
pnpm vite build (포털)             성공
release.yml                       prettier 로 파싱 확인 (job 5개 · needs 4개)
AndroidManifest · tv_banner       XML 태그 균형 확인
build.gradle.kts                  Gradle 이 스크립트를 컴파일하고 configure 단계까지 진행
                                  (문법·타입 오류면 여기서 '스크립트 컴파일 오류' 가 난다)
APK 실물                          **만들지 못했다** — 이 PC 의 NDK 문제 (아래)
```

배포판 판별은 `/etc/os-release` 를 가짜 파일로 바꿔 끼워 실제 스크립트를 돌려 확인했다.

| 넣은 값 | 나온 태그 | 나온 Depends |
|---|---|---|
| `debian 13` | `debian13` | `libmpv2` · `libgtk-3-0t64` … |
| `debian 12` | `debian12` | `libmpv1` · `libgtk-3-0` … |
| `ubuntu 24.04` | `ubuntu24` | `libmpv2` · `libgtk-3-0t64` … |
| `ubuntu 22.04` | `ubuntu22` | `libmpv1` · `libgtk-3-0` … |
| `alpine 3.20` | `linux` | t64 기준 |
| 환경변수 덮어쓰기 | `custom` | `libfoo, libbar` |

### 못 한 것

**리눅스·윈도우 쪽 GitHub Actions 실행은 확인하지 못했다.** 이 저장소에서 러너를 돌릴 수 없다.
확인이 필요한 것은 셋이다.

1. `ubuntu-24.04` · `ubuntu-24.04-arm` 러너에서 `flutter build linux` 가 통과하는지
   (필요 패키지를 다 적었는지 — 기존 arm64 job 의 목록에서 컨테이너 전용 항목만 뺐다)
2. `libstdc++-13-dev` 가 noble 에 있는지 (trixie 는 14, noble 은 13 이 기본이다)
3. 만들어진 자산 이름이 화면 matcher 와 맞는지 (`ls -lh dist/` 로그로 바로 보인다)

한 번 태그를 밀어 보면 세 개가 동시에 확인된다. `fail-fast: false` 라
일부가 실패해도 나머지 설치 파일은 나온다.

### 이 PC 의 NDK 가 깨져 있다 (별도 문제)

Android APK 를 로컬에서 빌드해 보다 발견했다. `build.gradle.kts` 가 핀으로 고정한
`ndkVersion = "27.0.12077973"` 의 로컬 폴더가 **`.installer` 만 남은 빈 폴더**다
(다운로드 실패본). 그래서 configure 단계에서 멈춘다.

```
[CXX1101] NDK at ...\sdk\ndk\27.0.12077973 did not have a source.properties file
```

설치된 NDK 셋 중 둘이 같은 상태였다.

| NDK | `source.properties` | 항목 수 |
|---|---|---|
| `27.0.12077973` (핀) | 없음 | 1 (`.installer` 만) |
| `28.2.13676358` | 없음 | 1 (`.installer` 만) |
| `30.0.14904198` | 있음 | 19 |

핀을 온전한 `30.0.14904198` 로 임시로 옮겨 봤지만 **그래도 막혔다.**
AGP 는 앱과 **플러그인 모듈들이 요구하는 NDK 중 가장 높은 버전**을 쓰는데,
어떤 플러그인이 `28.2.13676358` 을 요구하고 그 폴더도 깨져 있다.
즉 앱 쪽 핀을 바꾸는 것으로는 해결되지 않는다.

고치는 방법은 깨진 두 폴더를 지우는 것이다(Flutter 가 안내하는 방법).
`.installer` 파일 하나만 있는 빈 폴더라 지워도 잃는 것이 없고, AGP 가 다시 받는다.

```
rm -rf "$LOCALAPPDATA/Android/sdk/ndk/27.0.12077973" \
       "$LOCALAPPDATA/Android/sdk/ndk/28.2.13676358"
```

**저장소 설정이 아니라 이 PC 의 문제다.** CI 러너는 SDK 를 새로 받으므로 영향이 없다.
NDK 재다운로드는 용량이 크고 개발 PC 환경을 건드리는 일이라 여기서는 실행하지 않았다.
확인용으로 바꿔 둔 `ndkVersion` 은 **원래 값으로 되돌려 두었다.**
