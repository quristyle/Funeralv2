import 'package:flutter/material.dart';
import 'package:media_kit_video/media_kit_video.dart';
import '../models/device_models.dart';
import '../services/player/media_player_service.dart';

class PlayerShell extends StatelessWidget {
  final DeviceDto device;
  final MediaPlayerService playerService;
  final VoidCallback onOpenSettings;
  final Widget child; // 각 장비별 특화된 View (PortraitView, MultimediaView 등)
  final String? debugFileName; // 추가: 디버그용 파일명
  final bool showSettingsIcon; // 추가

  const PlayerShell({
    super.key,
    required this.device,
    required this.playerService,
    required this.onOpenSettings,
    required this.child,
    this.debugFileName,
    this.showSettingsIcon = true,
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
                if (showSettingsIcon)
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

                // [개발 전용] 화면 타입 디버그 라벨 (우측 하단)
                Positioned(
                  bottom: 10,
                  right: 10,
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    color: Colors.black54,
                    child: Text(
                      'DEBUG: ${device.deviceType}${debugFileName != null ? " ($debugFileName)" : ""}',
                      style: const TextStyle(color: Colors.yellow, fontSize: 10, fontWeight: FontWeight.bold),
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

  // 비디오 회전 각도 계산 (가로, 좌세로, 우세로, 반전 대응)
  int _getVideoTurns(DeviceDto dev) {
    if (dev.displayOrientation == 'PORTRAIT') {
      switch (dev.videoOrientation) {
        case 'HORIZONTAL':
          return 3;
        case 'VERTICAL':
        case 'VERTICAL_LEFT':
          return 0;
        case 'VERTICAL_RIGHT':
          return 2;
        case 'INVERTED':
          return 1;
        default:
          return 0;
      }
    } else {
      switch (dev.videoOrientation) {
        case 'HORIZONTAL':
          return 0;
        case 'VERTICAL':
        case 'VERTICAL_LEFT':
          return 1;
        case 'VERTICAL_RIGHT':
          return 3;
        case 'INVERTED':
          return 2;
        default:
          return 0;
      }
    }
  }
}
