import 'package:flutter/material.dart';
import 'package:media_kit_video/media_kit_video.dart';
import '../models/device_models.dart';
import '../services/player/media_player_service.dart';

/// [플레이어 공통 셸 위젯]
/// 모든 사이니지 화면의 공통 구조를 정의합니다.
/// 디스플레이 회전(`displayOrientation`), 화면 여백 수치(`displayPadding`), 
/// 백그라운드 영상 자동 매핑 및 화면 더블 탭 시 설정 창 진입과 같은 기본 제스처를 일괄 처리합니다.
class PlayerShell extends StatelessWidget {
  final DeviceDto device; // 장비 설정 정보 DTO
  final MediaPlayerService playerService; // 미디어 재생 서비스
  final VoidCallback onOpenSettings; // 설정 진입 콜백
  final Widget child; // 내부에 렌더링할 장비별 전용 뷰 (영정, 안내판 등)
  final String? debugFileName; // 디버깅용 파일 정보 텍스트
  final bool showSettingsIcon; // 우측 상단 톱니바퀴 설정 아이콘 노출 여부

  const PlayerShell({
    super.key,
    required this.device,
    required this.playerService,
    required this.onOpenSettings,
    required this.child,
    this.debugFileName,
    this.showSettingsIcon = true,
  });

  /// [위젯 빌드]
  /// 디스플레이 회전 상태에 따라 `RotatedBox`로 전체 레이아웃을 감싸고,
  /// 4개의 레이어(1. 비디오 배경 -> 2. 콘텐츠 자식 -> 3. 디버그 및 기어 -> 4. 더블 탭 제스처)를 순차적으로 얹습니다.
  @override
  Widget build(BuildContext context) {
    // 1. 모니터 물리적 세로 회전 여부 판별
    final bool isMonitorVertical = device.displayOrientation == 'PORTRAIT';

    return Scaffold(
      backgroundColor: Colors.black,
      body: SizedBox.expand(
        child: Container(
          color: Colors.black,
          child: RotatedBox(
            quarterTurns: isMonitorVertical ? 1 : 0, // 세로형 모니터일 경우 90도 회전
            child: Padding(
              // 2. 화면 전체 여백 설정 (%) 적용 - 회전된 좌표계 기준
              padding: EdgeInsets.only(
                top: MediaQuery.of(context).size.height * (device.displayPaddingTop / 100),
                bottom: MediaQuery.of(context).size.height * (device.displayPaddingBottom / 100),
                left: MediaQuery.of(context).size.width * (device.displayPaddingLeft / 100),
                right: MediaQuery.of(context).size.width * (device.displayPaddingRight / 100),
              ),
              child: Stack(
                fit: StackFit.expand, // 자식 위젯들이 부모 가로세로를 가득 채우도록 스택 레이아웃 확장
                children: [
                  // [레이어 1] 백그라운드 루프 비디오
                  if (device.isVideoEnabled)
                    RotatedBox(
                      quarterTurns: _getVideoTurns(device), // 영상 원본 방향에 맞추어 보정 회전
                      child: Video(
                        controller: playerService.videoController,
                        fit: BoxFit.fill, // 빈틈없이 늘려 채우기
                        controls: NoVideoControls, // MPV 비디오 기본 컨트롤 컨트롤 바 숨김
                      ),
                    ),

                  // [레이어 2] 실제 콘텐츠 위젯 (영정사진, 호실 정보 테이블 등)
                  child,

                  // [레이어 3] 투명 설정 버튼 및 우하단 디버그 로그 오버레이
                  if (showSettingsIcon)
                    Positioned(
                      top: 20,
                      right: 20,
                      child: Opacity(
                        opacity: 0.1, // 사용자에겐 안 보이게 극도로 투명하게 만듦 (관리자용 탭 공간)
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
                      onTap: onOpenSettings, // 디버그 텍스트 탭 시에도 즉시 설정 진입 허용
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

                  // [레이어 4] 화면 전체 더블 탭 감지용 투명 제스처 레이어
                  // 모니터 전체 영역 중 아무 곳이나 더블 클릭(더블 탭)하면 설정 화면으로 도망갈 수 있게 설계되었습니다.
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

  /// [비디오 회전 각도 판별기]
  /// 모니터 물리 방향([displayOrientation])과 영상 원본 렌더링 방향([videoOrientation])을 매핑하여
  /// `RotatedBox`가 회전해야 할 quarter(90도 단위) 값을 정밀 계산해 반환합니다.
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
