import 'dart:io';
import 'package:flutter/material.dart';
import 'package:video_player/video_player.dart';
import 'portrait_controller.dart';

class PortraitPage extends StatefulWidget {
  final String apiServerUrl;
  final String fileServerUrl;
  final String deviceCode;
  final Function() onOpenSettings;

  const PortraitPage({
    super.key,
    required this.apiServerUrl,
    required this.fileServerUrl,
    required this.deviceCode,
    required this.onOpenSettings,
  });

  @override
  State<PortraitPage> createState() => _PortraitPageState();
}

class _PortraitPageState extends State<PortraitPage> {
  final PortraitController _controller = PortraitController();

  @override
  void initState() {
    super.initState();
    _loadData();
  }

  void _loadData() {
    _controller.init(
      widget.apiServerUrl,
      widget.fileServerUrl,
      widget.deviceCode,
      () {
        // 비디오 초기화 완료 시 화면 강제 리빌드
        setState(() {});
      },
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      body: AnimatedBuilder(
        animation: _controller,
        builder: (context, child) {
          if (_controller.isLoading && _controller.device == null) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const CircularProgressIndicator(color: Colors.white),
                  const SizedBox(height: 20),
                  Text(
                    _controller.statusMessage,
                    style: const TextStyle(color: Colors.white, fontSize: 16),
                  ),
                ],
              ),
            );
          }

          // 비디오 컨트롤러 레퍼런스
          final vController = _controller.playerService.videoController;

          return Stack(
            fit: StackFit.expand,
            children: [
              // 1. 배경 동영상 레이어
              if (vController != null && vController.value.isInitialized)
                FittedBox(
                  fit: BoxFit.cover,
                  child: SizedBox(
                    width: vController.value.size.width,
                    height: vController.value.size.height,
                    child: VideoPlayer(vController),
                  ),
                )
              else
                // 동영상이 없는 경우 정적인 그라데이션 검정 배경 제공
                Container(
                  decoration: const BoxDecoration(
                    gradient: LinearGradient(
                      colors: [Color(0xFF141414), Colors.black],
                      begin: Alignment.topCenter,
                      end: Alignment.bottomCenter,
                    ),
                  ),
                ),

              // 2. 화면 전체 연출 오버레이 (근조 마크/장식 및 텍스트 프레임)
              _buildContentOverlay(),

              // 3. 우측 상단 설정 진입용 투명 톱니바퀴 버튼 (보안 및 터치용)
              Positioned(
                top: 20,
                right: 20,
                child: Opacity(
                  opacity: 0.1, // 미세하게 보이지만 터치 가능
                  child: IconButton(
                    icon: const Icon(Icons.settings, color: Colors.white, size: 28),
                    onPressed: widget.onOpenSettings,
                  ),
                ),
              ),
            ],
          );
        },
      ),
    );
  }

  // 영정 및 고인 정보, 근조 마크 합성 레이아웃
  Widget _buildContentOverlay() {
    final dev = _controller.device;
    final dec = _controller.deceased;

    if (dev == null) return const SizedBox();

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 40, vertical: 60),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          // A. 상단 구역 (근조 타이틀/마크)
          Column(
            children: [
              const Text(
                '謹 弔', // 근조(한자)
                style: TextStyle(
                  color: Colors.white70,
                  fontSize: 54,
                  fontWeight: FontWeight.w900,
                  letterSpacing: 20,
                ),
              ),
              const SizedBox(height: 10),
              Container(
                width: 100,
                height: 2,
                color: Colors.white24,
              ),
            ],
          ),

          // B. 중앙 구역 (영정사진 및 근조 리본 장식)
          if (dev.isMemorialPhotoEnabled && _controller.deceasedPhotoPath != null)
            Stack(
              alignment: Alignment.center,
              children: [
                // 영정 이미지 테두리 박스 (액자 스타일)
                Container(
                  width: 320,
                  height: 400,
                  decoration: BoxDecoration(
                    color: Colors.black45,
                    border: Border.all(color: const Color(0xFFC0A060), width: 8), // 금장 테두리
                    boxShadow: const [
                      BoxShadow(
                        color: Colors.black85,
                        blurRadius: 30,
                        spreadRadius: 5,
                      ),
                    ],
                  ),
                  child: Image.file(
                    File(_controller.deceasedPhotoPath!),
                    fit: BoxFit.cover,
                    errorBuilder: (context, error, stackTrace) => const Icon(
                      Icons.person,
                      color: Colors.white24,
                      size: 120,
                    ),
                  ),
                ),

                // 근조 장식 마크 (기본 에셋 이미지가 없을 경우 리본 데코 오버레이 구현)
                // 영정사진 왼쪽 위/오른쪽 위에 검은 리본 장식을 CustomPaint로 직접 렌더링하여
                // 에셋 로드 실패 걱정 없이 항상 안정적으로 오버레이되게 처리함!
                Positioned(
                  top: 0,
                  left: 0,
                  child: CustomPaint(
                    size: const Size(60, 60),
                    painter: RibbonPainter(isLeft: true),
                  ),
                ),
                Positioned(
                  top: 0,
                  right: 0,
                  child: CustomPaint(
                    size: const Size(60, 60),
                    painter: RibbonPainter(isLeft: false),
                  ),
                ),
              ],
            )
          else
            // 영정사진 비활성화 시 연출용 로고/문양
            const Icon(
              Icons.church,
              color: Colors.white10,
              size: 150,
            ),

          // C. 하단 구역 (고인 정보, 상주 정보)
          if (dec != null)
            Column(
              children: [
                // 고인 성함
                if (dev.isDeceasedNameVisible)
                  Text(
                    '故 ${dec.name} 魂靈',
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 38,
                      fontWeight: FontWeight.bold,
                      letterSpacing: 4,
                    ),
                  ),
                const SizedBox(height: 10),
                // 종교 및 행년
                Text(
                  '${dec.gender} (${dec.age}세)${dec.religion != null ? " / ${dec.religion}" : ""}',
                  style: const TextStyle(
                    color: Colors.white60,
                    fontSize: 18,
                  ),
                ),
                const SizedBox(height: 25),
                // 상주 연락처 및 상주명 오버레이
                if (dev.isFamilyContactVisible && dec.chiefMourner != null)
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
                    decoration: BoxDecoration(
                      color: Colors.white.withOpacity(0.04),
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(color: Colors.white12, width: 1),
                    ),
                    child: Text(
                      '상주 : ${dec.chiefMourner}',
                      style: const TextStyle(
                        color: Colors.white80,
                        fontSize: 20,
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                  ),
              ],
            )
          else
            const Text(
              '빈소 정보 준비 중',
              style: TextStyle(color: Colors.white38, fontSize: 20),
            ),
        ],
      ),
    );
  }
}

/// 영정사진 검은 리본 장식을 그리는 커스텀 페인터 (오프라인 완벽 독립 구동 보장)
class RibbonPainter extends CustomPainter {
  final bool isLeft;
  RibbonPainter({required this.isLeft});

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = Colors.black
      ..style = PaintingStyle.fill;

    final path = Path();
    if (isLeft) {
      // 왼쪽 위 리본 대각선 띠
      path.moveTo(0, 0);
      path.lineTo(size.width, 0);
      path.lineTo(0, size.height);
      path.close();
    } else {
      // 오른쪽 위 리본 대각선 띠
      path.moveTo(size.width, 0);
      path.lineTo(0, 0);
      path.lineTo(size.width, size.height);
      path.close();
    }

    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
