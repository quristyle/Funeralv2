import 'package:flutter/material.dart';
import '../player_shell.dart';
import 'entrance_guide_controller.dart';
import '../../models/device_models.dart';

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
  late Stream<DateTime> _timeStream;

  static const List<String> _weekdays = ['월', '화', '수', '목', '금', '토', '일'];

  @override
  void initState() {
    super.initState();
    _controller.init(
      widget.serverBaseUrl,
      widget.deviceCode,
      () => setState(() {}),
    );
    _timeStream = Stream.periodic(const Duration(seconds: 1), (_) => DateTime.now());
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  String _formatDate(DateTime dt) {
    final weekdayStr = _weekdays[dt.weekday - 1];
    return "${dt.year}년 ${dt.month.toString().padLeft(2, '0')}월 ${dt.day.toString().padLeft(2, '0')}일 ($weekdayStr)";
  }

  String _formatTime(DateTime dt) {
    return "${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}:${dt.second.toString().padLeft(2, '0')}";
  }

  String _formatDateTimeString(String dtStr) {
    try {
      final parsed = DateTime.parse(dtStr).toLocal();
      return "${parsed.year}년 ${parsed.month.toString().padLeft(2, '0')}월 ${parsed.day.toString().padLeft(2, '0')}일";
    } catch (_) {
      return dtStr;
    }
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        final dev = _controller.device;

        if (_controller.isLoading && dev == null) {
          return const Scaffold(
            backgroundColor: Color(0xFF111827),
            body: Center(
              child: CircularProgressIndicator(color: Colors.white),
            ),
          );
        }

        if (dev == null) {
          return const Scaffold(
            backgroundColor: Color(0xFF111827),
            body: Center(
              child: Text(
                "데이터 로드 실패",
                style: TextStyle(color: Colors.white, fontSize: 20),
              ),
            ),
          );
        }

        return PlayerShell(
          device: dev,
          playerService: _controller.playerService,
          onOpenSettings: widget.onOpenSettings,
          debugFileName: 'entrance_guide_view.dart',
          child: Scaffold(
            backgroundColor: Colors.transparent,
            body: SafeArea(
              child: Padding(
                padding: const EdgeInsets.all(24.0),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // 1. 헤더 영역 (타이틀 & 실시간 시계)
                    _buildHeader(dev),
                    const SizedBox(height: 24),

                    // 2. 호실 그리드 목록 영역
                    Expanded(
                      child: _controller.guideRooms.isEmpty
                          ? const Center(
                              child: Text(
                                "안내할 호실 정보가 없습니다.",
                                style: TextStyle(color: Colors.white60, fontSize: 24),
                              ),
                            )
                          : GridView.builder(
                              itemCount: _controller.guideRooms.length,
                              gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
                                maxCrossAxisExtent: 450,
                                mainAxisSpacing: 20,
                                crossAxisSpacing: 20,
                                childAspectRatio: 1.4,
                              ),
                              itemBuilder: (context, index) {
                                final room = _controller.guideRooms[index];
                                return _buildRoomCard(room);
                              },
                            ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        );
      },
    );
  }

  Widget _buildHeader(DeviceDto dev) {
    // 층 정보 우선, 없으면 건물 정보, 그것도 없으면 장비명 사용
    String locationTitle = "";
    if (dev.floorId != null && dev.floorId!.isNotEmpty) {
      locationTitle = dev.floorName ?? "층";
    } else if (dev.buildingId != null && dev.buildingId!.isNotEmpty) {
      locationTitle = dev.buildingName ?? "건물";
    } else {
      locationTitle = dev.name;
    }

    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Row(
          children: [
            Container(
              width: 6,
              height: 32,
              decoration: BoxDecoration(
                color: const Color(0xFFC5A880),
                borderRadius: BorderRadius.circular(3),
              ),
            ),
            const SizedBox(width: 12),
            Text(
              "$locationTitle 안내",
              style: const TextStyle(
                color: Colors.white,
                fontSize: 28,
                fontWeight: FontWeight.bold,
                letterSpacing: 1.5,
              ),
            ),
          ],
        ),
        StreamBuilder<DateTime>(
          stream: _timeStream,
          builder: (context, snapshot) {
            final now = snapshot.data ?? DateTime.now();
            return Row(
              children: [
                Text(
                  _formatDate(now),
                  style: const TextStyle(color: Colors.white70, fontSize: 18),
                ),
                const SizedBox(width: 16),
                Text(
                  _formatTime(now),
                  style: const TextStyle(
                    color: Color(0xFFC5A880),
                    fontSize: 22,
                    fontWeight: FontWeight.bold,
                    fontFamily: 'monospace',
                  ),
                ),
              ],
            );
          },
        ),
      ],
    );
  }

  Widget _buildRoomCard(EntranceGuideRoomDto room) {
    final hasDeceased = room.deceasedDetail != null;
    final deceased = room.deceasedDetail;

    return Container(
      decoration: BoxDecoration(
        color: const Color(0xAA1F2937),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: hasDeceased ? const Color(0x4DC5A880) : Colors.white10,
          width: 1.5,
        ),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.3),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(16),
        child: Column(
          children: [
            Container(
              width: double.infinity,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
              color: hasDeceased ? const Color(0xFF1E2530) : const Color(0xFF111827),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    room.roomName,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 20,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  if (hasDeceased)
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                      decoration: BoxDecoration(
                        color: const Color(0xFFC5A880).withOpacity(0.2),
                        borderRadius: BorderRadius.circular(4),
                        border: Border.all(color: const Color(0xFFC5A880), width: 0.5),
                      ),
                      child: const Text(
                        "사용 중",
                        style: TextStyle(
                          color: Color(0xFFC5A880),
                          fontSize: 12,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    )
                  else
                    const Text(
                      "빈 소",
                      style: TextStyle(
                        color: Colors.white38,
                        fontSize: 14,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                ],
              ),
            ),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.all(16.0),
                child: hasDeceased && deceased != null
                    ? Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          _buildMemorialPhoto(deceased),
                          const SizedBox(width: 16),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                Row(
                                  children: [
                                    const Text(
                                      "故 ",
                                      style: TextStyle(
                                        color: Colors.white54,
                                        fontSize: 18,
                                        fontWeight: FontWeight.bold,
                                      ),
                                    ),
                                    Text(
                                      deceased.name,
                                      style: const TextStyle(
                                        color: Colors.white,
                                        fontSize: 22,
                                        fontWeight: FontWeight.bold,
                                      ),
                                    ),
                                    const SizedBox(width: 8),
                                    Text(
                                      "(${deceased.age}세/${deceased.gender == 'MALE' ? '남' : '여'})",
                                      style: const TextStyle(
                                        color: Colors.white70,
                                        fontSize: 14,
                                      ),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 6),
                                if (_getMournersString(deceased.mourners).isNotEmpty)
                                  Expanded(
                                    child: Row(
                                      crossAxisAlignment: CrossAxisAlignment.start,
                                      children: [
                                        Container(
                                          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 1),
                                          margin: const EdgeInsets.only(top: 2),
                                          decoration: BoxDecoration(
                                            color: const Color(0xFFC5A880).withOpacity(0.1),
                                            border: Border.all(color: const Color(0xFFC5A880).withOpacity(0.5)),
                                            borderRadius: BorderRadius.circular(3),
                                          ),
                                          child: const Text("상주", style: TextStyle(color: Color(0xFFC5A880), fontSize: 10, fontWeight: FontWeight.bold)),
                                        ),
                                        const SizedBox(width: 8),
                                        Expanded(
                                          child: Text(
                                            _getMournersString(deceased.mourners),
                                            maxLines: 5,
                                            overflow: TextOverflow.ellipsis,
                                            style: const TextStyle(
                                              color: Colors.white70,
                                              fontSize: 14,
                                              height: 1.4,
                                            ),
                                          ),
                                        ),
                                      ],
                                    ),
                                  )
                                else
                                  const Spacer(),
                                Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    if (deceased.funeralDate != null)
                                      _buildInfoRow(
                                        "발인",
                                        _formatDateTimeString(deceased.funeralDate!),
                                      ),
                                    if (deceased.burialDate != null)
                                      _buildInfoRow(
                                        "장지",
                                        _formatDateTimeString(deceased.burialDate!),
                                      ),
                                  ],
                                )
                              ],
                            ),
                          ),
                        ],
                      )
                    : const Center(
                        child: Text(
                          "사용 가능한 장례가 없습니다.",
                          style: TextStyle(color: Colors.white30, fontSize: 16),
                        ),
                      ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildMemorialPhoto(DeceasedDto deceased) {
    final photoUrl = deceased.memorialEditedPhotoUrl ?? deceased.memorialPhotoUrl;

    return Container(
      width: 80,
      height: 105,
      decoration: BoxDecoration(
        border: Border.all(color: const Color(0xFFC5A880), width: 1.5),
        borderRadius: BorderRadius.circular(8),
        color: Colors.black26,
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(6),
        child: photoUrl != null && photoUrl.isNotEmpty
            ? Image.network(
                "${widget.serverBaseUrl}$photoUrl",
                fit: BoxFit.cover,
                errorBuilder: (context, error, stackTrace) => _buildPlaceholderPhoto(),
              )
            : _buildPlaceholderPhoto(),
      ),
    );
  }

  Widget _buildPlaceholderPhoto() {
    return const Center(
      child: Icon(
        Icons.person_outline,
        color: Color(0xFFC5A880),
        size: 40,
      ),
    );
  }

  Widget _buildInfoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(top: 4.0),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 1),
            decoration: BoxDecoration(
              color: Colors.black45,
              borderRadius: BorderRadius.circular(4),
            ),
            child: Text(
              label,
              style: const TextStyle(color: Color(0xFFC5A880), fontSize: 11, fontWeight: FontWeight.bold),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(color: Colors.white, fontSize: 13),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ],
      ),
    );
  }

  String _getMournersString(List<MournerDto> mourners) {
    if (mourners.isEmpty) return "";

    final Map<String, List<String>> grouped = {};
    for (var m in mourners) {
      if (m.name == null || m.name!.isEmpty) continue;
      final relName = m.relationName ?? m.relation ?? '';
      grouped.putIfAbsent(relName, () => []).add(m.isChief ? "[상주] ${m.name}" : m.name!);
    }

    return grouped.entries.map((e) => "${e.key}: ${e.value.join(', ')}").join("\n");
  }
}
