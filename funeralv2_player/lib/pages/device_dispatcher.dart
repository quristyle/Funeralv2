import 'package:flutter/material.dart';
import '../models/device_models.dart';
import '../services/api/api_service.dart';
import 'portrait/portrait_view.dart';

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
  DeviceDto? device;
  bool isLoading = true;
  String? error;

  @override
  void initState() {
    super.initState();
    _loadDeviceType();
  }

  Future<void> _loadDeviceType() async {
    try {
      final fetched = await ApiService().fetchDevice(widget.serverBaseUrl, widget.deviceCode);
      if (fetched != null) {
        setState(() {
          device = fetched;
          isLoading = false;
        });
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
              Text(error ?? "알 수 없는 오류"),
              const SizedBox(height: 20),
              ElevatedButton(onPressed: widget.onOpenSettings, child: const Text("설정으로 돌아가기")),
            ],
          ),
        ),
      );
    }

    // 장비 타입별 분기 처리
    switch (device!.code.contains('JSI-06') ? 'FUNERAL_PORTRAIT' : 'UNKNOWN') { // 예시: 코드로 분기하거나 deviceType 필드 사용
      case 'FUNERAL_PORTRAIT':
        return PortraitView(
          serverBaseUrl: widget.serverBaseUrl,
          deviceCode: widget.deviceCode,
          onOpenSettings: widget.onOpenSettings,
        );
      
      // 향후 여기에 추가
      // case 'MULTIMEDIA': return MultimediaView(...);
      // case 'ROOM_GUIDE': return RoomGuideView(...);

      default:
        return PortraitView( // 기본값
          serverBaseUrl: widget.serverBaseUrl,
          deviceCode: widget.deviceCode,
          onOpenSettings: widget.onOpenSettings,
        );
    }
  }
}
