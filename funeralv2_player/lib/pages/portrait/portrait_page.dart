import 'dart:io' as io;
import 'package:flutter/foundation.dart';
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
                  const CircularProgressIndicator(color: Color(0xFFC0A060)),
                  const SizedBox(height: 24),
                  Text(
                    _controller.statusMessage,
                    style: const TextStyle(color: Colors.white, fontSize: 18),
                  ),
                  const SizedBox(height: 48),
                  TextButton.icon(
                    onPressed: widget.onOpenSettings,
                    icon: const Icon(Icons.close, color: Colors.white54),
                    label: const Text(
                      '로딩 취소 및 설정으로 돌아가기',
                      style: TextStyle(color: Colors.white54),
                    ),
                  ),
                ],
              ),
            );
          }

          if (!_controller.isLoading && _controller.device == null) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(Icons.error_outline, color: Colors.redAccent, size: 60),
                  const SizedBox(height: 20),
                  Text(
                    _controller.statusMessage,
                    textAlign: TextAlign.center,
                    style: const TextStyle(color: Colors.white, fontSize: 18),
                  ),
                  const SizedBox(height: 40),
                  ElevatedButton.icon(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFFC0A060),
                      foregroundColor: Colors.black,
                      padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 16),
                    ),
                    onPressed: widget.onOpenSettings,
                    icon: const Icon(Icons.settings),
                    label: const Text('설정 다시 하기', style: TextStyle(fontWeight: FontWeight.bold)),
                  ),
                ],
              ),
            );
          }

          final vController = _controller.playerService.videoController;

          return Stack(
            fit: StackFit.expand,
            children: [
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
                Container(
                  decoration: const BoxDecoration(
                    gradient: LinearGradient(
                      colors: [Color(0xFF141414), Colors.black],
                      begin: Alignment.topCenter,
                      end: Alignment.bottomCenter,
                    ),
                  ),
                ),

              _buildContentOverlay(),

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

  Widget _buildContentOverlay() {
    final dev = _controller.device;
    final dec = _controller.deceased;

    if (dev == null) return const SizedBox();

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 40, vertical: 60),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Column(
            children: [
              const Text(
                '謹 弔',
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

          if (dev.isMemorialPhotoEnabled && _controller.deceasedPhotoPath != null)
            Stack(
              alignment: Alignment.center,
              children: [
                Container(
                  width: 320,
                  height: 400,
                  decoration: BoxDecoration(
                    color: Colors.black45,
                    border: Border.all(color: const Color(0xFFC0A060), width: 8),
                    boxShadow: const [
                      BoxShadow(
                        color: Colors.black87,
                        blurRadius: 30,
                        spreadRadius: 5,
                      ),
                    ],
                  ),
                  child: _buildDeceasedImage(),
                ),

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
            const Icon(
              Icons.church,
              color: Colors.white10,
              size: 150,
            ),

          if (dec != null)
            Column(
              children: [
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
                Text(
                  '${dec.gender} (${dec.age}세)${dec.religion != null ? " / ${dec.religion}" : ""}',
                  style: const TextStyle(
                    color: Colors.white60,
                    fontSize: 18,
                  ),
                ),
                const SizedBox(height: 25),
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
                        color: Colors.white70,
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

  Widget _buildDeceasedImage() {
    final path = _controller.deceasedPhotoPath;
    if (path == null) return const Icon(Icons.person, color: Colors.white24, size: 120);

    if (kIsWeb) {
      return Image.network(
        path,
        fit: BoxFit.cover,
        errorBuilder: (context, error, stackTrace) => const Icon(Icons.person, color: Colors.white24, size: 120),
      );
    } else {
      // 네이티브에서만 io.File 사용
      return Image.file(
        io.File(path),
        fit: BoxFit.cover,
        errorBuilder: (context, error, stackTrace) => const Icon(Icons.person, color: Colors.white24, size: 120),
      );
    }
  }
}

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
      path.moveTo(0, 0);
      path.lineTo(size.width, 0);
      path.lineTo(0, size.height);
      path.close();
    } else {
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
