import 'dart:async';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import '../services/display/display_mode_service.dart';
import '../services/update/update_service.dart';
import '../widgets/update_dialog.dart';

/// [환경 설정 화면 위젯]
/// 사이니지 단말이 백엔드 통합 서버와 소통할 수 있도록
/// 서버 주소 및 기기 식별 코드를 입력받고, 서버 가동 여부 헬스체크를 수행한 뒤 설정을 저장합니다.
/// 단말의 식별 정보인 로컬 IP, 공인 IP, MAC 주소를 자동으로 조회하여 읽기 전용으로 화면에 노출합니다.
/// 모니터 물리적 회전율([initialRotationTurns]) 및 수동 회전 액션에 반응하여 전체 UI를 회전 렌더링합니다.
/// 우하단 영역에 플레이어 셸과 동일한 형태의 디버그 정보 텍스트 박스를 노출합니다.
/// 취소 콜백([onCancel])이 제공될 경우, 이전 화면으로 복귀 가능한 취소 버튼을 활성화합니다.
class SettingsScreen extends StatefulWidget {
  final String initialServer; // 로컬 저장소에서 읽어온 기존 서버 주소
  final String initialCode; // 로컬 저장소에서 읽어온 기존 장비 식별 코드
  final String initialIp; // 기존 IP 주소
  final String initialMac; // 기존 MAC 주소
  final String initialPublicIp; // 기존 공인 IP 주소
  final String initialOrientation; // 화면 가로/세로 방향 설정
  final int initialRotationTurns; // 기존 화면 회전 각도값 (0~3 범위)
  
  // 저장을 누를 때 로컬 저장소(SharedPreferences) 및 상태 변경을 위해 부모(MainRouter)가 전달해 준 콜백 함수
  final Function(String server, String code, String ip, String mac, String publicIp, int rotationTurns) onSave;
  // 설정 변경 작업을 취소하고 이전 구동 화면으로 회피하기 위한 콜백 함수
  final VoidCallback? onCancel;

  const SettingsScreen({
    super.key,
    required this.initialServer,
    required this.initialCode,
    required this.initialIp,
    required this.initialMac,
    required this.initialPublicIp,
    required this.initialOrientation,
    required this.initialRotationTurns,
    required this.onSave,
    this.onCancel,
  });

  @override
  State<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends State<SettingsScreen> {
  // 폼 유효성(Validation) 체크를 위한 전역 키
  final _formKey = GlobalKey<FormState>();
  
  // 입력 필드 제어용 컨트롤러들
  late TextEditingController _serverController;
  late TextEditingController _codeController;
  late TextEditingController _ipController;
  late TextEditingController _macController;
  late TextEditingController _publicIpController;

  // 서버 연결 상태 필드 (IDLE: 대기, TESTING: 테스트 진행 중, SUCCESS: 성공, FAIL: 실패)
  String _connectionStatus = 'IDLE';
  // 상태 메시지 텍스트
  String _statusMessage = '서버 연결 상태를 확인해 주십시오.';

  // 로컬 수동 화면 회전값 (0: 0도, 1: 90도, 2: 180도, 3: 270도)
  late int _screenRotationTurns;

  // 화면 비율(해상도) 프리셋 상태
  DisplayAspect _displayAspect = DisplayAspect.ratio16x9;
  // 해상도 적용 결과 안내 문구
  String? _aspectMessage;
  // 해상도 적용 중 여부 (중복 클릭 방지)
  bool _applyingAspect = false;

  // 새 버전 확인 결과. 화면을 열 때 조용히 한 번 확인해 두고,
  // 새 버전이 있으면 머리줄 아이콘에 점을 찍는다.
  UpdateCheck? _updateCheck;

  /// [화면 방향성과 회전 각도 턴수 정합성 보정]
  /// displayOrientation 속성값에 맞추어 수동 회전 각도 상태(_screenRotationTurns)가 모순되지 않도록 맞춥니다.
  /// (LANDSCAPE 시에는 짝수 0 또는 2턴, PORTRAIT 시에는 홀수 1 또는 3턴으로 강제 유도)
  void _alignRotationTurnsWithOrientation() {
    final bool isPortrait = widget.initialOrientation == 'PORTRAIT';
    if (isPortrait) {
      if (_screenRotationTurns % 2 == 0) {
        _screenRotationTurns = 1; // 세로인데 가로 회전값으로 되어 있다면 기본 90도 회전 적용
      }
    } else {
      if (_screenRotationTurns % 2 == 1) {
        _screenRotationTurns = 0; // 가로인데 세로 회전값으로 되어 있다면 기본 0도 회전 적용
      }
    }
  }

  /// [초기 상태 설정]
  /// 부모로부터 전달받은 초기값을 각 텍스트 컨트롤러와 수동 회전 상태에 채우고, 헬스체크 및 네트워크 상태 감지를 기동합니다.
  @override
  void initState() {
    super.initState();
    _serverController = TextEditingController(text: widget.initialServer);
    _codeController = TextEditingController(text: widget.initialCode);
    _ipController = TextEditingController(text: widget.initialIp.isEmpty ? '조회 중...' : widget.initialIp);
    _macController = TextEditingController(text: widget.initialMac.isEmpty ? '조회 중...' : widget.initialMac);
    _publicIpController = TextEditingController(text: widget.initialPublicIp.isEmpty ? '조회 중...' : widget.initialPublicIp);
    
    // 수동 회전 초기값 설정 및 방향성 정합성 교정
    _screenRotationTurns = widget.initialRotationTurns;
    _alignRotationTurnsWithOrientation();

    print('[SettingsScreen] initState() 완료: '
          'initialOrientation=${widget.initialOrientation}, '
          'initialRotationTurns=${widget.initialRotationTurns} '
          '-> _screenRotationTurns=$_screenRotationTurns');
    
    // 저장된 화면 비율(해상도) 프리셋을 불러옵니다.
    DisplayModeService.loadSaved().then((aspect) {
      if (mounted) setState(() => _displayAspect = aspect);
    });

    // 첫 프레임이 다 그려진 후 자동으로 헬스체크 및 네트워크 정보 조회를 시도합니다.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _testConnection();
      _fetchNetworkDetails();
      _checkUpdateQuietly();
    });
  }

  /// [새 버전 조용히 확인]
  /// 설정 화면을 열 때 한 번만 확인한다. 실패하면 아무것도 알리지 않는다 —
  /// 인터넷이 닿지 않는 현장이 있고, 그때 붉은 문구가 뜨면 설정이 잘못된 것처럼 보인다.
  /// 결과는 머리줄 아이콘의 점으로만 알리고, 자세한 것은 아이콘을 눌러 팝업에서 본다.
  Future<void> _checkUpdateQuietly() async {
    final result = await UpdateService.check();
    if (!mounted) return;
    setState(() => _updateCheck = result);
  }

  /// [화면 비율(해상도) 프리셋 적용]
  /// 선택한 비율의 해상도를 즉시 출력에 반영하고 로컬에 저장합니다.
  /// 저장된 값은 앱이 다시 기동될 때도 자동으로 다시 적용됩니다.
  Future<void> _applyDisplayAspect(DisplayAspect aspect) async {
    if (_applyingAspect) return;

    setState(() {
      _applyingAspect = true;
      _aspectMessage = '${aspect.label} 적용 중...';
    });

    final (ok, message) = await DisplayModeService.apply(aspect);
    if (ok) {
      await DisplayModeService.save(aspect);
    }

    if (!mounted) return;
    setState(() {
      _applyingAspect = false;
      _aspectMessage = message;
      if (ok) _displayAspect = aspect;
    });
  }

  /// [화면 비율 선택 버튼 한 개]
  Widget _buildAspectButton(DisplayAspect aspect) {
    final bool selected = _displayAspect == aspect;
    final bool enabled = DisplayModeService.isSupported && !_applyingAspect;

    return Expanded(
      child: OutlinedButton(
        style: OutlinedButton.styleFrom(
          backgroundColor: selected ? const Color(0xFFC0A060) : Colors.transparent,
          foregroundColor: selected ? Colors.black : Colors.white70,
          side: BorderSide(color: selected ? const Color(0xFFC0A060) : Colors.white24),
          minimumSize: const Size(0, 44),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
        ),
        onPressed: enabled ? () => _applyDisplayAspect(aspect) : null,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(aspect.label, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14)),
            Text(
              '${aspect.preferred.$1}x${aspect.preferred.$2}',
              style: TextStyle(
                fontSize: 11,
                color: selected ? Colors.black54 : Colors.white38,
              ),
            ),
          ],
        ),
      ),
    );
  }

  /// [화면 비율(해상도) 설정 영역]
  Widget _buildAspectSection() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          '화면 해상도',
          style: TextStyle(color: Color(0xFFC0A060), fontWeight: FontWeight.bold, fontSize: 13),
        ),
        const SizedBox(height: 8),
        Row(
          children: [
            _buildAspectButton(DisplayAspect.ratio16x9),
            const SizedBox(width: 12),
            _buildAspectButton(DisplayAspect.ratio16x10),
          ],
        ),
        const SizedBox(height: 6),
        Text(
          DisplayModeService.isSupported
              ? (_aspectMessage ?? '패널 비율에 맞는 해상도를 선택하면 즉시 적용됩니다.')
              : '해상도 전환은 라즈베리파이(Linux) 환경에서만 지원됩니다.',
          style: const TextStyle(color: Colors.white38, fontSize: 11),
        ),
      ],
    );
  }

  /// [위젯 설정값 변경 감지 수신]
  /// 부모 위젯이 리빌드되면서 새로운 회전 설정값(initialRotationTurns)이 도달하면
  /// 내부 상태값(_screenRotationTurns)을 동기화하여 화면 방향을 즉각 물리적으로 재정렬합니다.
  @override
  void didUpdateWidget(covariant SettingsScreen oldWidget) {
    super.didUpdateWidget(oldWidget);
    print('[SettingsScreen] didUpdateWidget() 감지: '
          'oldWidget.initialRotationTurns=${oldWidget.initialRotationTurns} '
          '-> widget.initialRotationTurns=${widget.initialRotationTurns}');
    if (oldWidget.initialRotationTurns != widget.initialRotationTurns ||
        oldWidget.initialOrientation != widget.initialOrientation) {
      setState(() {
        _screenRotationTurns = widget.initialRotationTurns;
        _alignRotationTurnsWithOrientation();
      });
      print('[SettingsScreen] didUpdateWidget() 회전값 동기화 완료: _screenRotationTurns=$_screenRotationTurns');
    }
  }

  /// [네트워크 상세 정보 조회]
  /// 다트 표준 라이브러리(`NetworkInterface`)를 통해 기기의 물리 IP 주소를 감지하고,
  /// 윈도우/리눅스 쉘 명령을 통해 MAC 주소를 파싱하며, 외부 오픈 API를 조회하여 공인 IP 주소를 확보합니다.
  Future<void> _fetchNetworkDetails() async {
    String localIp = '알 수 없음';
    String mac = '알 수 없음';
    String publicIp = '알 수 없음';

    // 1. 로컬 IP 주소 조회 (IPv4 기준)
    try {
      final interfaces = await NetworkInterface.list();
      for (var interface in interfaces) {
        for (var addr in interface.addresses) {
          if (addr.type == InternetAddressType.IPv4 && !addr.isLoopback) {
            localIp = addr.address;
            break;
          }
        }
        if (localIp != '알 수 없음') break;
      }
    } catch (e) {
      print('[Network] 로컬 IP 획득 실패: $e');
    }

    // 2. MAC 주소 조회 (운영체제 명령어 실행)
    try {
      if (Platform.isWindows) {
        final result = await Process.run('getmac', []);
        if (result.exitCode == 0) {
          // getmac 결과물에서 MAC 주소 표준 패턴(XX-XX-XX-XX-XX-XX) 정규식 추출
          final reg = RegExp(r'([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})');
          final match = reg.firstMatch(result.stdout.toString());
          if (match != null) {
            mac = match.group(0) ?? '알 수 없음';
          }
        }
      } else if (Platform.isLinux) {
        final result = await Process.run('cat', ['/sys/class/net/eth0/address']);
        if (result.exitCode == 0) {
          mac = result.stdout.toString().trim();
        }
      }
    } catch (e) {
      print('[Network] MAC 주소 획득 실패: $e');
    }

    // 3. 공인 IP 주소 조회 (인터넷 망 연결 필요)
    try {
      final response = await http.get(Uri.parse('https://api.ipify.org')).timeout(const Duration(seconds: 3));
      if (response.statusCode == 200) {
        publicIp = response.body.trim();
      }
    } catch (e) {
      print('[Network] 공인 IP 획득 실패: $e');
    }

    if (mounted) {
      setState(() {
        _ipController.text = localIp;
        _macController.text = mac;
        _publicIpController.text = publicIp;
      });
    }
  }

  /// [서버 연결 테스트 (헬스체크)]
  /// 입력된 통합 서버 주소의 유효성을 검증하기 위해 원격 헬스체크 API 엔드포인트를 호출합니다.
  /// 4초 타임아웃을 두어 지연 현상을 가드하며 통신 가능 여부를 화면에 피드백합니다.
  Future<void> _testConnection() async {
    final url = _serverController.text.trim();
    if (url.isEmpty) return;

    print('[Settings] 서버 연결 테스트 시도: $url');
    setState(() {
      _connectionStatus = 'TESTING';
      _statusMessage = '서버 연결 확인 중...';
    });

    try {
      // 끝자리에 붙은 슬래시 제거 처리
      final baseUrl = url.endsWith('/') ? url.substring(0, url.length - 1) : url;
      
      // 장비 헬스체크용 공통 백엔드 API 엔드포인트 기동
      final response = await http.get(Uri.parse('$baseUrl/api/funeral/building/device/code/HEALTH_CHECK'))
          .timeout(const Duration(seconds: 4));

      setState(() {
        // HTTP 상태코드가 500 미만(200~499 등)이면 서버 포트 및 라우터 자체는 가동 중인 것으로 식별
        if (response.statusCode < 500) {
          print('[Settings] 서버 연결 성공 (Status: ${response.statusCode})');
          _connectionStatus = 'SUCCESS';
          _statusMessage = '서버 통신 가능 (정상)';
        } else {
          print('[Settings] 서버 응답 오류: ${response.statusCode}');
          _connectionStatus = 'FAIL';
          _statusMessage = '서버 응답 오류 (HTTP ${response.statusCode})';
        }
      });
    } catch (e) {
      print('[Settings] 연결 실패 예외: $e');
      setState(() {
        _connectionStatus = 'FAIL';
        _statusMessage = '접속 실패: 서버가 꺼져있거나 주소가 잘못되었습니다.';
      });
    }
  }

  /// [화면 빌드]
  /// 상위 위젯 전체를 `RotatedBox`로 감싸주어 우측 상단 수동 회전 버튼을 누를 때마다 전체 레이아웃이 90도씩 물리적으로 회전 연출되게 렌더합니다.
  /// 로컬 IP, 공인 IP, MAC 주소는 `readOnly: true` 옵션을 인가하여 확인용 읽기 전용 필드로 렌더링합니다.
  /// Scaffold body 영역에 Stack 구조를 인가하여 우하단 겹침 포지션에 노란색 텍스트 디버그 박스를 배치합니다.
  /// [서버 주소 입력 필드 빌더]
  Widget _buildServerField() {
    return TextFormField(
      controller: _serverController,
      style: const TextStyle(fontSize: 14, color: Colors.white),
      decoration: const InputDecoration(
        labelText: '통합 서버 주소',
        border: OutlineInputBorder(),
        contentPadding: EdgeInsets.symmetric(vertical: 14, horizontal: 12),
      ),
      onChanged: (_) => setState(() => _connectionStatus = 'IDLE'),
      validator: (v) => (v == null || v.isEmpty) ? '서버 주소 필수' : null,
    );
  }

  /// [장비 식별 코드 입력 필드 빌더]
  Widget _buildCodeField() {
    return TextFormField(
      controller: _codeController,
      style: const TextStyle(fontSize: 14, color: Colors.white),
      decoration: const InputDecoration(
        labelText: '장비 코드',
        border: OutlineInputBorder(),
        contentPadding: EdgeInsets.symmetric(vertical: 14, horizontal: 12),
      ),
      validator: (v) => (v == null || v.isEmpty) ? '장비 코드 필수' : null,
    );
  }

  /// [네트워크 정보 표출 행 빌더]
  Widget _buildInfoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: const TextStyle(color: Colors.white38, fontSize: 13)),
          Text(
            value,
            style: const TextStyle(
              color: Colors.white70,
              fontSize: 13,
              fontWeight: FontWeight.bold,
              fontFamily: 'Consolas', // 고정폭 글꼴로 정밀 출력
            ),
          ),
        ],
      ),
    );
  }

  /// [화면 빌드]
  /// 상위 위젯 전체를 `RotatedBox`로 감싸주어 우측 상단 수동 회전 버튼을 누를 때마다 전체 레이아웃이 90도씩 물리적으로 회전 연출되게 렌더합니다.
  /// 저해상도 모니터 대응 컨셉(No Scroll)을 유지하기 위해 가로형 레이아웃에서는 입력 폼을 2열로 자동 스위칭하고 여백을 극대화 축소합니다.
  @override
  Widget build(BuildContext context) {
    print('[SettingsScreen] build() 진입 - 현재 적용할 _screenRotationTurns=$_screenRotationTurns');
    Color statusColor = _connectionStatus == 'SUCCESS' 
        ? Colors.greenAccent 
        : (_connectionStatus == 'FAIL' ? Colors.redAccent : Colors.orangeAccent);

    // 가로 방향 모드(0도, 180도 회전) 여부 판별
    final bool isFormHorizontal = _screenRotationTurns % 2 == 0;

    return RotatedBox(
      quarterTurns: _screenRotationTurns, // 수동 설정 회전값 바인딩 (0, 1, 2, 3)
      child: Scaffold(
        backgroundColor: Colors.black,
        appBar: AppBar(
          title: const Text('환경 설정', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)), 
          centerTitle: true, 
          backgroundColor: Colors.black,
          toolbarHeight: 46, // 헤더 높이 축소로 가용 면적 확보
          actions: [
            // 새 버전 확인 버튼. 새 버전이 있으면 아이콘 오른쪽 위에 점이 찍힌다.
            IconButton(
              icon: Stack(
                clipBehavior: Clip.none,
                children: [
                  const Icon(Icons.system_update, color: Color(0xFFC0A060), size: 24),
                  if (_updateCheck?.hasUpdate == true)
                    Positioned(
                      right: -1,
                      top: -1,
                      child: Container(
                        width: 8,
                        height: 8,
                        decoration: const BoxDecoration(
                          color: Colors.redAccent,
                          shape: BoxShape.circle,
                        ),
                      ),
                    ),
                ],
              ),
              tooltip: _updateCheck?.hasUpdate == true
                  ? '새 버전 ${_updateCheck!.latestVersion} 있음'
                  : '버전 확인',
              onPressed: () => UpdateDialog.show(
                context,
                initial: _updateCheck,
                quarterTurns: _screenRotationTurns,
              ),
            ),
            // 수동 화면 시계방향 회전 버튼
            IconButton(
              icon: const Icon(Icons.rotate_right, color: Color(0xFFC0A060), size: 24),
              tooltip: '화면 회전',
              onPressed: () {
                setState(() {
                  _screenRotationTurns = (_screenRotationTurns + 1) % 4;
                });
              },
            ),
            const SizedBox(width: 16),
          ],
        ),
        body: Stack(
          fit: StackFit.expand,
          children: [
            Center(
              child: Container(
                constraints: const BoxConstraints(maxWidth: 580), // 카드 최대 폭 제한
                padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
                margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                decoration: BoxDecoration(
                  border: Border.all(color: const Color(0xFFC0A060).withOpacity(0.2)),
                  borderRadius: BorderRadius.circular(12),
                  color: Colors.white.withOpacity(0.01),
                ),
                child: Form(
                  key: _formKey,
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      // 1) 서버 연결 상태 알림 바 (콤팩트화)
                      Container(
                        padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 12),
                        decoration: BoxDecoration(
                          color: statusColor.withOpacity(0.08),
                          borderRadius: BorderRadius.circular(6),
                          border: Border.all(color: statusColor.withOpacity(0.2)),
                        ),
                        child: Row(
                          children: [
                            Icon(
                              _connectionStatus == 'SUCCESS' ? Icons.check_circle : Icons.error,
                              color: statusColor,
                              size: 18,
                            ),
                            const SizedBox(width: 10),
                            Expanded(
                              child: Text(
                                _statusMessage,
                                style: TextStyle(color: statusColor, fontSize: 13, fontWeight: FontWeight.bold),
                              ),
                            ),
                            IconButton(
                              icon: const Icon(Icons.refresh, size: 18),
                              onPressed: _testConnection,
                              color: statusColor,
                              padding: EdgeInsets.zero,
                              constraints: const BoxConstraints(),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 14),

                      // 2) 통합 서버 주소 및 장비 코드 분기형 배치 (가로 모드 2열, 세로 모드 1열)
                      if (isFormHorizontal)
                        Row(
                          children: [
                            Expanded(child: _buildServerField()),
                            const SizedBox(width: 12),
                            Expanded(child: _buildCodeField()),
                          ],
                        )
                      else
                        Column(
                          children: [
                            _buildServerField(),
                            const SizedBox(height: 12),
                            _buildCodeField(),
                          ],
                        ),
                      const SizedBox(height: 14),
                      const Divider(color: Colors.white12, height: 1),
                      const SizedBox(height: 10),
                      
                      // 3) 기기 고유 네트워크 식별 정보 콤팩트 테이블 영역 (기존의 무거운 TextFormField 3개를 완전 제거하여 압축)
                      const Align(
                        alignment: Alignment.centerLeft,
                        child: Text(
                          '기기 식별 정보 (수정 불가)',
                          style: TextStyle(color: Color(0xFFC0A060), fontWeight: FontWeight.bold, fontSize: 13),
                        ),
                      ),
                      const SizedBox(height: 8),
                      Container(
                        padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 14),
                        decoration: BoxDecoration(
                          color: Colors.white.withOpacity(0.02),
                          borderRadius: BorderRadius.circular(8),
                          border: Border.all(color: Colors.white.withOpacity(0.05)),
                        ),
                        child: Column(
                          children: [
                            _buildInfoRow("로컬 IP 주소", _ipController.text),
                            _buildInfoRow("공인 IP 주소", _publicIpController.text),
                            _buildInfoRow("MAC 주소", _macController.text),
                          ],
                        ),
                      ),
                      const SizedBox(height: 14),
                      const Divider(color: Colors.white12, height: 1),
                      const SizedBox(height: 10),

                      // 4) 화면 비율(해상도) 프리셋 선택 영역
                      _buildAspectSection(),
                      const SizedBox(height: 20),

                      // 5) 버튼 제어 영역
                      Row(
                        children: [
                          if (widget.onCancel != null) ...[
                            Expanded(
                              child: OutlinedButton(
                                style: OutlinedButton.styleFrom(
                                  foregroundColor: Colors.white70,
                                  side: const BorderSide(color: Colors.white24),
                                  minimumSize: const Size(0, 44),
                                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                                ),
                                onPressed: widget.onCancel,
                                child: const Text('취소', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14)),
                              ),
                            ),
                            const SizedBox(width: 12),
                          ],
                          Expanded(
                            flex: widget.onCancel != null ? 1 : 2,
                            child: ElevatedButton(
                              style: ElevatedButton.styleFrom(
                                backgroundColor: const Color(0xFFC0A060),
                                foregroundColor: Colors.black,
                                minimumSize: const Size(double.infinity, 44),
                                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                              ),
                              onPressed: () {
                                if (_formKey.currentState!.validate()) {
                                  widget.onSave(
                                    _serverController.text.trim(), 
                                    _codeController.text.trim(), 
                                    _ipController.text.trim(), 
                                    _macController.text.trim(), 
                                    _publicIpController.text.trim(),
                                    _screenRotationTurns, 
                                  );
                                }
                              },
                              child: const Text('저장 및 실행', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14)),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
            ),
            
            // 우하단 레이어: 디버그 모니터링 텍스트 오버레이 박스
            Positioned(
              bottom: 8,
              right: 8,
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                color: Colors.black54,
                child: AnimatedBuilder(
                  animation: Listenable.merge([_codeController, _ipController]),
                  builder: (context, _) {
                    return Text(
                      'DEBUG: settings_screen.dart | Code: ${_codeController.text} | IP: ${_ipController.text}',
                      style: const TextStyle(color: Colors.yellow, fontSize: 9, fontWeight: FontWeight.bold),
                    );
                  }
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
