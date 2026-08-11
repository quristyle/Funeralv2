import 'dart:async';
import 'dart:math';
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

    // 3) 'DeviceChanged' 서버 전송 푸시 이벤트 구독 설정
    // 데이터 변경 사항이 밀려올 때 1초 디바운스를 주어 화면 깜빡임과 잦은 API 조회를 방지합니다.
    _hubConnection!.on('DeviceChanged', (arguments) {
      print('[SignalR] << DeviceChanged 이벤트 수신');
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
    final delaySec = min(_maxReconnectDelaySec, 5 * pow(2, _reconnectAttempt).toInt());
    _reconnectAttempt++;

    _reconnectTimer = Timer(Duration(seconds: delaySec), () {
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
