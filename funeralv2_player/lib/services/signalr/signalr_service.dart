import 'package:signalr_netcore/signalr_netcore.dart';

class SignalRService {
  HubConnection? _connection;
  bool _isConnecting = false;

  // SignalR 연결 수립
  Future<void> connect(
    String apiServerUrl, 
    String deviceCode, 
    Function() onDeviceChanged,
  ) async {
    if (_isConnecting) return;
    _isConnecting = true;

    try {
      // 백엔드 SignalR 허브 주소: {apiServerUrl}/hubs/device
      final hubUrl = '$apiServerUrl/hubs/device';
      
      _connection = HubConnectionBuilder()
          .withUrl(hubUrl)
          .withAutomaticReconnect()
          .build();

      // 연결이 끊어졌을 때 다시 그룹 등록을 수행하도록 리액션 추가
      _connection!.onreconnected(({connectionId}) async {
        print('SignalR 재연결 완료. 그룹 재등록 수행.');
        await _registerDevice(deviceCode);
      });

      // 변경 이벤트 리스너 등록
      _connection!.on('DeviceChanged', (arguments) {
        print('SignalR 실시간 변경 이벤트 수신! 데이터 동기화 개시.');
        onDeviceChanged();
      });

      await _connection!.start();
      print('SignalR 소켓 연결 성공!');
      
      await _registerDevice(deviceCode);
    } catch (e) {
      print('SignalR 소켓 연결 에러 (오프라인 상태일 수 있음): $e');
    } finally {
      _isConnecting = false;
    }
  }

  // 장비 등록 수행
  Future<void> _registerDevice(String deviceCode) async {
    if (_connection != null && _connection!.state == HubConnectionState.Connected) {
      await _connection!.invoke('RegisterDevice', args: [deviceCode]);
      print('RegisterDevice 그룹 구독 호출 완료: $deviceCode');
    }
  }

  // 연결 종료
  Future<void> disconnect(String deviceCode) async {
    if (_connection != null) {
      try {
        await _connection!.invoke('UnregisterDevice', args: [deviceCode]);
      } catch (_) {}
      await _connection!.stop();
      _connection = null;
      print('SignalR 소켓 연결 종료.');
    }
  }
}
