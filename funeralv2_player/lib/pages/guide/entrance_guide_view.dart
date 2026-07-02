import 'package:flutter/material.dart';
import '../player_shell.dart';
import 'entrance_guide_controller.dart';

class EntranceGuideView extends StatefulWidget {
  final String serverBaseUrl;
  final String deviceCode;
  final VoidCallback onOpenSettings;

  const EntranceGuideView({
    super.key,
    required this.serverBaseUrl,
    required this.deviceCode,
    required this.onOpenSettings,
  });

  @override
  State<EntranceGuideView> createState() => _EntranceGuideViewState();
}

class _EntranceGuideViewState extends State<EntranceGuideView> {
  final EntranceGuideController _controller = EntranceGuideController();

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

        if (_controller.isLoading && dev == null) return const Center(child: CircularProgressIndicator());
        if (dev == null) return const Center(child: Text("데이터 로드 실패"));

        return PlayerShell(
          device: dev,
          playerService: _controller.playerService,
          onOpenSettings: widget.onOpenSettings,
          child: const Center(
            child: Text(
              "입구 안내 화면 (준비 중)",
              style: TextStyle(color: Colors.white, fontSize: 40, fontWeight: FontWeight.bold),
            ),
          ),
        );
      },
    );
  }
}
