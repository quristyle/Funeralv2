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
  
  // 하위 뷰 갱신 제어를 위한 고유 키
  Key _viewKey = UniqueKey();
  
  // 로딩 플래그 및 오류 발생 시 메시지 보관 변수
  bool isLoading = true;
  String? error;
  bool _isRefreshing = false; // 새로고침 중복 기동 제어 플래그 (디바운스 목적)
  bool _isLastConnectionFailed = false; // 직전 백엔드 서버 연결 실패 상태인지 여부 기록

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

  /// [장비 정보 로드 핵심 제어 루틴 (Offline-First 적용)]
  /// 앱 구동 즉시 로컬 캐시 DB에서 장비 정보를 불러와 화면을 100ms 이내에 표출합니다. (TTV 최소화)
  /// 화면이 준비된 직후 백그라운드 비동기로 백엔드 서버에 접속하여, 데이터 변경 감지 시에만 화면을 리프레시합니다.
  Future<void> _loadDevice() async {
    if (_isRefreshing) {
      print('[Dispatcher] _loadDevice() 스킵: 이미 진행 중입니다.');
      return;
    }
    _isRefreshing = true;
    _retryTimer?.cancel(); 

    print('[Dispatcher] 데이터 로드 루틴 시작 (장비코드: ${widget.deviceCode})');
    
    // 1. 즉시 로컬 캐시 DB 조회 시도 (TTV 최적화)
    final cached = await ApiService().getCachedDevice(widget.deviceCode);
    if (cached != null) {
      print('[Dispatcher] [Cache-First] 로컬 캐시 조회 성공! 화면을 먼저 기동합니다.');
      if (mounted) {
        setState(() {
          device = cached;
          isLoading = false;
          error = null;
        });
      }
    } else {
      // 캐시조차 없는 경우 연결에 성공할 때까지 최초 1회만 로딩바 노출
      if (mounted) {
        setState(() {
          isLoading = (device == null);
          error = null;
        });
      }
    }

    try {
      print('[Dispatcher] [Background] API 서버에 최신 장비 정보 요청 중...');
      final fetched = await ApiService().fetchDevice(widget.serverBaseUrl, widget.deviceCode);
      
      // 잦은 새로고침을 막기 위해 2초의 방어 대기 후 스로틀을 해제합니다.
      Future.delayed(const Duration(seconds: 2), () => _isRefreshing = false);

      if (!mounted) return;

      if (fetched != null) {
        // 기존 캐시 정보와 서버의 최신 정보에 변동 사항이 있는지 검사
        final isDifferent = device == null || 
            _isLastConnectionFailed || // 서버 오프라인 복구 시에는 강제 갱신 유도
            device!.id != fetched.id ||
            device!.deviceType != fetched.deviceType ||
            device!.roomId != fetched.roomId ||
            device!.displayOrientation != fetched.displayOrientation || // 화면 방향 갱신 조건 추가
            device!.portraitOrientation != fetched.portraitOrientation || // 영정 방향 갱신 조건 추가
            device!.videoOrientation != fetched.videoOrientation || // 동영상 방향 갱신 조건 추가
            device!.isVideoEnabled != fetched.isVideoEnabled ||
            device!.isMusicEnabled != fetched.isMusicEnabled ||
            device!.isMuted != fetched.isMuted ||
            device!.contentIntervalSec != fetched.contentIntervalSec ||
            device!.memorialPhotoEffect != fetched.memorialPhotoEffect ||
            device!.isBackgroundImageEnabled != fetched.isBackgroundImageEnabled ||
            device!.backgroundImageUrl != fetched.backgroundImageUrl;

        _isLastConnectionFailed = false; // 연결에 성공했으므로 실패 플래그 해제

        if (isDifferent) {
          print('[Dispatcher] [Background] 서버 데이터 갱신 및 차이 발견 -> 화면을 업데이트합니다.');
          _handleDeviceLoaded(fetched);
        } else {
          print('[Dispatcher] [Background] 데이터 일치 -> 화면 갱신을 생략하고 기존 재생 상태를 유지합니다.');
          _connectSignalR();
        }
      } else {
        print('[Dispatcher] [Background] 서버 응답 없음 -> 기존 로컬 캐시 화면 상태를 계속 유지합니다.');
        _isLastConnectionFailed = true; // 서버 응답 실패 마킹
        if (device == null) {
          // 화면도 없고 캐시도 없고 서버 통신도 실패한 예외적 최악의 케이스에만 에러 표출
          setState(() {
            error = "서버 연결에 실패했으며 저장된 로컬 정보가 없습니다.";
            isLoading = false;
          });
          _startRetryTimer();
        }
      }
    } catch (e) {
      print('[Dispatcher] [Background] 서버 조회 중 예외 발생: $e');
      _isRefreshing = false;
      _isLastConnectionFailed = true; // 서버 조회 예외 실패 마킹
      if (mounted && device == null) {
        setState(() {
          error = "서버 연결에 실패했으며 저장된 로컬 정보가 없습니다.";
          isLoading = false;
        });
        _startRetryTimer();
      }
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
  /// 뷰 갱신용 고유 키를 변경한 후 웹소켓 연결을 기동합니다.
  void _handleDeviceLoaded(DeviceDto loadedDevice) {
    print('[Dispatcher] 최종 데이터 할당 및 화면 준비 완료');
    
    _saveOrientationCache(loadedDevice.displayOrientation);

    setState(() {
      device = loadedDevice;
      _viewKey = UniqueKey(); // 새로운 장비 정보 수신 시 뷰 키 갱신 -> 하위 위젯 리로드 유도
      isLoading = false;
    });

    _connectSignalR();
  }

  /// [실시간 서버 알림 서비스 연결]
  void _connectSignalR() {
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
    // 방향성에 따른 90도 회전 턴수(Turns) 자동 동기화 매핑저장 (PORTRAIT = 1턴, LANDSCAPE = 0턴)
    final int targetTurns = (orientation == 'PORTRAIT') ? 1 : 0;
    await prefs.setInt('displayRotationTurns', targetTurns);
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
        return PortraitView(key: _viewKey, serverBaseUrl: widget.serverBaseUrl, deviceCode: widget.deviceCode, onOpenSettings: widget.onOpenSettings);
      
      case 'ROOM_GUIDE':
        return RoomGuideView(key: _viewKey, serverBaseUrl: widget.serverBaseUrl, deviceCode: widget.deviceCode, onOpenSettings: widget.onOpenSettings);
      
      case 'ENTRANCE_GUIDE':
        return EntranceGuideView(key: _viewKey, serverBaseUrl: widget.serverBaseUrl, deviceCode: widget.deviceCode, onOpenSettings: widget.onOpenSettings);
      
      case 'KIOSK':
        return KioskView(key: _viewKey, serverBaseUrl: widget.serverBaseUrl, deviceCode: widget.deviceCode, onOpenSettings: widget.onOpenSettings);
      
      case 'MULTIMEDIA':
        return MultimediaView(key: _viewKey, serverBaseUrl: widget.serverBaseUrl, deviceCode: widget.deviceCode, onOpenSettings: widget.onOpenSettings);
      
      default:
        return PortraitView(key: _viewKey, serverBaseUrl: widget.serverBaseUrl, deviceCode: widget.deviceCode, onOpenSettings: widget.onOpenSettings);
    }
  }
}
