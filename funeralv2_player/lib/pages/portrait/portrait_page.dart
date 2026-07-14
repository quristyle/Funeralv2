import 'dart:io' as io;
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:media_kit_video/media_kit_video.dart';
import 'portrait_controller.dart';
import '../../models/device_models.dart';

/// [영정 페이지 단독 위젯]
/// 사이니지에 얹어지는 개별 영정 정보 출력 페이지 컴포넌트입니다.
/// 모니터의 물리적 회전율을 계산하여 스택 형태로 영상 배경(1) -> 회전 보정 영정(2) -> 한자 고인명 텍스트(3)를 차곡차곡 쌓아 렌더링합니다.
/// 설정 아이콘 톱니바퀴를 제거하고, 화면을 단순 터치/클릭하는 액션만으로 환경설정 화면으로 즉각 진입하도록 설계되었습니다.
class PortraitPage extends StatefulWidget {
  final String serverBaseUrl; // 통합 서버 Base URL
  final String deviceCode; // 장비 식별 코드
  final Function() onOpenSettings; // 환경 설정 기동 콜백

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
  // 영정 화면 비즈니스 로직 및 캐시 상태 컨트롤러
  final PortraitController _controller = PortraitController();

  /// [위젯 초기화 상태 설정]
  @override
  void initState() {
    super.initState();
    _loadData();
  }

  /// [컨트롤러 기동 및 동기화 시작]
  void _loadData() {
    _controller.init(
      widget.serverBaseUrl,
      widget.deviceCode,
      () {
        setState(() {}); // 백그라운드 비디오 프레임 렌더 완료 시 위젯 상태 갱신
      },
    );
  }

  /// [자원 해제]
  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  /// [위젯 빌드]
  /// 컨트롤러 상태를 구독하여 로딩/에러 화면을 분기하며,
  /// 요구사항에 따라 설정 버튼 톱니바퀴를 소거하고 화면 단일 탭 감지 제스처([GestureDetector])를 감싸 처리합니다.
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

          // 모니터 물리적 회전 비율 설정 (PORTRAIT = 90도 회전)
          final bool isMonitorVertical = dev!.displayOrientation == 'PORTRAIT';
          
          return GestureDetector(
            behavior: HitTestBehavior.translucent, // 터치 이벤트 관통 지원
            onTap: widget.onOpenSettings, // 단순 탭만으로 즉각 환경설정 진입
            child: Stack(
              fit: StackFit.expand,
              children: [
                const ColoredBox(color: Colors.black),

                RotatedBox(
                  quarterTurns: isMonitorVertical ? 1 : 0, // 세로 화면 지원
                  child: Padding(
                    // 1) 장비에서 전달받은 화면 패딩 적용 (%)
                    padding: EdgeInsets.only(
                      top: MediaQuery.of(context).size.height * (dev.displayPaddingTop / 100),
                      bottom: MediaQuery.of(context).size.height * (dev.displayPaddingBottom / 100),
                      left: MediaQuery.of(context).size.width * (dev.displayPaddingLeft / 100),
                      right: MediaQuery.of(context).size.width * (dev.displayPaddingRight / 100),
                    ),
                    child: Stack(
                      fit: StackFit.expand,
                      children: [
                        // [레이어 1] 배경 동영상
                        RotatedBox(
                          quarterTurns: _getVideoTurns(dev),
                          child: Video(
                            controller: _controller.playerService.videoController,
                            fit: BoxFit.cover,
                            controls: NoVideoControls,
                          ),
                        ),
                        
                        // [레이어 2] 회전 보정 영정사진 레이아웃
                        if (dev.isMemorialPhotoEnabled && _controller.deceasedPhotoPath != null)
                          _buildPortraitPhotoLayer(dev),

                        // [레이어 3] 텍스트 정보 레이아웃 (고인 한자명 및 상주)
                        _buildUILayoutLayer(dev),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  /// [영정사진 레이어 세부 빌더]
  /// 수평 정렬(`photoHorizontalAlignment`) 및 수직 정렬(`photoVerticalAlignment`) 값에 기초해 Alignment를 만들고
  /// 영정 내부 마진 비율([memorialPadding])을 가산하여 렌더링합니다.
  Widget _buildPortraitPhotoLayer(DeviceDto dev) {
    if (!dev.isMemorialPhotoEnabled || _controller.deceasedPhotoPath == null) {
      return const SizedBox();
    }

    // 1) 수평 정렬 매핑 (LEFT, CENTER, RIGHT)
    double x = 0; 
    if (dev.photoHorizontalAlignment == 'LEFT') x = -1;
    else if (dev.photoHorizontalAlignment == 'RIGHT') x = 1;

    // 2) 수직 정렬 매핑 (TOP, CENTER, BOTTOM)
    double y = 0; 
    if (dev.photoVerticalAlignment == 'TOP') y = -1;
    else if (dev.photoVerticalAlignment == 'BOTTOM') y = 1;
    
    final finalAlignment = Alignment(x, y);

    // 3) 영정사진 자체 회전 보정 (VERTICAL_LEFT, VERTICAL_RIGHT 등)
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

  /// [UI 텍스트 정보 레이아웃 제어 분기]
  /// 모니터 가로/세로 비에 맞추어 세로형 하단(Vertical) 또는 가로형 우측(Horizontal)으로 분기하여 배치합니다.
  Widget _buildUILayoutLayer(DeviceDto dev) {
    final bool isMonitorVertical = dev.displayOrientation == 'PORTRAIT';
    
    if (isMonitorVertical) {
      return _buildVerticalUILayout(dev, infoAtBottom: true);
    } else {
      return _buildHorizontalUILayout(dev, infoAtLeft: false);
    }
  }

  /// [가로 모니터용 UI 배치 빌더]
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

  /// [세로 모니터용 UI 배치 빌더]
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

  /// [고인 세부 정보 글자 렌더러]
  /// 고인의 한자/한글 이름('故 고인명 魂靈') 및 대표 상주 뱃지를 가변 크기(`FittedBox`)로 조립합니다.
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

  /// [상주 이름 뱃지 빌더]
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

  /// [로컬 영정사진 파일 바인딩 헬퍼]
  Widget _buildDeceasedImage() {
    final path = _controller.deceasedPhotoPath;
    final dev = _controller.device;
    final BoxFit fitMode = (dev?.isMemorialPhotoKeepAspectRatio ?? true) ? BoxFit.contain : BoxFit.fill;

    if (path == null) return const Icon(Icons.person, color: Colors.white24, size: 120);
    if (kIsWeb) return Image.network(path, fit: fitMode, errorBuilder: (c, e, s) => const Icon(Icons.person, color: Colors.white24, size: 120));
    return Image.file(io.File(path), fit: fitMode, errorBuilder: (c, e, s) => const Icon(Icons.person, color: Colors.white24, size: 120));
  }

  /// [배경 비디오 90도 회전 비율 매핑 연산기]
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

  /// [로딩 화면 컴포넌트 빌더]
  Widget _buildLoadingView() {
    return const Center(child: CircularProgressIndicator(color: Color(0xFFC0A060)));
  }

  /// [에러 경고 화면 컴포넌트 빌더]
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
