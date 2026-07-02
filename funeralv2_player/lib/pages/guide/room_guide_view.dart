import 'package:flutter/material.dart';
import '../player_shell.dart';
import 'room_guide_controller.dart';

class RoomGuideView extends StatefulWidget {
  final String serverBaseUrl;
  final String deviceCode;
  final VoidCallback onOpenSettings;

  const RoomGuideView({
    super.key,
    required this.serverBaseUrl,
    required this.deviceCode,
    required this.onOpenSettings,
  });

  @override
  State<RoomGuideView> createState() => _RoomGuideViewState();
}

class _RoomGuideViewState extends State<RoomGuideView> {
  final RoomGuideController _controller = RoomGuideController();

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
              "호실 안내 화면 (준비 중)",
              style: TextStyle(color: Colors.white, fontSize: 40, fontWeight: FontWeight.bold),
            ),
          ),
        );
      },
    );
  }
}
