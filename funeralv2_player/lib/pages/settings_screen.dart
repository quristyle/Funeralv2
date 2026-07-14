import 'dart:async';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

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
    
    // 수동 회전 초기값 설정
    _screenRotationTurns = widget.initialRotationTurns;
    
    // 첫 프레임이 다 그려진 후 자동으로 헬스체크 및 네트워크 정보 조회를 시도합니다.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _testConnection();
      _fetchNetworkDetails();
    });
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
  /// 취소 가능 여부에 따라 버튼 영역에 '취소(OutlinedButton)' 와 '저장 및 실행(ElevatedButton)'이 나란히 표시되도록 정렬합니다.
  @override
  Widget build(BuildContext context) {
    // 접속 연결 상태별 테마 색상 지정
    Color statusColor = _connectionStatus == 'SUCCESS' 
        ? Colors.greenAccent 
        : (_connectionStatus == 'FAIL' ? Colors.redAccent : Colors.orangeAccent);

    return RotatedBox(
      quarterTurns: _screenRotationTurns, // 수동 설정 회전값 바인딩 (0, 1, 2, 3)
      child: Scaffold(
        backgroundColor: Colors.black,
        appBar: AppBar(
          title: const Text('환경 설정'), 
          centerTitle: true, 
          backgroundColor: Colors.black,
          actions: [
            // 수동 화면 시계방향 회전 버튼
            IconButton(
              icon: const Icon(Icons.rotate_right, color: Color(0xFFC0A060), size: 28),
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
            // 중앙 영역: 환경설정 카드 및 텍스트 폼 구성요소들
            Center(
              child: SingleChildScrollView( // 필드 추가로 인해 화면 높이가 꽉 찼을 경우를 대비한 스크롤 탑재
                child: Container(
                  constraints: const BoxConstraints(maxWidth: 600), // 모니터가 너무 가로로 길 때를 대비한 최대폭 고정
                  padding: const EdgeInsets.all(32),
                  margin: const EdgeInsets.symmetric(vertical: 24),
                  decoration: BoxDecoration(
                    border: Border.all(color: const Color(0xFFC0A060).withOpacity(0.3)),
                    borderRadius: BorderRadius.circular(16),
                  ),
                  child: Form(
                    key: _formKey,
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        // 1) 서버 연결 상태 알림 바
                        Container(
                          padding: const EdgeInsets.all(12),
                          decoration: BoxDecoration(
                            color: statusColor.withOpacity(0.1),
                            borderRadius: BorderRadius.circular(8),
                            border: Border.all(color: statusColor.withOpacity(0.3)),
                          ),
                          child: Row(
                            children: [
                              Icon(
                                _connectionStatus == 'SUCCESS' ? Icons.check_circle : Icons.error,
                                color: statusColor,
                                size: 20,
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Text(
                                  _statusMessage,
                                  style: TextStyle(color: statusColor, fontWeight: FontWeight.bold),
                                ),
                              ),
                              IconButton(
                                icon: const Icon(Icons.refresh),
                                onPressed: _testConnection,
                                color: statusColor,
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 24),
                        // 2) 통합 서버 주소 텍스트 필드
                        TextFormField(
                          controller: _serverController,
                          decoration: const InputDecoration(labelText: '통합 서버 주소', border: OutlineInputBorder()),
                          onChanged: (_) => setState(() => _connectionStatus = 'IDLE'), // 수정 시 수동 테스트 재유도
                          validator: (v) => (v == null || v.isEmpty) ? '서버 주소 필수' : null,
                        ),
                        const SizedBox(height: 16),
                        // 3) 장비 고유 식별 코드 텍스트 필드
                        TextFormField(
                          controller: _codeController,
                          decoration: const InputDecoration(labelText: '장비 코드', border: OutlineInputBorder()),
                          validator: (v) => (v == null || v.isEmpty) ? '장비 코드 필수' : null,
                        ),
                        const SizedBox(height: 24),
                        const Divider(color: Colors.white24, height: 1),
                        const SizedBox(height: 16),
                        
                        // 기기 고유 네트워크 식별 정보 헤더 라벨
                        const Align(
                          alignment: Alignment.centerLeft,
                          child: Text(
                            '기기 정보 (확인용 / 수정 불가)',
                            style: TextStyle(color: Color(0xFFC0A060), fontWeight: FontWeight.bold, fontSize: 14),
                          ),
                        ),
                        const SizedBox(height: 12),
                        
                        // 4) 로컬 IP 주소 (읽기 전용)
                        TextFormField(
                          controller: _ipController,
                          readOnly: true,
                          decoration: const InputDecoration(
                            labelText: '로컬 IP 주소',
                            border: OutlineInputBorder(),
                            filled: true,
                            fillColor: Colors.black26,
                            prefixIcon: Icon(Icons.settings_ethernet, color: Colors.white30),
                          ),
                        ),
                        const SizedBox(height: 16),
                        
                        // 5) 공인 IP 주소 (읽기 전용)
                        TextFormField(
                          controller: _publicIpController,
                          readOnly: true,
                          decoration: const InputDecoration(
                            labelText: '공인 IP 주소',
                            border: OutlineInputBorder(),
                            filled: true,
                            fillColor: Colors.black26,
                            prefixIcon: Icon(Icons.public, color: Colors.white30),
                          ),
                        ),
                        const SizedBox(height: 16),
                        
                        // 6) MAC 주소 (읽기 전용)
                        TextFormField(
                          controller: _macController,
                          readOnly: true,
                          decoration: const InputDecoration(
                            labelText: 'MAC 주소',
                            border: OutlineInputBorder(),
                            filled: true,
                            fillColor: Colors.black26,
                            prefixIcon: Icon(Icons.fingerprint, color: Colors.white30),
                          ),
                        ),
                        const SizedBox(height: 32),
                        
                        // 7) 버튼 제어 영역 (취소 활성화 시 1:1 배치)
                        Row(
                          children: [
                            if (widget.onCancel != null) ...[
                              Expanded(
                                child: OutlinedButton(
                                  style: OutlinedButton.styleFrom(
                                    foregroundColor: Colors.white70,
                                    side: const BorderSide(color: Colors.white24),
                                    minimumSize: const Size(0, 50),
                                  ),
                                  onPressed: widget.onCancel,
                                  child: const Text('취소', style: TextStyle(fontWeight: FontWeight.bold)),
                                ),
                              ),
                              const SizedBox(width: 16),
                            ],
                            Expanded(
                              flex: widget.onCancel != null ? 1 : 2,
                              child: ElevatedButton(
                                style: ElevatedButton.styleFrom(
                                  backgroundColor: const Color(0xFFC0A060),
                                  foregroundColor: Colors.black,
                                  minimumSize: const Size(double.infinity, 50),
                                ),
                                onPressed: () {
                                  // 유효성 체크 통과 시 부모 콜백 호출을 통해 상태 저장 및 화면 변경
                                  if (_formKey.currentState!.validate()) {
                                    widget.onSave(
                                      _serverController.text.trim(), 
                                      _codeController.text.trim(), 
                                      _ipController.text.trim(), 
                                      _macController.text.trim(), 
                                      _publicIpController.text.trim(),
                                      _screenRotationTurns, // 수동 회전 각도 함께 인계
                                    );
                                  }
                                },
                                child: const Text('저장 및 실행', style: TextStyle(fontWeight: FontWeight.bold)),
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
            
            // 우하단 레이어: 디버그 모니터링 텍스트 오버레이 박스
            Positioned(
              bottom: 10,
              right: 10,
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                color: Colors.black54,
                child: AnimatedBuilder(
                  animation: Listenable.merge([_codeController, _ipController]),
                  builder: (context, _) {
                    return Text(
                      'DEBUG: settings_screen.dart | Code: ${_codeController.text} | IP: ${_ipController.text}',
                      style: const TextStyle(color: Colors.yellow, fontSize: 10, fontWeight: FontWeight.bold),
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
