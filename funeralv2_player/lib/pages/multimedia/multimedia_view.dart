import 'package:flutter/material.dart';
import '../player_shell.dart';
import 'multimedia_controller.dart';
import '../../models/device_models.dart';

class MultimediaView extends StatefulWidget {
  final String serverBaseUrl;
  final String deviceCode;
  final VoidCallback onOpenSettings;

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
  final MultimediaController _controller = MultimediaController();

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

        if (_controller.isLoading && dev == null) return const Center(child: CircularProgressIndicator(color: Color(0xFFC0A060)));
        if (dev == null) return const Center(child: Text("데이터 로드 실패", style: TextStyle(color: Colors.white)));

        return PlayerShell(
          device: dev,
          playerService: _controller.playerService,
          onOpenSettings: widget.onOpenSettings,
          debugFileName: 'multimedia_view.dart',
          child: _buildPhotoLayer(dev, _controller.deceased),
        );
      },
    );
  }

  Widget _buildPhotoLayer(DeviceDto dev, DeceasedDto? dec) {
    if (dec == null || dec.deviceRibbons.isEmpty) {
      return const Center(child: Text("추모 사진이 없습니다.", style: TextStyle(color: Colors.white54, fontSize: 24)));
    }

    final currentPhoto = dec.deviceRibbons[_controller.currentPhotoIndex];
    final imageUrl = "${widget.serverBaseUrl}${currentPhoto.mediaSourceUrl}";

    // 효과 분기 처리
    if (dev.memorialPhotoEffect == 'SLIDE') {
      return AnimatedSwitcher(
        duration: const Duration(milliseconds: 1000),
        transitionBuilder: (Widget child, Animation<double> animation) {
          return SlideTransition(
            position: Tween<Offset>(
              begin: const Offset(1.0, 0.0),
              end: Offset.zero,
            ).animate(animation),
            child: child,
          );
        },
        child: _buildImage(imageUrl, ValueKey(_controller.currentPhotoIndex)),
      );
    } else {
      // 기본값 FADE
      return AnimatedSwitcher(
        duration: const Duration(milliseconds: 1500),
        child: _buildImage(imageUrl, ValueKey(_controller.currentPhotoIndex)),
      );
    }
  }

  Widget _buildImage(String url, Key key) {
    return SizedBox.expand(
      key: key,
      child: Image.network(
        url,
        fit: BoxFit.contain,
        errorBuilder: (c, e, s) => const Icon(Icons.broken_image, size: 100, color: Colors.white10),
      ),
    );
  }
}
