import 'dart:io' as io;
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import '../player_shell.dart';
import 'entrance_guide_controller.dart';
import '../../models/device_models.dart';

/// [입구 종합 안내 뷰 위젯]
/// 장례식장 로비나 층별 입구에 배치되는 대형 사이니지 화면입니다.
/// 현재 운영 중인 빈소의 호실 명칭, 상주, 고인, 발인 및 장지 정보를 그리드(Grid) 카드 형태로 표출합니다.
class EntranceGuideView extends StatefulWidget {
  final String serverBaseUrl; // 통합 서버 Base URL
  final String deviceCode; // 장비 식별 코드
  final VoidCallback onOpenSettings; // 환경 설정 진입 콜백

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
  // 화면 데이터 및 미디어를 총괄 제어하는 컨트롤러 인스턴스
  final EntranceGuideController _controller = EntranceGuideController();
  // 실시간 현재 시각 렌더링을 위한 1초 단위 타이머 스트림
  late Stream<DateTime> _timeStream;

  // 요일 명칭 매핑 상수
  static const List<String> _weekdays = ['월', '화', '수', '목', '금', '토', '일'];

  /// [위젯 초기 상태 셋업]
  /// 컨트롤러의 `init`을 기동하여 데이터를 조회하고, 매 1초마다 이벤트를 방출하는 날짜/시간 스트림을 생성합니다.
  @override
  void initState() {
    super.initState();
    print('[EntranceView] initState() 호출');
    _controller.init(
      widget.serverBaseUrl,
      widget.deviceCode,
      () => setState(() {}), // 백그라운드 비디오 첫 프레임 로드 완료 시 화면을 갱신합니다.
    );
    _timeStream = Stream.periodic(const Duration(seconds: 1), (_) => DateTime.now());
  }

  /// [자원 해제]
  @override
  void dispose() {
    print('[EntranceView] dispose() 호출');
    _controller.dispose();
    super.dispose();
  }

  /// [위젯 설정 갱신 대응]
  @override
  void didUpdateWidget(covariant EntranceGuideView oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.deviceCode != widget.deviceCode || oldWidget.serverBaseUrl != widget.serverBaseUrl) {
      _controller.init(
        widget.serverBaseUrl,
        widget.deviceCode,
        () => setState(() {}),
      );
    }
  }

  /// [날짜 포맷 헬퍼]
  /// DateTime 객체를 'YYYY년 MM월 DD일 (요일)' 형태의 문자열로 변환합니다.
  String _formatDate(DateTime dt) {
    final weekdayStr = _weekdays[dt.weekday - 1];
    return "${dt.year}년 ${dt.month.toString().padLeft(2, '0')}월 ${dt.day.toString().padLeft(2, '0')}일 ($weekdayStr)";
  }

  /// [시각 포맷 헬퍼]
  /// DateTime 객체를 'HH:MM:SS' 형태의 초 단위 시각 문자열로 변환합니다.
  String _formatTime(DateTime dt) {
    return "${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}:${dt.second.toString().padLeft(2, '0')}";
  }

  /// [행사 날짜 포맷 헬퍼]
  /// ISO8601 포맷 날짜 문자열을 시각 정보를 제외하고 'YYYY년 MM월 DD일'로 단축 가공합니다.
  String _formatDateTimeString(String dtStr) {
    try {
      final parsed = DateTime.parse(dtStr).toLocal();
      return "${parsed.year}년 ${parsed.month.toString().padLeft(2, '0')}월 ${parsed.day.toString().padLeft(2, '0')}일";
    } catch (_) {
      return dtStr;
    }
  }

  /// [위젯 빌드]
  /// 컨트롤러 상태 변경에 맞춰 뼈대 셸(`PlayerShell`)을 두르고,
  /// 층/건물 정보 헤더 및 호실 목록 그리드 카드를 조합하여 최종 렌더링합니다.
  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        final dev = _controller.device;
        print('[EntranceView] build() - isLoading=${_controller.isLoading}, hasDevice=${dev != null}, roomsCount=${_controller.guideRooms.length}');

        if (_controller.isLoading && dev == null) {
          return const Center(child: CircularProgressIndicator(color: Color(0xFFC0A060)));
        }

        if (dev == null) {
          return const Center(child: Text("장치 정보 없음", style: TextStyle(color: Colors.red, fontSize: 20)));
        }

        return PlayerShell(
          device: dev,
          playerService: _controller.playerService,
          onOpenSettings: widget.onOpenSettings,
          debugFileName: 'entrance_guide_view.dart',
          child: Container(
            padding: const EdgeInsets.all(24.0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // 1. 헤더 영역 (현재 위치 타이틀 및 실시간 시계)
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
                            maxCrossAxisExtent: 450, // 개별 카드의 최대 가로폭
                            mainAxisSpacing: 20, // 위아래 카드 간격
                            crossAxisSpacing: 20, // 좌우 카드 간격
                            childAspectRatio: 1.4, // 카드의 가로/세로 비율
                          ),
                          itemBuilder: (context, index) {
                            final room = _controller.guideRooms[index];
                            return _buildRoomCard(room); // 개별 카드 생성
                          },
                        ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  /// [헤더 영역 컴포넌트 빌더]
  /// 장비 설정(층 또는 건물)에 맞는 대메뉴 타이틀과 Stream 기반 실시간 시간 정보를 렌더링합니다.
  Widget _buildHeader(DeviceDto dev) {
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
              style: const TextStyle(color: Colors.white, fontSize: 28, fontWeight: FontWeight.bold, letterSpacing: 1.5),
            ),
          ],
        ),
        StreamBuilder<DateTime>(
          stream: _timeStream,
          builder: (context, snapshot) {
            final now = snapshot.data ?? DateTime.now();
            return Row(
              children: [
                Text(_formatDate(now), style: const TextStyle(color: Colors.white70, fontSize: 18)),
                const SizedBox(width: 16),
                Text(_formatTime(now), style: const TextStyle(color: Color(0xFFC5A880), fontSize: 22, fontWeight: FontWeight.bold, fontFamily: 'monospace')),
              ],
            );
          },
        ),
      ],
    );
  }

  /// [호실 안내 카드 빌더]
  /// 사용 중인 빈소와 비어 있는 빈소를 명확하게 시각적으로 구분하여 정보를 그립니다.
  /// 사용 중일 경우 고인 성함, 성별/나이, 대표상주, 상주 구성원, 발인일, 장지, 보정 영정사진을 표출합니다.
  Widget _buildRoomCard(EntranceGuideRoomDto room) {
    final hasDeceased = room.deceasedDetail != null;
    final deceased = room.deceasedDetail;

    return Container(
      decoration: BoxDecoration(
        color: const Color(0xAA1F2937),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: hasDeceased ? const Color(0x4DC5A880) : Colors.white10, width: 1.5),
        boxShadow: [BoxShadow(color: Colors.black.withOpacity(0.3), blurRadius: 10, offset: const Offset(0, 4))],
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(16),
        child: Column(
          children: [
            // 카드의 상단 헤더 (호실 명칭 및 현황 태그)
            Container(
              width: double.infinity,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
              color: hasDeceased ? const Color(0xFF1E2530) : const Color(0xFF111827),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(room.roomName, style: const TextStyle(color: Colors.white, fontSize: 20, fontWeight: FontWeight.bold)),
                  if (hasDeceased)
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                      decoration: BoxDecoration(
                        color: const Color(0xFFC5A880).withOpacity(0.2),
                        borderRadius: BorderRadius.circular(4),
                        border: Border.all(color: const Color(0xFFC5A880), width: 0.5),
                      ),
                      child: const Text("사용 중", style: TextStyle(color: Color(0xFFC5A880), fontSize: 12, fontWeight: FontWeight.bold)),
                    )
                  else
                    const Text("빈 소", style: TextStyle(color: Colors.white38, fontSize: 14, fontWeight: FontWeight.bold)),
                ],
              ),
            ),
            // 카드의 내용부 (고인/상주 신원 및 일정)
            Expanded(
              child: Padding(
                padding: const EdgeInsets.all(16.0),
                child: hasDeceased && deceased != null
                    ? Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          // 고인 영정사진 이미지 레이아웃
                          _buildMemorialPhoto(deceased),
                          const SizedBox(width: 16),
                          // 인적사항 및 행사 정보 레이아웃
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                Row(
                                  children: [
                                    const Text("故 ", style: TextStyle(color: Colors.white54, fontSize: 18, fontWeight: FontWeight.bold)),
                                    Text(deceased.name, style: const TextStyle(color: Colors.white, fontSize: 22, fontWeight: FontWeight.bold)),
                                    const SizedBox(width: 8),
                                    Text("(${deceased.age}세/${deceased.gender == 'MALE' ? '남' : '여'})", style: const TextStyle(color: Colors.white70, fontSize: 14)),
                                  ],
                                ),
                                const SizedBox(height: 6),
                                // 상주 구성원이 존재할 경우 라인 표출
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
                                            style: const TextStyle(color: Colors.white70, fontSize: 14, height: 1.4),
                                          ),
                                        ),
                                      ],
                                    ),
                                  )
                                else
                                  const Spacer(),
                                // 발인 및 장지 2열 배치
                                Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    if (deceased.funeralDate != null)
                                      _buildInfoRow("발인", _formatDateTimeString(deceased.funeralDate!)),
                                    if (deceased.burialDate != null)
                                      _buildInfoRow("장지", _formatDateTimeString(deceased.burialDate!)),
                                  ],
                                )
                              ],
                            ),
                          ),
                        ],
                      )
                    : const Center(child: Text("사용 가능한 장례가 없습니다.", style: TextStyle(color: Colors.white30, fontSize: 16))),
              ),
            ),
          ],
        ),
      ),
    );
  }

  /// [고인 영정사진 위젯 빌드]
  /// 보정된 편집 이미지([memorialEditedPhotoUrl])를 우선하여 띄우며,
  /// 엑박이 나거나 주소가 없을 경우 실루엣 아이콘 플레이스홀더를 매핑합니다.
  Widget _buildMemorialPhoto(DeceasedDto deceased) {
    final photoUrl = deceased.memorialEditedPhotoUrl ?? deceased.memorialPhotoUrl;
    final localPath = _controller.deceasedPhotoPaths[deceased.id];

    return Container(
      width: 80, height: 105,
      decoration: BoxDecoration(border: Border.all(color: const Color(0xFFC5A880), width: 1.5), borderRadius: BorderRadius.circular(8), color: Colors.black26),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(6),
        child: (localPath != null && localPath.isNotEmpty && !kIsWeb)
            ? Image.file(
                io.File(localPath),
                fit: BoxFit.cover,
                errorBuilder: (c, e, s) => Image.network(
                  "${widget.serverBaseUrl}$photoUrl",
                  fit: BoxFit.cover,
                  errorBuilder: (c, e, s) => _buildPlaceholderPhoto(),
                ),
              )
            : (photoUrl != null && photoUrl.isNotEmpty)
                ? Image.network("${widget.serverBaseUrl}$photoUrl", fit: BoxFit.cover, errorBuilder: (c, e, s) => _buildPlaceholderPhoto())
                : _buildPlaceholderPhoto(),
      ),
    );
  }

  /// [이미지 플레이스홀더 아이콘 빌드]
  Widget _buildPlaceholderPhoto() {
    return const Center(child: Icon(Icons.person_outline, color: Color(0xFFC5A880), size: 40));
  }

  /// [카드 하단 일정 정보 행(Row) 구성]
  Widget _buildInfoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(top: 4.0),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 1),
            decoration: BoxDecoration(color: Colors.black45, borderRadius: BorderRadius.circular(4)),
            child: Text(label, style: const TextStyle(color: Color(0xFFC5A880), fontSize: 11, fontWeight: FontWeight.bold)),
          ),
          const SizedBox(width: 8),
          Expanded(child: Text(value, style: const TextStyle(color: Colors.white, fontSize: 13), maxLines: 1, overflow: TextOverflow.ellipsis)),
        ],
      ),
    );
  }

  /// [상주 관계형 목록 문자열 포맷팅]
  /// 전체 상주 리스트를 순회하여 관계(상주, 자, 녀, 사위 등)별로 그룹핑한 뒤,
  /// '관계: 이름1, 이름2' 포맷의 줄바꿈 목록 텍스트로 합산합니다.
  String _getMournersString(List<MournerDto> mourners) {
    if (mourners.isEmpty) return "";
    final Map<String, List<String>> grouped = {};
    for (var m in mourners) {
      if (m.name == null || m.name!.isEmpty) continue;
      final relName = m.relationName ?? m.relation ?? '';
      // 대표 상주일 경우 이름 앞에 강조 표식을 삽입합니다.
      grouped.putIfAbsent(relName, () => []).add(m.isChief ? "[상주] ${m.name}" : m.name!);
    }
    return grouped.entries.map((e) => "${e.key}: ${e.value.join(', ')}").join("\n");
  }
}
