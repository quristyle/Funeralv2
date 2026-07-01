import 'dart:io' as io;
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:media_kit_video/media_kit_video.dart';
import 'portrait_controller.dart';
import '../../models/device_models.dart';

class PortraitPage extends StatefulWidget {
  final String serverBaseUrl;
  final String deviceCode;
  final Function() onOpenSettings;

  const PortraitPage({
    super.key,
    required this.serverBaseUrl,
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
      widget.serverBaseUrl,
      widget.deviceCode,
      () {
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
          final dev = _controller.device;

          if (_controller.isLoading && dev == null) {
            return _buildLoadingView();
          }

          if (!_controller.isLoading && dev == null) {
            return _buildErrorView();
          }

          // [물리적 회전 대응] displayOrientation: PORTRAIT이면 화면을 90도 회전
          final bool isMonitorVertical = dev!.displayOrientation == 'PORTRAIT';
          
          return Container(
            color: Colors.black,
            child: Padding(
              padding: EdgeInsets.only(
                top: MediaQuery.of(context).size.height * (dev.displayPaddingTop / 100),
                bottom: MediaQuery.of(context).size.height * (dev.displayPaddingBottom / 100),
                left: MediaQuery.of(context).size.width * (dev.displayPaddingLeft / 100),
                right: MediaQuery.of(context).size.width * (dev.displayPaddingRight / 100),
              ),
              child: RotatedBox(
                quarterTurns: isMonitorVertical ? 1 : 0,
                child: Stack(
                  fit: StackFit.expand,
                  children: [
                    // 레이어 1: 배경 동영상
                    SizedBox.expand(
                      child: RotatedBox(
                        quarterTurns: _getVideoTurns(dev),
                        child: Video(
                          controller: _controller.playerService.videoController,
                          fit: BoxFit.cover,
                        ),
                      ),
                    ),
                    
                    // 레이어 2: 영정사진 (전체 화면 기준 패딩 적용)
                    _buildPortraitPhotoLayer(dev),

                    // 레이어 3 & 4: 장식 및 글자 (기존 오리엔테이션 로직 유지)
                    _buildUILayoutLayer(dev),

                    // 설정 버튼 (최상단)
                    Positioned(
                      top: 20,
                      right: 20,
                      child: Opacity(
                        opacity: 0.1,
                        child: IconButton(
                          icon: const Icon(Icons.settings, color: Colors.white, size: 28),
                          onPressed: widget.onOpenSettings,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }

  // --- 레이어별 구현부 ---

  // [레이어 2] 영정사진 레이어: 화면 전체를 기준으로 비율 패딩 적용
  Widget _buildPortraitPhotoLayer(DeviceDto dev) {
    if (!dev.isMemorialPhotoEnabled || _controller.deceasedPhotoPath == null) {
      return const SizedBox();
    }

    return LayoutBuilder(
      builder: (context, constraints) {
        // Positioned를 Stack의 직계 자식으로 두기 위해 여기서 Stack을 한 번 더 쓰거나, 
        // 상위 Stack의 크기를 이미 알고 있다면 Positioned를 직접 반환해야 합니다.
        // 여기서는 가장 안전하게 Padding과 Align을 사용하여 영정사진 레이어를 구성합니다.
        return Padding(
          padding: EdgeInsets.only(
            top: constraints.maxHeight * (dev.memorialPaddingTop / 100),
            bottom: constraints.maxHeight * (dev.memorialPaddingBottom / 100),
            left: constraints.maxWidth * (dev.memorialPaddingLeft / 100),
            right: constraints.maxWidth * (dev.memorialPaddingRight / 100),
          ),
          child: Center(
            child: _buildDeceasedImage(),
          ),
        );
      },
    );
  }

  // [레이어 3 & 4] UI 레이아웃 레이어: 글자 및 장식 요소 배치
  Widget _buildUILayoutLayer(DeviceDto dev) {
    switch (dev.portraitOrientation) {
      case 'VERTICAL_LEFT':
        return _buildVerticalUILayout(dev, infoAtBottom: true);
      case 'VERTICAL_RIGHT':
        return _buildVerticalUILayout(dev, infoAtBottom: false);
      case 'INVERTED':
        return _buildHorizontalUILayout(dev, infoAtLeft: true);
      case 'HORIZONTAL':
      default:
        return _buildHorizontalUILayout(dev, infoAtLeft: false);
    }
  }

  // 가로형 UI 배치 (사진 영역을 비워두고 글자만 배치)
  Widget _buildHorizontalUILayout(DeviceDto dev, {required bool infoAtLeft}) {
    final dec = _controller.deceased;
    // 사진이 차지하던 공간은 투명한 Spacer로 대체하여 글자 위치를 유지함
    final spacer = const Expanded(flex: 1, child: SizedBox());
    final infoWidget = Expanded(
      flex: 1,
      child: _buildDeceasedInfo(dev, dec, isHorizontal: true),
    );

    return SizedBox.expand(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 60, vertical: 40),
        child: FittedBox(
          fit: BoxFit.contain,
          child: ConstrainedBox(
            constraints: const BoxConstraints(minWidth: 1200, maxWidth: 1920, minHeight: 600, maxHeight: 1080),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              crossAxisAlignment: CrossAxisAlignment.center,
              children: infoAtLeft ? [infoWidget, const SizedBox(width: 60), spacer] : [spacer, const SizedBox(width: 60), infoWidget],
            ),
          ),
        ),
      ),
    );
  }

  // 세로형 UI 배치
  Widget _buildVerticalUILayout(DeviceDto dev, {required bool infoAtBottom}) {
    final dec = _controller.deceased;
    
    final titleWidget = Column(
      children: [
        //const Text('謹 弔', style: TextStyle(color: Colors.white70, fontSize: 54, fontWeight: FontWeight.w900, letterSpacing: 20)),
        const SizedBox(height: 10),
        //Container(width: 100, height: 2, color: Colors.white24),
      ],
    );

    // 사진 영역을 위한 투명 공간
    final spacer = const Expanded(flex: 2, child: SizedBox());
    final infoWidget = Expanded(
      flex: 3,
      child: _buildDeceasedInfo(dev, dec, isHorizontal: false),
    );

    return SizedBox.expand(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 40, vertical: 60),
        child: FittedBox(
          fit: BoxFit.contain,
          child: ConstrainedBox(
            constraints: const BoxConstraints(minWidth: 400, maxWidth: 800, minHeight: 1000, maxHeight: 1600),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: infoAtBottom 
                ? [titleWidget, spacer, infoWidget]
                : [titleWidget, infoWidget, spacer],
            ),
          ),
        ),
      ),
    );
  }

  // 고인 및 상주 정보 위젯 (글자 레이어)
  Widget _buildDeceasedInfo(DeviceDto dev, DeceasedDto? dec, {required bool isHorizontal}) {
    if (dec == null) {
      return Center(child: Text('빈소 정보 준비 중', style: TextStyle(color: Colors.white38, fontSize: isHorizontal ? 32 : 24)));
    }

    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: isHorizontal ? CrossAxisAlignment.start : CrossAxisAlignment.center,
      children: [
        if (!isHorizontal) const SizedBox(height: 0) else ...[
          //const Text('謹 弔', style: TextStyle(color: Colors.white70, fontSize: 64, fontWeight: FontWeight.w900, letterSpacing: 25)),
          const SizedBox(height: 20),
          //Container(width: 150, height: 3, color: Colors.white24),
          //const SizedBox(height: 60),
        ],
        if (dev.isDeceasedNameVisible)
          Text('故 ${dec.name} 魂靈', style: TextStyle(color: Colors.white, fontSize: isHorizontal ? 60 : 48, fontWeight: FontWeight.bold, letterSpacing: isHorizontal ? 5 : 4)),
        const SizedBox(height: 16),
        //Text('${dec.gender} (${dec.age}세)${dec.religion != null && dec.religion != "NONE" ? " / ${dec.religion}" : ""}', style: TextStyle(color: Colors.white60, fontSize: isHorizontal ? 32 : 24)),
        //const SizedBox(height: 32),
        if (dev.isFamilyContactVisible && dec.chiefMourner != null)
          _buildChiefMournerBox(dec.chiefMourner!, isHorizontal ? 32 : 26, isHorizontal ? 16 : 12, isHorizontal ? 24 : 16, isHorizontal ? 48 : 32),
      ],
    );
  }

  // --- 헬퍼 메소드들 ---

  Widget _buildChiefMournerBox(String name, double fontS, double radius, double padV, double padH) {
    return Container(
      padding: EdgeInsets.symmetric(horizontal: padH, vertical: padV),
      decoration: BoxDecoration(
        color: Colors.white.withOpacity(0.04),
        borderRadius: BorderRadius.circular(radius),
        border: Border.all(color: Colors.white12, width: 1),
      ),
      child: Text('상주 : $name', style: TextStyle(color: Colors.white70, fontSize: fontS, fontWeight: FontWeight.w500)),
    );
  }

  Widget _buildDeceasedImage() {
    final path = _controller.deceasedPhotoPath;
    if (path == null) return const Icon(Icons.person, color: Colors.white24, size: 120);
    if (kIsWeb) return Image.network(path, fit: BoxFit.contain, errorBuilder: (c, e, s) => const Icon(Icons.person, color: Colors.white24, size: 120));
    return Image.file(io.File(path), fit: BoxFit.contain, errorBuilder: (c, e, s) => const Icon(Icons.person, color: Colors.white24, size: 120));
  }

  int _getVideoTurns(DeviceDto dev) {
    if (dev.displayOrientation == 'PORTRAIT') return (dev.videoOrientation == 'HORIZONTAL') ? 3 : 0;
    return (dev.videoOrientation == 'VERTICAL') ? 1 : 0;
  }

  Widget _buildLoadingView() {
    return Center(child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
      const CircularProgressIndicator(color: Color(0xFFC0A060)),
      const SizedBox(height: 24),
      Text(_controller.statusMessage, style: const TextStyle(color: Colors.white, fontSize: 18)),
      const SizedBox(height: 48),
      TextButton.icon(onPressed: widget.onOpenSettings, icon: const Icon(Icons.close, color: Colors.white54), label: const Text('로딩 취소', style: TextStyle(color: Colors.white54))),
    ]));
  }

  Widget _buildErrorView() {
    return Center(child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
      const Icon(Icons.error_outline, color: Colors.redAccent, size: 60),
      const SizedBox(height: 20),
      Text(_controller.statusMessage, textAlign: TextAlign.center, style: const TextStyle(color: Colors.white, fontSize: 18)),
      const SizedBox(height: 40),
      ElevatedButton.icon(style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFFC0A060), foregroundColor: Colors.black), onPressed: widget.onOpenSettings, icon: const Icon(Icons.settings), label: const Text('설정 다시 하기')),
    ]));
  }
}

class RibbonPainter extends CustomPainter {
  final bool isLeft;
  RibbonPainter({required this.isLeft});
  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()..color = Colors.black..style = PaintingStyle.fill;
    final path = Path();
    if (isLeft) {
      path.moveTo(0, 0); path.lineTo(size.width, 0); path.lineTo(0, size.height); path.close();
    } else {
      path.moveTo(size.width, 0); path.lineTo(0, 0); path.lineTo(size.width, size.height); path.close();
    }
    canvas.drawPath(path, paint);
  }
  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
