# 플레이어 새 버전 확인 · 업그레이드

작성: 2026-09-02
대상: `funeralv2_player/lib/services/update/update_service.dart` ·
`lib/widgets/update_dialog.dart` · `lib/pages/settings_screen.dart` ·
`android/app/src/main/{AndroidManifest.xml,kotlin/.../MainActivity.kt,res/xml/file_paths.xml}` ·
`android/app/build.gradle.kts`

> 지시: "jsini-portal 의 url /system/player-download 는 릴리즈 정보는 화면이다. 즉 릴리즈
> 배포는 github 를 통해서 되고 있다. funeralv2_player 의 환경설정 화면에 새버전을 확인하는
> 아이콘과 새버전으로 업그레이드 하는 기능을 넣고 싶다. 방법을 설명하고 결정사항이 없다면
> 시행해줘."

**확인은 전 플랫폼에서 넣었고, 실제 교체는 안드로이드만 넣었다.**
데스크톱(윈도우·리눅스)의 자동 교체는 결정이 필요해서 남겼다(6·7절).

---

## 1. 릴리스가 어디서 오는가

이미 돌고 있는 흐름을 그대로 쓴다. 새로 만든 경로가 없다.

```
저장소에 태그 v1.0.1 푸시
  → .github/workflows/release.yml 이 OS 별 설치 파일을 만든다 (23번 문서)
  → GitHub Releases 에 자산으로 붙는다
  → 포털 /system/player-download 가 그것을 보여 준다   (사람이 받아 가는 화면)
  → 플레이어 환경 설정이 같은 것을 본다                  ← 이번에 만든 것
```

v1.0.0 에 실제로 붙어 있는 자산은 아홉이다.

```
funeralv2_player-1.0.0-windows-x64.zip                 40 MB
funeralv2-player_1.0.0_debian13_arm64.deb              12 MB
funeralv2-player_1.0.0_ubuntu24_amd64.deb              14 MB
funeralv2-player_1.0.0_ubuntu24_arm64.deb              13 MB
funeralv2_player-1.0.0-android-releasesigned.apk      105 MB
funeralv2_player-1.0.0-{배포판}-{아키}.tar.gz  셋      수동 설치용
SHA256SUMS.txt
```

## 2. 왜 통합 서버를 거치지 않고 GitHub 을 직접 보는가

저장소가 공개라 **인증 없이 읽힌다.** 포털 다운로드 화면도 브라우저에서
`api.github.com/repos/quristyle/Funeralv2/releases/latest` 를 직접 부른다.
서버(AuthServer)를 한 번 거치게 하면 엔드포인트가 하나 늘 뿐 얻는 것이 없다.

대신 **장비가 인터넷에 닿아야 한다.** 이미 같은 전제가 있다 — 설정 화면은 공인 IP 를
보려고 `api.ipify.org` 를 부른다. 닿지 않으면 확인만 실패하고 재생은 그대로 돈다.
확인 실패는 설정 화면에 아무것도 띄우지 않는다(팝업을 열면 이유가 나온다).
인터넷이 없는 현장에서 붉은 문구가 뜨면 설정이 잘못된 것처럼 보이기 때문이다.

> 저장소가 비공개로 바뀌면 이 경로는 그날 끊긴다. 그때는 토큰을 플레이어에 심는 대신
> **서버가 릴리스 정보를 중계**해야 한다(플레이어에 심은 토큰은 회수할 방법이 없다).

GitHub API 는 인증 없이 **IP 당 시간당 60회**다. 확인은 설정 화면을 열 때 한 번과
사람이 누를 때뿐이라 걸리지 않는다.

## 3. 이 장비가 받아야 하는 파일을 어떻게 고르는가

자산 이름에 플랫폼 · 배포판 · 아키텍처가 들어 있다. **`.deb` 로만 고르면 안 된다** —
라즈베리파이용(debian13, glibc 2.41)을 Ubuntu 24.04(2.39)에 주면 실행되지 않는다.
포털 화면의 matcher 가 배포판까지 보는 것과 같은 이유다(23번 문서 1절).

| 이 장비 | 판정 | 고르는 것 |
|---|---|---|
| 윈도우 | `Platform.isWindows` | `windows` + `.zip` |
| 안드로이드 | `Platform.isAndroid` | `.apk` (`releasesigned` 우선) |
| 리눅스 | `/etc/os-release` 의 `ID`·`VERSION_ID` + `uname -m` | `{배포판}` + `{아키}` + `.deb` |

배포판 태그는 `packaging/build_deb.sh` 가 파일 이름에 넣는 것과 같은 규칙으로 만든다
(`debian13` · `ubuntu24` …). 모르는 배포판은 `debian13` 으로 본다 — 지금 만드는 것 중
glibc 가 가장 낮아 틀렸을 때도 돌아갈 확률이 높다.

`.tar.gz` 는 고르지 않는다. 같은 조건에 `.deb` 와 둘이 걸리고, 수동 설치용이다.

**APK 가 둘이면 `releasesigned` 를 먼저 쓴다.** 디버그 키 서명은 빌드마다 서명이 달라
이미 깔린 앱 위에 덮어쓰기가 거부된다(`INSTALL_FAILED_UPDATE_INCOMPATIBLE`).

버전 비교에서 **빌드 번호(`+7`)는 뺀다.** 릴리스 태그에는 없고 앱 자신의 버전에는
붙어 있어서, 그대로 비교하면 같은 버전이 계속 "새 버전" 으로 보인다.
비교는 자리별 **숫자**로 한다 — 문자열로 하면 `1.10.0 < 1.9.0` 이 되어 새 버전을 못 본다.

## 4. 화면

환경 설정 머리줄에 아이콘 하나를 더했다(`system_update`, 회전 단추 왼쪽).
새 버전이 있으면 아이콘 오른쪽 위에 빨간 점이 찍힌다.

**설정 카드 안에 줄을 더하지 않고 팝업으로 뺐다.** 설정 화면은 저해상도 사이니지
패널에서 세로 스크롤 없이 한 화면에 담는 것을 전제로 짜여 있다(준수사항 4, 화면 주석의
"No Scroll" 컨셉). 카드에 줄을 더하면 720p 세로 모드에서 넘친다. 팝업은 카드 높이를
건드리지 않고, 진행률·파일 이름·안내를 놓을 자리도 넉넉하다.

팝업은 설정 화면의 회전값(`quarterTurns`)을 물려받는다. 세로로 세운 패널에서
팝업 글자만 누워 있으면 읽을 수 없다.

> **준수사항 3(팝업 드래그)은 여기서는 대상이 아니다.** 그 규칙은 vben·antd 모달에
> 공통 레이어로 걸어 둔 것이고, 목적은 "팝업 뒤의 목록을 보면서 일하기" 다.
> 전체화면 키오스크에는 창이 하나뿐이고 마우스가 없는 장비가 많다.
> 규칙의 예외 조항(전체화면 모달은 드래그를 끈다)과 같은 상황이다.

## 5. 플랫폼별로 '업그레이드' 가 실제로 무엇인가

여기가 이 작업의 핵심이다. **세 플랫폼이 서로 다른 일이다.**

| | 앱이 할 수 있는 것 | 왜 |
|---|---|---|
| **안드로이드** | 받기 + **시스템 설치 화면 띄우기** | 앱이 패키지 설치를 요청할 수 있다. 다만 조용히는 못 한다 |
| **윈도우** | 받기 | **돌고 있는 exe 를 자기가 덮어쓸 수 없다.** 종료 후 교체할 도우미가 필요하다 |
| **리눅스** | 받기 | `.deb` 설치에 **root** 가 필요하다. 플레이어는 `quri` 로 돈다 |

### 안드로이드 — 넣었다

받은 APK 를 시스템 설치 화면에 넘긴다. 필요한 것 넷을 함께 넣었다.

```
REQUEST_INSTALL_PACKAGES 권한          설치 요청 자격
FileProvider (+ res/xml/file_paths.xml) API 24 부터 file:// URI 를 넘기면 예외가 난다
MainActivity 의 update 채널             installAllowed · openInstallSettings · installApk
androidx.core:core 명시적 의존           FileProvider 를 직접 부르므로
```

**조용히 설치되지 않는다.** 시스템 앱이 아닌 앱은 사용자 확인 없이 패키지를 깔 수 없다.
화면의 확인을 사람이 눌러야 한다(TV 박스는 리모컨). 기기에서 "알 수 없는 앱 설치" 를
한 번 허용해야 하고, 허용되지 않았으면 팝업이 그 설정 화면을 여는 단추를 준다 —
이걸 안내하지 않으면 **아무 일도 안 일어난 것처럼 보인다.**

받은 파일은 앱 캐시(`getTemporaryDirectory()`)에 둔다. 저장 권한이 필요 없고,
공간이 모자라면 시스템이 지워도 되는 자리다. FileProvider 도 그 폴더만 내보낸다.
같은 이름·같은 크기가 이미 있으면 다시 받지 않는다 — 105MB 를 두 번 받으면
현장 회선을 두 번 먹는다.

### 윈도우 · 리눅스 — 받기까지만 넣었다

받은 뒤 **어디에 받았는지와 무엇을 해야 하는지**를 팝업에 적는다.
`.deb` 면 `sudo apt install ./파일` 과 서비스 재시작까지, `.zip` 이면 종료 후 덮어쓰기다.

자동 교체를 여기서 진행하지 않은 이유는 하나다 — **되돌리기 어렵고 현장이 조용히
멈출 수 있다.** 장례가 진행되는 중에 플레이어가 뜨지 않으면 그 자리에서 알 방법이 없다.
그래서 7절의 결정으로 올린다.

## 6. 만든 것 (파일)

```
lib/services/update/update_service.dart   릴리스 조회 · 자산 판정 · 버전 비교 · 내려받기
lib/widgets/update_dialog.dart            팝업 (확인 → 받기 → 설치)
lib/pages/settings_screen.dart            머리줄 아이콘 + 조용한 사전 확인
test/update_service_test.dart             버전 비교 · 자산 판정 시험 15개
android/.../AndroidManifest.xml           권한 + FileProvider
android/.../res/xml/file_paths.xml        FileProvider 가 내보낼 폴더
android/.../MainActivity.kt               update 채널 (설치 화면 호출)
android/app/build.gradle.kts              androidx.core:core 명시
pubspec.yaml                              package_info_plus (앱 자신의 버전 읽기)
```

`package_info_plus` 는 **이미 다른 패키지가 끌어오고 있던 것**이라 잠금 파일이 3줄만
바뀌었다(transitive → direct). 리눅스·윈도우에서 `native_build=false` 라
데스크톱 빌드에 새로 붙는 네이티브 코드가 없다.

## 7. 결정이 필요한 것

### D-P1. 윈도우에서 앱이 스스로 교체할지

- **지금**: 받아서 경로를 알려 준다. 사람이 종료하고 덮어쓴다.
- **왜 결정인가**: 돌고 있는 exe 는 자기를 덮어쓸 수 없어, "종료를 기다렸다가 교체하고
  다시 띄우는" 도우미 프로세스(배치/PowerShell)를 띄워야 한다. 교체 중에 전원이 나가면
  **플레이어가 뜨지 않는 상태로 남는다.** 현장에서 그것을 알아채는 사람이 없다.
- **선택지**
  1. 그대로 둔다(사람이 교체)
  2. 도우미 스크립트로 자동 교체 + **옛 폴더를 남겨 두고**, 새 것이 60초 안에 뜨지 않으면
     되돌린다
  3. 자동 교체만 (되돌림 없음)
- **내 의견**: 2안. 되돌림이 없으면 1안이 낫다. 설치 폴더에 쓰기 권한이 없으면
  (Program Files) 조용히 실패하지 않고 그 사실을 화면에 적어야 한다.

### D-P2. 리눅스에서 앱이 `.deb` 를 설치할 수 있게 할지

- **지금**: 받아서 `sudo apt install ./파일` 을 안내한다.
- **왜 결정인가**: root 가 필요하다. 앱은 `quri` 로 돈다. 열어 주려면 `.deb` 가
  **sudoers 규칙이나 polkit 정책을 깔아야** 하고, 그건 "이 앱이 임의의 패키지를
  설치할 수 있다" 는 뜻이 된다. 보안 표면이 늘어난다.
- **선택지**
  1. 그대로 둔다(사람이 ssh 로 설치)
  2. `apt install` 한 줄만 허용하는 sudoers 규칙을 `.deb` 에 넣는다
     (`NOPASSWD: /usr/bin/apt-get install -y /opt/funeralv2-player/updates/*.deb` 처럼 좁게)
  3. 자체 apt 저장소를 세우고 `unattended-upgrades` 에 맡긴다
- **내 의견**: 장기적으로 3안이 가장 깔끔하다(서명·롤백·표준 도구). 지금 당장이면
  2안을 **경로까지 좁혀서**. 1안도 나쁘지 않다 — 리눅스 장비는 대개 ssh 가 열려 있다.

### D-P3. 업그레이드를 현장에서 누를지, 포털에서 원격으로 밀지

- **지금**: 현장에서 설정 화면을 열어 사람이 누른다.
- **왜 결정인가**: 플레이어는 **이미 SignalR 로 서버와 붙어 있다.** 장비가 수십 대면
  현장을 돌아다니는 것보다 포털에서 "이 장비들 업데이트" 를 미는 편이 맞다.
  다만 그건 D-P1·D-P2 가 먼저 정해져야 하고, 화면·서버·SignalR 명령이 늘어난다.
- **선택지**: ① 현장만(지금) ② 포털에서 원격 지시 + 현장에서도 가능 ③ 원격만
- **내 의견**: 2안. 장비 관리 화면에 "버전" 칸과 "업데이트" 단추가 붙는 모양이 된다.
  지금 만든 `UpdateService` 를 SignalR 핸들러에서 그대로 부르면 된다.

### D-P4. 주기적으로 확인하고 자동으로 올릴지

- **지금**: 설정 화면을 열 때만 확인한다. 자동 설치는 없다.
- **왜 결정인가**: 사이니지는 몇 달씩 아무도 안 만진다. 자동이 아니면 버전이 갈린다.
  그런데 **장례가 진행되는 중에 재시작하면 안 된다.**
- **선택지**: ① 지금처럼 수동 ② 주기 확인 + 알림만 ③ 정해진 시간대(예: 새벽 4시)에
  자동 설치 ④ 행사가 없는 것을 서버에 물어보고 설치
- **내 의견**: 2안으로 시작하고, D-P3 이 2안으로 정해지면 4안이 자연스럽다.
  ③은 "새벽에 빈소가 없다" 는 보장이 없어 위험하다.

## 8. 확인한 것 · 못 한 것

```
flutter test test/update_service_test.dart   18개 전부 통과
                                             (버전 비교 7 · 자산 판정 8 · 결과 2 · 표기 1)
실제 릴리스 조회 (임시 시험)                   v1.0.0 을 읽고 이 PC 용 자산으로
                                             windows-x64.zip(38.2 MB) 을 골랐다
flutter analyze                              새로 늘어난 지적은 withOpacity 경고 둘.
                                             저장소에 이미 48개 있는 것과 같은 종류다
                                             (update_service.dart 는 지적 0)
flutter build windows --release              성공 (다트 트리 전체 컴파일)
aapt2 compile --dir res + link               권한 · FileProvider(authority 치환 확인) ·
                                             @xml/file_paths 참조 해소까지 확인
```

현재 버전을 어디서 읽는지도 확인했다. 윈도우는 exe 의 `ProductVersion`(`1.0.0+1` →
`+` 앞만 쓴다), 리눅스는 `data/flutter_assets/version.json`(Flutter 의 linux 타깃이
번들에 넣는다), 안드로이드는 패키지 정보다. **리눅스에서 그 파일을 못 읽으면 빈 값**이
오는데, 빈 값을 그대로 비교하면 항상 새 버전이 있는 것이 되므로 `알 수 없음` 으로
바꾸고 판정을 접는다(그 경우에는 최신 버전과 받을 파일만 보여 준다).

### APK 실물은 만들지 못했다 — 이 PC 의 NDK 문제

> **2026-09-03 해소.** 빈 NDK 폴더 둘을 지우자 AGP 가 다시 받았고(약 6.5분)
> `flutter build apk --release` 가 성공했다. 이 절에서 확인 못 했다고 적은
> Kotlin(설치 채널)이 컴파일되는 것까지 확인했고, **v1.0.1 릴리스**로
> CI 서명 APK(releasesigned, v1.0.0 과 같은 인증서)도 나갔다.
> 아래는 당시 기록으로 남긴다.

23번 문서 7절에 적힌 것이 그대로다. **깨진 NDK 폴더 둘**이 원인이고 이번에 정확히
어디서 막히는지 확인했다.

```
:app  ndkVersion 27.0.12077973 → [CXX1101] source.properties 없음
:jni  (플러그인 모듈) 28.2.13676358 → 같은 오류
```

`:app` 의 핀을 온전한 `30.0.14904198` 로 임시로 옮겨 봤지만 그때는 `:jni` 에서 막혔다
(23번 문서의 추정이 맞았다 — 앱 핀만 바꿔서는 해결되지 않는다).
**구성(configure) 단계에서 막히므로 `:app:compileReleaseKotlin` 만 따로 돌리는 것도 안 된다.**
그래서 이번에 넣은 Kotlin(설치 채널)은 **로컬에서 컴파일 확인을 하지 못했다.**
임시로 바꾼 `ndkVersion` 은 원래 값으로 되돌려 두었다.

고치는 방법은 Flutter 가 안내하는 그대로다. 빈 폴더라 지워도 잃는 것이 없지만
AGP 가 다시 받는 용량이 크고 개발 PC 환경을 건드리는 일이라 **여기서는 실행하지 않았다.**

```bash
rm -rf "$LOCALAPPDATA/Android/sdk/ndk/27.0.12077973" \
       "$LOCALAPPDATA/Android/sdk/ndk/28.2.13676358"
```

CI 러너는 SDK 를 새로 받으므로 영향이 없다. 안드로이드 job 이 도는 순간
Kotlin 컴파일까지 확인된다.

## 9. 남겨 둔 것 하나 더

`test/widget_test.dart` 는 **Flutter 템플릿 그대로**다. 존재하지 않는 `MyApp` 과
카운터 화면을 시험하고 있어 `flutter analyze` 의 유일한 `error` 이고,
`flutter test` (전체)를 실패시킨다. 이번 작업과 무관해서 손대지 않았다 —
지우거나 실제 화면 시험으로 바꾸면 된다.
