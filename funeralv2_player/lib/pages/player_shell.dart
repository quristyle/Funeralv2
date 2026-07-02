import 'package:flutter/material.dart';
import 'package:media_kit_video/media_kit_video.dart';
import '../models/device_models.dart';
import '../services/player/media_player_service.dart';

class PlayerShell extends StatelessWidget {
  final DeviceDto device;
  final MediaPlayerService playerService;
  final VoidCallback onOpenSettings;
  final Widget child; // 각 장비별 특화된 View (PortraitView, MultimediaView 등)

  const PlayerShell({
    super.key,
    required this.device,
    required this.playerService,
    required this.onOpenSettings,
    required this.child,
  });

  @override
  Widget build(BuildContext context) {
    // [물리적 회전 대응] displayOrientation: PORTRAIT이면 화면을 90도 회전
    final bool isMonitorVertical = device.displayOrientation == 'PORTRAIT';

    return Scaffold(
      backgroundColor: Colors.black,
      body: Container(
        color: Colors.black,
        width: double.infinity,
        height: double.infinity,
        child: RotatedBox(
          quarterTurns: isMonitorVertical ? 1 : 0,
          child: Padding(
            // 모니터 회전 후의 좌표계를 기준으로 패딩 적용
            padding: EdgeInsets.only(
              top: MediaQuery.of(context).size.height * (device.displayPaddingTop / 100),
              bottom: MediaQuery.of(context).size.height * (device.displayPaddingBottom / 100),
              left: MediaQuery.of(context).size.width * (device.displayPaddingLeft / 100),
              right: MediaQuery.of(context).size.width * (device.displayPaddingRight / 100),
            ),
            child: Stack(
              fit: StackFit.expand,
              children: [
                // 레이어 1: 공통 배경 동영상
                SizedBox.expand(
                  child: RotatedBox(
                    quarterTurns: _getVideoTurns(device),
                    child: Video(
                      controller: playerService.videoController,
                      fit: BoxFit.cover,
                      controls: NoVideoControls,
                    ),
                  ),
                ),

                // 레이어 2~: 장비별 특화 콘텐츠 (PortraitView 등)
                child,

                // 설정 버튼 (최상단)
                Positioned(
                  top: 20,
                  right: 20,
                  child: Opacity(
                    opacity: 0.1,
                    child: IconButton(
                      icon: const Icon(Icons.settings, color: Colors.white, size: 28),
                      onPressed: onOpenSettings,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  // 비디오 회전 각도 계산 (기존 로직 유지)
  int _getVideoTurns(DeviceDto dev) {
    if (dev.displayOrientation == 'PORTRAIT') {
      return (dev.videoOrientation == 'HORIZONTAL') ? 3 : 0;
    }
    return (dev.videoOrientation == 'VERTICAL') ? 1 : 0;
  }
}
