import 'dart:async';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../models/device_models.dart';
import '../services/api/api_service.dart';
import '../services/signalr/signalr_service.dart';
import 'portrait/portrait_view.dart';
import 'guide/room_guide_view.dart';
import 'guide/entrance_guide_view.dart';
import 'kiosk/kiosk_view.dart';
import 'multimedia/multimedia_view.dart';

/// [장비 라우터 디스패처 위젯]
/// 앱이 로딩된 후, 서버 또는 로컬 캐시로부터 장비의 세부 설정 정보를 가져와 
/// 장비 타입(`deviceType`)에 대응하는 실제 사이니지 화면(영정, 호실 안내, 종합 안내, 키오스크 등)으로 
/// 분기하여 화면을 렌더링하고 실시간 업데이트 구독을 활성화하는 역할을 담당합니다.
class DeviceDispatcher extends StatefulWidget {
  final String serverBaseUrl; // 통합 서버 Base URL
  final String deviceCode; // 장비 식별 코드
  final String ipAddress; // 기기 로컬 IP 주소
  final String macAddress; // 기기 MAC 주소
  final String publicIpAddress; // 기기 공인 IP 주소
  final VoidCallback onOpenSettings; // 플레이어 동작 도중 강제로 환경 설정 화면을 열기 위한 콜백

  const DeviceDispatcher({
    super.key,
    required this.serverBaseUrl,
    required this.deviceCode,
    required this.ipAddress,
    required this.macAddress,
    required this.publicIpAddress,
    required this.onOpenSettings,
  });

  @override
  State<DeviceDispatcher> createState() => _DeviceDispatcherState();
}

class _DeviceDispatcherState extends State<DeviceDispatcher> {
  // 실시간 서버 알림(웹소켓)을 수신하기 위한 서비스 객체
  final SignalRService _signalRService = SignalRService();
  
  // 서버 또는 로컬 DB에서 불러온 장비의 메타데이터 및 레이아웃 설정 정보
  DeviceDto? device;
  
  // 로딩 플래그 및 오류 발생 시 메시지 보관 변수
  bool isLoading = true;
  String? error;
  bool _isRefreshing = false; // 새로고침 중복 기동 제어 플래그 (디바운스 목적)

  // 서버 연결 실패 시 백그라운드 자동 재시도 처리를 위한 타이머
  Timer? _retryTimer;
  int _retryCountdown = 20; // 자동 재시도 대기 시간(초)

  /// [위젯 초기화]
  /// 위젯 기동 시점에 [_loadDevice] 메서드를 호출하여 장비 세부 정보를 가져옵니다.
  @override
  void initState() {
    super.initState();
    print('[Dispatcher] initState() - 장비 초기화 프로세스 시작');
    _loadDevice();
  }

  /// [자원 해제]
  /// 위젯 소멸 시점에 재연결 타이머를 취소하고 SignalR 소켓 커넥션을 물리적으로 파괴합니다.
  @override
  void dispose() {
    print('[Dispatcher] dispose() - 기존 연결 및 타이머 정리');
    _retryTimer?.cancel();
    _signalRService.disconnect(widget.deviceCode);
    super.dispose();
  }

  /// [장비 정보 로드 핵심 제어 루틴]
  /// 서버 API에서 최신 데이터를 비동기로 조회하며, 성공 시 소켓을 맺고 화면 전환 처리를 진행합니다.
  /// 네트워크 불통 또는 서버 오프라인 시 로컬 저장소 캐시로 복구를 유도합니다.
  Future<void> _loadDevice() async {
    if (_isRefreshing) {
      print('[Dispatcher] _loadDevice() 스킵: 이미 진행 중입니다.');
      return;
    }
    _isRefreshing = true;
    _retryTimer?.cancel(); 

    print('[Dispatcher] 데이터 로드 루틴 시작 (장비코드: ${widget.deviceCode})');
    setState(() {
      isLoading = (device == null);
      error = null;
      _retryCountdown = 20;
    });

    try {
      print('[Dispatcher] API 서버에 최신 장비 정보 요청 중...');
      final fetched = await ApiService().fetchDevice(widget.serverBaseUrl, widget.deviceCode);
      
      // 잦은 새로고침을 막기 위해 2초의 방어 대기 후 스로틀을 해제합니다.
      Future.delayed(const Duration(seconds: 2), () => _isRefreshing = false);

      if (!mounted) return;

      if (fetched != null) {
        print('[Dispatcher] 서버 데이터 수신 성공: 타입=${fetched.deviceType}');
        _handleDeviceLoaded(fetched);
      } else {
        print('[Dispatcher] 서버 응답이 없습니다. 로컬 캐시(저장된 데이터)를 확인합니다.');
        _loadFromCache();
      }
    } catch (e) {
      print('[Dispatcher] 로드 중 예외 발생: $e');
      _isRefreshing = false;
      if (mounted) _loadFromCache();
    }
  }

  /// [로컬 데이터베이스 캐시 기반 복구]
  /// 서버 오프라인 시 기기 내부에 보관하고 있던 마지막 장비 설정 정보로 기기를 기동합니다.
  /// 캐시조차 없는 경우 화면에 에러를 표시하고 20초 간격 자동 재시도 스케줄러를 구동합니다.
  Future<void> _loadFromCache() async {
    print('[Dispatcher] 로컬 DB(SQLite) 조회 시도...');
    final cached = await ApiService().getCachedDevice(widget.deviceCode);
    
    if (!mounted) return;

    if (cached != null) {
      print('[Dispatcher] 로컬 데이터 복구 성공! 오프라인 모드로 구동합니다.');
      _handleDeviceLoaded(cached);
    } else {
      print('[Dispatcher] 저장된 로컬 데이터가 없습니다. 복구 불가능.');
      setState(() {
        error = "서버 연결에 실패했으며 저장된 로컬 정보가 없습니다.";
        isLoading = false;
      });
      _startRetryTimer();
    }
  }

  /// [재시도 타이머 가동]
  /// 20초 카운트다운을 시작하고 0초가 되면 [_loadDevice]를 재호출합니다.
  void _startRetryTimer() {
    _retryCountdown = 20;
    _retryTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) { timer.cancel(); return; }
      setState(() {
        if (_retryCountdown > 1) {
          _retryCountdown--;
        } else {
          print('[Dispatcher] 타이머 만료 - 서버 재접속을 자동 시도합니다.');
          timer.cancel();
          _loadDevice();
        }
      });
    });
  }

  /// [장비 정보 로딩 후속 처리]
  /// 장비 DTO가 확보되면 화면 방향(가로/세로) 정보를 캐싱하고,
  /// 웹소켓 허브에 연결하여 'DeviceChanged' 실시간 설정을 구독합니다.
  void _handleDeviceLoaded(DeviceDto loadedDevice) {
    print('[Dispatcher] 최종 데이터 할당 및 화면 준비 완료');
    
    _saveOrientationCache(loadedDevice.displayOrientation);

    setState(() {
      device = loadedDevice;
      isLoading = false;
    });

    _signalRService.connect(
      serverUrl: widget.serverBaseUrl,
      deviceCode: widget.deviceCode,
      ipAddress: widget.ipAddress,
      macAddress: widget.macAddress,
      publicIpAddress: widget.publicIpAddress,
      onDeviceChanged: () {
        if (mounted) {
          print('[Dispatcher] << 서버로부터 설정 변경 신호 수신! 최신화 로직 가동');
          _loadDevice();
        }
      },
    );
  }

  /// [화면 방향 정보 영속화]
  /// 플레이어 시작 시 윈도우 회전 기준값을 디바이스 메모리에 적재합니다.
  Future<void> _saveOrientationCache(String orientation) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('displayOrientation', orientation);
  }

  /// [위젯 빌드]
  /// 로딩 상태, 오류 상태, 그리고 정상 동작 상태에 따른 5가지 사이니지 장비 유형 분기 화면을 정의합니다.
  @override
  Widget build(BuildContext context) {
    if (isLoading) {
      return const Scaffold(
        body: Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              CircularProgressIndicator(color: Color(0xFFC0A060)),
              SizedBox(height: 20),
              Text("장비 환경을 구성하고 있습니다...", style: TextStyle(color: Colors.white54)),
            ],
          ),
        ),
      );
    }

    if (error != null || device == null) {
      return Scaffold(
        body: Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.cloud_off, color: Colors.white24, size: 80),
              const SizedBox(height: 24),
              Text(error ?? "연결 오류", textAlign: TextAlign.center, style: const TextStyle(color: Colors.white, fontSize: 18)),
              const SizedBox(height: 40),
              ElevatedButton(
                style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFFC0A060), foregroundColor: Colors.black),
                onPressed: widget.onOpenSettings, 
                child: const Text("설정 다시 하기")
              ),
              const SizedBox(height: 24),
              TextButton.icon(
                onPressed: _loadDevice, 
                icon: const Icon(Icons.refresh, size: 18),
                label: Text("즉시 재시도 ($_retryCountdown초 후 자동 시도)", style: const TextStyle(color: Colors.white54)),
              ),
            ],
          ),
        ),
      );
    }

    print('[Dispatcher] 현재 장비 타입에 맞는 화면을 렌더링합니다: ${device!.deviceType}');
    
    switch (device!.deviceType) {
      case 'FUNERAL_PORTRAIT':
        return PortraitView(serverBaseUrl: widget.serverBaseUrl, deviceCode: widget.deviceCode, onOpenSettings: widget.onOpenSettings);
      
      case 'ROOM_GUIDE':
        return RoomGuideView(serverBaseUrl: widget.serverBaseUrl, deviceCode: widget.deviceCode, onOpenSettings: widget.onOpenSettings);
      
      case 'ENTRANCE_GUIDE':
        return EntranceGuideView(serverBaseUrl: widget.serverBaseUrl, deviceCode: widget.deviceCode, onOpenSettings: widget.onOpenSettings);
      
      case 'KIOSK':
        return KioskView(serverBaseUrl: widget.serverBaseUrl, deviceCode: widget.deviceCode, onOpenSettings: widget.onOpenSettings);
      
      case 'MULTIMEDIA':
        return MultimediaView(serverBaseUrl: widget.serverBaseUrl, deviceCode: widget.deviceCode, onOpenSettings: widget.onOpenSettings);
      
      default:
        return PortraitView(serverBaseUrl: widget.serverBaseUrl, deviceCode: widget.deviceCode, onOpenSettings: widget.onOpenSettings);
    }
  }
}
