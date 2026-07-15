import 'dart:io' as io;
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'portrait_controller.dart';
import '../../models/device_models.dart';
import '../player_shell.dart';

/// [영정 메인 뷰 위젯]
/// 사이니지에 얹어지는 영정 전용 화면 뷰입니다.
/// `PlayerShell` 골조 아래에 5가지 비주얼 레이어(배경 스킨 -> 영정사진 -> 근조리본 -> 정보 뱃지 -> 사용자 텍스트 오버레이)를
/// 적층 방식으로 구성하여 최종 디스플레이 화면을 완성합니다.
/// 관리자 설정 아이콘을 미노출하고 화면 전체 영역의 단일 탭(클릭)을 통해 환경설정으로 이동하도록 지원합니다.
class PortraitView extends StatefulWidget {
  final String serverBaseUrl; // 통합 서버 Base URL
  final String deviceCode; // 장비 식별 코드
  final VoidCallback onOpenSettings; // 환경 설정 기동 콜백

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
  // 영정 화면 비즈니스 로직 및 캐시 데이터 컨트롤러
  final PortraitController _controller = PortraitController();

  /// [위젯 초기화 상태 설정]
  @override
  void initState() {
    super.initState();
    _controller.init(
      widget.serverBaseUrl,
      widget.deviceCode,
      () => setState(() {}), // 배경 비디오 프레임 준비 완료 시 화면 리프레시
    );
  }

  /// [자원 해제]
  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  /// [위젯 설정 갱신 대응]
  @override
  void didUpdateWidget(covariant PortraitView oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.deviceCode != widget.deviceCode || oldWidget.serverBaseUrl != widget.serverBaseUrl) {
      _controller.init(
        widget.serverBaseUrl,
        widget.deviceCode,
        () => setState(() {}),
      );
    }
  }

  /// [위젯 빌드]
  /// 로딩 상태에 따라 분기하며, 정상 상태 도달 시 공통 골조 셸(`PlayerShell`) 하단에 적층 스택 구조를 주입합니다.
  /// 설정 아이콘 숨김([showSettingsIcon] = false) 처리를 하고, 단일 탭 제스처 감지기([GestureDetector])를 연동합니다.
  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        final dev = _controller.device;

        if (_controller.isLoading && dev == null) {
          return _buildLoadingView();
        }
        if (!_controller.isLoading && dev == null) {
          return _buildErrorView();
        }

        return PlayerShell(
          device: dev!,
          playerService: _controller.playerService,
          onOpenSettings: widget.onOpenSettings,
          debugFileName: 'portrait_view.dart',
          showSettingsIcon: false, // 요구사항 반영: 설정 진입용 톱니바퀴 아이콘 숨김
          child: GestureDetector(
            behavior: HitTestBehavior.translucent, // 하위 렌더 레이어 터치 뚫고 지나가도록 설정
            onTap: widget.onOpenSettings, // 요구사항 반영: 화면 단순 클릭(탭)만으로 즉시 환경설정 이동
            child: Stack(
              fit: StackFit.expand,
              children: [
                // [레이어 0] 배경 스킨 이미지 레이어 (동영상 바로 위, 영정 아래 배치)
                _buildBackgroundImageLayer(dev),
                // [레이어 1] 고인 영정사진 이미지 레이어
                _buildPortraitPhotoLayer(dev),
                // [레이어 2] 화면 장식 레이어 (서버 연동 근조 리본 장식 등)
                _buildDecorationsLayer(dev),
                // [레이어 3] 정보 레이아웃 레이어 (故 고인 성함 한자명 및 상주 명단 박스)
                _buildUILayoutLayer(dev),
                // [레이어 4] 텍스트 오버레이 레이어 (원격 커스텀 텍스트 렌더러)
                _buildTextOverlaysLayer(dev),
              ],
            ),
          ),
        );
      },
    );
  }

  // --- 레이어별 세부 빌더 구현부 ---

  /// [레이어 4: 커스텀 텍스트 오버레이 렌더러]
  /// 서버 디렉토리 설정에서 내려온 텍스트 오버레이 객체들을 화면 가로세로 비율 좌표(%)에 맞추어 절대 좌표로 위치시킵니다.
  /// 폰트 크기 비율, 글자 두께, 정렬, 배경색 및 가시성 개선을 위한 텍스트 음영(Shadow) 처리를 수행합니다.
  Widget _buildTextOverlaysLayer(DeviceDto dev) {
    final overlays = _controller.deceased?.deviceTextOverlays;
    if (overlays == null || overlays.isEmpty) return const SizedBox();

    // 장비 물리 방향에 따른 베이스 회전 각도값 계산
    int deviceTurns = 0;
    switch (dev.portraitOrientation) {
      case 'VERTICAL_LEFT': deviceTurns = 3; break;
      case 'VERTICAL_RIGHT': deviceTurns = 1; break;
      case 'INVERTED': deviceTurns = 2; break;
      default: deviceTurns = 0; break;
    }

    return LayoutBuilder(builder: (context, constraints) {
      final width = constraints.maxWidth;
      final height = constraints.maxHeight;

      return Stack(
        children: overlays.map((text) {
          final itemTurns = (text.rotation / 90).round();
          final finalTurns = (itemTurns + deviceTurns) % 4; // 아이템 고유 회전과 장비 기본 방향을 합산

          return Positioned(
            left: width * text.positionLeft / 100,
            top: height * text.positionTop / 100,
            width: width * text.width / 100,
            height: height * text.height / 100,
            child: RotatedBox(
              quarterTurns: finalTurns,
              child: Container(
                alignment: _getTextAlignment(text.textAlign),
                color: _parseColor(text.backgroundColor),
                child: Text(
                  text.textContent,
                  style: TextStyle(
                    fontSize: width * (text.fontSize / 100), // 모니터 스케일에 맞춘 반응형 폰트 크기 계산
                    color: _parseColor(text.fontColor),
                    fontWeight: text.fontWeight == 'bold' ? FontWeight.bold : FontWeight.normal,
                    shadows: [
                      Shadow(
                        offset: const Offset(2.0, 2.0),
                        blurRadius: 4.0,
                        color: _getShadowColor(text.fontColor), // 가시성 음영 처리
                      ),
                    ],
                  ),
                ),
              ),
            ),
          );
        }).toList(),
      );
    });
  }

  /// [텍스트 정렬 매퍼]
  Alignment _getTextAlignment(String align) {
    switch (align.toLowerCase()) {
      case 'left': return Alignment.centerLeft;
      case 'right': return Alignment.centerRight;
      case 'center':
      default: return Alignment.center;
    }
  }

  /// [HEX 컬러 파서]
  /// '#FFFFFF' 또는 'transparent' 형식의 문자열을 Flutter `Color` 객체로 변환합니다.
  Color _parseColor(String colorStr) {
    if (colorStr == 'transparent') return Colors.transparent;
    try {
      final hex = colorStr.replaceAll('#', '');
      return Color(int.parse('FF$hex', radix: 16));
    } catch (_) {
      return Colors.white;
    }
  }

  /// [텍스트 그림자 색상 판별 연산자]
  /// 글자색 밝기(Luminance)가 밝은 톤일 경우 어두운 음영을, 어두운 톤일 경우 밝은 음영을 매핑하여 대비를 줍니다.
  Color _getShadowColor(String fontColorStr) {
    Color fontColor = _parseColor(fontColorStr);
    return fontColor.computeLuminance() > 0.5 
        ? Colors.black.withOpacity(0.8) 
        : Colors.white.withOpacity(0.8);
  }

  /// [레이어 2: 근조 리본 장식 레이어]
  /// 제단용 모바일 사이니지 화면의 모서리나 특정 영역에 절대 비율(%)로 근조 리본 이미지를 얹어 렌더링합니다.
  Widget _buildDecorationsLayer(DeviceDto dev) {
    final ribbons = _controller.deceased?.deviceRibbons;
    if (ribbons == null || ribbons.isEmpty) {
      return const SizedBox();
    }

    int deviceTurns = 0;
    switch (dev.portraitOrientation) {
      case 'VERTICAL_LEFT': deviceTurns = 3; break;
      case 'VERTICAL_RIGHT': deviceTurns = 1; break;
      case 'INVERTED': deviceTurns = 2; break;
      default: deviceTurns = 0; break;
    }

    return LayoutBuilder(builder: (context, constraints) {
      final width = constraints.maxWidth;
      final height = constraints.maxHeight;

      return Stack(
        children: ribbons.map((ribbon) {
          if (ribbon.mediaSourceUrl == null) return const SizedBox();
          
          final String? localPath = _controller.ribbonPaths[ribbon.id];
          final itemTurns = (ribbon.rotation / 90).round();
          final finalTurns = (itemTurns + deviceTurns) % 4;

          return Positioned(
            left: width * ribbon.positionLeft / 100,
            top: height * ribbon.positionTop / 100,
            width: width * ribbon.width / 100,
            height: height * ribbon.height / 100,
            child: RotatedBox(
              quarterTurns: finalTurns,
              child: _buildDynamicImage(localPath, ribbon.mediaSourceUrl!),
            ),
          );
        }).toList(),
      );
    });
  }

  /// [로컬 캐시 동적 이미지 빌더]
  /// 로컬 파일 캐시 경로([localPath])가 있으면 `Image.file`로 기동하고 오프라인 동작을 수행하며,
  /// 없거나 웹 환경일 경우 다이렉트 네트워크 이미지([networkUrl])로 폴백합니다.
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

  /// [네트워크 이미지 요청 헬퍼]
  Widget _buildNetworkImage(String url) {
    final fullUrl = url.startsWith('http') ? url : '${widget.serverBaseUrl}$url';
    return Image.network(
      fullUrl,
      fit: BoxFit.fill,
      errorBuilder: (c, e, s) => const SizedBox(),
    );
  }

  /// [레이어 0: 제단 스킨 배경 이미지 레이어]
  /// 동영상 백그라운드 위에 얹어지는 정적 배경 디자인 플레이트 스킨입니다.
  Widget _buildBackgroundImageLayer(DeviceDto dev) {
    if (!dev.isBackgroundImageEnabled || _controller.localBackgroundPath == null) {
      return const SizedBox();
    }

    int turns = 0;
    switch (dev.backgroundOrientation) {
      case 'VERTICAL_LEFT': turns = 3; break;
      case 'VERTICAL_RIGHT': turns = 1; break;
      case 'INVERTED': turns = 2; break;
      default: turns = 0; break;
    }

    return LayoutBuilder(
      builder: (context, constraints) {
        return SizedBox.expand(
          child: RotatedBox(
            quarterTurns: turns,
            child: _buildBackgroundImage(),
          ),
        );
      },
    );
  }

  /// [로컬 배경 이미지 렌더러]
  Widget _buildBackgroundImage() {
    final path = _controller.localBackgroundPath;
    if (path == null) return const SizedBox();

    if (kIsWeb) {
      return Image.network(
        path,
        fit: BoxFit.fill,
        errorBuilder: (c, e, s) => const SizedBox(),
      );
    }
    return Image.file(
      io.File(path),
      fit: BoxFit.fill,
      errorBuilder: (c, e, s) => const SizedBox(),
    );
  }

  /// [레이어 1: 보정 완료된 영정사진 레이아웃 빌더]
  /// 영정사진 배치 정렬 조건 및 영정 내부 여백 마진 비율([memorialPadding])을 가산하여 렌더링하고 방향을 잡습니다.
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
          child: SizedBox.expand(
            child: RotatedBox(
              quarterTurns: turns,
              child: _buildDeceasedImage(Alignment(x, y)),
            ),
          ),
        );
      },
    );
  }

  /// [레이어 3: 성함 및 상주 정보 UI 레이아웃 분기]
  Widget _buildUILayoutLayer(DeviceDto dev) {
    final bool isMonitorVertical = dev.displayOrientation == 'PORTRAIT';
    if (isMonitorVertical) {
      return _buildVerticalUILayout(dev, infoAtBottom: true);
    } else {
      return _buildHorizontalUILayout(dev, infoAtLeft: false);
    }
  }

  /// [가로 모니터용 성함 및 상주 정보 빌더]
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

  /// [세로 모니터용 성함 및 상주 정보 빌더]
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

  /// [고인 인적 정보 한자 텍스트 생성]
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

  /// [상주 성명 노출 상자 빌더]
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

  /// [영정사진 렌더러]
  Widget _buildDeceasedImage(Alignment alignment) {
    final path = _controller.deceasedPhotoPath;
    final dev = _controller.device;
    final BoxFit fitMode = (dev?.isMemorialPhotoKeepAspectRatio ?? true) ? BoxFit.contain : BoxFit.fill;
    const placeholder = Icon(Icons.person, color: Colors.white24, size: 120);

    if (path == null) return placeholder;

    if (kIsWeb) {
      return Image.network(
        path, 
        fit: fitMode, 
        alignment: alignment,
        errorBuilder: (c, e, s) => placeholder
      );
    }
    return Image.file(
      io.File(path), 
      fit: fitMode, 
      alignment: alignment,
      errorBuilder: (c, e, s) => placeholder
    );
  }

  /// [로딩 컴포넌트 렌더러]
  Widget _buildLoadingView() {
    return const Center(child: CircularProgressIndicator(color: Color(0xFFC0A060)));
  }

  /// [에러 경고 컴포넌트 렌더러]
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
