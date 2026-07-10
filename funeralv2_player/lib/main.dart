import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:http/http.dart' as http;
import 'package:media_kit/media_kit.dart';
import 'package:window_manager/window_manager.dart';
import 'pages/device_dispatcher.dart'; // 수정됨

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  MediaKit.ensureInitialized();

  // 1. 데스크톱 플랫폼(Windows, Linux) 키오스크 모드 (전체화면 + 항상 최상위 노출) 적용
  if (Platform.isWindows || Platform.isLinux) {
    try {
      await windowManager.ensureInitialized();
      WindowOptions windowOptions = const WindowOptions(
        center: true,
        backgroundColor: Colors.black,
        skipTaskbar: false,
        titleBarStyle: TitleBarStyle.hidden, // 타이틀바 경계선 숨김
      );
      await windowManager.waitUntilReadyToShow(windowOptions, () async {
        await windowManager.setFullScreen(true);  // 모니터 전체 화면 꽉 채우기
        await windowManager.setAlwaysOnTop(true);  // 다른 모든 창보다 항상 위에 띄우기
        await windowManager.show();
        await windowManager.focus();
      });
    } catch (e) {
      print('[Kiosk] 데스크톱 창 설정 실패: $e');
    }
  }

  // 2. 모바일/임베디드(Android) 키오스크 몰입 모드 적용 (상태바/하단 내비바 원천 제거)
  if (Platform.isAndroid) {
    try {
      SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
    } catch (e) {
      print('[Kiosk] 안드로이드 몰입 모드 설정 실패: $e');
    }
  }

  runApp(const FuneralPlayerApp());
}

class FuneralPlayerApp extends StatelessWidget {
  const FuneralPlayerApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Funeral Signage Player',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        brightness: Brightness.dark,
        scaffoldBackgroundColor: Colors.black,
        primaryColor: const Color(0xFFC0A060),
      ),
      home: const MainRouter(),
    );
  }
}

class MainRouter extends StatefulWidget {
  const MainRouter({super.key});

  @override
  State<MainRouter> createState() => _MainRouterState();
}

class _MainRouterState extends State<MainRouter> {
  String? serverBaseUrl;
  String? deviceCode;
  String? ipAddress;
  String? macAddress;
  String? publicIpAddress;
  String? displayOrientation;
  bool isConfigured = false;
  bool isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadConfiguration();
  }

  // SharedPreferences에서 로컬 설정 정보 로드
  Future<void> _loadConfiguration() async {
    final prefs = await SharedPreferences.getInstance();
    var savedPublicIp = prefs.getString('publicIpAddress') ?? '';

    // 만약 로컬 캐시된 공인 IP가 비어 있다면, 비동기로 3초 타임아웃 룰 하에 1회 백그라운드 스캔 시도
    if (savedPublicIp.isEmpty) {
      try {
        final res = await http.get(Uri.parse('https://api.ipify.org')).timeout(const Duration(seconds: 3));
        if (res.statusCode == 200) {
          savedPublicIp = res.body.trim();
          await prefs.setString('publicIpAddress', savedPublicIp);
        }
      } catch (e) {
        print('[MainRouter] 초기 공인 IP 백그라운드 조회 실패: $e');
      }
    }

    setState(() {
      serverBaseUrl = prefs.getString('serverBaseUrl') ?? 'http://localhost:5265';
      deviceCode = prefs.getString('deviceCode') ?? '';
      ipAddress = prefs.getString('ipAddress') ?? '';
      macAddress = prefs.getString('macAddress') ?? '';
      publicIpAddress = savedPublicIp;
      displayOrientation = prefs.getString('displayOrientation') ?? 'LANDSCAPE';
      
      isConfigured = deviceCode != null && deviceCode!.isNotEmpty;
      isLoading = false;
    });
  }

  // 설정 저장 처리
  Future<void> _saveConfiguration(String server, String code, String ip, String mac, String publicIp) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('serverBaseUrl', server);
    await prefs.setString('deviceCode', code);
    await prefs.setString('ipAddress', ip);
    await prefs.setString('macAddress', mac);
    await prefs.setString('publicIpAddress', publicIp);

    setState(() {
      serverBaseUrl = server;
      deviceCode = code;
      ipAddress = ip;
      macAddress = mac;
      publicIpAddress = publicIp;
      isConfigured = true;
    });
  }

  @override
  Widget build(BuildContext context) {
    if (isLoading) {
      return const Scaffold(
        body: Center(
          child: CircularProgressIndicator(color: Color(0xFFC0A060)),
        ),
      );
    }

    if (!isConfigured) {
      return SettingsScreen(
        initialServer: serverBaseUrl ?? 'http://localhost:5265',
        initialCode: deviceCode ?? '',
        initialIp: ipAddress ?? '',
        initialMac: macAddress ?? '',
        initialOrientation: displayOrientation ?? 'LANDSCAPE',
        onSave: _saveConfiguration,
      );
    }

    return DeviceDispatcher(
      serverBaseUrl: serverBaseUrl!,
      deviceCode: deviceCode!,
      ipAddress: ipAddress ?? '',
      macAddress: macAddress ?? '',
      publicIpAddress: publicIpAddress ?? '',
      onOpenSettings: () async {
        final prefs = await SharedPreferences.getInstance();
        setState(() {
          displayOrientation = prefs.getString('displayOrientation') ?? 'LANDSCAPE';
          isConfigured = false;
        });
      },
    );
  }
}

/// 플레이어 환경 설정 화면 (통합 게이트웨이 주소 사용)
class SettingsScreen extends StatefulWidget {
  final String initialServer;
  final String initialCode;
  final String initialIp;
  final String initialMac;
  final String initialOrientation;
  final Function(String server, String code, String ip, String mac, String publicIp) onSave;

  const SettingsScreen({
    super.key,
    required this.initialServer,
    required this.initialCode,
    required this.initialIp,
    required this.initialMac,
    required this.initialOrientation,
    required this.onSave,
  });

  @override
  State<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends State<SettingsScreen> {
  final _formKey = GlobalKey<FormState>();
  late TextEditingController _serverController;
  late TextEditingController _codeController;
  late TextEditingController _ipController;
  late TextEditingController _macController;
  late TextEditingController _publicIpController;

  @override
  void initState() {
    super.initState();
    _serverController = TextEditingController(text: widget.initialServer);
    _codeController = TextEditingController(text: widget.initialCode);
    _ipController = TextEditingController(text: widget.initialIp);
    _macController = TextEditingController(text: widget.initialMac);
    _publicIpController = TextEditingController(text: '조회 중...');

    if (widget.initialIp.isEmpty) {
      _autoDetectLocalIp();
    }
    if (widget.initialMac.isEmpty) {
      _autoDetectMacAddress();
    }
    _autoDetectPublicIp();
  }

  Future<void> _autoDetectPublicIp() async {
    try {
      final res = await http.get(Uri.parse('https://api.ipify.org')).timeout(const Duration(seconds: 4));
      if (res.statusCode == 200) {
        setState(() {
          _publicIpController.text = res.body.trim();
        });
      } else {
        setState(() {
          _publicIpController.text = '조회 실패';
        });
      }
    } catch (e) {
      setState(() {
        _publicIpController.text = '조회 실패';
      });
      print('[Settings] 공인 IP 조회 실패: $e');
    }
  }

  static const _channel = MethodChannel('com.quristyle.funeralv2_player/device_info');

  Future<void> _autoDetectMacAddress() async {
    // 1. Android 환경 ➡ Kotlin MethodChannel 활용
    if (Platform.isAndroid) {
      try {
        final String? mac = await _channel.invokeMethod<String>('getMacAddress');
        if (mac != null && mac.isNotEmpty && mac != '02:00:00:00:00:00') {
          setState(() {
            _macController.text = mac;
          });
          return;
        }
      } catch (e) {
        print('[Settings] 안드로이드 네이티브 MAC 감지 실패: $e');
      }
    }

    // 2. Windows 환경 ➡ getmac 명령어 실행 후 첫 번째 활성 주소 파싱
    if (Platform.isWindows) {
      try {
        final result = await Process.run('getmac', ['/fo', 'csv', '/nh']);
        if (result.exitCode == 0) {
          final lines = result.stdout.toString().split('\n');
          for (var line in lines) {
            if (line.trim().isEmpty) continue;
            final parts = line.split(',');
            if (parts.isNotEmpty) {
              final mac = parts[0]!.replaceAll('"', '').trim().replaceAll('-', ':').toUpperCase();
              if (mac.isNotEmpty && mac != 'N/A' && mac != '02:00:00:00:00:00') {
                setState(() {
                  _macController.text = mac;
                });
                return;
              }
            }
          }
        }
      } catch (e) {
        print('[Settings] 윈도우 getmac 실행 실패: $e');
      }
    }

    // 3. Linux 환경 (Ubuntu, Raspberry Pi 등) ➡ /sys/class/net/ 또는 ip link 파싱
    if (Platform.isLinux) {
      try {
        // 리눅스 파일 시스템에서 바로 읽기 시도
        for (var interfaceName in ['eth0', 'wlan0', 'enp3s0', 'wlo1', 'eth1', 'wlan1']) {
          final file = File('/sys/class/net/$interfaceName/address');
          if (await file.exists()) {
            final mac = await file.readAsString();
            final trimmedMac = mac.trim().toUpperCase();
            if (trimmedMac.isNotEmpty && trimmedMac != '02:00:00:00:00:00') {
              setState(() {
                _macController.text = trimmedMac;
              });
              return;
            }
          }
        }

        // ip link 명령어 실행 백업
        final result = await Process.run('ip', ['link']);
        if (result.exitCode == 0) {
          final stdoutStr = result.stdout.toString();
          final regExp = RegExp(r'link/ether\s+([0-9a-fA-F:]{17})');
          final match = regExp.firstMatch(stdoutStr);
          if (match != null && match.groupCount >= 1) {
            final mac = match.group(1)?.toUpperCase();
            if (mac != null) {
              setState(() {
                _macController.text = mac;
              });
              return;
            }
          }
        }
      } catch (e) {
        print('[Settings] 리눅스 MAC 감지 실패: $e');
      }
    }

    // 4. 공통 안드로이드 백업 (Android 9 이하 구버전 물리 파일 감지)
    if (Platform.isAndroid) {
      try {
        for (var interfaceName in ['eth0', 'wlan0', 'eth1', 'wlan1']) {
          final file = File('/sys/class/net/$interfaceName/address');
          if (await file.exists()) {
            final mac = await file.readAsString();
            final trimmedMac = mac.trim().toUpperCase();
            if (trimmedMac.isNotEmpty && trimmedMac != '02:00:00:00:00:00') {
              setState(() {
                _macController.text = trimmedMac;
              });
              return;
            }
          }
        }
      } catch (e) {
        print('[Settings] 안드로이드 백업 파일 조회 실패: $e');
      }
    }
  }

  Future<void> _autoDetectLocalIp() async {
    try {
      final interfaces = await NetworkInterface.list();
      for (var interface in interfaces) {
        for (var addr in interface.addresses) {
          if (addr.type == InternetAddressType.IPv4 && !addr.isLoopback) {
            setState(() {
              _ipController.text = addr.address;
            });
            return;
          }
        }
      }
    } catch (e) {
      print('[Settings] 로컬 사설 IP 감지 실패: $e');
    }
  }

  @override
  void dispose() {
    _serverController.dispose();
    _codeController.dispose();
    _ipController.dispose();
    _macController.dispose();
    _publicIpController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    // 1. 장비 오리엔테이션 상태가 세로(PORTRAIT) 모드인지 여부 판단
    final bool isPortrait = widget.initialOrientation == 'PORTRAIT';

    // 2. 입력 필드 위젯들 정의 (세로 90도 회전된 환경에서도 UI 정합성을 위해 2열 배치 기본 적용)
    final serverField = TextFormField(
      controller: _serverController,
      decoration: const InputDecoration(
        labelText: '통합 서버 주소 (Gateway)',
        labelStyle: TextStyle(color: Colors.white60, fontSize: 13),
        border: OutlineInputBorder(),
        prefixIcon: Icon(Icons.lan, color: Colors.white54, size: 20),
        contentPadding: EdgeInsets.symmetric(vertical: 12, horizontal: 10),
      ),
      validator: (v) => (v == null || v.isEmpty) ? '서버 주소를 입력해 주십시오.' : null,
    );

    final codeField = TextFormField(
      controller: _codeController,
      decoration: const InputDecoration(
        labelText: '장비 식별 코드',
        labelStyle: TextStyle(color: Colors.white60, fontSize: 13),
        border: OutlineInputBorder(),
        prefixIcon: Icon(Icons.developer_board, color: Colors.white54, size: 20),
        contentPadding: EdgeInsets.symmetric(vertical: 12, horizontal: 10),
      ),
      validator: (v) => (v == null || v.isEmpty) ? '장비코드를 입력해 주십시오.' : null,
    );

    final ipField = TextFormField(
      controller: _ipController,
      readOnly: true,
      enabled: false,
      style: const TextStyle(color: Colors.white38, fontSize: 13),
      decoration: const InputDecoration(
        labelText: '사설 IP 주소 (자동 감지)',
        labelStyle: TextStyle(color: Colors.white38, fontSize: 12),
        disabledBorder: OutlineInputBorder(
          borderSide: BorderSide(color: Colors.white12),
        ),
        prefixIcon: Icon(Icons.settings_ethernet, color: Colors.white24, size: 20),
        contentPadding: EdgeInsets.symmetric(vertical: 12, horizontal: 10),
      ),
    );

    final publicIpField = TextFormField(
      controller: _publicIpController,
      readOnly: true,
      enabled: false,
      style: const TextStyle(color: Colors.white38, fontSize: 13),
      decoration: const InputDecoration(
        labelText: '공인 IP 주소 (자동 감지)',
        labelStyle: TextStyle(color: Colors.white38, fontSize: 12),
        disabledBorder: OutlineInputBorder(
          borderSide: BorderSide(color: Colors.white12),
        ),
        prefixIcon: Icon(Icons.public, color: Colors.white24, size: 20),
        contentPadding: EdgeInsets.symmetric(vertical: 12, horizontal: 10),
      ),
    );

    final macField = TextFormField(
      controller: _macController,
      readOnly: true,
      enabled: false,
      style: const TextStyle(color: Colors.white38, fontSize: 13),
      decoration: const InputDecoration(
        labelText: '장비 맥 주소 (MAC Address - 자동 감지)',
        labelStyle: TextStyle(color: Colors.white38, fontSize: 12),
        disabledBorder: OutlineInputBorder(
          borderSide: BorderSide(color: Colors.white12),
        ),
        prefixIcon: Icon(Icons.fingerprint, color: Colors.white24, size: 20),
        contentPadding: EdgeInsets.symmetric(vertical: 12, horizontal: 10),
      ),
    );

    final mainContent = Scaffold(
      backgroundColor: const Color(0xFF121212),
      appBar: AppBar(
        title: const Text('사이니지 플레이어 환경 설정'),
        backgroundColor: Colors.black,
        elevation: 0,
        centerTitle: true,
      ),
      body: Center(
        child: SingleChildScrollView(
          // 가로모드는 스크롤 방지, 세로모드는 높이가 길어질 수 있으므로 탄력 스크롤 허용
          physics: isPortrait ? const ClampingScrollPhysics() : const NeverScrollableScrollPhysics(),
          padding: const EdgeInsets.symmetric(horizontal: 24.0, vertical: 16.0),
          child: Container(
            constraints: BoxConstraints(maxWidth: isPortrait ? 450 : 650),
            padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 24),
            decoration: BoxDecoration(
              color: Colors.black,
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: const Color(0xFFC0A060).withOpacity(0.3), width: 1.5),
              boxShadow: const [
                BoxShadow(
                  color: Colors.black54,
                  blurRadius: 40,
                  spreadRadius: 10,
                ),
              ],
            ),
            child: Form(
              key: _formKey,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Icon(
                    Icons.settings_suggest,
                    color: Color(0xFFC0A060),
                    size: 44,
                  ),
                  const SizedBox(height: 12),
                  const Text(
                    '통합 서버 설정',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 4),
                  const Text(
                    '장비 구동을 위해 정보를 입력 및 확인해 주십시오.',
                    textAlign: TextAlign.center,
                    style: TextStyle(color: Colors.white38, fontSize: 11),
                  ),
                  const SizedBox(height: 20),

                  // ─── 필드 레이아웃 렌더링 ───
                  Row(
                    children: [
                      Expanded(child: serverField),
                      const SizedBox(width: 16),
                      Expanded(child: codeField),
                    ],
                  ),
                  const SizedBox(height: 14),
                  Row(
                    children: [
                      Expanded(child: ipField),
                      const SizedBox(width: 16),
                      Expanded(child: publicIpField),
                    ],
                  ),
                  const SizedBox(height: 14),
                  macField,

                  const SizedBox(height: 24), // 버튼 위 여백 최적화

                  // 저장 버튼
                  ElevatedButton(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFFC0A060),
                      foregroundColor: Colors.black,
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(8),
                      ),
                      textStyle: const TextStyle(fontSize: 15, fontWeight: FontWeight.bold),
                    ),
                    onPressed: () {
                      if (_formKey.currentState!.validate()) {
                        widget.onSave(
                          _serverController.text.trim(),
                          _codeController.text.trim(),
                          _ipController.text.trim(),
                          _macController.text.trim(),
                          _publicIpController.text.trim(),
                        );
                      }
                    },
                    child: const Text('설정 저장 및 실행'),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );

    // 장비가 세로형 모니터 모드(PORTRAIT)일 경우, 뷰포트 전체를 90도 소프트웨어 회전
    if (isPortrait) {
      return RotatedBox(
        quarterTurns: 1, // 90도 시계방향 회전
        child: mainContent,
      );
    }

    return mainContent;
  }
}
