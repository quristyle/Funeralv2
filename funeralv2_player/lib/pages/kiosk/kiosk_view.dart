import 'package:flutter/material.dart';
import '../player_shell.dart';
import 'kiosk_controller.dart';
import '../../models/device_models.dart';

/// [종합 안내 키오스크 뷰 위젯]
/// 방문 조문객들을 위한 터치 스크린 방식의 종합 안내판 화면입니다.
/// 전체 호실 찾기/검색(층별 필터 및 6개 카드 단위 터치 페이지네이션) 및
/// 주차 안내(약도 이미지 터치 슬라이더)의 두 가지 탭 기능을 탑재하고 있습니다.
class KioskView extends StatefulWidget {
  final String serverBaseUrl; // 통합 서버 Base URL
  final String deviceCode; // 장비 식별 코드
  final VoidCallback onOpenSettings; // 환경 설정 진입 콜백

  const KioskView({
    super.key,
    required this.serverBaseUrl,
    required this.deviceCode,
    required this.onOpenSettings,
  });

  @override
  State<KioskView> createState() => _KioskViewState();
}

class _KioskViewState extends State<KioskView> {
  // 키오스크 데이터를 관리하는 비즈니스 로직 컨트롤러
  final KioskController _controller = KioskController();

  // 현재 선택된 상단 탭 키 ("ROOM_GUIDE" 또는 "PARKING_GUIDE")
  String _selectedTab = "ROOM_GUIDE";

  // 현재 선택된 필터용 층 이름 ("전체" 또는 특정 층 명칭)
  String _selectedFloor = "전체";

  // 호실 그리드 페이징 인덱스 (0-indexed)
  int _currentPage = 0;
  // 한 화면에 표출할 호실 카드의 수 (터치 환경을 고려해 6개로 스로틀)
  static const int _itemsPerPage = 6;

  // 주차 약도 슬라이드를 제어하는 페이지 뷰 컨트롤러
  late PageController _parkingPageController;
  // 현재 슬라이딩된 주차 이미지 인덱스
  int _currentParkingPageIndex = 0;

  /// [위젯 초기 구동]
  /// 컨트롤러 `init`를 동작시켜 장비 및 키오스크 데이터를 서버에서 호출해 오고,
  /// 주차장 페이지 제어를 위한 `PageController` 세션을 엽니다.
  @override
  void initState() {
    super.initState();
    _parkingPageController = PageController(initialPage: 0);
    _controller.init(
      widget.serverBaseUrl,
      widget.deviceCode,
      () => setState(() {}), // 배경 비디오 첫 프레임 준비 시 화면을 리프레시합니다.
    );
  }

  /// [자원 소멸]
  @override
  void dispose() {
    _parkingPageController.dispose();
    _controller.dispose();
    super.dispose();
  }

  /// [위젯 빌드]
  /// 로딩 상태 분기 및 공통 셸(`PlayerShell`)을 바탕에 입히고 탭에 맞는 메인 콘텐츠 뷰를 주입합니다.
  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        final dev = _controller.device;

        if (_controller.isLoading && dev == null) {
          return const Scaffold(
            backgroundColor: Colors.black,
            body: Center(
              child: CircularProgressIndicator(color: Color(0xFFC5A880)),
            ),
          );
        }
        if (dev == null) {
          return const Scaffold(
            backgroundColor: Colors.black,
            body: Center(
              child: Text(
                "데이터 로드 실패",
                style: TextStyle(color: Colors.white, fontSize: 18),
              ),
            ),
          );
        }

        return PlayerShell(
          device: dev,
          playerService: _controller.playerService,
          onOpenSettings: widget.onOpenSettings,
          debugFileName: 'kiosk_view.dart',
          child: Scaffold(
            backgroundColor: Colors.transparent, // 배경 비디오 노출을 위해 투명 처리
            body: SafeArea(
              child: Column(
                children: [
                  // 1. 상단 타이틀 및 대형 터치형 탭 버튼 바
                  _buildHeader(dev),
                  // 2. 탭에 매핑된 상세 콘텐츠 영역
                  Expanded(
                    child: _selectedTab == "ROOM_GUIDE"
                        ? _buildRoomGuideContent()
                        : _buildParkingGuideContent(),
                  ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }

  /// [상단 헤더 영역 빌더]
  /// 장례식장 명칭과 터치 기반의 2단 대형 탭 버튼("호실안내", "주차안내")을 렌더링합니다.
  Widget _buildHeader(DeviceDto dev) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 40, vertical: 20),
      decoration: BoxDecoration(
        color: Colors.black.withOpacity(0.6),
        border: const Border(
          bottom: BorderSide(color: Color(0xFFC5A880), width: 1.5),
        ),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            children: [
              const Icon(Icons.business, color: Color(0xFFC5A880), size: 36),
              const SizedBox(width: 15),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    dev.buildingName ?? dev.name,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 24,
                      fontWeight: FontWeight.bold,
                      letterSpacing: 1.2,
                    ),
                  ),
                  const Text(
                    "종합 안내 키오스크",
                    style: TextStyle(color: Colors.white60, fontSize: 13),
                  ),
                ],
              ),
            ],
          ),
          Row(
            children: [
              _buildTabButton("호실안내(검색)", "ROOM_GUIDE"),
              const SizedBox(width: 20),
              _buildTabButton("주차안내", "PARKING_GUIDE"),
            ],
          ),
        ],
      ),
    );
  }

  /// [개별 탭 전환 버튼 빌더]
  Widget _buildTabButton(String label, String tabKey) {
    final isSelected = _selectedTab == tabKey;
    return InkWell(
      onTap: () {
        setState(() {
          _selectedTab = tabKey;
          _selectedFloor = "전체";
          _currentPage = 0;
        });
      },
      splashColor: const Color(0xFFC5A880).withOpacity(0.3),
      borderRadius: BorderRadius.circular(30),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 35, vertical: 15),
        decoration: BoxDecoration(
          color: isSelected ? const Color(0xFFC5A880) : Colors.white.withOpacity(0.05),
          borderRadius: BorderRadius.circular(30),
          border: Border.all(
            color: isSelected ? const Color(0xFFC5A880) : Colors.white24,
            width: 1.5,
          ),
          boxShadow: isSelected
              ? [
                  BoxShadow(
                    color: const Color(0xFFC5A880).withOpacity(0.4),
                    blurRadius: 10,
                    offset: const Offset(0, 4),
                  )
                ]
              : null,
        ),
        child: Text(
          label,
          style: TextStyle(
            color: isSelected ? Colors.black : Colors.white,
            fontSize: 18,
            fontWeight: FontWeight.bold,
          ),
        ),
      ),
    );
  }

  /// [호실 안내(검색) 콘텐츠 영역 빌더]
  /// 좌측에는 층별 필터 터치 패널을 배치하고, 우측에는 해당 층에 안치된 빈소 카드 그리드를 페이징 컨트롤러와 함께 렌더링합니다.
  Widget _buildRoomGuideContent() {
    if (_controller.rooms.isEmpty) {
      return const Center(
        child: Text(
          "배정된 호실 정보가 없습니다.",
          style: TextStyle(color: Colors.white60, fontSize: 20),
        ),
      );
    }

    // 1) 수집된 전체 호실 목록에서 존재하는 모든 '층 명칭'을 추출하고 중복을 제거합니다.
    final floors = <String>["전체"];
    for (var r in _controller.rooms) {
      if (r.floorName.isNotEmpty && !floors.contains(r.floorName)) {
        floors.add(r.floorName);
      }
    }
    // '전체' 항목이 맨 위에 오도록 조절하며 층별 이름을 정렬합니다.
    floors.sort((a, b) {
      if (a == "전체") return -1;
      if (b == "전체") return 1;
      return a.compareTo(b);
    });

    // 2) 선택된 층을 기준으로 호실 목록을 필터링합니다.
    final filteredRooms = _controller.rooms.where((r) {
      if (_selectedFloor == "전체") return true;
      return r.floorName == _selectedFloor;
    }).toList();

    // 3) 필터링된 결과물에 대해 6개 카드 단위로 페이징 연산을 처리합니다.
    final totalRooms = filteredRooms.length;
    final maxPage = (totalRooms / _itemsPerPage).ceil();
    final startIndex = _currentPage * _itemsPerPage;
    final endIndex = (startIndex + _itemsPerPage < totalRooms)
        ? startIndex + _itemsPerPage
        : totalRooms;
    final paginatedRooms = (startIndex < totalRooms)
        ? filteredRooms.sublist(startIndex, endIndex)
        : <EntranceGuideRoomDto>[];

    return Row(
      children: [
        // [사이드 패널] 층 필터 터치 패널 (키오스크 환경이므로 드래그 스크롤을 억제하고 고정 버튼 터치 구조 적용)
        Container(
          width: 220,
          padding: const EdgeInsets.symmetric(vertical: 20, horizontal: 15),
          decoration: BoxDecoration(
            color: Colors.black.withOpacity(0.4),
            border: const Border(
              right: BorderSide(color: Colors.white10, width: 1),
            ),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Padding(
                padding: EdgeInsets.only(left: 10, bottom: 15),
                child: Text(
                  "층별 안내",
                  style: TextStyle(
                    color: Color(0xFFC5A880),
                    fontSize: 16,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
              Expanded(
                child: ListView.builder(
                  physics: const NeverScrollableScrollPhysics(), // 드래그 억제
                  itemCount: floors.length,
                  itemBuilder: (context, index) {
                    final floorName = floors[index]!;
                    final isSelected = _selectedFloor == floorName;
                    return Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: InkWell(
                        onTap: () {
                          setState(() {
                            _selectedFloor = floorName;
                            _currentPage = 0; // 필터링 조건 변경 시 페이지는 0번으로 초기화
                          });
                        },
                        borderRadius: BorderRadius.circular(10),
                        child: AnimatedContainer(
                          duration: const Duration(milliseconds: 150),
                          padding: const EdgeInsets.symmetric(vertical: 18, horizontal: 15),
                          decoration: BoxDecoration(
                            color: isSelected
                                ? const Color(0xFFC5A880).withOpacity(0.2)
                                : Colors.white.withOpacity(0.03),
                            borderRadius: BorderRadius.circular(10),
                            border: Border.all(
                              color: isSelected ? const Color(0xFFC5A880) : Colors.white10,
                              width: 1.5,
                            ),
                          ),
                          child: Center(
                            child: Text(
                              floorName,
                              style: TextStyle(
                                color: isSelected ? const Color(0xFFC5A880) : Colors.white70,
                                fontSize: 18,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                        ),
                      ),
                    );
                  },
                ),
              ),
            ],
          ),
        ),

        // [메인 콘텐츠 패널] 호실 정보 그리드 렌더링 및 이전/다음 페이징 터치 바
        Expanded(
          child: Container(
            padding: const EdgeInsets.all(30),
            color: Colors.black.withOpacity(0.1),
            child: Column(
              children: [
                Expanded(
                  child: paginatedRooms.isEmpty
                      ? const Center(
                          child: Text(
                            "해당 층에 운영 중인 빈소가 없습니다.",
                            style: TextStyle(color: Colors.white30, fontSize: 18),
                          ),
                        )
                      : LayoutBuilder(
                          builder: (context, constraints) {
                            // 모니터 해상도 및 상위 레이아웃 가용 너비/높이에 맞춰 가로 3열, 세로 2줄 카드의 비율을 자동 보정합니다.
                            final double availableWidth = constraints.maxWidth;
                            final double availableHeight = constraints.maxHeight;

                            const int crossAxisCount = 3;
                            const double crossAxisSpacing = 18.0;
                            const double mainAxisSpacing = 18.0;
                            const int rowCount = 2;

                            final double cardWidth = (availableWidth - (crossAxisSpacing * (crossAxisCount - 1))) / crossAxisCount;
                            final double cardHeight = (availableHeight - (mainAxisSpacing * (rowCount - 1))) / rowCount;
                            final double dynamicAspectRatio = (cardHeight > 0) ? (cardWidth / cardHeight) : 1.05;

                            return GridView.builder(
                              physics: const NeverScrollableScrollPhysics(), // 드래그 차단
                              gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                                crossAxisCount: crossAxisCount,
                                childAspectRatio: dynamicAspectRatio,
                                crossAxisSpacing: crossAxisSpacing,
                                mainAxisSpacing: mainAxisSpacing,
                              ),
                              itemCount: paginatedRooms.length,
                              itemBuilder: (context, index) {
                                final room = paginatedRooms[index];
                                return _buildRoomCard(room); // 개별 호실 카드 빌드
                              },
                            );
                          },
                        ),
                ),
                const SizedBox(height: 20),
                // 이전/다음 페이지네이션 터치 버튼 (페이지가 2개 이상일 때 노출)
                if (maxPage > 1)
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      InkWell(
                        onTap: _currentPage > 0
                            ? () => setState(() => _currentPage--)
                            : null,
                        borderRadius: BorderRadius.circular(10),
                        child: Container(
                          padding: const EdgeInsets.symmetric(horizontal: 25, vertical: 15),
                          decoration: BoxDecoration(
                            color: _currentPage > 0
                                ? Colors.white.withOpacity(0.1)
                                : Colors.white.withOpacity(0.02),
                            borderRadius: BorderRadius.circular(10),
                            border: Border.all(
                              color: _currentPage > 0 ? Colors.white30 : Colors.white10,
                              width: 1,
                            ),
                          ),
                          child: Row(
                            children: [
                              Icon(
                                Icons.arrow_back_ios,
                                size: 16,
                                color: _currentPage > 0 ? Colors.white : Colors.white30,
                              ),
                              const SizedBox(width: 8),
                              Text(
                                "이전",
                                style: TextStyle(
                                  color: _currentPage > 0 ? Colors.white : Colors.white30,
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                      const SizedBox(width: 40),
                      Text(
                        "${_currentPage + 1} / $maxPage",
                        style: const TextStyle(
                          color: Color(0xFFC5A880),
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      const SizedBox(width: 40),
                      InkWell(
                        onTap: (_currentPage + 1) < maxPage
                            ? () => setState(() => _currentPage++)
                            : null,
                        borderRadius: BorderRadius.circular(10),
                        child: Container(
                          padding: const EdgeInsets.symmetric(horizontal: 25, vertical: 15),
                          decoration: BoxDecoration(
                            color: (_currentPage + 1) < maxPage
                                ? Colors.white.withOpacity(0.1)
                                : Colors.white.withOpacity(0.02),
                            borderRadius: BorderRadius.circular(10),
                            border: Border.all(
                              color: (_currentPage + 1) < maxPage ? Colors.white30 : Colors.white10,
                              width: 1,
                            ),
                          ),
                          child: Row(
                            children: [
                              Text(
                                "다음",
                                style: TextStyle(
                                  color: (_currentPage + 1) < maxPage ? Colors.white : Colors.white30,
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              const SizedBox(width: 8),
                              Icon(
                                Icons.arrow_forward_ios,
                                size: 16,
                                color: (_currentPage + 1) < maxPage ? Colors.white : Colors.white30,
                              ),
                            ],
                          ),
                        ),
                      ),
                    ],
                  ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  /// [개별 호실 카드 빌더 (Glassmorphism 적용)]
  /// 호실 사용 상태에 따라 색상을 분기하고, 사용 중인 호실의 고인, 대표상주, 발인일정, 영정 사진을 밀도 높게 채웁니다.
  Widget _buildRoomCard(EntranceGuideRoomDto room) {
    final deceased = room.deceasedDetail;
    final isOccupied = deceased != null;

    return Container(
      decoration: BoxDecoration(
        color: isOccupied ? Colors.white.withOpacity(0.06) : Colors.white.withOpacity(0.02),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: isOccupied ? const Color(0xFFC5A880).withOpacity(0.6) : Colors.white10,
          width: 1.2,
        ),
        boxShadow: isOccupied
            ? [
                BoxShadow(
                  color: Colors.black.withOpacity(0.3),
                  blurRadius: 10,
                  offset: const Offset(0, 3),
                )
              ]
            : null,
      ),
      child: Column(
        children: [
          // 1) 카드 내부 호실명 표시 상단 띠
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
            decoration: BoxDecoration(
              color: isOccupied
                  ? const Color(0xFFC5A880).withOpacity(0.15)
                  : Colors.white.withOpacity(0.02),
              borderRadius: const BorderRadius.only(
                topLeft: Radius.circular(10),
                topRight: Radius.circular(10),
              ),
              border: Border(
                bottom: BorderSide(
                  color: isOccupied ? const Color(0xFFC5A880).withOpacity(0.3) : Colors.white10,
                  width: 1,
                ),
              ),
            ),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    const Icon(Icons.door_sliding, color: Color(0xFFC5A880), size: 18),
                    const SizedBox(width: 6),
                    Text(
                      room.roomName,
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 16,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ],
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(
                    color: isOccupied
                        ? const Color(0xFFC5A880).withOpacity(0.3)
                        : Colors.white.withOpacity(0.05),
                    borderRadius: BorderRadius.circular(4),
                  ),
                  child: Text(
                    isOccupied ? "사용 중" : "준비 중",
                    style: TextStyle(
                      color: isOccupied ? const Color(0xFFC5A880) : Colors.white30,
                      fontSize: 11,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
              ],
            ),
          ),
          // 2) 카드 내부 바디 영역 (고인 이미지 + 세부 사항 인적사항 목록)
          Expanded(
            child: Padding(
              padding: const EdgeInsets.all(12.0),
              child: isOccupied
                  ? Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        _buildMemorialPhoto(deceased),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                crossAxisAlignment: CrossAxisAlignment.baseline,
                                textBaseline: TextBaseline.alphabetic,
                                children: [
                                  Text(
                                    "故 ${deceased.name}",
                                    style: const TextStyle(
                                      color: Colors.white,
                                      fontSize: 17,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                  const SizedBox(width: 6),
                                  Text(
                                    "${deceased.gender ?? ''}/${deceased.age ?? ''}",
                                    style: const TextStyle(
                                      color: Colors.white70,
                                      fontSize: 11,
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 4),
                              Expanded(
                                child: Text(
                                  _getMournersString(deceased.mourners),
                                  maxLines: 2,
                                  overflow: TextOverflow.ellipsis,
                                  style: const TextStyle(
                                    color: Colors.white60,
                                    fontSize: 11,
                                    height: 1.25,
                                  ),
                                ),
                              ),
                              if (deceased.funeralDate != null)
                                _buildInfoRow(
                                  "발인",
                                  _formatDateTimeString(deceased.funeralDate!),
                                ),
                              if (deceased.burialDate != null)
                                _buildInfoRow(
                                  "장지",
                                  deceased.burialDate ?? '',
                                ),
                            ],
                          ),
                        ),
                      ],
                    )
                  : const Center(
                      child: Text(
                        "현재 사용 대기 중인 호실입니다.",
                        style: TextStyle(color: Colors.white24, fontSize: 13),
                      ),
                    ),
            ),
          ),
        ],
      ),
    );
  }

  /// [고인 보정 영정 이미지 컴포넌트 빌더]
  Widget _buildMemorialPhoto(DeceasedDto deceased) {
    final photoUrl = deceased.memorialEditedPhotoUrl ?? deceased.memorialPhotoUrl;

    return Container(
      width: 60,
      height: 80,
      decoration: BoxDecoration(
        border: Border.all(color: const Color(0xFFC5A880), width: 1.0),
        borderRadius: BorderRadius.circular(5),
        color: Colors.black38,
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(4),
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

  /// [영정 실루엣 플레이스홀더]
  Widget _buildPlaceholderPhoto() {
    return const Center(
      child: Icon(
        Icons.person_outline,
        color: Color(0xFFC5A880),
        size: 26,
      ),
    );
  }

  /// [카드 내부 한 줄 세부일정 항목 빌더]
  Widget _buildInfoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(top: 3.0),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 1),
            decoration: BoxDecoration(
              color: Colors.black45,
              borderRadius: BorderRadius.circular(3),
            ),
            child: Text(
              label,
              style: const TextStyle(color: Color(0xFFC5A880), fontSize: 9, fontWeight: FontWeight.bold),
            ),
          ),
          const SizedBox(width: 5),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(color: Colors.white, fontSize: 11),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ],
      ),
    );
  }

  /// [상주 관계형 라인 텍스트 압축화 포맷터]
  String _getMournersString(List<MournerDto> mourners) {
    if (mourners.isEmpty) return "등록된 상주 정보 없음";

    final sorted = List<MournerDto>.from(mourners);
    sorted.sort((a, b) {
      if (a.isChief && !b.isChief) return -1;
      if (!a.isChief && b.isChief) return 1;
      return 0;
    });

    final List<String> parts = [];
    for (var m in sorted) {
      if (m.name != null && m.name!.isNotEmpty) {
        final rel = (m.relationName != null && m.relationName!.isNotEmpty) ? m.relationName : (m.relation ?? '');
        if (m.isChief) {
          parts.add("[상주] ${m.name}($rel)");
        } else {
          parts.add("${m.name}($rel)");
        }
      }
    }
    return parts.join(", ");
  }

  /// [일정 날짜/시간 가공 헬퍼]
  String _formatDateTimeString(String dateTimeStr) {
    try {
      final dt = DateTime.parse(dateTimeStr).toLocal();
      final weekdays = ["월", "화", "수", "목", "금", "토", "일"];
      final yyyy = dt.year.toString();
      final mm = dt.month.toString().padLeft(2, '0');
      final dd = dt.day.toString().padLeft(2, '0');
      final e = weekdays[dt.weekday - 1];
      final hh = dt.hour.toString().padLeft(2, '0');
      final min = dt.minute.toString().padLeft(2, '0');
      return "$yyyy-$mm-$dd ($e) $hh:$min";
    } catch (e) {
      return dateTimeStr;
    }
  }

  /// [주차 안내 탭 콘텐츠 영역 빌더]
  /// 서버에 등록된 주차 약도 사진 목록을 PageView 기반 터치 슬라이더(좌우 넘김 화살표 제공)로 매핑합니다.
  Widget _buildParkingGuideContent() {
    final parkingPhotos = _controller.parkingPhotos;

    if (parkingPhotos.isEmpty) {
      return Center(
        child: Container(
          margin: const EdgeInsets.symmetric(horizontal: 100, vertical: 50),
          padding: const EdgeInsets.all(50),
          decoration: BoxDecoration(
            color: Colors.white.withOpacity(0.03),
            borderRadius: BorderRadius.circular(20),
            border: Border.all(color: const Color(0xFFC5A880).withOpacity(0.3), width: 1.5),
          ),
          child: const Column(
            mainAxisSize: MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(Icons.local_parking, size: 100, color: Color(0xFFC5A880)),
              SizedBox(height: 30),
              Text(
                "주차안내 준비",
                style: TextStyle(
                  color: Colors.white,
                  fontSize: 32,
                  fontWeight: FontWeight.bold,
                  letterSpacing: 2,
                ),
              ),
              SizedBox(height: 15),
              Text(
                "서비스 준비 중입니다. 이용에 불편을 드려 죄송합니다.",
                style: TextStyle(color: Colors.white30, fontSize: 16),
              ),
            ],
          ),
        ),
      );
    }

    return Stack(
      children: [
        // 1) 대형 약도 이미지 롤링 뷰어 (PageView 적용, 터치 슬라이딩 스와이프 제어)
        Positioned.fill(
          child: Container(
            color: Colors.black45,
            padding: const EdgeInsets.symmetric(horizontal: 100, vertical: 40),
            child: PageView.builder(
              controller: _parkingPageController,
              physics: const NeverScrollableScrollPhysics(), // 드래그 완전 방지
              itemCount: parkingPhotos.length,
              onPageChanged: (index) {
                setState(() {
                  _currentParkingPageIndex = index;
                });
              },
              itemBuilder: (context, index) {
                final photoUrl = parkingPhotos[index];
                return ClipRRect(
                  borderRadius: BorderRadius.circular(15),
                  child: Container(
                    decoration: BoxDecoration(
                      border: Border.all(color: const Color(0xFFC5A880).withOpacity(0.5), width: 1.5),
                    ),
                    child: Image.network(
                      "${widget.serverBaseUrl}$photoUrl",
                      fit: BoxFit.contain,
                      errorBuilder: (context, error, stackTrace) => const Center(
                        child: Icon(Icons.broken_image, size: 80, color: Colors.white24),
                      ),
                    ),
                  ),
                );
              },
            ),
          ),
        ),

        // 2) 이전 이미지 터치 화살표 (첫 페이지 아닐 때 표출)
        if (_currentParkingPageIndex > 0)
          Positioned(
            left: 30,
            top: 0,
            bottom: 0,
            child: Center(
              child: InkWell(
                onTap: () {
                  _parkingPageController.previousPage(
                    duration: const Duration(milliseconds: 300),
                    curve: Curves.easeInOut,
                  );
                },
                borderRadius: BorderRadius.circular(50),
                child: Container(
                  width: 60,
                  height: 60,
                  decoration: BoxDecoration(
                    color: Colors.black.withOpacity(0.6),
                    shape: BoxShape.circle,
                    border: Border.all(color: const Color(0xFFC5A880), width: 1.5),
                  ),
                  child: const Icon(Icons.arrow_back_ios_new, color: Color(0xFFC5A880), size: 28),
                ),
              ),
            ),
          ),

        // 3) 다음 이미지 터치 화살표 (마지막 페이지 아닐 때 표출)
        if (_currentParkingPageIndex < parkingPhotos.length - 1)
          Positioned(
            right: 30,
            top: 0,
            bottom: 0,
            child: Center(
              child: InkWell(
                onTap: () {
                  _parkingPageController.nextPage(
                    duration: const Duration(milliseconds: 300),
                    curve: Curves.easeInOut,
                  );
                },
                borderRadius: BorderRadius.circular(50),
                child: Container(
                  width: 60,
                  height: 60,
                  decoration: BoxDecoration(
                    color: Colors.black.withOpacity(0.6),
                    shape: BoxShape.circle,
                    border: Border.all(color: const Color(0xFFC5A880), width: 1.5),
                  ),
                  child: const Icon(Icons.arrow_forward_ios, color: Color(0xFFC5A880), size: 28),
                ),
              ),
            ),
          ),

        // 4) 하단 페이지 네비게이션 뱃지 오버레이
        Positioned(
          bottom: 25,
          left: 0,
          right: 0,
          child: Center(
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 8),
              decoration: BoxDecoration(
                color: Colors.black.withOpacity(0.7),
                borderRadius: BorderRadius.circular(20),
                border: Border.all(color: const Color(0xFFC5A880).withOpacity(0.4), width: 1),
              ),
              child: Text(
                "${_currentParkingPageIndex + 1} / ${parkingPhotos.length}",
                style: const TextStyle(
                  color: Color(0xFFC5A880),
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                  letterSpacing: 1.5,
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }
}
