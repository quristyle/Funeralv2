# funeralv2_player 분석 (장례식장 사이니지 플레이어)

> 분석 범위: `funeralv2_player/` (Flutter). `build/`, `.dart_tool/`, `.idea/` 제외.
> 기준일: 2026-08-14

---

## 1. 개요

### 역할
`funeralv2_player`는 장례식장(빈소/입구/키오스크 등)의 디스플레이 화면에서 24시간 상시 구동되는 **디지털 사이니지(DID) 플레이어**입니다. 백엔드(`funeralv2Api`)의 SignalR 허브에 접속해 장비 설정/고인 정보 변경을 실시간으로 수신하고, 장비 유형(`deviceType`)에 따라 영정/호실안내/입구안내/키오스크/멀티미디어 화면을 렌더링합니다.

### 기술 스택
- **Flutter (Dart SDK `^3.12.2`)**, Material, 다크 테마 고정
- **실시간 통신**: `signalr_netcore ^1.4.4` (WebSocket 기반 SignalR 클라이언트)
- **HTTP**: `http ^1.2.2` (REST 폴백/헬스체크)
- **로컬 저장**: `shared_preferences`(설정), `sqflite` + `sqflite_common_ffi`(오프라인 캐시 DB), `path_provider`(미디어 파일 캐시 디렉터리)
- **미디어 재생**: `media_kit` + `media_kit_video`(내부적으로 데스크톱은 mpv/libmpv)
- **키오스크 윈도잉**: `window_manager ^0.3.9` (전체화면·최상단 고정)

### 배포 환경
- 주 타깃은 **Raspberry Pi OS Lite (aarch64)** + Wayland + labwc 최소 GUI. Windows/데스크톱도 지원.
- `docs/raspberry실행환경.md`, `docs/raspberry빌드환경및실행.md` 기준:
  - `systemd` 서비스(`player.service`)로 `Restart=always`, `RestartSec=3` — **앱 크래시 시 자동 재시작은 OS 레벨에서 보장**.
  - 화면 꺼짐 방지(`consoleblank=0`), 절전 해제(`logind.conf`), HDMI 핫플러그 강제는 **OS 설정에 의존**(앱은 관여하지 않음 → 6.1 참조).
  - SSL/SignalR을 위해 `timedatectl`로 시간 동기화 필수(문서에 명시).
  - `libmpv2`, `mpv`, `ffmpeg` 런타임 필수.

---

## 2. 앱 구조

```
lib/
├── main.dart                     # 진입점: window_manager 초기화 + MainRouter(설정 유무 분기)
├── models/
│   └── device_models.dart        # 모든 DTO (DeviceDto, DeceasedDto, Mourner, Ribbon, TextOverlay, 안내/키오스크)
├── services/
│   ├── signalr/signalr_service.dart   # SignalR 싱글톤 (연결/재연결/이벤트)
│   ├── api/api_service.dart           # REST 조회 + 오프라인 폴백(로컬 DB)
│   ├── cache/local_db_service.dart    # SQLite 캐시 (devices/deceased/guide/kiosk/media_sources)
│   ├── cache/cache_manager.dart       # 미디어 파일(영상/이미지/음원) 로컬 다운로드 캐시
│   └── player/media_player_service.dart # media_kit 비디오/BGM 재생 래퍼
└── pages/
    ├── device_dispatcher.dart    # deviceType별 라우팅 + SignalR 구독 + 재시도 타이머
    ├── player_shell.dart         # 공통 셸(회전/여백/디버그/설정 진입 제스처)
    ├── settings_screen.dart      # 서버 주소·장비코드 입력, 헬스체크, IP/MAC 수집
    └── portrait|guide|kiosk|multimedia/  # 각 (controller: ChangeNotifier) + (view: Widget)
```

### 2.1 진입점 (`main.dart`)
- `WidgetsFlutterBinding.ensureInitialized()` → `MediaKit.ensureInitialized()` → (Windows/Linux) `windowManager.ensureInitialized()`.
- 저장된 `serverBaseUrl`에 `localhost`/`127.0.0.1` 포함 여부로 **로컬 개발 vs 상용** 판별(`main.dart:31`). 상용일 때만 전체화면+최상단 고정 적용(`main.dart:53-58`, 200ms 지연 후).
- `MainRouter`(`main.dart:95`)가 `SharedPreferences`의 `deviceCode` 유무로 `SettingsScreen` 또는 `DeviceDispatcher`를 표시. 설정 저장/재진입 로직 보유.

### 2.2 디스패처 (`device_dispatcher.dart`)
- **Offline-First**: 시작 즉시 로컬 캐시(`getCachedDevice`)로 화면 기동(`:92`), 이후 백그라운드로 `fetchDevice` 호출 후 **필드 단위 diff**로 변경 시에만 뷰 갱신(`:123-137`).
- `deviceType` switch로 5개 뷰 분기(`:284-302`), 미매칭 시 `PortraitView` 기본값.
- 서버 연결 실패 & 캐시 없음일 때만 에러 화면 + 20초 자동 재시도 타이머(`:176-190`).
- `dispose()`에서 `SignalRService.disconnect()` 호출(`:74`).

### 2.3 컨트롤러 패턴 (portrait/guide/kiosk/multimedia)
- 4개 컨트롤러 모두 `ChangeNotifier` + `_isDisposed` 가드 + `notifyListeners` 오버라이드로 동일 구조.
- 공통 흐름: `init()`에서 로컬 캐시 즉시 표출 → `_syncWithServer()`에서 서버 조회 후 필드 diff → 변경 시 미디어 재캐싱/재생.
- 멀티미디어는 `contentIntervalSec`(기본 10초) 주기 사진 롤링 타이머(`multimedia_controller.dart:194-206`).

### 2.4 모델 (`device_models.dart`)
- `DeviceDto`(대량 설정 필드), `DeceasedDto`(고인/상주/리본/텍스트오버레이/가족사진), `EntranceGuideRoomDto`, `KioskGuideResponseDto` 등.
- `fromJson`이 3가지 응답 래핑 패턴(단일/`result` 배열/`data.result`)과 `1/true`, 문자열 JSON 배열을 유연하게 처리 — 방어적이지만 백엔드 계약이 불명확함을 시사.

---

## 3. 백엔드 연동 (SignalR)

### 3.1 연결
- 허브 URL: `{serverBaseUrl}/api/funeral/hubs/device` (`signalr_service.dart:222-225`).
- `HubConnectionBuilder().withUrl(hubUrl).withAutomaticReconnect(retryDelays:[0,2000,5000,10000]).build()` (`:62-65`).
- 연결 성공 후 `RegisterDevice(code, ip, mac, publicIp)` RPC 호출로 서버에 온라인/물리정보 등록(`:139-151`).
- REST 엔드포인트(모두 `http.get`, 4초 타임아웃):
  - `/api/funeral/building/device/code/{code}`
  - `/api/funeral/building/deceased/deviceCode/{code}`
  - `/api/funeral/building/deceased/guide/deviceCode/{code}`
  - `/api/funeral/building/deceased/kiosk/deviceCode/{code}`
  - `/api/funeral/building/source/{sourceId}`
  - 파일: `/api/file/download/id/{fileId}`

### 3.2 이벤트
- 서버 → 클라 푸시: `DeviceChanged` 1개. 수신 시 **1초 디바운스** 후 `_onDeviceChanged()` 콜백(=`DeviceDispatcher._loadDevice`) 실행(`:97-106`).
- 재연결/최초 연결 성공 시에도 강제로 콜백을 1회 실행해 데이터 동기화 유도(`:74-78`, `:117-120`).

### 3.3 재연결
- **2중 재연결 전략**: (1) 라이브러리 `automaticReconnect`(4회), (2) `onclose` 시 앱 레벨 수동 지수 백오프(`5 * 2^attempt`초, 최대 60초, `:156-177`).
- 장비코드 변경 시 기존 세션 강제 정리 후 재연결(`:43-46`).

### 3.4 URL 설정
- 하드코딩된 상수는 아니지만 **기본값 `http://localhost:5265`가 `main.dart:127`, `main.dart:203` 두 곳에 중복**. 서버 주소는 최초 1회 `SettingsScreen`에서 수동 입력해 `SharedPreferences`에 영속화.

---

## 4. 강점

1. **Offline-First 설계가 일관됨**: SQLite(설정/고인/안내 JSON) + 파일 캐시(미디어)로 서버·네트워크 장애 시에도 마지막 상태로 계속 재생. 사이니지의 핵심 요구를 잘 충족.
2. **불필요한 화면 리프레시 억제**: 서버 재조회 후 필드 diff로 변경 시에만 갱신 → 영상/음악 재생 연속성 유지, 깜빡임 방지.
3. **재연결 이중화 + 디바운스**: 라이브러리 자동 재연결과 수동 백오프를 병행, `DeviceChanged` 폭주에 1초 디바운스 적용.
4. **OS 레벨 복원력 문서화**: `systemd Restart=always`, 절전/화면꺼짐 해제, 시간 동기화 등 24시간 운영 절차가 문서로 정리됨.
5. **미디어 재생 품질 튜닝**: mpv `hwdec=auto-safe`, `scale=spline36`, `video-sync=display-resample` 등 저사양 Pi에서의 프레임 안정화 고려(`media_player_service.dart:27-45`).
6. **_isDisposed 가드**: 모든 컨트롤러가 dispose 이후 `notifyListeners`/비동기 콜백 오작동을 방지.
7. **관리자 탈출 동선 확보**: 화면 탭/더블탭으로 언제든 설정 진입 가능(`player_shell.dart:100-104`).

---

## 5. 개선점

### 5.A 우선순위 — 높음 (always-on 사이니지 복원력 직결)

#### H1. 화면 꺼짐 방지를 앱이 보장하지 않음 (wakelock 부재)
- **근거**: `pubspec.yaml`에 `wakelock`/`wakelock_plus` 없음. 절전 방지는 전적으로 `consoleblank=0`, `logind.conf` 등 **OS 수동 설정에 의존**(`raspberry실행환경.md:280-293`).
- **문제**: OS 설정 누락·이미지 교체·다른 하드웨어(데스크톱/Orange Pi/Mini PC) 이관 시 화면이 꺼져 사이니지가 무용지물. 앱 자체 방어선이 없음.
- **개선**: `wakelock_plus` 도입 후 `main()`에서 `WakelockPlus.enable()` 호출. OS 설정과 앱 설정 이중화.

#### H2. SignalR "half-open"(좀비) 연결 미탐지 — 워치독 부재
- **근거**: `signalr_service.dart`에서 `serverTimeoutInMilliseconds`/`keepAliveIntervalInMilliseconds` **미설정**. 재연결은 오직 `onclose` 이벤트에만 의존(`:82-93`).
- **문제**: Pi의 Wi-Fi/유선 단절, NAT 타임아웃, 서버 재기동 시 TCP가 조용히 죽으면 `onclose`가 즉시 안 뜨고 클라이언트는 "연결됨"으로 착각 → **변경 푸시를 장시간 놓침**. 상시 운영에서 가장 치명적.
- **개선**:
  - `HubConnectionBuilder`에 `serverTimeout`/`keepAliveInterval` 명시.
  - 주기적(예: 30~60초) **워치독 타이머**로 `isConnected` 확인 + 최근 수신 시각 검사, 이상 시 강제 `disconnect→connect`.
  - 또는 RTT 확인용 서버 RPC(ping)를 주기 호출.

#### H3. 재연결 백오프에 지터(jitter) 없음 — thundering herd
- **근거**: `_scheduleManualReconnect`의 지연이 `5 * 2^attempt`로 결정적(`:164`).
- **문제**: 서버 1대 재기동 시 다수 빈소 단말이 **동일 시각에 동시 재접속** → 서버 순간 과부하로 재장애 반복 가능(장례식장 전체 단말 수 고려).
- **개선**: 백오프에 `± random jitter`(예: `delay * (0.5~1.5)`) 추가.

#### H4. 미디어 파일 캐시가 무한 증식 + 갱신 실패 (SD 카드 수명/디스크 고갈)
- **근거**: `cache_manager.dart`는 파일 존재 시 재다운로드하지 않음(`:50-52`, `:98-100`). **삭제/TTL/용량 상한 로직 전무.** 캐시 키가 `fileId` 또는 파일명(`relativePath.split('/').last`).
- **문제 (2가지)**:
  1. 장례 행사가 계속 바뀌며 새 영정/영상이 누적 → **캐시 폴더가 무한 증가**, Pi의 SD 카드 용량 고갈 및 쓰기 수명 저하.
  2. 서버가 **같은 경로/파일명으로 콘텐츠를 교체**하면, 로컬에 동일명 파일이 있어 영원히 **stale(구버전) 파일**을 재생(특히 `getLocalFile`은 네트워크 확인 없이 옛 파일 반환).
- **개선**: LRU/용량 상한 기반 캐시 정리(주기 GC), 파일명에 콘텐츠 해시/버전 부여, ETag/Last-Modified 조건부 다운로드.

#### H5. 변경 감지 diff가 표시 필드를 상당수 누락
- **근거**: 각 컨트롤러의 `isChanged`가 소수 필드만 비교. 예: `portrait_controller.dart:129-138`은 리본 위치/텍스트 오버레이/여백/정렬/`memorialPhotoEffect`/`musicVolume` 변경을 **감지하지 못함**. `dispatcher`의 diff(`:123-137`)도 padding/alignment/overlay 누락.
- **문제**: 관리자가 리본 위치·텍스트·여백만 바꾸면 `DeviceChanged` 푸시가 와도 화면이 **재부팅 전까지 갱신 안 됨**.
- **개선**: DTO에 `==`/`hashCode`(equatable) 또는 정규화 JSON 비교로 **전체 상태 비교**로 전환. 필드 나열식 diff 제거.

#### H6. 전 구간 평문 HTTP + 무인증 + 물리정보 노출
- **근거**: 모든 통신 `http://`(기본 `:5265`), API/SignalR에 인증 토큰 없음. `RegisterDevice`가 IP/MAC을 평문 전송(`signalr_service.dart:143`).
- **문제**: 사내망이라도 스푸핑/무단 등록 가능. 외부망 노출 시 심각.
- **개선(제안)**: HTTPS/WSS 전환, 장비별 사전 발급 토큰 헤더, `SettingsScreen`도 https 헬스체크. (LAN 격리 정책이면 문서에 명시.)

### 5.B 우선순위 — 중간

#### M1. 동일 데이터를 2~3회 중복 조회
- **근거**: `DeviceDispatcher._loadDevice`가 `fetchDevice`(`device_dispatcher.dart:114`) 실행 후, 라우팅된 각 뷰의 컨트롤러 `init`이 다시 `fetchDevice`+`fetchDeceased`를 호출(`portrait_controller.dart:121-125` 등). `DeviceChanged` 1회에 네트워크 요청이 곱절.
- **개선**: 디스패처가 조회한 `DeviceDto`를 뷰에 주입(prop drilling 또는 공유 상태)해 중복 제거.

#### M2. 상태관리 방식 혼재 & 로직 중복(DRY 위반)
- **근거**: 전역 싱글톤 가변상태(`SignalRService`, `LocalDbService`, `CacheManager`) + `ChangeNotifier` 컨트롤러 + 디스패처의 수동 `setState`가 혼재. `_syncWithServer`/`isChanged`/미디어 재캐싱 블록이 4개 컨트롤러에 거의 복붙.
- **개선**: 공통 베이스 컨트롤러(추상 클래스)로 sync/diff/미디어 로드 추출. Provider/Riverpod 등 일관된 DI 도입 검토.

#### M3. 오프라인 캐시 파싱의 방어 부족
- **근거**: `api_service.dart:104` `json['data']['result'] as List`를 널/구조 검사 없이 접근(입구 안내 오프라인 경로). 여러 곳에서 `as List` 언가드 캐스트.
- **문제**: 캐시 본문이 손상/구스키마면 예외로 화면 붕괴 가능.
- **개선**: 안전 캐스트(`as?`)+널 병합, try/catch 일관 적용(키오스크 경로는 이미 try/catch 있음 → 통일).

#### M4. 헬스체크가 편법적
- **근거**: `settings_screen.dart:208`이 `device/code/HEALTH_CHECK`를 헬스 프로브로 사용하고 `statusCode < 500`이면 성공 처리(`:213`) — 404도 "정상"으로 판정.
- **개선**: 전용 `/health` 엔드포인트 사용, 200 계열만 성공.

#### M5. 프로덕션에 디버그 UI 상시 노출 + 오조작 위험
- **근거**: `player_shell.dart:82-96`이 노란 `DEBUG:` 박스를 항상 렌더. `portrait_view.dart:87-89`는 **화면 아무 곳이나 단일 탭으로 설정 진입**.
- **문제**: 조문객이 터치스크린을 만지면 설정 화면 노출/장비 오설정 가능. 디버그 텍스트가 실사용 화면에 노출.
- **개선**: `kReleaseMode`에서 DEBUG 박스 숨김. 설정 진입은 숨은 제스처(예: 코너 롱프레스 3초, 특정 키 조합, PIN)로 게이팅.

#### M6. 타임아웃/상수 하드코딩·불일치
- **근거**: API 4초, `getCachedFile` 30초(`cache_manager.dart:55`) vs `getCachedFileByPath` 4초(`:103`) 비일관. 재시도 20초, 디바운스 1초, 백오프 60초 등 산재.
- **개선**: `AppConfig`/상수 파일로 집약. 저사양·저속망 Pi 고려해 값 재검토.

### 5.C 우선순위 — 낮음

#### L1. 테스트 사실상 없음 + 기본 테스트가 컴파일 실패
- **근거**: `test/widget_test.dart`가 존재하지 않는 `MyApp`(실제는 `FuneralPlayerApp`)과 카운터 앱을 참조 → **빌드 불가한 스텁**.
- **개선**: 삭제/대체 후 DTO `fromJson`(1/true·문자열배열·래핑 패턴), `_buildHubUrl`, 백오프 계산, 오프라인 폴백에 대한 단위 테스트 작성.

#### L2. 기본 서버 URL 등 상수 중복
- **근거**: `http://localhost:5265`가 `main.dart:127`, `:203` 중복. `_buildHubUrl`/URL 정리 로직도 여러 파일에 산재.
- **개선**: 단일 상수/유틸로 통합.

#### L3. `print` 기반 로깅 남용
- **근거**: 전 파일에서 `print(...)`. 릴리스에서도 stdout 방출(→ journald 누적).
- **개선**: 레벨 있는 `logger` 도입, 릴리스에서 debug 로그 억제, 로그 로테이션 정책.

#### L4. 방향/회전 상태 이중 표현
- **근거**: `displayOrientation`(문자열)과 `displayRotationTurns`(int)를 동시에 저장·상호 매핑(`main.dart:135,171`, `device_dispatcher.dart:228-234`). 하위호환 매핑이 여러 곳에 흩어짐.
- **개선**: 단일 소스(turns)로 정규화하고 파생값은 getter로.

#### L5. 대형 이미지 다운스케일 부재 (Pi 메모리)
- **근거**: 영정/가족 사진을 원본 파일 그대로 표시. 고해상도 다수 이미지는 Pi에서 이미지 캐시 메모리 압박 가능.
- **개선**: `ResizeImage`/`cacheWidth`로 디코드 해상도 제한, Flutter `imageCache` 상한 조정.

#### L6. `DeceasedDto.fromJson`이 결과 배열의 첫 요소만 사용
- **근거**: `device_models.dart:79,295` `list[0]`. 다중 결과 시 나머지 무시(현 스키마상 의도된 듯하나 암묵적).

---

## 6. 종합 권고

이 앱은 **오프라인 우선 + 재생 연속성**이라는 사이니지의 핵심 요구를 이미 상당히 잘 구현했고, OS 레벨(systemd `Restart=always`) 복원력도 문서화되어 있습니다. 그러나 **"항상 켜져 있음"의 실제 신뢰성**을 좌우하는 세 축에 구멍이 있습니다.

1. **연결 복원력 (최우선)**: SignalR keepAlive/serverTimeout 명시 + **앱 레벨 워치독**으로 half-open 연결을 능동 탐지(H2), 재연결 백오프에 **지터** 추가(H3). 이 둘이 없으면 네트워크가 불안정한 현장에서 "겉으론 살아있으나 실제론 업데이트를 못 받는" 상태가 발생합니다.
2. **화면·자원 지속성**: 앱 자체 **wakelock**으로 절전 방어선 이중화(H1), **미디어 캐시 GC/버저닝**으로 SD 카드 고갈과 stale 재생 방지(H4).
3. **변경 반영 정확성**: diff 로직을 **전체 상태 비교**로 전환해 리본/텍스트/여백 변경이 실제 화면에 반영되도록(H5).

그다음으로 중복 조회 제거(M1), 상태관리/중복 로직 정리(M2), 프로덕션 디버그 UI·오조작 게이팅(M5)을 정리하면 유지보수성과 현장 안정성이 크게 향상됩니다. 보안(H6)은 배포 네트워크 정책(LAN 격리 여부)에 따라 우선순위를 조정하되, 최소한 문서로 전제를 명시할 것을 권합니다. 마지막으로 컴파일조차 되지 않는 기본 테스트(L1)를 제거하고 DTO 파싱·URL·백오프 등 순수 로직 단위 테스트부터 확보하면 리팩터링 안전망이 마련됩니다.
