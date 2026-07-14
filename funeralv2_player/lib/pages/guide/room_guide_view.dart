import 'dart:io' as io;
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import '../player_shell.dart';
import 'room_guide_controller.dart';
import '../../models/device_models.dart';

/// [호실 입구 안내 뷰 위젯]
/// 특정 빈소(호실)의 정문에 매칭하여 배치하는 입구 안내 사이니지 화면입니다.
/// 고인명, 향년, 종교, 영정 이미지, 상주 목록, 발인일 및 장지 일정을 미려하게 표출합니다.
class RoomGuideView extends StatefulWidget {
  final String serverBaseUrl; // 통합 서버 Base URL
  final String deviceCode; // 장비 식별 코드
  final VoidCallback onOpenSettings; // 환경 설정 진입 콜백

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
  // 호실 데이터 및 미디어 로드를 관장하는 컨트롤러
  final RoomGuideController _controller = RoomGuideController();

  /// [위젯 초기 상태 설정]
  /// 컨트롤러의 `init`을 기동하여 데이터를 끌어오며, 비디오 로드 완료 콜백 수신 시 상태를 재갱신합니다.
  @override
  void initState() {
    super.initState();
    _controller.init(
      widget.serverBaseUrl,
      widget.deviceCode,
      () => setState(() {}),
    );
  }

  /// [자원 해제]
  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  /// [위젯 빌드]
  /// 컨트롤러 상태를 구독하며 로딩 및 에러 처리 후 공통 셸(`PlayerShell`)에 콘텐츠 위젯을 주입합니다.
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

  /// [콘텐츠 레이아웃 빌더]
  /// 호실 안내 정보(고인 미등록 시 빈소 준비 중 메시지)를 띄우며,
  /// 모니터 방향(가로/세로)에 맞는 반응형 하위 조립 함수로 분기합니다.
  Widget _buildContent(DeviceDto dev, DeceasedDto? dec) {
    if (dec == null) {
      return const Center(child: Text("빈소 준비 중입니다.", style: TextStyle(color: Colors.white70, fontSize: 32)));
    }

    final bool isPortrait = dev.displayOrientation == 'PORTRAIT';

    return Padding(
      padding: const EdgeInsets.all(40.0),
      child: Column(
        children: [
          // 1. 상단 호실 이름 뱃지
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

          // 2. 메인 안내판 영역
          Expanded(
            child: isPortrait ? _buildVerticalContent(dev, dec) : _buildHorizontalContent(dev, dec),
          ),
        ],
      ),
    );
  }

  /// [가로형 모니터 화면 구성]
  /// 좌측에 고인 보정 영정사진을 크게 배치하고, 우측에 고인명 정보 및 일정/상주 테이블을 나열합니다.
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

  /// [세로형 모니터 화면 구성]
  /// 위에서 아래 방향으로 고인명 -> 영정사진 -> 일정/상주 테이블을 일렬 배치하고 수평 중앙 정렬합니다.
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

  /// [상세 일정 및 상주 리스트 뷰 빌더]
  /// 상주를 관계별로 묶어 줄바꿈하고, 발인일 및 장지 행을 생성해 최종 테이블 위젯을 리턴합니다.
  Widget _buildInfoSection(DeviceDto dev, DeceasedDto dec, {required double width}) {
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

  /// [영정사진 렌더러]
  /// 캐싱된 로컬 파일 경로를 확인하여 존재하면 로컬 이미지 파일 위젯(`Image.file`)으로,
  /// Web 환경이거나 경로 로딩 중이면 기본 네트워크 이미지를 표출합니다.
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

  /// [고인명 및 세부 신원 빌더]
  /// 고인 한글 성함 뒤에 나이, 성별, 종교 정보 등을 포맷팅하여 그립니다.
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

  /// [테이블 행(Row) 컴포넌트 구성]
  /// 좌측에 금색 박스로 라벨을 얹고 우측에 흰색 큰 텍스트로 상세 값(상주 명단 등)을 매핑합니다.
  Widget _buildInfoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 12.0),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start, // 여러 줄 상주 텍스트 정렬 시 라벨 상단 고정
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
              style: const TextStyle(color: Colors.white, fontSize: 26, fontWeight: FontWeight.w500, height: 1.4),
            ),
          ),
        ],
      ),
    );
  }

  /// [날짜 포맷 가공 헬퍼]
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
