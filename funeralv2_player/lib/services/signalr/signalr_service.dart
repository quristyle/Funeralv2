import 'package:signalr_netcore/hub_connection.dart';
import 'package:signalr_netcore/hub_connection_builder.dart';

class SignalRService {
  HubConnection? _connection;
  bool _isConnecting = false;
  bool _isInitialized = false; // 추가: 리스너 중복 등록 방지용

  // SignalR 연결 수립
  Future<void> connect(
    String serverBaseUrl, 
    String deviceCode, 
    Function() onDeviceChanged,
  ) async {
    // 이미 연결 중이거나 초기화되었다면 중복 실행 방지
    if (_isConnecting || (_connection != null && _connection!.state == HubConnectionState.Connected)) {
      return;
    }
    
    _isConnecting = true;

    try {
      final baseUrl = serverBaseUrl.endsWith('/') ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) : serverBaseUrl;
      final hubUrl = '$baseUrl/api/funeral/hubs/device';
      
      _connection = HubConnectionBuilder()
          .withUrl(hubUrl)
          .withAutomaticReconnect()
          .build();

      _connection!.onreconnected(({connectionId}) async {
        print('[SignalR] 재연결 완료. 그룹 재등록 수행.');
        await _registerDevice(deviceCode);
      });

      // 리스너는 단 한 번만 등록
      if (!_isInitialized) {
        _connection!.on('DeviceChanged', (arguments) {
          print('[SignalR Event] DeviceChanged 수신!');
          onDeviceChanged();
        });
        _isInitialized = true;
      }

      print('[SignalR Connection] 연결 시도 중...');
      await _connection!.start();
      print('[SignalR Connection] 소켓 연결 성공!');
      
      await _registerDevice(deviceCode);
    } catch (e) {
      print('[SignalR Error] 소켓 연결 에러: $e');
    } finally {
      _isConnecting = false;
    }
  }

  Future<void> _registerDevice(String deviceCode) async {
    if (_connection != null && _connection!.state == HubConnectionState.Connected) {
      await _connection!.invoke('RegisterDevice', args: [deviceCode]);
      print('[SignalR] RegisterDevice 그룹 구독 완료: $deviceCode');
    }
  }

  Future<void> disconnect(String deviceCode) async {
    if (_connection != null) {
      try {
        await _connection!.invoke('UnregisterDevice', args: [deviceCode]);
      } catch (_) {}
      await _connection!.stop();
      _connection = null;
      _isInitialized = false; // 초기화 상태 리셋
      print('[SignalR] 소켓 연결 종료.');
    }
  }
}
