import 'dart:async';
import 'dart:math';
import '../display/display_mode_service.dart';
import 'package:signalr_netcore/signalr_client.dart';

/// [실시간 푸시 통신 서비스]
/// 백엔드의 SignalR 허브와 소켓을 연결하여 장비 설정 변경 등의 실시간 업데이트 메시지를 수신합니다.
/// 지수 백오프(Exponential Backoff) 수동 재연결 로직 및 변경 이벤트 수신 시 1초 디바운스를 적용합니다.
class SignalRService {
  // 싱글톤 인스턴스
  static final SignalRService _instance = SignalRService._internal();
  factory SignalRService() => _instance;
  SignalRService._internal();

  HubConnection? _hubConnection; // SignalR 연결 세션 객체
  bool _isConnecting = false; // 연결 진행 중 플래그
  Timer? _reconnectTimer; // 수동 재연결 예약을 위한 타이머
  Timer? _debounceTimer; // DeviceChanged 푸시 폭주 방지용 디바운스 타이머
  int _reconnectAttempt = 0; // 재연결 시도 횟수
  static const int _maxReconnectDelaySec = 60; // 최대 재연결 대기 시간 (60초)
  String? _currentDeviceCode; // 현재 연결된 장비 코드 추적용 변수
  bool _intentionalClose = false; // 의도적 종료 여부 플래그 (onclose에서 불필요한 자동 재연결 방지용)

  // ---------------------------------------------------------------------------
  // 좀비(half-open) 연결 대응
  //
  // Wi-Fi 단절, NAT 세션 만료, 서버 재기동 시 TCP 가 조용히 죽으면 onclose 가 뜨지 않아
  // 클라이언트는 "연결됨"으로 착각한 채 변경 푸시를 계속 놓친다.
  // 24시간 돌아가는 사이니지에서는 이 상태가 가장 치명적이므로,
  // 라이브러리 수준의 서버 타임아웃과 앱 수준의 워치독을 함께 건다.
  // ---------------------------------------------------------------------------

  /// 서버로부터 이 시간 동안 아무 메시지(핑 포함)도 못 받으면 연결이 끊긴 것으로 판정한다.
  static const int _serverTimeoutMs = 60 * 1000;

  /// 클라이언트가 서버로 핑을 보내는 주기.
  static const int _keepAliveIntervalMs = 15 * 1000;

  /// 워치독 점검 주기.
  static const Duration _watchdogInterval = Duration(seconds: 60);

  /// 연결됐다고 보고되지만 이 시간 동안 아무 수신이 없으면 능동 탐침을 보낸다.
  static const Duration _idleProbeThreshold = Duration(minutes: 5);

  Timer? _watchdogTimer; // 좀비 연결 감시 타이머
  DateTime? _lastActivityAt; // 마지막으로 서버와 실제 주고받은 시각

  // 워치독이 스스로 재연결할 수 있도록 마지막 접속 파라미터를 보관한다.
  String? _lastServerUrl;
  String? _lastIpAddress;
  String? _lastMacAddress;
  String? _lastPublicIpAddress;

  // 업데이트가 필요한 경우 화면 컴포넌트(Controller)에서 주입받아 호출할 콜백 함수
  Function()? _onDeviceChanged;

  /// [소켓 연결 여부 확인]
  bool get isConnected => _hubConnection?.state == HubConnectionState.Connected;

  /// [허브 소켓 연결 기동]
  /// 서버 Base URL과 식별자([deviceCode]), 하드웨어 정보(IP/MAC)를 받아
  /// 웹소켓 허브 주소([_buildHubUrl])로 세션을 생성하고 실시간 업데이트 수신 대기를 시작합니다.
  Future<void> connect({
    required String serverUrl,
    required String deviceCode,
    String? ipAddress,
    String? macAddress,
    String? publicIpAddress,
    required Function() onDeviceChanged,
  }) async {
    // 실시간 이벤트 수신 시 실행할 리프레시 콜백을 최신화합니다.
    _onDeviceChanged = onDeviceChanged;

    // 장비코드가 변경된 경우 기존 소켓 및 타이머를 먼저 강제 정리하고 새롭게 재연결합니다.
    if (_currentDeviceCode != null && _currentDeviceCode != deviceCode) {
      print('[SignalR] 장비코드 변경 감지: $_currentDeviceCode -> $deviceCode. 기존 세션 정리 후 재시도.');
      await disconnect(_currentDeviceCode!);
    }

    // 이미 접속 중이거나 시도 중이면 중복 요청을 무시합니다.
    if (_isConnecting || isConnected) {
      print('[SignalR] 이미 연결 진행 중이거나 연결된 상태입니다. 연결 요청 스킵.');
      return;
    }

    _isConnecting = true;
    _currentDeviceCode = deviceCode; // 현재 접속 코드 등록
    print('[SignalR] 연결 프로세스 시작...');

    // 기존의 접속 정보가 있다면 안전하게 먼저 종료 처리합니다.
    // 이 종료로 인해 옛 커넥션의 onclose가 발동하더라도 자동 재연결이 걸리지 않도록 의도적 종료로 표시합니다.
    _intentionalClose = true;
    await _disposeConnection();

    final hubUrl = _buildHubUrl(serverUrl);
    // [단일 재연결 정책] 라이브러리 자동 재연결(withAutomaticReconnect)을 제거하고,
    // 지수 백오프 기반 수동 재연결(_scheduleManualReconnect)만 사용하여 이중 재연결로 인한 중복 발화를 방지합니다.
    _hubConnection = HubConnectionBuilder()
        .withUrl(hubUrl)
        .build();

    // 서버 침묵 감지용 타임아웃/핑 주기를 명시한다.
    // 기본값에 의존하면 죽은 소켓을 몇 분씩 붙들고 있을 수 있다.
    _hubConnection!.serverTimeoutInMilliseconds = _serverTimeoutMs;
    _hubConnection!.keepAliveIntervalInMilliseconds = _keepAliveIntervalMs;

    // 워치독이 사용할 접속 파라미터를 보관한다.
    _lastServerUrl = serverUrl;
    _lastIpAddress = ipAddress;
    _lastMacAddress = macAddress;
    _lastPublicIpAddress = publicIpAddress;

    // 연결 세션이 끊어졌을 때의 콜백 등록
    _hubConnection!.onclose(({error}) {
      print('[SignalR] 연결 완전 종료. error: ${error?.toString()}');
      _isConnecting = false;

      // [의도적 종료 가드] disconnect()나 재연결 준비 과정에서 의도적으로 끊은 경우에는
      // 재연결을 예약하지 않습니다. (좀비 재연결 타이머 생성으로 인한 무한 핑퐁 방지)
      if (_intentionalClose) {
        print('[SignalR] 의도적 종료로 확인됨 -> 자동 재연결 예약을 건너뜁니다.');
        return;
      }

      // 예기치 못한 물리적 유실인 경우에만 수동 재연결 예약을 가동합니다.
      _scheduleManualReconnect(
        serverUrl: serverUrl,
        deviceCode: deviceCode,
        ipAddress: ipAddress,
        macAddress: macAddress,
        publicIpAddress: publicIpAddress,
      );
    });

    // 2-1) 'ScreenPower' 원격 모니터 전원 제어 이벤트 구독
    // 관리자가 웹에서 특정 장비의 화면을 끄거나 켤 때 서버가 이 이벤트를 밀어준다.
    // 저장되는 설정이 아니라 즉시 실행 명령이다. (재기동하면 화면은 다시 켜진 상태로 뜬다)
    _hubConnection!.on('ScreenPower', (arguments) {
      _lastActivityAt = DateTime.now();
      final raw = (arguments != null && arguments.isNotEmpty) ? arguments.first?.toString() : null;
      final on = !(raw?.toUpperCase() == 'OFF' || raw?.toLowerCase() == 'false');
      print('[SignalR] << ScreenPower 수신: $raw -> ${on ? "켜기" : "끄기"}');
      DisplayModeService.setScreenPower(on);
    });

    // 3) 'DeviceChanged' 서버 전송 푸시 이벤트 구독 설정
    // 데이터 변경 사항이 밀려올 때 1초 디바운스를 주어 화면 깜빡임과 잦은 API 조회를 방지합니다.
    _hubConnection!.on('DeviceChanged', (arguments) {
      print('[SignalR] << DeviceChanged 이벤트 수신');
      _lastActivityAt = DateTime.now(); // 워치독용 최근 수신 시각 갱신
      _debounceTimer?.cancel();
      _debounceTimer = Timer(const Duration(milliseconds: 1000), () {
        if (_onDeviceChanged != null) {
          print('[SignalR] DeviceChanged 콜백 실행 (Debounced 1s)');
          _onDeviceChanged!();
        }
      });
    });

    try {
      // 새 커넥션을 실제로 기동하기 직전에 의도적 종료 플래그를 해제합니다.
      // 이 시점 이후의 onclose는 '예기치 못한 유실'로 간주되어 정상적으로 수동 재연결이 동작합니다.
      _intentionalClose = false;
      // 소켓 통신을 기동합니다.
      await _hubConnection!.start();
      print('[SignalR] 연결 성공!');
      _reconnectAttempt = 0;
      _lastActivityAt = DateTime.now();
      _startWatchdog();
      // 기동 후 즉시 허브에 현재 장비를 등록(그룹 바인딩)합니다.
      await _registerDevice(deviceCode, ipAddress, macAddress, publicIpAddress);

      // 소켓 연결 및 수동 재연결 성공 시 즉각 서버로부터 설정을 동기화하도록 유도합니다.
      if (_onDeviceChanged != null) {
        print('[SignalR] 최초/수동 연결 성공에 따른 화면 데이터 갱신 트리거 호출');
        _onDeviceChanged!();
      }
    } catch (e) {
      print('[SignalR] 연결 시작 중 에러: $e');
      _isConnecting = false;
      _scheduleManualReconnect(
        serverUrl: serverUrl,
        deviceCode: deviceCode,
        ipAddress: ipAddress,
        macAddress: macAddress,
        publicIpAddress: publicIpAddress,
      );
    } finally {
      _isConnecting = false;
    }
  }

  /// [허브에 장치 세션 등록]
  /// 서버 허브의 `RegisterDevice` RPC 메서드를 원격 호출하여,
  /// 백엔드가 소켓 연결 세션에 해당하는 기기의 물리 상태(IP/MAC) 및 장비 코드를 추적하도록 만듭니다.
  Future<void> _registerDevice(String deviceCode, String? ip, String? mac, String? pip) async {
    print('[SignalR] _registerDevice 호출됨: code=$deviceCode, ip=$ip, mac=$mac');
    if (isConnected) {
      try {
        await _hubConnection!.invoke('RegisterDevice', args: [deviceCode, ip ?? "", mac ?? "", pip ?? ""]);
        print('[SignalR] >> RegisterDevice 서버 전송 완료 (Online 처리 기대): $deviceCode');
      } catch (e) {
        print('[SignalR] !! RegisterDevice 서버 전송 에러: $e');
      }
    } else {
      print('[SignalR] !! 서버에 연결되지 않은 상태라 RegisterDevice를 보낼 수 없습니다.');
    }
  }

  /// [수동 재연결 스케줄러]
  /// 지수 백오프 알고리즘($5 * 2^{attempt}$초, 최대 60초)을 활용해 네트워크 장애 발생 시
  /// 순차적으로 연결 재시도 간격을 늘리며 서버에 접속을 시도합니다.
  void _scheduleManualReconnect({
    required String serverUrl,
    required String deviceCode,
    String? ipAddress,
    String? macAddress,
    String? publicIpAddress,
  }) {
    // [코드 일치 가드] 이미 다른 장비 코드로 재구성되었거나 완전히 종료(_currentDeviceCode == null)된 경우,
    // 예약 대상이 된 옛 장비 코드로는 재연결을 시도하지 않습니다. (옛 코드 부활로 인한 왕복 방지)
    if (deviceCode != _currentDeviceCode) {
      print('[SignalR] 재연결 예약 취소: 대상 코드($deviceCode)가 현재 코드($_currentDeviceCode)와 불일치.');
      return;
    }

    _reconnectTimer?.cancel();
    final baseSec = min(_maxReconnectDelaySec, 5 * pow(2, _reconnectAttempt).toInt());
    // [지터] 서버 재기동 시 현장 장비 수십 대가 같은 초에 일제히 재접속하면
    // 서버가 다시 밀려 넘어진다(thundering herd). 대기 시간의 ±30% 를 무작위로 흔든다.
    final jitterMs = (baseSec * 1000 * 0.3 * (Random().nextDouble() * 2 - 1)).round();
    final delayMs = max(1000, baseSec * 1000 + jitterMs);
    _reconnectAttempt++;
    print('[SignalR] 재연결 예약: ${(delayMs / 1000).toStringAsFixed(1)}초 후 (기본 ${baseSec}초 + 지터)');

    _reconnectTimer = Timer(Duration(milliseconds: delayMs), () {
      // 타이머 발동 시점에도 코드가 여전히 유효한지 재확인합니다.
      if (deviceCode != _currentDeviceCode) {
        print('[SignalR] 재연결 실행 취소: 대상 코드($deviceCode)가 현재 코드($_currentDeviceCode)와 불일치.');
        return;
      }
      connect(
        serverUrl: serverUrl,
        deviceCode: deviceCode,
        ipAddress: ipAddress,
        macAddress: macAddress,
        publicIpAddress: publicIpAddress,
        onDeviceChanged: _onDeviceChanged ?? () {},
      );
    });
  }

  /// [좀비 연결 감시 워치독 기동]
  /// 주기적으로 두 가지를 본다.
  ///  1) 연결이 끊겼는데 재연결 예약도 없는 상태(타이머 유실) → 즉시 재연결
  ///  2) 연결됐다고 보고되지만 오랫동안 아무 수신이 없는 상태 → 능동 탐침 후 실패 시 강제 재연결
  void _startWatchdog() {
    _watchdogTimer?.cancel();
    _watchdogTimer = Timer.periodic(_watchdogInterval, (_) => _checkConnectionHealth());
  }

  /// [연결 건강도 점검]
  Future<void> _checkConnectionHealth() async {
    // 의도적으로 끊어둔 상태(설정 화면 진입 등)에서는 개입하지 않는다.
    if (_intentionalClose || _currentDeviceCode == null) return;
    if (_isConnecting) return;

    final deviceCode = _currentDeviceCode!;
    final serverUrl = _lastServerUrl;
    if (serverUrl == null) return;

    // 1) 끊겼는데 재연결 예약이 없는 경우
    if (!isConnected) {
      if (_reconnectTimer == null || !_reconnectTimer!.isActive) {
        print('[SignalR][워치독] 연결이 끊겼는데 재연결 예약이 없다 -> 재연결 시작');
        _scheduleManualReconnect(
          serverUrl: serverUrl,
          deviceCode: deviceCode,
          ipAddress: _lastIpAddress,
          macAddress: _lastMacAddress,
          publicIpAddress: _lastPublicIpAddress,
        );
      }
      return;
    }

    // 2) 연결됐다고 하는데 장시간 조용한 경우 -> 능동 탐침
    final last = _lastActivityAt;
    if (last == null || DateTime.now().difference(last) < _idleProbeThreshold) return;

    print('[SignalR][워치독] ${_idleProbeThreshold.inMinutes}분간 수신 없음 -> 탐침 전송');
    try {
      // RegisterDevice 는 멱등하므로 살아있는지 확인하는 용도로 안전하게 재호출할 수 있다.
      await _hubConnection!
          .invoke('RegisterDevice',
              args: [deviceCode, _lastIpAddress ?? '', _lastMacAddress ?? '', _lastPublicIpAddress ?? ''])
          .timeout(const Duration(seconds: 10));
      _lastActivityAt = DateTime.now();
      print('[SignalR][워치독] 탐침 성공. 연결 정상.');
    } catch (e) {
      print('[SignalR][워치독] 탐침 실패 -> 좀비 연결로 판단, 강제 재연결: $e');
      _intentionalClose = true;
      await _disposeConnection();
      _intentionalClose = false;
      _reconnectAttempt = 0; // 즉시 재시도할 수 있도록 백오프를 초기화한다.
      _scheduleManualReconnect(
        serverUrl: serverUrl,
        deviceCode: deviceCode,
        ipAddress: _lastIpAddress,
        macAddress: _lastMacAddress,
        publicIpAddress: _lastPublicIpAddress,
      );
    }
  }

  /// [소켓 연결 완전 해제 및 구독 해제]
  /// 사이니지 설정 모드 진입 등으로 소켓 연결을 의도적으로 끊을 때 호출하며,
  /// 서버에 UnregisterDevice 메서드를 날리고 모든 백그라운드 재연결 타이머를 정지합니다.
  Future<void> disconnect(String deviceCode) async {
    print('[SignalR] !!! 장치 구독 해제 및 모든 타이머 중단: $deviceCode');
    // 의도적 종료로 표시하여, 이어지는 stop()이 트리거하는 onclose가 재연결을 예약하지 못하도록 합니다.
    _intentionalClose = true;
    _currentDeviceCode = null; // 현재 매핑 코드 제거

    // 1. 모든 타이머 및 상태 초기화 (재연결 방지)
    _reconnectTimer?.cancel();
    _reconnectTimer = null;
    _debounceTimer?.cancel();
    _debounceTimer = null;
    _watchdogTimer?.cancel();
    _watchdogTimer = null;
    _lastActivityAt = null;
    _onDeviceChanged = null;
    _isConnecting = false;
    _reconnectAttempt = 0;

    if (_hubConnection != null) {
      if (isConnected) {
        try {
          await _hubConnection!.invoke('UnregisterDevice', args: [deviceCode]);
          print('[SignalR] >> UnregisterDevice 서버 전송 완료');
        } catch (e) {
          print('[SignalR] 구독 해제 호출 에러: $e');
        }
      }
      
      // 2. 소켓 연결 해제
      await _disposeConnection();
      print('[SignalR] 소켓 물리적 연결 종료됨.');
    }
  }

  /// [허브 커넥션 파괴 및 소거]
  Future<void> _disposeConnection() async {
    if (_hubConnection != null) {
      try {
        await _hubConnection!.stop();
      } catch (_) {}
      _hubConnection = null;
    }
  }

  /// [서버 SignalR 엔드포인트 빌더]
  String _buildHubUrl(String baseUrl) {
    final cleanBaseUrl = baseUrl.endsWith('/') ? baseUrl.substring(0, baseUrl.length - 1) : baseUrl;
    return '$cleanBaseUrl/api/funeral/hubs/device';
  }
}
