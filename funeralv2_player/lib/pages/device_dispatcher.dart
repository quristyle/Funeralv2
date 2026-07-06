import 'package:flutter/material.dart';
import '../models/device_models.dart';
import '../services/api/api_service.dart';
import '../services/signalr/signalr_service.dart'; // 추가
import 'portrait/portrait_view.dart';
import 'guide/room_guide_view.dart';
import 'guide/entrance_guide_view.dart';
import 'kiosk/kiosk_view.dart';
import 'multimedia/multimedia_view.dart'; // 추가 // 추가

/// 장비 타입에 따라 어떤 화면(View)을 보여줄지 결정하는 허브 컨트롤러
class DeviceDispatcher extends StatefulWidget {
  final String serverBaseUrl;
  final String deviceCode;
  final VoidCallback onOpenSettings;

  const DeviceDispatcher({
    super.key,
    required this.serverBaseUrl,
    required this.deviceCode,
    required this.onOpenSettings,
  });

  @override
  State<DeviceDispatcher> createState() => _DeviceDispatcherState();
}

class _DeviceDispatcherState extends State<DeviceDispatcher> {
  final SignalRService _signalRService = SignalRService(); // 추가
  DeviceDto? device;
  bool isLoading = true;
  String? error;

  @override
  void initState() {
    super.initState();
    _loadDevice();
  }

  // 장비 정보 로드 및 SignalR 연결
  Future<void> _loadDevice() async {
    try {
      final fetched = await ApiService().fetchDevice(widget.serverBaseUrl, widget.deviceCode);
      if (fetched != null) {
        setState(() {
          device = fetched;
          isLoading = false;
        });

        // SignalR 연결: 타입 변경 알림 시 다시 로드하여 화면 갱신
        await _signalRService.connect(
          serverUrl: widget.serverBaseUrl,
          deviceCode: widget.deviceCode,
          onDeviceChanged: () {
            print('[Dispatcher] 장비 정보 변경 알림 수신 - 다시 로드합니다.');
            _loadDevice();
          },
        );
      } else {
        setState(() {
          error = "장비 정보를 불러올 수 없습니다.";
          isLoading = false;
        });
      }
    } catch (e) {
      setState(() {
        error = e.toString();
        isLoading = false;
      });
    }
  }

  @override
  void dispose() {
    _signalRService.disconnect(widget.deviceCode); // 해제
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (isLoading) {
      return const Scaffold(body: Center(child: CircularProgressIndicator(color: Color(0xFFC0A060))));
    }

    if (error != null || device == null) {
      return Scaffold(
        body: Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.error_outline, color: Colors.red, size: 60),
              const SizedBox(height: 20),
              Text(error ?? "알 수 없는 오류", style: const TextStyle(color: Colors.white, fontSize: 18)),
              const SizedBox(height: 40),
              ElevatedButton(onPressed: widget.onOpenSettings, child: const Text("설정으로 돌아가기")),
            ],
          ),
        ),
      );
    }

    // [동적 분기] deviceType이 바뀌면 build가 다시 호출되면서 다른 View가 반환됨
    switch (device!.deviceType) {
      case 'FUNERAL_PORTRAIT':
        return PortraitView(
          serverBaseUrl: widget.serverBaseUrl,
          deviceCode: widget.deviceCode,
          onOpenSettings: widget.onOpenSettings,
        );
      
      case 'ROOM_GUIDE':
        return RoomGuideView(
          serverBaseUrl: widget.serverBaseUrl,
          deviceCode: widget.deviceCode,
          onOpenSettings: widget.onOpenSettings,
        );

      case 'ENTRANCE_GUIDE':
        return EntranceGuideView(
          serverBaseUrl: widget.serverBaseUrl,
          deviceCode: widget.deviceCode,
          onOpenSettings: widget.onOpenSettings,
        );

      case 'KIOSK':
        return KioskView(
          serverBaseUrl: widget.serverBaseUrl,
          deviceCode: widget.deviceCode,
          onOpenSettings: widget.onOpenSettings,
        );

      case 'MULTIMEDIA':
        return MultimediaView(
          serverBaseUrl: widget.serverBaseUrl,
          deviceCode: widget.deviceCode,
          onOpenSettings: widget.onOpenSettings,
        );

      default:
        return PortraitView(
          serverBaseUrl: widget.serverBaseUrl,
          deviceCode: widget.deviceCode,
          onOpenSettings: widget.onOpenSettings,
        );
    }
  }
}
