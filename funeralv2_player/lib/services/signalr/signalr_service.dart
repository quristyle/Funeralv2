import 'dart:async';

import 'package:signalr_netcore/signalr_client.dart';

class SignalRService {
  HubConnection? _hubConnection;
  bool _isConnecting = false;
  bool _isListenerInitialized = false; // 리스너 초기화 상태
  Timer? _reconnectTimer; // 수동 재연결 타이머

  bool get isConnected => _hubConnection?.state == HubConnectionState.Connected;

  Future<void> connect({
    required String serverUrl,
    required String deviceCode,
    required Function() onDeviceChanged,
  }) async {
    if (_isConnecting || isConnected) {
      print('[SignalR] 이미 연결 중이거나 연결된 상태입니다.');
      return;
    }
    _isConnecting = true;
    print('[SignalR] 연결 프로세스 시작...');

    // 이전 연결 및 타이머 정리
    await _disposeConnection();

    final hubUrl = _buildHubUrl(serverUrl);
    _hubConnection = HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect() // 자동 재연결 활성화
        .build();

    // --- 이벤트 핸들러 등록 ---

    // 1. 자동 재연결 성공 시
    _hubConnection!.onreconnected(({connectionId}) {
      print('[SignalR] 자동 재연결 성공. ConnectionId: $connectionId');
      // 재연결 후 장치 재등록
      _registerDevice(deviceCode);
    });

    // 2. 연결이 완전히 종료되었을 때 (자동 재연결 실패 후 등)
    _hubConnection!.onclose(({error}) {
      print('[SignalR] 연결 종료: ${error?.toString()}');
      // 10초 후 수동으로 다시 연결 시도
      _scheduleManualReconnect(
          serverUrl: serverUrl,
          deviceCode: deviceCode,
          onDeviceChanged: onDeviceChanged);
    });

    // 3. 서버로부터 메시지 수신 (최초 한 번만 등록)
    if (!_isListenerInitialized) {
      _hubConnection!.on('DeviceChanged', (arguments) {
        print('[SignalR] << DeviceChanged 이벤트 수신');
        onDeviceChanged();
      });
       _hubConnection!.on('ReceiveSystemMessage', (arguments) {
        print('[SignalR] << System Message: ${arguments?.first}');
      });
      _isListenerInitialized = true;
    }

    try {
      await _hubConnection!.start();
      print('[SignalR] 연결 성공!');
      // 최초 연결 후 장치 등록
      await _registerDevice(deviceCode);
    } catch (e) {
      print('[SignalR] 연결 시작 중 에러: $e');
      // 에러 발생 시 onclose가 호출되므로 여기서 수동 재연결 로직이 시작됨
    } finally {
      _isConnecting = false;
    }
  }

  Future<void> _registerDevice(String deviceCode) async {
    if (isConnected) {
      try {
        await _hubConnection!.invoke('RegisterDevice', args: [deviceCode]);
        print('[SignalR] >> RegisterDevice 그룹 구독 완료: $deviceCode');
      } catch (e) {
        print('[SignalR] 장치 등록 중 에러: $e');
      }
    } else {
      print('[SignalR] 연결이 끊어져 장치를 등록할 수 없습니다.');
    }
  }

  void _scheduleManualReconnect({
    required String serverUrl,
    required String deviceCode,
    required Function() onDeviceChanged,
  }) {
    _reconnectTimer?.cancel(); // 기존 타이머 취소
    _reconnectTimer = Timer(const Duration(seconds: 10), () {
      print('[SignalR] 10초 후 수동으로 재연결을 시도합니다...');
      connect(
        serverUrl: serverUrl,
        deviceCode: deviceCode,
        onDeviceChanged: onDeviceChanged,
      );
    });
  }

  Future<void> disconnect(String deviceCode) async {
    print('[SignalR] 연결 해제 요청.');
    if (_hubConnection == null) return;
    
    if (isConnected) {
       try {
        await _hubConnection!.invoke('UnregisterDevice', args: [deviceCode]);
        print('[SignalR] >> UnregisterDevice 그룹 구독 해제 완료');
      } catch (e) {
        print('[SignalR] 장치 구독 해제 중 에러: $e');
      }
    }
    await _disposeConnection();
    print('[SignalR] 연결이 완전히 해제되었습니다.');
  }

  Future<void> _disposeConnection() async {
    _reconnectTimer?.cancel();
    _reconnectTimer = null;
    if (_hubConnection != null) {
      await _hubConnection!.stop();
      _hubConnection = null;
    }
  }

  String _buildHubUrl(String baseUrl) {
    final cleanBaseUrl = baseUrl.endsWith('/') ? baseUrl.substring(0, baseUrl.length - 1) : baseUrl;
    return '$cleanBaseUrl/api/funeral/hubs/device';
  }
}
