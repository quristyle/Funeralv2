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

class DeviceDispatcher extends StatefulWidget {
  final String serverBaseUrl;
  final String deviceCode;
  final String ipAddress;
  final String macAddress;
  final String publicIpAddress;
  final VoidCallback onOpenSettings;

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
  final SignalRService _signalRService = SignalRService();
  DeviceDto? device;
  bool isLoading = true;
  String? error;
  bool _isRefreshing = false;

  Timer? _retryTimer;
  int _retryCountdown = 20;

  @override
  void initState() {
    super.initState();
    print('[Dispatcher] initState() - 새로운 장비 코드 데이터 로드 시작: ${widget.deviceCode}');
    _loadDevice();
  }

  @override
  void dispose() {
    print('[Dispatcher] dispose() - 기존 장치 연결 해제: ${widget.deviceCode}');
    _retryTimer?.cancel();
    // 현재 장비 코드로 명시적 연결 해제 신호 전송
    _signalRService.disconnect(widget.deviceCode);
    super.dispose();
  }

  Future<void> _loadDevice() async {
    if (_isRefreshing) {
      print('[Dispatcher] _loadDevice() 스킵: 이미 로딩 중');
      return;
    }
    _isRefreshing = true;

    print('[Dispatcher] _loadDevice() 시작: ${widget.deviceCode}');
    _retryTimer?.cancel(); 
    setState(() {
      isLoading = device == null; 
      error = null;
      _retryCountdown = 20;
    });

    try {
      print('[Dispatcher] API 호출 시도: ${widget.serverBaseUrl}');
      final fetched = await ApiService().fetchDevice(widget.serverBaseUrl, widget.deviceCode);
      
      Future.delayed(const Duration(seconds: 2), () {
        _isRefreshing = false;
      });

      if (!mounted) return;

      if (fetched != null) {
        print('[Dispatcher] 서버로부터 데이터 수신 성공: type=${fetched.deviceType}');
        _handleDeviceLoaded(fetched);
      } else {
        print('[Dispatcher] 서버 응답 없음. 캐시 조회를 시도합니다.');
        _loadFromCache();
      }
    } catch (e) {
      print('[Dispatcher] API 예외 발생: $e');
      _isRefreshing = false;
      if (mounted) _loadFromCache();
    }
  }

  Future<void> _loadFromCache() async {
    print('[Dispatcher] 로컬 DB(캐시) 조회 시작');
    final cached = await ApiService().getCachedDevice(widget.deviceCode);
    if (!mounted) return;

    if (cached != null) {
      print('[Dispatcher] 캐시 데이터 로드 성공: type=${cached.deviceType}');
      _handleDeviceLoaded(cached);
    } else {
      print('[Dispatcher] 캐시 데이터도 없습니다. 에러 화면을 표시합니다.');
      setState(() {
        error = "서버에 연결할 수 없으며 저장된 로컬 데이터도 없습니다.";
        isLoading = false;
      });
      _startRetryTimer();
    }
  }

  void _startRetryTimer() {
    print('[Dispatcher] 20초 자동 재시도 타이머 가동');
    _retryTimer?.cancel();
    _retryCountdown = 20;
    _retryTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) {
        timer.cancel();
        return;
      }
      setState(() {
        if (_retryCountdown > 1) {
          _retryCountdown--;
        } else {
          print('[Dispatcher] 타이머 만료 - 재시도 실행');
          timer.cancel();
          _loadDevice();
        }
      });
    });
  }

  void _handleDeviceLoaded(DeviceDto loadedDevice) {
    print('[Dispatcher] _handleDeviceLoaded: 화면 갱신 및 SignalR 대기 시작');
    _saveOrientationCache(loadedDevice.displayOrientation);
    setState(() {
      device = loadedDevice;
      isLoading = false;
    });

    print('[Dispatcher] SignalR 연결 프로세스 백그라운드 호출');
    _signalRService.connect(
      serverUrl: widget.serverBaseUrl,
      deviceCode: widget.deviceCode,
      ipAddress: widget.ipAddress,
      macAddress: widget.macAddress,
      publicIpAddress: widget.publicIpAddress,
      onDeviceChanged: () {
        if (mounted) {
          print('[Dispatcher] << SignalR 알림 수신: 최신 정보로 갱신합니다.');
          _loadDevice();
        }
      },
    );
  }

  Future<void> _saveOrientationCache(String orientation) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString('displayOrientation', orientation);
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    print('[Dispatcher] build(): isLoading=$isLoading, hasDevice=${device != null}, error=$error');
    
    if (isLoading) {
      return const Scaffold(
        body: Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              CircularProgressIndicator(color: Color(0xFFC0A060)),
              SizedBox(height: 20),
              Text("장비 정보를 구성하고 있습니다...", style: TextStyle(color: Colors.white54)),
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
                child: const Text("설정 확인")
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

    print('[Dispatcher] 최종 화면 렌더링 타입: ${device!.deviceType}');

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
