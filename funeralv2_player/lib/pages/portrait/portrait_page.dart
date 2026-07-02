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

          if (_controller.isLoading && dev == null) return _buildLoadingView();
          if (!_controller.isLoading && dev == null) return _buildErrorView();

          // 모니터 물리 회전
          final bool isMonitorVertical = dev!.displayOrientation == 'PORTRAIT';
          
          return Stack(
            fit: StackFit.expand,
            children: [
              const ColoredBox(color: Colors.black),

              RotatedBox(
                quarterTurns: isMonitorVertical ? 1 : 0,
                child: Padding(
                  // 화면 전체 여백 (%)
                  padding: EdgeInsets.only(
                    top: MediaQuery.of(context).size.height * (dev.displayPaddingTop / 100),
                    bottom: MediaQuery.of(context).size.height * (dev.displayPaddingBottom / 100),
                    left: MediaQuery.of(context).size.width * (dev.displayPaddingLeft / 100),
                    right: MediaQuery.of(context).size.width * (dev.displayPaddingRight / 100),
                  ),
                  child: Stack(
                    fit: StackFit.expand,
                    children: [
                      // 레이어 1: 배경 동영상 (비디오 방향 보정 포함)
                      RotatedBox(
                        quarterTurns: _getVideoTurns(dev),
                        child: Video(
                          controller: _controller.playerService.videoController,
                          fit: BoxFit.cover,
                          controls: NoVideoControls,
                        ),
                      ),
                      
                      // 레이어 2: 영정사진 (portraitOrientation에 따라 이미지만 회전)
                      if (dev.isMemorialPhotoEnabled && _controller.deceasedPhotoPath != null)
                        _buildPortraitPhotoLayer(dev),

                      // 레이어 3: 글자 정보 (표준 레이아웃 고정)
                      _buildUILayoutLayer(dev),
                    ],
                  ),
                ),
              ),

              // 설정 버튼
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
          );
        },
      ),
    );
  }

  // [레이어 2] 영정사진 레이어
  Widget _buildPortraitPhotoLayer(DeviceDto dev) {
    if (!dev.isMemorialPhotoEnabled || _controller.deceasedPhotoPath == null) {
      return const SizedBox();
    }

    // 수평 정렬값 결정
    double x = 0; // 기본 CENTER
    if (dev.photoHorizontalAlignment == 'LEFT') x = -1;
    else if (dev.photoHorizontalAlignment == 'RIGHT') x = 1;

    // 수직 정렬값 결정
    double y = 0; // 기본 CENTER
    if (dev.photoVerticalAlignment == 'TOP') y = -1;
    else if (dev.photoVerticalAlignment == 'BOTTOM') y = 1;
    
    final finalAlignment = Alignment(x, y);

    // 회전 각도 결정 (portraitOrientation)
    int turns = 0;
    switch (dev.portraitOrientation) {
      case 'VERTICAL_LEFT': turns = 3; break;
      case 'VERTICAL_RIGHT': turns = 1; break;
      case 'INVERTED': turns = 2; break;
      default: turns = 0; break;
    }

    return LayoutBuilder(
      builder: (context, constraints) {
        return Padding(
          padding: EdgeInsets.only(
            top: constraints.maxHeight * (dev.memorialPaddingTop / 100),
            bottom: constraints.maxHeight * (dev.memorialPaddingBottom / 100),
            left: constraints.maxWidth * (dev.memorialPaddingLeft / 100),
            right: constraints.maxWidth * (dev.memorialPaddingRight / 100),
          ),
          child: Align(
            alignment: finalAlignment,
            child: RotatedBox(
              quarterTurns: turns,
              child: _buildDeceasedImage(),
            ),
          ),
        );
      },
    );
  }

  // [UI 레이아웃] 글자 정보의 위치 (모니터 방향에 맞춘 표준 배치)
  Widget _buildUILayoutLayer(DeviceDto dev) {
    final bool isMonitorVertical = dev.displayOrientation == 'PORTRAIT';
    
    if (isMonitorVertical) {
      // 세로 모니터 표준: 하단 정보 배치
      return _buildVerticalUILayout(dev, infoAtBottom: true);
    } else {
      // 가로 모니터 표준: 우측 정보 배치
      return _buildHorizontalUILayout(dev, infoAtLeft: false);
    }
  }

  Widget _buildHorizontalUILayout(DeviceDto dev, {required bool infoAtLeft}) {
    final dec = _controller.deceased;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 60, vertical: 40),
      child: Row(
        children: infoAtLeft 
          ? [Expanded(child: _buildDeceasedInfo(dev, dec, isHorizontal: true)), const Expanded(child: SizedBox())]
          : [const Expanded(child: SizedBox()), Expanded(child: _buildDeceasedInfo(dev, dec, isHorizontal: true))],
      ),
    );
  }

  Widget _buildVerticalUILayout(DeviceDto dev, {required bool infoAtBottom}) {
    final dec = _controller.deceased;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 40, vertical: 80),
      child: Column(
        children: infoAtBottom
          ? [const Expanded(flex: 3, child: SizedBox()), Expanded(flex: 2, child: _buildDeceasedInfo(dev, dec, isHorizontal: false))]
          : [Expanded(flex: 2, child: _buildDeceasedInfo(dev, dec, isHorizontal: false)), const Expanded(flex: 3, child: SizedBox())],
      ),
    );
  }

  Widget _buildDeceasedInfo(DeviceDto dev, DeceasedDto? dec, {required bool isHorizontal}) {
    if (dec == null) return const SizedBox();

    return FittedBox(
      fit: BoxFit.scaleDown,
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: isHorizontal ? CrossAxisAlignment.start : CrossAxisAlignment.center,
        children: [
          if (dev.isDeceasedNameVisible)
            Text('故 ${dec.name} 魂靈', style: const TextStyle(color: Colors.white, fontSize: 80, fontWeight: FontWeight.bold, letterSpacing: 5)),
          const SizedBox(height: 24),
          if (dev.isFamilyContactVisible && dec.chiefMourner != null)
            _buildChiefMournerBox(dec.chiefMourner!, 36, 16, 24, 48),
        ],
      ),
    );
  }

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
    return const Center(child: CircularProgressIndicator(color: Color(0xFFC0A060)));
  }

  Widget _buildErrorView() {
    return Center(child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
      const Icon(Icons.error_outline, color: Colors.redAccent, size: 60),
      const SizedBox(height: 20),
      Text(_controller.statusMessage, style: const TextStyle(color: Colors.white, fontSize: 18)),
      const SizedBox(height: 40),
      ElevatedButton(onPressed: widget.onOpenSettings, child: const Text('설정 다시 하기')),
    ]));
  }
}
