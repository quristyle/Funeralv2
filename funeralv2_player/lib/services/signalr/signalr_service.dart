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
      if (_onDeviceChanged != null) {
        _onDeviceChanged!();
      }
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
    if (isConnected) {
      try {
        await _hubConnection!.invoke('RegisterDevice', args: [deviceCode, ip ?? "", mac ?? "", pip ?? ""]);
        print('[SignalR] >> RegisterDevice 그룹 구독 완료: $deviceCode');
      } catch (e) {
        print('[SignalR] 장치 등록 에러: $e');
      }
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
    _reconnectTimer?.cancel();
    _onDeviceChanged = null;
    if (_hubConnection != null) {
      if (isConnected) {
        try { await _hubConnection!.invoke('UnregisterDevice', args: [deviceCode]); } catch (_) {}
      }
      await _disposeConnection();
    }
  }

  Future<void> _disposeConnection() async {
    if (_hubConnection != null) {
      try { await _hubConnection!.stop(); } catch (_) {}
      _hubConnection = null;
    }
  }

  String _buildHubUrl(String baseUrl) {
    final cleanBaseUrl = baseUrl.endsWith('/') ? baseUrl.substring(0, baseUrl.length - 1) : baseUrl;
    return '$cleanBaseUrl/api/funeral/hubs/device';
  }
}
