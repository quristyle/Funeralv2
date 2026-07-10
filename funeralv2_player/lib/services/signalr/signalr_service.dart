import 'dart:async';
import 'dart:math';

import 'package:signalr_netcore/signalr_client.dart';
import 'package:http/http.dart' as http;

class SignalRService {
  HubConnection? _hubConnection;
  bool _isConnecting = false;
  Timer? _reconnectTimer;

  // 수동 재연결 시도 횟수 (exponential backoff 계산용)
  int _reconnectAttempt = 0;

  // 재연결 대기 상한선(초)
  static const int _maxReconnectDelaySec = 60;

  bool get isConnected => _hubConnection?.state == HubConnectionState.Connected;

  Future<void> connect({
    required String serverUrl,
    required String deviceCode,
    String? ipAddress,
    String? macAddress,
    String? publicIpAddress,
    required Function() onDeviceChanged,
  }) async {
    if (_isConnecting) {
      print('[SignalR] 이미 연결 시도 중입니다. 중복 요청 무시.');
      return;
    }
    if (isConnected) {
      print('[SignalR] 이미 연결된 상태입니다.');
      return;
    }

    _isConnecting = true;
    print('[SignalR] 연결 프로세스 시작... (시도 #$_reconnectAttempt)');

    // 이전 연결 및 타이머 정리
    await _disposeConnection();

    final hubUrl = _buildHubUrl(serverUrl);

    // ── 핵심: 새 HubConnection 객체마다 리스너를 반드시 등록해야 함 ──
    // _isListenerInitialized 같은 플래그를 사용하면 재연결 시 새 객체에
    // 리스너가 등록되지 않아 메시지를 수신하지 못하는 버그가 발생함.
    _hubConnection = HubConnectionBuilder()
        .withUrl(hubUrl)
        // 자동 재연결: 0, 2, 5, 10초 간격으로 4회 시도 후 onclose 호출
        // onclose에서 수동 재연결(exponential backoff)을 이어받음
        .withAutomaticReconnect(retryDelays: [0, 2000, 5000, 10000])
        .build();

    // 1. 자동 재연결 성공 시 → 장치 재등록
    _hubConnection!.onreconnected(({connectionId}) {
      print('[SignalR] 자동 재연결 성공. ConnectionId: $connectionId');
      _reconnectAttempt = 0; // 성공 시 카운터 초기화
      _registerDevice(deviceCode, ipAddress, macAddress, publicIpAddress);
    });

    // 2. 자동 재연결 실패 후 연결이 완전히 종료되었을 때
    //    → 수동 재연결 스케줄링 (exponential backoff)
    _hubConnection!.onclose(({error}) {
      print('[SignalR] 연결 완전 종료. error: ${error?.toString()}');
      _isConnecting = false;
      _scheduleManualReconnect(
        serverUrl: serverUrl,
        deviceCode: deviceCode,
        ipAddress: ipAddress,
        macAddress: macAddress,
        publicIpAddress: publicIpAddress,
        onDeviceChanged: onDeviceChanged,
      );
    });

    // 3. 서버 메시지 수신 리스너 등록
    //    ★ 반드시 새 _hubConnection 객체 생성 후 매번 등록해야 함 ★
    _hubConnection!.on('DeviceChanged', (arguments) {
      print('[SignalR] << DeviceChanged 이벤트 수신');
      onDeviceChanged();
    });
    _hubConnection!.on('ReceiveSystemMessage', (arguments) {
      print('[SignalR] << System Message: ${arguments?.first}');
    });

    try {
      await _hubConnection!.start();
      print('[SignalR] 연결 성공!');
      _reconnectAttempt = 0; // 연결 성공 시 카운터 초기화
      await _registerDevice(deviceCode, ipAddress, macAddress, publicIpAddress);
    } catch (e) {
      print('[SignalR] 연결 시작 중 에러: $e');
      // start() 실패 시 onclose가 호출되지 않을 수 있으므로 여기서도 처리
      _isConnecting = false;
      _scheduleManualReconnect(
        serverUrl: serverUrl,
        deviceCode: deviceCode,
        ipAddress: ipAddress,
        macAddress: macAddress,
        publicIpAddress: publicIpAddress,
        onDeviceChanged: onDeviceChanged,
      );
      return;
    } finally {
      _isConnecting = false;
    }
  }

  Future<void> _registerDevice(String deviceCode, String? ipAddress, String? macAddress, String? publicIpAddress) async {
    if (isConnected) {
      try {
        await _hubConnection!.invoke('RegisterDevice', args: [deviceCode, ipAddress ?? "", macAddress ?? "", publicIpAddress ?? ""]);
        print('[SignalR] >> RegisterDevice 그룹 구독 완료: $deviceCode (IP: $ipAddress, MAC: $macAddress, PublicIP: $publicIpAddress)');
      } catch (e) {
        print('[SignalR] 장치 등록 중 에러: $e');
      }
    } else {
      print('[SignalR] 연결이 끊어져 장치를 등록할 수 없습니다.');
    }
  }

  /// Exponential backoff 재연결 스케줄링
  /// 시도 횟수에 따라 대기 시간: 5, 10, 20, 40, 60, 60, ... 초
  void _scheduleManualReconnect({
    required String serverUrl,
    required String deviceCode,
    String? ipAddress,
    String? macAddress,
    String? publicIpAddress,
    required Function() onDeviceChanged,
  }) {
    _reconnectTimer?.cancel();

    final delaySec = min(
      _maxReconnectDelaySec,
      5 * pow(2, _reconnectAttempt).toInt(),
    );
    _reconnectAttempt++;

    print('[SignalR] ${delaySec}초 후 수동 재연결을 시도합니다... (시도 #$_reconnectAttempt)');

    _reconnectTimer = Timer(Duration(seconds: delaySec), () {
      connect(
        serverUrl: serverUrl,
        deviceCode: deviceCode,
        ipAddress: ipAddress,
        macAddress: macAddress,
        onDeviceChanged: onDeviceChanged,
      );
    });
  }

  Future<void> disconnect(String deviceCode) async {
    print('[SignalR] 연결 해제 요청.');
    _reconnectTimer?.cancel();
    _reconnectTimer = null;

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
      try {
        await _hubConnection!.stop();
      } catch (_) {}
      _hubConnection = null;
    }
    // _isConnecting은 호출부에서 명시적으로 관리
  }

  String _buildHubUrl(String baseUrl) {
    final cleanBaseUrl = baseUrl.endsWith('/')
        ? baseUrl.substring(0, baseUrl.length - 1)
        : baseUrl;
    return '$cleanBaseUrl/api/funeral/hubs/device';
  }
}
