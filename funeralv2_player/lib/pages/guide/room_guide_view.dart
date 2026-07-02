import 'dart:io' as io;
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import '../player_shell.dart';
import 'room_guide_controller.dart';
import '../../models/device_models.dart';

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

        if (_controller.isLoading && dev == null) return const Center(child: CircularProgressIndicator(color: Color(0xFFC0A060)));
        if (dev == null) return const Center(child: Text("장비 데이터 로드 실패", style: TextStyle(color: Colors.white)));

        return PlayerShell(
          device: dev,
          playerService: _controller.playerService,
          onOpenSettings: widget.onOpenSettings,
          debugFileName: 'room_guide_view.dart',
          child: _buildContent(dev, _controller.deceased),
        );
      },
    );
  }

  Widget _buildContent(DeviceDto dev, DeceasedDto? dec) {
    if (dec == null) {
      return const Center(child: Text("빈소 준비 중입니다.", style: TextStyle(color: Colors.white70, fontSize: 32)));
    }

    return Padding(
      padding: const EdgeInsets.all(40.0),
      child: Column(
        children: [
          // 1. 상단 호실 이름 영역
          Container(
            padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 40),
            decoration: BoxDecoration(
              color: const Color(0xFFC0A060).withOpacity(0.8),
              borderRadius: BorderRadius.circular(50),
            ),
            child: Text(
              dec.roomName ?? dev.name,
              style: const TextStyle(color: Colors.black, fontSize: 36, fontWeight: FontWeight.w900),
            ),
          ),
          const SizedBox(height: 40),

          // 2. 메인 콘텐츠 (사진 + 정보)
          Expanded(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // 좌측: 영정 사진 (설정 시에만 표시)
                if (dev.isMemorialPhotoEnabled) ...[
                  Expanded(
                    flex: 4,
                    child: _buildPhotoSection(),
                  ),
                  const SizedBox(width: 40),
                ],
                
                // 우측/전체: 상세 정보
                Expanded(
                  flex: 6,
                  child: Column(
                    crossAxisAlignment: dev.isMemorialPhotoEnabled ? CrossAxisAlignment.start : CrossAxisAlignment.center,
                    children: [
                      _buildDeceasedName(dec, isCentered: !dev.isMemorialPhotoEnabled),
                      const Divider(color: Colors.white24, thickness: 1.5, height: 40),
                      
                      // 정보 섹션: 사진이 없을 때 중앙 정렬을 위해 너비가 제한된 컨테이너 사용
                      SizedBox(
                        width: dev.isMemorialPhotoEnabled ? double.infinity : 700,
                        child: Column(
                          children: [
                            _buildInfoRow(
                              "상 주", 
                              dec.mourners.isNotEmpty 
                                ? dec.mourners.map((m) => m.name ?? '').where((n) => n.isNotEmpty).join(', ')
                                : (dec.chiefMourner ?? "-"), 
                            ),
                            _buildInfoRow("발 인", _formatDate(dec.funeralDate)),
                            _buildInfoRow("장 지", dec.burialDate != null ? _formatDate(dec.burialDate!) : "-"),
                          ],
                        ),
                      ),
                      const Spacer(),
                      // 하단 안내 문구
                      Text(
                        "삼가 고인의 명복을 빕니다.",
                        style: TextStyle(color: Colors.white.withOpacity(0.5), fontSize: 24),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildPhotoSection() {
    final path = _controller.deceasedPhotoPath;
    return Container(
      decoration: BoxDecoration(
        border: Border.all(color: const Color(0xFFC0A060), width: 4),
        boxShadow: const [BoxShadow(color: Colors.black87, blurRadius: 20)],
      ),
      child: path == null
          ? const Icon(Icons.person, size: 200, color: Colors.white10)
          : kIsWeb
              ? Image.network(path, fit: BoxFit.cover)
              : Image.file(io.File(path), fit: BoxFit.cover),
    );
  }

  Widget _buildDeceasedName(DeceasedDto dec, {bool isCentered = false}) {
    return Column(
      crossAxisAlignment: isCentered ? CrossAxisAlignment.center : CrossAxisAlignment.start,
      children: [
        Text(
          "故 ${dec.name} 님",
          style: const TextStyle(color: Colors.white, fontSize: 54, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 8),
        Text(
          "${dec.gender == 'M' ? '남' : '여'} / 향년 ${dec.age}세${(dec.religion != null && dec.religion != 'NONE') ? ' / ${dec.religion}' : ''}",
          style: const TextStyle(color: Colors.white70, fontSize: 24),
        ),
      ],
    );
  }

  Widget _buildInfoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 12.0),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.start,
        children: [
          Container(
            width: 100,
            padding: const EdgeInsets.symmetric(vertical: 4, horizontal: 8),
            decoration: BoxDecoration(border: Border.all(color: const Color(0xFFC0A060))),
            child: Text(label, textAlign: TextAlign.center, style: const TextStyle(color: Color(0xFFC0A060), fontSize: 20, fontWeight: FontWeight.bold)),
          ),
          const SizedBox(width: 20),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(color: Colors.white, fontSize: 26, fontWeight: FontWeight.w500),
            ),
          ),
        ],
      ),
    );
  }

  String _formatDate(String? dateStr) {
    if (dateStr == null || dateStr.isEmpty) return "-";
    try {
      final date = DateTime.parse(dateStr).toLocal();
      return "${date.year}년 ${date.month}월 ${date.day}일 ${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}";
    } catch (e) {
      return dateStr;
    }
  }
}
