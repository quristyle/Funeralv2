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

### 서명 키는 어디에 있나 (2026-09-02 확인)

찾을 곳을 몰라 헤매기 쉬워서 적어 둔다. **키스토어 파일의 사본은 GitHub Actions
secret 안에만 있다.**

| 찾아본 곳 | 결과 |
|---|---|
| 저장소 (`git log --all -- '*.jks'` 포함) | 없다 (`.gitignore` 가 `*.jks` · `key.properties` 를 막는다) |
| 이 개발 장비 (`~`, Downloads, Desktop, Documents, `C:\dev`, `C:\down`) | 없다 |
| 운영서버 (`/srv/jsini`, `/home/lee`) | 없다 (APK 는 CI 가 만들므로 서버에 있을 이유도 없다) |
| GitHub Actions secret `ANDROID_KEYSTORE_BASE64` | **여기 있다.** 값은 되읽을 수 없다(쓰기 전용) |

그래서 **개발 장비에서 릴리스 서명으로 빌드하려면 사람이 `release.jks` 를 가져와야 한다.**
secret 을 API 로 복호화해 오는 방법은 없다.

배포된 APK 에서 뽑은 서명 인증서는 이렇다(v1.0.0). **비밀이 아니다** — APK 를 가진
사람이면 누구나 계산할 수 있다.

```
DN       : CN=Quristyle, OU=JSini, O=JSini, L=Dong, ST=Ulsan, C=KR
알고리즘 : RSA 4096, APK Signature Scheme v2
SHA-256  : cfd76065a0dc08b2b002ddf9d61244d20fee0e88586602f07687976ef51a32d5
```

`scripts/player-signing-setup.sh` 가 이 지문을 알고 있고, **넘겨받은 키스토어의 지문이
다르면 아무것도 쓰지 않고 멈춘다.** 엉뚱한 키로 설정해도 빌드는 성공하기 때문이다 —
문제는 그 APK 를 현장 기기에 넣을 때가 되어서야 드러난다.

```bash
scripts/player-signing-setup.sh /경로/release.jks
```

> **새 키스토어를 만들어 채우면 안 된다.** 서명이 달라지면 이미 깔린 앱을 덮어쓸 수
> 없고(삭제 후 재설치뿐), 그 키를 secret 에 넣어 버리면 **현장에 깔린 모든 기기가
> 영구히 업데이트를 못 받는다.** 안드로이드는 설치된 앱의 서명을 바꿀 방법이 없다.

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

## 8. 아이콘을 JSINI 마크로 바꿨다 (2026-09-02)

> 지시: "최종 배포된 결과물을 확인하면 아이콘이 jsini 심볼을 사용하고 있지 않다.
> jsini 심볼을 사용하도록 변경해줘."

맞다. 릴리스되던 것은 **Flutter 기본 아이콘 그대로**였다 — 안드로이드 `ic_launcher.png`
다섯 밀도, 윈도우 `app_icon.ico` 가 손대지 않은 템플릿 파일이었다.
TV 배너는 그 기본 아이콘을 어두운 판 가운데 올려 조립한 XML 이었다(6절).

### 심볼을 그대로 넣지 않았다

브랜드 규칙([docs/brand/README.md](../brand/README.md) 5절)이 **정사각에 심볼을 넣지
말라**고 한다. 심볼은 84×60(1.4:1) 이라 정사각 틀에서는 좌우가 빈다. 정사각 자리는
블레이드 J 한 자로 축약한 것을 쓰고, 이것이 이 브랜드에서 아이콘 자리의 마크다.

축약형 둘 중 어느 것도 OS 런처에는 맞지 않아서 **세 번째 형태**를 세웠다.

| 형태 | 왜 아이콘 자리에 못 쓰나 |
|---|---|
| `favicon` (깎인 잉크 블록에 J 를 **음각**) | 뚫린 J 로 배경이 비친다. 사진 벽지·어두운 작업표시줄에서 글자로 읽히지 않는다 |
| `app-icon` (배경 없이 J 글자만) | 벽지 위에 흰/검은 글자만 떠서 무엇이든 배경이면 사라진다 |
| **꽉 찬 잉크 판 + 종이색 J (양각)** | 이것을 쓴다. 배경이 무엇이든 대비가 유지된다 |

세 번째는 새로 지은 것이 아니다. PWA maskable · apple-touch 가 이미 같은 이유로
같은 모양을 쓰고 있었다(`generate.py` 8-3 의 `_fullbleed_shape`). 그것을 아이콘 자리에
공통으로 돌려 쓰니 **안드로이드와 윈도우가 같은 모양**이 된다.

### 만든 것

`docs/brand/generate.py` 가 **플레이어의 플랫폼 폴더로 바로 쓴다.** `public/brand/` 같은
복사본을 두지 않았다 — 플랫폼 폴더는 파일 이름이 정해져 있어 복사 단계를 하나 더 두면
어긋나기만 한다. 손으로 내보낸 래스터를 커밋하지 않는다는 규칙은 그대로다.

```
mipmap-{mdpi..xxxhdpi}/ic_launcher.png              48dp  API 25 이하 런처
mipmap-{mdpi..xxxhdpi}/ic_launcher_foreground.png  108dp  어댑티브 전경 (API 26+)
mipmap-anydpi-v26/ic_launcher.xml                         배경(색)+전경+monochrome
values/ic_launcher_background.xml                         어댑티브 배경색 = Ink
drawable-xhdpi/tv_banner.png                     320×180  TV 런처 배너
windows/runner/resources/app_icon.ico            16~256   윈도우 exe · 작업표시줄
```

**어댑티브 아이콘을 새로 넣었다.** 전에는 레거시 비트맵 하나뿐이라 안드로이드 8 이상에서는
런처가 흰 판에 아이콘을 축소해 얹었다(테두리가 보이는 그 모양이다). 이제 배경은 Ink 색판,
전경은 J 다. 마스크 모양은 기기가 정하므로(원·둥근 사각·사각) J 는 108dp 캔버스에서
높이 44 로 두어 가운데 66dp 안전 원 안에 들어간다 — 대각선 약 54.6 이다.
API 33+ 테마 아이콘용 `monochrome` 은 알파만 쓰이므로 전경을 그대로 넘겼다.

**TV 배너는 이제 실물 PNG 다.** 320×180 은 1.78:1 이라 정사각 규칙의 반대쪽이고,
심볼이 제 비율로 들어갈 수 있는 유일한 아이콘 자리다. 그래서 배너만 **가로 조합**
(심볼 + JSINI 워드마크) 녹아웃을 잉크 판 위에 올렸다.
6절이 예고한 대로 `drawable/tv_banner.xml` 은 지웠다 — 남겨 두면 한 리소스에 설계가
둘이 되고, 그 XML 은 새 아이콘(잉크 블록)을 `#111214` 판에 올려 잉크 위 잉크가 된다.

### 확인한 것

```
python docs/brand/generate.py     기존 브랜드 산출물은 바이트가 그대로다
                                  (git diff 에 generate.py 만 뜬다 — 리팩터링이 무해했다는 뜻)
aapt2 compile --dir res           새 리소스 전부 컴파일 통과
aapt2 link                        @color/ic_launcher_background · @mipmap/ic_launcher_foreground
                                  참조 해소. tv_banner 는 xhdpi PNG 하나로만 잡힌다
flutter build windows --release   성공. 만들어진 exe 에서 아이콘을 꺼내 보니 블레이드 J 다
```

`generate.py` 를 손볼 때 `write_ico` · `write_png` 가 그리는 모양을 함수로 받도록
바꿨다(전에는 다각형 두 개를 인자로 받았다). 아이콘 자리마다 모양이 달라서다.
기존 산출물이 바이트까지 같은 것으로 이 리팩터링이 무해함을 확인했다.

### 못 한 것 · 손대지 않은 것

- **APK 실물은 여전히 못 만들었다.** 이 PC 의 NDK 두 폴더가 깨져 있는 문제(7절)가 그대로다.
  리소스 단계는 `aapt2` 로 따로 확인했으므로 아이콘 때문에 깨질 일은 없다.
- **리눅스 `.deb` · `tar.gz` 에는 아이콘이 없다.** 데스크톱 항목(`.desktop`) 없이
  systemd + cage 로 전체화면 키오스크로 뜨므로 아이콘을 보는 자리가 없다.
- **`web/` · `ios/` · `macos/` 는 그대로 두었다.** 릴리스 대상이 아니다(7절).
  대상이 되면 같은 함수로 한 줄씩 늘리면 된다.
- 앱 이름(`android:label` 등)은 이때 손대지 않았다가 **9절에서 바꿨다.**

## 9. 앱 이름을 JSINI 로 바꿨다 (2026-09-02)

> 지시: "앱 이름도 JSINI 로 바꿔줘"

**표시 이름만 바꾸고 식별자는 그대로 두었다.** 둘을 함께 바꾸면 현장에 깔린 것을
덮어쓰지 못한다(아래 표의 이유들).

### 바꾼 것 — 사람이 보는 이름

| 자리 | 파일 | 보이는 곳 |
|---|---|---|
| `android:label` | `AndroidManifest.xml` | 런처 아이콘 아래 이름 |
| `MaterialApp.title` | `lib/main.dart` | 안드로이드 '최근 앱' 목록 |
| 창 제목 | `windows/runner/main.cpp` | 윈도우 제목줄 · 작업표시줄 |
| `FileDescription` · `ProductName` | `windows/runner/Runner.rc` | 작업 관리자 · 파일 속성 |
| GTK 창 제목 (둘) | `linux/runner/my_application.cc` | 리눅스 창 제목 |
| systemd `Description` | `packaging/build_deb.sh` | `systemctl status` |
| 콘솔 창 제목 | `run_player.bat` | 개발용 부트스트래퍼 창 |

저장소 전체에서 옛 이름 `Funeral Signage Player` 를 다시 찾아 남은 곳이 없음을 확인했다.

### 바꾸지 않은 것 — 식별자

| 그대로 둔 것 | 값 | 바꾸면 |
|---|---|---|
| `applicationId` · `namespace` | `com.quristyle.funeralv2_player` | **현장 기기에 두 개가 나란히 설치된다.** 덮어쓰기 업데이트가 안 된다 |
| `BINARY_NAME` (윈도우 · 리눅스) | `funeralv2_player` | exe/elf 이름이 바뀐다 → systemd `ExecStart` · 런처 스크립트 · zip 안내 문구가 모두 어긋난다 |
| `InternalName` · `OriginalFilename` | `funeralv2_player(.exe)` | 규격상 **실제 파일 이름과 같아야 한다.** 표시 이름이 아니다 |
| deb `Package` | `funeralv2-player` | apt 가 업그레이드로 보지 않고 새 패키지로 깐다 |
| 릴리스 자산 이름 | `funeralv2_player-<ver>-...` | 다운로드 화면 matcher 가 자산을 못 찾는다 (2절) |
| `pubspec.yaml` 의 `name` | `funeralv2_player` | `package:funeralv2_player/...` 임포트 전부 |
| `APPLICATION_ID` (리눅스) | `com.quristyle.funeralv2_player` | 데스크톱 세션이 창을 다른 앱으로 본다 |

`Runner.rc` 의 `CompanyName`/`LegalCopyright` 는 `com.quristyle` 그대로다 —
회사 표기는 앱 이름과 다른 문제라 손대지 않았다.

### 확인한 것

```
AndroidManifest.xml               XML 파싱 후 android:label = "JSINI" 확인
bash -n packaging/build_deb.sh    문법 오류 없음
flutter build windows --release   성공 (.rc · 제목 변경 반영)
빌드된 exe 의 버전 정보            ProductName · FileDescription = JSINI
```

### `windows/runner` 소스에는 한글 주석을 넣을 수 없다

여기서 실제로 부딪혔다. 이 저장소의 주석은 한글인데, `main.cpp` 에 한글 주석을 넣자
윈도우 빌드가 **깨졌다.**

```
warning C4819: 현재 코드 페이지(949)에서 표시할 수 없는 문자가 파일에 들어 있습니다
error C2220: 다음 경고는 오류로 처리됩니다
```

MSVC 는 BOM 없는 UTF-8 파일을 시스템 코드 페이지(949)로 읽고, Flutter 의 윈도우 러너는
`/WX`(경고를 오류로) 로 빌드한다. 그래서 **`windows/runner/*` 는 ASCII 로 둔다** —
그 파일들의 주석만 영어다. `linux/runner/*`(GCC)와 `.dart` · `.xml` 은 한글이 문제없다.

한글을 쓰려면 `windows/CMakeLists.txt` 의 컴파일 옵션에 `/utf-8` 을 더하거나 파일에
BOM 을 붙이면 된다. 릴리스 대상의 빌드 설정을 주석 언어 때문에 건드릴 일은 아니라고 보고
두었다. 필요하면 한 줄이다.

### 남은 판단

**이름이 "JSINI" 한 단어다.** 기기에 앱이 하나뿐인 사이니지에서는 문제가 없지만,
장비 목록·원격 관리 화면에서 다른 JSINI 앱과 구분이 필요해지면
`JSINI 사이니지` 처럼 늘리는 편이 낫다. 위 표의 여섯 곳을 같이 고치면 된다.

`web/` · `ios/` · `macos/` 의 이름은 아이콘과 같은 이유로 그대로 두었다 — 릴리스 대상이 아니다.
