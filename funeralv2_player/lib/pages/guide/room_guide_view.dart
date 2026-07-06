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

    final bool isPortrait = dev.displayOrientation == 'PORTRAIT';

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
              dev.roomName ?? dev.name,
              style: const TextStyle(color: Colors.black, fontSize: 36, fontWeight: FontWeight.w900),
            ),
          ),
          const SizedBox(height: 40),

          // 2. 메인 콘텐츠
          Expanded(
            child: isPortrait ? _buildVerticalContent(dev, dec) : _buildHorizontalContent(dev, dec),
          ),
        ],
      ),
    );
  }

  Widget _buildHorizontalContent(DeviceDto dev, DeceasedDto dec) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (dev.isMemorialPhotoEnabled) ...[
          Expanded(flex: 4, child: _buildPhotoSection()),
          const SizedBox(width: 40),
        ],
        Expanded(
          flex: 6,
          child: Column(
            crossAxisAlignment: dev.isMemorialPhotoEnabled ? CrossAxisAlignment.start : CrossAxisAlignment.center,
            children: [
              _buildDeceasedName(dec, isCentered: !dev.isMemorialPhotoEnabled),
              const Divider(color: Colors.white24, thickness: 1.5, height: 40),
              _buildInfoSection(dev, dec, width: dev.isMemorialPhotoEnabled ? double.infinity : 700),
              const Spacer(),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildVerticalContent(DeviceDto dev, DeceasedDto dec) {
    return Column(
      children: [
        _buildDeceasedName(dec, isCentered: true),
        const SizedBox(height: 30),
        
        if (dev.isMemorialPhotoEnabled) ...[
          Expanded(
            flex: 5,
            child: _buildPhotoSection(),
          ),
          const SizedBox(height: 40),
        ],

        // 하단 정보 섹션 (전체 중앙 정렬)
        Center(
          child: _buildInfoSection(dev, dec, width: 700),
        ),
        const Spacer(),
      ],
    );
  }

  // 상주 리스트를 [관계 성명] 형식으로 세로 나열하여 렌더링
  Widget _buildInfoSection(DeviceDto dev, DeceasedDto dec, {required double width}) {
    // 상주 리스트 관계별로 그룹화하여 세로 나열
    String mournerDisplay = "-";
    if (dec.mourners.isNotEmpty) {
      final Map<String, List<String>> grouped = {};
      for (var m in dec.mourners) {
        if (m.name == null || m.name!.isEmpty) continue;
        final relName = m.relationName ?? m.relation ?? '';
        grouped.putIfAbsent(relName, () => []).add(m.isChief ? "[상주] ${m.name}" : m.name!);
      }
      mournerDisplay = grouped.entries.map((e) => "${e.key}: ${e.value.join(', ')}").join("\n");
    } else if (dec.chiefMourner != null) {
      mournerDisplay = dec.chiefMourner!;
    }

    return SizedBox(
      width: width,
      child: Column(
        children: [
          _buildInfoRow("상 주", mournerDisplay),
          _buildInfoRow("발 인", _formatDate(dec.funeralDate)),
          _buildInfoRow("장 지", dec.burialDate != null ? _formatDate(dec.burialDate!) : "-"),
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
              ? Image.network(path, fit: BoxFit.contain)
              : Image.file(io.File(path), fit: BoxFit.contain),
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
        crossAxisAlignment: CrossAxisAlignment.start, // 상주가 여러 명일 때 라벨이 상단에 고정되도록 함
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
              style: const TextStyle(color: Colors.white, fontSize: 26, fontWeight: FontWeight.w500, height: 1.4), // height 추가로 줄간격 조절
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
      return "${date.year}년 ${date.month}월 ${date.day}일";
    } catch (e) {
      return dateStr;
    }
  }
}
