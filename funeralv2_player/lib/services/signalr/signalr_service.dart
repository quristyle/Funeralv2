import 'dart:async';
import 'dart:math';
import 'package:signalr_netcore/signalr_client.dart';

class SignalRService {
  // 싱글톤 인스턴스
  static final SignalRService _instance = SignalRService._internal();
  factory SignalRService() => _instance;
  SignalRService._internal();

  HubConnection? _hubConnection;
  bool _isConnecting = false;
  Timer? _reconnectTimer;
  Timer? _debounceTimer;
  int _reconnectAttempt = 0;
  static const int _maxReconnectDelaySec = 60;

  // 알림을 위한 콜백 보관
  Function()? _onDeviceChanged;

  bool get isConnected => _hubConnection?.state == HubConnectionState.Connected;

  Future<void> connect({
    required String serverUrl,
    required String deviceCode,
    String? ipAddress,
    String? macAddress,
    String? publicIpAddress,
    required Function() onDeviceChanged,
  }) async {
    // 콜백 항상 최신화
    _onDeviceChanged = onDeviceChanged;

    if (_isConnecting || isConnected) {
      return;
    }

    _isConnecting = true;
    print('[SignalR] 연결 프로세스 시작...');

    await _disposeConnection();

    final hubUrl = _buildHubUrl(serverUrl);
    _hubConnection = HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect(retryDelays: [0, 2000, 5000, 10000])
        .build();

    _hubConnection!.onreconnected(({connectionId}) {
      print('[SignalR] 자동 재연결 성공. ConnectionId: $connectionId');
      _reconnectAttempt = 0;
      _registerDevice(deviceCode, ipAddress, macAddress, publicIpAddress);
    });

    _hubConnection!.onclose(({error}) {
      print('[SignalR] 연결 완전 종료. error: ${error?.toString()}');
      _isConnecting = false;
      _scheduleManualReconnect(
        serverUrl: serverUrl,
        deviceCode: deviceCode,
        ipAddress: ipAddress,
        macAddress: macAddress,
        publicIpAddress: publicIpAddress,
      );
    });

    // 서버 메시지 수신 (리스너는 HubConnection 객체 생성 시 한 번만 등록됨)
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
      await _hubConnection!.start();
      print('[SignalR] 연결 성공!');
      _reconnectAttempt = 0;
      await _registerDevice(deviceCode, ipAddress, macAddress, publicIpAddress);
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

  void _scheduleManualReconnect({
    required String serverUrl,
    required String deviceCode,
    String? ipAddress,
    String? macAddress,
    String? publicIpAddress,
  }) {
    _reconnectTimer?.cancel();
    final delaySec = min(_maxReconnectDelaySec, 5 * pow(2, _reconnectAttempt).toInt());
    _reconnectAttempt++;

    _reconnectTimer = Timer(Duration(seconds: delaySec), () {
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

  Future<void> disconnect(String deviceCode) async {
    print('[SignalR] !!! 장치 구독 해제 및 모든 타이머 중단: $deviceCode');
    
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

  Future<void> _disposeConnection() async {
    if (_hubConnection != null) {
      try {
        // onclose 핸들러를 제거하거나 무시하도록 처리하여 재연결 트리거 방지
        await _hubConnection!.stop();
      } catch (_) {}
      _hubConnection = null;
    }
  }

  String _buildHubUrl(String baseUrl) {
    final cleanBaseUrl = baseUrl.endsWith('/') ? baseUrl.substring(0, baseUrl.length - 1) : baseUrl;
    return '$cleanBaseUrl/api/funeral/hubs/device';
  }
}
