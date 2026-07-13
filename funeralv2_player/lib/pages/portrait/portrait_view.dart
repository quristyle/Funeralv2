import 'dart:io' as io;
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'portrait_controller.dart';
import '../../models/device_models.dart';
import '../player_shell.dart';

class PortraitView extends StatefulWidget {
  final String serverBaseUrl;
  final String deviceCode;
  final VoidCallback onOpenSettings;

  const PortraitView({
    super.key,
    required this.serverBaseUrl,
    required this.deviceCode,
    required this.onOpenSettings,
  });

  @override
  State<PortraitView> createState() => _PortraitViewState();
}

class _PortraitViewState extends State<PortraitView> {
  final PortraitController _controller = PortraitController();

  @override
  void initState() {
    super.initState();
    _controller.init(
      widget.serverBaseUrl,
      widget.deviceCode,
      () => setState(() {}),
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        final dev = _controller.device;

        // 1. 로딩 및 에러 처리 (셸 내부에서 보여질 내용)
        if (_controller.isLoading && dev == null) {
          return _buildLoadingView();
        }
        if (!_controller.isLoading && dev == null) {
          return _buildErrorView();
        }

        // 2. 정상 상태일 때 PlayerShell 호출 (설정 아이콘을 숨기고, 화면 전체 터치 이벤트 연동)
        return PlayerShell(
          device: dev!,
          playerService: _controller.playerService,
          onOpenSettings: widget.onOpenSettings,
          debugFileName: 'portrait_view.dart',
          showSettingsIcon: false,
          child: GestureDetector(
            behavior: HitTestBehavior.opaque, // 투명한 빈 영역 클릭도 모두 감지
            onTap: widget.onOpenSettings,
            child: Stack(
              fit: StackFit.expand,
              children: [
                // 영정사진 레이어
                _buildPortraitPhotoLayer(dev),
                // 장식 레이어
                _buildDecorationsLayer(dev),
                // 정보 레이아웃 레이어
                _buildUILayoutLayer(dev),
                // 텍스트 오버레이 레이어 (deviceTextOverlays) - 추가됨
                _buildTextOverlaysLayer(dev),
              ],
            ),
          ),
        );
      },
    );
  }

  // --- 레이어별 구현부 ---

  // [레이어 4] 텍스트 오버레이 레이어
  Widget _buildTextOverlaysLayer(DeviceDto dev) {
    final overlays = _controller.deceased?.deviceTextOverlays;
    if (overlays == null || overlays.isEmpty) return const SizedBox();

    return LayoutBuilder(builder: (context, constraints) {
      final width = constraints.maxWidth;
      final height = constraints.maxHeight;

      return Stack(
        children: overlays.map((text) {
          return Positioned(
            left: width * text.positionLeft / 100,
            top: height * text.positionTop / 100,
            width: width * text.width / 100,
            height: height * text.height / 100,
            child: Container(
              alignment: _getTextAlignment(text.textAlign),
              color: _parseColor(text.backgroundColor),
              child: Text(
                text.textContent,
                style: TextStyle(
                  fontSize: width * (text.fontSize / 100), // 화면 너비 대비 비율 폰트 크기
                  color: _parseColor(text.fontColor),
                  fontWeight: text.fontWeight == 'bold' ? FontWeight.bold : FontWeight.normal,
                  shadows: [
                    Shadow(
                      offset: const Offset(2.0, 2.0),
                      blurRadius: 4.0,
                      color: _getShadowColor(text.fontColor),
                    ),
                  ],
                ),
              ),
            ),
          );
        }).toList(),
      );
    });
  }

  Alignment _getTextAlignment(String align) {
    switch (align.toLowerCase()) {
      case 'left': return Alignment.centerLeft;
      case 'right': return Alignment.centerRight;
      case 'center':
      default: return Alignment.center;
    }
  }

  Color _parseColor(String colorStr) {
    if (colorStr == 'transparent') return Colors.transparent;
    try {
      final hex = colorStr.replaceAll('#', '');
      return Color(int.parse('FF$hex', radix: 16));
    } catch (_) {
      return Colors.white;
    }
  }

  Color _getShadowColor(String fontColorStr) {
    Color fontColor = _parseColor(fontColorStr);
    // 글자색이 밝으면(흰색 계열) 검정 그림자, 어두우면(검정 계열) 흰색 그림자 적용
    return fontColor.computeLuminance() > 0.5 
        ? Colors.black.withOpacity(0.8) 
        : Colors.white.withOpacity(0.8);
  }

  // [레이어 3] 장식 레이어 (리본 등)
  Widget _buildDecorationsLayer(DeviceDto dev) {
    final ribbons = _controller.deceased?.deviceRibbons;
    if (ribbons == null || ribbons.isEmpty) {
      return const SizedBox();
    }

    return LayoutBuilder(builder: (context, constraints) {
      final width = constraints.maxWidth;
      final height = constraints.maxHeight;

      return Stack(
        children: ribbons.map((ribbon) {
          if (ribbon.mediaSourceUrl == null) return const SizedBox();
          
          // 컨트롤러에서 다운로드한 로컬 경로 조회
          final String? localPath = _controller.ribbonPaths[ribbon.id];

          return Positioned(
            left: width * ribbon.positionLeft / 100,
            top: height * ribbon.positionTop / 100,
            width: width * ribbon.width / 100,
            height: height * ribbon.height / 100,
            child: _buildDynamicImage(localPath, ribbon.mediaSourceUrl!),
          );
        }).toList(),
      );
    });
  }

  // 로컬 경로가 있으면 파일로, 없으면 네트워크로 시도하는 범용 이미지 빌더
  Widget _buildDynamicImage(String? localPath, String networkUrl) {
    if (localPath != null && !kIsWeb) {
      return Image.file(
        io.File(localPath),
        fit: BoxFit.fill,
        errorBuilder: (c, e, s) => _buildNetworkImage(networkUrl),
      );
    }
    return _buildNetworkImage(networkUrl);
  }

  Widget _buildNetworkImage(String url) {
    final fullUrl = url.startsWith('http') ? url : '${widget.serverBaseUrl}$url';
    return Image.network(
      fullUrl,
      fit: BoxFit.fill,
      errorBuilder: (c, e, s) => const SizedBox(),
    );
  }

  Widget _buildPortraitPhotoLayer(DeviceDto dev) {
    if (!dev.isMemorialPhotoEnabled || _controller.deceasedPhotoPath == null) {
      return const SizedBox();
    }


    double x = 0;
    double y = (dev.photoVerticalAlignment == 'TOP') ? -1 : (dev.photoVerticalAlignment == 'CENTER' ? 0 : 1);
    if (dev.photoHorizontalAlignment == 'LEFT') x = -1;
    else if (dev.photoHorizontalAlignment == 'RIGHT') x = 1;

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
            alignment: Alignment(x, y),
            child: RotatedBox(
              quarterTurns: turns,
              child: _buildDeceasedImage(),
            ),
          ),
        );
      },
    );
  }

  Widget _buildUILayoutLayer(DeviceDto dev) {
    final bool isMonitorVertical = dev.displayOrientation == 'PORTRAIT';
    if (isMonitorVertical) {
      return _buildVerticalUILayout(dev, infoAtBottom: true);
    } else {
      return _buildHorizontalUILayout(dev, infoAtLeft: false);
    }
  }

  Widget _buildHorizontalUILayout(DeviceDto dev, {required bool infoAtLeft}) {
    final dec = _controller.deceased;
    final spacer = const Expanded(flex: 1, child: SizedBox());
    final infoWidget = Expanded(
      flex: 1,
      child: _buildDeceasedInfo(dev, dec, isHorizontal: true),
    );

    return Positioned.fill(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 60, vertical: 40),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.center,
          children: infoAtLeft ? [infoWidget, const SizedBox(width: 60), spacer] : [spacer, const SizedBox(width: 60), infoWidget],
        ),
      ),
    );
  }

  Widget _buildVerticalUILayout(DeviceDto dev, {required bool infoAtBottom}) {
    final dec = _controller.deceased;
    final spacer = const Expanded(flex: 2, child: SizedBox());
    final infoWidget = Expanded(
      flex: 3,
      child: _buildDeceasedInfo(dev, dec, isHorizontal: false),
    );

    return Positioned.fill(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 40, vertical: 60),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: infoAtBottom 
            ? [const SizedBox(height: 100), spacer, infoWidget]
            : [infoWidget, const SizedBox(height: 100), spacer],
        ),
      ),
    );
  }

  Widget _buildDeceasedInfo(DeviceDto dev, DeceasedDto? dec, {required bool isHorizontal}) {
    if (dec == null) return const SizedBox();
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: isHorizontal ? CrossAxisAlignment.start : CrossAxisAlignment.center,
      children: [
        if (dev.isDeceasedNameVisible)
          Text('故 ${dec.name} 魂靈', style: TextStyle(color: Colors.white, fontSize: isHorizontal ? 80 : 60, fontWeight: FontWeight.bold, letterSpacing: isHorizontal ? 5 : 4)),
        const SizedBox(height: 24),
        if (dev.isFamilyContactVisible && dec.chiefMourner != null)
          _buildChiefMournerBox(dec.chiefMourner!, isHorizontal ? 36 : 28, 16, 24, 48),
      ],
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
