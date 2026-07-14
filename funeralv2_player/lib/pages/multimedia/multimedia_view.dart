import 'package:flutter/material.dart';
import '../player_shell.dart';
import 'multimedia_controller.dart';
import '../../models/device_models.dart';

/// [멀티미디어 추모 롤링 뷰 위젯]
/// 빈소 안쪽에 조문객들이 고인의 생전 추억을 돌아볼 수 있도록 설치하는 추모 액자 사이니지 화면입니다.
/// 유족이 올린 슬라이드 이미지들을 장비 설정 효과(FADE, SLIDE 등)에 맞추어 루프 렌더링합니다.
class MultimediaView extends StatefulWidget {
  final String serverBaseUrl; // 통합 서버 Base URL
  final String deviceCode; // 장비 식별 코드
  final VoidCallback onOpenSettings; // 환경 설정 진입 콜백

  const MultimediaView({
    super.key,
    required this.serverBaseUrl,
    required this.deviceCode,
    required this.onOpenSettings,
  });

  @override
  State<MultimediaView> createState() => _MultimediaViewState();
}

class _MultimediaViewState extends State<MultimediaView> {
  // 슬라이드 루프 제어 및 미디어 리소스 상태 컨트롤러
  final MultimediaController _controller = MultimediaController();

  /// [위젯 초기화]
  @override
  void initState() {
    super.initState();
    _controller.init(
      widget.serverBaseUrl,
      widget.deviceCode,
      () => setState(() {}), // 배경 비디오 기동 시 리프레시 처리
    );
  }

  /// [자원 소멸]
  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  /// [위젯 빌드]
  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        final dev = _controller.device;

        if (_controller.isLoading && dev == null) return const Center(child: CircularProgressIndicator(color: Color(0xFFC0A060)));
        if (dev == null) return const Center(child: Text("데이터 로드 실패", style: TextStyle(color: Colors.white)));

        return PlayerShell(
          device: dev,
          playerService: _controller.playerService,
          onOpenSettings: widget.onOpenSettings,
          debugFileName: 'multimedia_view.dart',
          child: _buildPhotoLayer(dev, _controller.deceased), // 사진 슬라이드 레이어 생성 주입
        );
      },
    );
  }

  /// [사진 슬라이드 롤링 레이어 구성]
  /// 고인에게 매핑된 사진 배열이 없으면 빈 안내 문구를 표출하고,
  /// 사진이 있을 경우 설정된 화면 전환 이펙트(`memorialPhotoEffect`)를 판별하여
  /// `AnimatedSwitcher` 아래에 페이드(Fade) 또는 슬라이드(Slide) 애니메이션 트랜지션을 생성해 렌더링합니다.
  Widget _buildPhotoLayer(DeviceDto dev, DeceasedDto? dec) {
    if (dec == null || dec.familyPhotos.isEmpty) {
      return const Center(child: Text("추모 사진이 없습니다.", style: TextStyle(color: Colors.white54, fontSize: 24)));
    }

    // 현재 인덱스에 해당하는 패밀리 포토 이미지 서버 주소 획득
    final imageUrl = "${widget.serverBaseUrl}${dec.familyPhotos[_controller.currentPhotoIndex]}";

    // 1) SLIDE 효과 분기 처리 (우측에서 좌측으로 흐름)
    if (dev.memorialPhotoEffect == 'SLIDE') {
      return AnimatedSwitcher(
        duration: const Duration(milliseconds: 1000), // 1.0초간 이동 애니메이션
        transitionBuilder: (Widget child, Animation<double> animation) {
          return SlideTransition(
            position: Tween<Offset>(
              begin: const Offset(1.0, 0.0), // 가로 방향으로 밖에서 안으로 인입
              end: Offset.zero,
            ).animate(animation),
            child: child,
          );
        },
        // 이미지 객체의 갱신 및 스위칭 감지를 위해 ValueKey 주입
        child: _buildImage(imageUrl, ValueKey(_controller.currentPhotoIndex)),
      );
    } else {
      // 2) 기본값 FADE 교차 효과 처리 (불투명도가 점점 살아남)
      return AnimatedSwitcher(
        duration: const Duration(milliseconds: 1500), // 1.5초간 서서히 노출
        transitionBuilder: (Widget child, Animation<double> animation) {
          return FadeTransition(
            opacity: animation,
            child: child,
          );
        },
        child: _buildImage(imageUrl, ValueKey(_controller.currentPhotoIndex)),
      );
    }
  }

  /// [네트워크 이미지 렌더러 구성]
  /// 서버에서 사진 데이터를 안전하게 불러오며, 엑박 발생 시 경고 플레이스홀더 아이콘을 매핑합니다.
  Widget _buildImage(String url, Key key) {
    return SizedBox.expand(
      key: key,
      child: Image.network(
        url,
        fit: BoxFit.contain, // 이미지 원본 비율을 깨지 않고 전체 채우기
        errorBuilder: (c, e, s) => const Icon(Icons.broken_image, size: 100, color: Colors.white10),
      ),
    );
  }
}
