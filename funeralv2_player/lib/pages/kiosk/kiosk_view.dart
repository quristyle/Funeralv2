import 'package:flutter/material.dart';
import '../player_shell.dart';
import 'kiosk_controller.dart';
import '../../models/device_models.dart';

class KioskView extends StatefulWidget {
  final String serverBaseUrl;
  final String deviceCode;
  final VoidCallback onOpenSettings;

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
  final KioskController _controller = KioskController();

  // 상단 탭 상태 ("ROOM_GUIDE" 또는 "PARKING_GUIDE")
  String _selectedTab = "ROOM_GUIDE";

  // 층 필터 상태 ("전체" 또는 특정 층 이름)
  String _selectedFloor = "전체";

  // 페이지네이션 상태
  int _currentPage = 0;
  static const int _itemsPerPage = 6; // 한 페이지에 노출할 호실 카드 수

  // 주차장 이미지 슬라이더 제어용
  late PageController _parkingPageController;
  int _currentParkingPageIndex = 0;

  @override
  void initState() {
    super.initState();
    _parkingPageController = PageController(initialPage: 0);
    _controller.init(
      widget.serverBaseUrl,
      widget.deviceCode,
      () => setState(() {}),
    );
  }

  @override
  void dispose() {
    _parkingPageController.dispose();
    _controller.dispose();
    super.dispose();
  }

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
            backgroundColor: Colors.transparent,
            body: SafeArea(
              child: Column(
                children: [
                  _buildHeader(dev),
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

  // 상단 헤더 영역 (로고/타이틀 + 대형 탭 바)
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
          // 대형 탭 바 (터치 기반)
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

  // 호실안내(검색) 콘텐츠 빌더
  Widget _buildRoomGuideContent() {
    if (_controller.rooms.isEmpty) {
      return const Center(
        child: Text(
          "배정된 호실 정보가 없습니다.",
          style: TextStyle(color: Colors.white60, fontSize: 20),
        ),
      );
    }

    // 층 목록 구성 (중복 제거 및 정렬)
    final floors = <String>["전체"];
    for (var r in _controller.rooms) {
      if (r.floorName.isNotEmpty && !floors.contains(r.floorName)) {
        floors.add(r.floorName);
      }
    }
    // 층 순서 정렬
    floors.sort((a, b) {
      if (a == "전체") return -1;
      if (b == "전체") return 1;
      return a.compareTo(b);
    });

    // 선택된 층으로 필터링
    final filteredRooms = _controller.rooms.where((r) {
      if (_selectedFloor == "전체") return true;
      return r.floorName == _selectedFloor;
    }).toList();

    // 페이지네이션 처리
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
        // 1. 왼쪽: 층 선택 터치 패널 (드래그 대신 클릭 터치 구조)
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
                  physics: const NeverScrollableScrollPhysics(), // 드래그 완전 방지
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
                            _currentPage = 0;
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

        // 2. 오른쪽: 호실 카드 그리드 & 터치 기반 페이지네이션
        Expanded(
          child: Container(
            padding: const EdgeInsets.all(30),
            color: Colors.black.withOpacity(0.1),
            child: Column(
              children: [
                // 그리드 영역 (드래그 스크롤 금지)
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
                            final double availableWidth = constraints.maxWidth;
                            final double availableHeight = constraints.maxHeight;

                            const int crossAxisCount = 3;
                            const double crossAxisSpacing = 18.0;
                            const double mainAxisSpacing = 18.0;
                            const int rowCount = 2;

                            // 가용 영역에 기초하여 카드의 가로/세로 길이 계산
                            final double cardWidth = (availableWidth - (crossAxisSpacing * (crossAxisCount - 1))) / crossAxisCount;
                            final double cardHeight = (availableHeight - (mainAxisSpacing * (rowCount - 1))) / rowCount;

                            // 세로 길이가 0 이하인 극단적 예외 상황 방어
                            final double dynamicAspectRatio = (cardHeight > 0) ? (cardWidth / cardHeight) : 1.05;

                            return GridView.builder(
                              physics: const NeverScrollableScrollPhysics(), // 드래그 완전 방지
                              gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                                crossAxisCount: crossAxisCount,
                                childAspectRatio: dynamicAspectRatio,
                                crossAxisSpacing: crossAxisSpacing,
                                mainAxisSpacing: mainAxisSpacing,
                              ),
                              itemCount: paginatedRooms.length,
                              itemBuilder: (context, index) {
                                final room = paginatedRooms[index];
                                return _buildRoomCard(room);
                              },
                            );
                          },
                        ),
                ),
                const SizedBox(height: 20),
                // 페이지네이션 컨트롤러 (이전/다음 클릭 버튼)
                if (maxPage > 1)
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      // 이전 페이지 버튼
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
                      // 페이지 표시
                      Text(
                        "${_currentPage + 1} / $maxPage",
                        style: const TextStyle(
                          color: Color(0xFFC5A880),
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      const SizedBox(width: 40),
                      // 다음 페이지 버튼
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

  // 개별 호실 안내 카드 빌드 (Glassmorphism + Gold border)
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
          // 호실 번호 & 운영 상태 헤더
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
          // 빈소 정보 본문
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

  Widget _buildPlaceholderPhoto() {
    return const Center(
      child: Icon(
        Icons.person_outline,
        color: Color(0xFFC5A880),
        size: 26,
      ),
    );
  }

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

  // 주차안내 콘텐츠 빌더 (실제 주차장 이미지 롤링 또는 플레이스홀더)
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
        // 1. 대형 주차 이미지 슬라이더 (PageView, 드래그 차단)
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

        // 2. 왼쪽 이전 이동 터치 버튼
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

        // 3. 오른쪽 다음 이동 터치 버튼
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

        // 4. 하단 중앙 페이지네이션 표시
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
