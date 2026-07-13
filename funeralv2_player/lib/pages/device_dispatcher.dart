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
    print('[Dispatcher] initState() - 장비 초기화 시작');
    _loadDevice();
  }

  @override
  void dispose() {
    print('[Dispatcher] dispose() - 정리 중');
    _retryTimer?.cancel();
    super.dispose();
  }

  Future<void> _loadDevice() async {
    if (_isRefreshing) {
      print('[Dispatcher] _loadDevice 스킵: 이미 새로고침 중');
      return;
    }
    _isRefreshing = true;
    _retryTimer?.cancel();

    print('[Dispatcher] 데이터 로드 루틴 시작 (Code: ${widget.deviceCode})');
    setState(() {
      isLoading = (device == null);
      error = null;
    });

    try {
      print('[Dispatcher] API 서버 요청 시도...');
      final fetched = await ApiService().fetchDevice(widget.serverBaseUrl, widget.deviceCode);
      
      Future.delayed(const Duration(seconds: 2), () => _isRefreshing = false);

      if (!mounted) return;

      if (fetched != null) {
        print('[Dispatcher] 서버 데이터 수신 성공 (Type: ${fetched.deviceType})');
        _handleDeviceLoaded(fetched);
      } else {
        print('[Dispatcher] 서버 응답 없음 ➡ 로컬 캐시 조회로 전환');
        _loadFromCache();
      }
    } catch (e) {
      print('[Dispatcher] 로드 중 오류 발생: $e');
      _isRefreshing = false;
      if (mounted) _loadFromCache();
    }
  }

  Future<void> _loadFromCache() async {
    print('[Dispatcher] 로컬 DB에서 마지막 설정 불러오는 중...');
    final cached = await ApiService().getCachedDevice(widget.deviceCode);
    if (!mounted) return;

    if (cached != null) {
      print('[Dispatcher] 캐시 데이터 복구 성공 (Type: ${cached.deviceType})');
      _handleDeviceLoaded(cached);
    } else {
      print('[Dispatcher] 캐시된 정보도 없습니다. 복구 불가능.');
      setState(() {
        error = "서버 연결 실패 및 로컬 데이터 없음";
        isLoading = false;
      });
      _startRetryTimer();
    }
  }

  void _startRetryTimer() {
    _retryCountdown = 20;
    _retryTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) { timer.cancel(); return; }
      setState(() {
        if (_retryCountdown > 1) {
          _retryCountdown--;
        } else {
          print('[Dispatcher] 타이머 만료 ➡ 자동 재접속 시도');
          timer.cancel();
          _loadDevice();
        }
      });
    });
  }

  void _handleDeviceLoaded(DeviceDto loadedDevice) {
    print('[Dispatcher] 최종 데이터 할당 및 화면 전환 준비');
    _saveOrientationCache(loadedDevice.displayOrientation);
    setState(() {
      device = loadedDevice;
      isLoading = false;
    });

    // SignalR 연결 (백그라운드)
    _signalRService.connect(
      serverUrl: widget.serverBaseUrl,
      deviceCode: widget.deviceCode,
      ipAddress: widget.ipAddress,
      macAddress: widget.macAddress,
      publicIpAddress: widget.publicIpAddress,
      onDeviceChanged: () {
        if (mounted) {
          print('[Dispatcher] << SignalR 데이터 변경 감지');
          _loadDevice();
        }
      },
    );
  }

  Future<void> _saveOrientationCache(String orientation) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('displayOrientation', orientation);
  }

  @override
  Widget build(BuildContext context) {
    print('[Dispatcher] build() 호출 - isLoading=$isLoading, deviceType=${device?.deviceType}');

    if (isLoading) {
      return const Scaffold(body: Center(child: CircularProgressIndicator(color: Color(0xFFC0A060))));
    }

    if (error != null || device == null) {
      return Scaffold(
        body: Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.cloud_off, color: Colors.white24, size: 60),
              const SizedBox(height: 20),
              Text(error ?? "연결 오류", style: const TextStyle(color: Colors.white, fontSize: 18)),
              const SizedBox(height: 40),
              ElevatedButton(onPressed: widget.onOpenSettings, child: const Text("설정 확인")),
              TextButton(onPressed: _loadDevice, child: Text("재시도 ($_retryCountdown초 후 자동 시도)")),
            ],
          ),
        ),
      );
    }

    print('[Dispatcher] 분기 처리: ${device!.deviceType}');
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
