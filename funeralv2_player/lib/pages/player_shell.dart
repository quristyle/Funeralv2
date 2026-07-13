import 'package:flutter/material.dart';
import 'package:media_kit_video/media_kit_video.dart';
import '../models/device_models.dart';
import '../services/player/media_player_service.dart';

class PlayerShell extends StatelessWidget {
  final DeviceDto device;
  final MediaPlayerService playerService;
  final VoidCallback onOpenSettings;
  final Widget child; 
  final String? debugFileName;
  final bool showSettingsIcon;

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
    // 1. 모니터 물리 회전 여부
    final bool isMonitorVertical = device.displayOrientation == 'PORTRAIT';

    return Scaffold(
      backgroundColor: Colors.black,
      body: SizedBox.expand(
        child: Container(
          color: Colors.black,
          child: RotatedBox(
            quarterTurns: isMonitorVertical ? 1 : 0,
            child: Padding(
              // 2. [복구] 화면 전체 여백 (%) - 회전된 좌표계 기준
              padding: EdgeInsets.only(
                top: MediaQuery.of(context).size.height * (device.displayPaddingTop / 100),
                bottom: MediaQuery.of(context).size.height * (device.displayPaddingBottom / 100),
                left: MediaQuery.of(context).size.width * (device.displayPaddingLeft / 100),
                right: MediaQuery.of(context).size.width * (device.displayPaddingRight / 100),
              ),
              child: Stack(
                fit: StackFit.expand, // [핵심] 자식들이 부모 영역을 가득 채우도록 강제
                children: [
                  // [레이어 1] 배경 동영상
                  if (device.isVideoEnabled)
                    RotatedBox(
                      quarterTurns: _getVideoTurns(device),
                      child: Video(
                        controller: playerService.videoController,
                        fit: BoxFit.fill,
                        controls: NoVideoControls,
                      ),
                    ),

                  // [레이어 2] 실제 콘텐츠 (영정사진, 텍스트 등)
                  child,

                  // [레이어 3] 설정 버튼 및 디버그 정보
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

                  Positioned(
                    bottom: 10,
                    right: 10,
                    child: GestureDetector(
                      onTap: onOpenSettings, // 디버그 영역 단순 탭 시에도 설정 열기
                      child: Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        color: Colors.black54,
                        child: Text(
                          'DEBUG: ${device.deviceType} | v:${device.isVideoEnabled} | m:${device.isMusicEnabled}${debugFileName != null ? " ($debugFileName)" : ""}',
                          style: const TextStyle(color: Colors.yellow, fontSize: 10, fontWeight: FontWeight.bold),
                        ),
                      ),
                    ),
                  ),

                  // [레이어 4] 전체 화면 더블 탭 제스처 (어디서나 더블 탭 시 설정 오픈)
                  GestureDetector(
                    behavior: HitTestBehavior.translucent,
                    onDoubleTap: onOpenSettings,
                    child: const SizedBox.expand(),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

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
