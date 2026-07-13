import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:http/http.dart' as http;
import 'package:media_kit/media_kit.dart';
import 'package:window_manager/window_manager.dart';
import 'pages/device_dispatcher.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  MediaKit.ensureInitialized();

  if (Platform.isWindows || Platform.isLinux) {
    try {
      await windowManager.ensureInitialized();
      WindowOptions windowOptions = const WindowOptions(
        center: true,
        backgroundColor: Colors.black,
        skipTaskbar: false,
        titleBarStyle: TitleBarStyle.hidden,
      );
      await windowManager.waitUntilReadyToShow(windowOptions, () async {
        await windowManager.setFullScreen(true);
        await windowManager.setAlwaysOnTop(true);
        await windowManager.show();
        await windowManager.focus();
      });
    } catch (e) {
      print('[Kiosk] 데스크톱 창 설정 실패: $e');
    }
  }

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

  Future<void> _loadConfiguration() async {
    print('[Main] _loadConfiguration() 시작');
    final prefs = await SharedPreferences.getInstance();
    var savedPublicIp = prefs.getString('publicIpAddress') ?? '';

    if (savedPublicIp.isEmpty) {
      try {
        final res = await http.get(Uri.parse('https://api.ipify.org')).timeout(const Duration(seconds: 3));
        if (res.statusCode == 200) {
          savedPublicIp = res.body.trim();
          await prefs.setString('publicIpAddress', savedPublicIp);
        }
      } catch (e) {
        print('[Main] 초기 공인 IP 조회 실패: $e');
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

  Future<void> _saveConfiguration(String server, String code, String ip, String mac, String publicIp) async {
    print('[Main] _saveConfiguration 진입: code=$code');
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
      return const Scaffold(body: Center(child: CircularProgressIndicator(color: Color(0xFFC0A060))));
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

  // 서버 연결 상태
  String _connectionStatus = 'IDLE'; // IDLE, TESTING, SUCCESS, FAIL
  String _statusMessage = '서버 연결을 확인해 주십시오.';

  @override
  void initState() {
    super.initState();
    _serverController = TextEditingController(text: widget.initialServer);
    _codeController = TextEditingController(text: widget.initialCode);
    _ipController = TextEditingController(text: widget.initialIp);
    _macController = TextEditingController(text: widget.initialMac);
    _publicIpController = TextEditingController(text: '조회 중...');

    if (widget.initialIp.isEmpty) _autoDetectLocalIp();
    if (widget.initialMac.isEmpty) _autoDetectMacAddress();
    _autoDetectPublicIp();
    
    // 시작 시 자동 테스트
    WidgetsBinding.instance.addPostFrameCallback((_) => _testConnection());
  }

  Future<void> _testConnection() async {
    final url = _serverController.text.trim();
    if (url.isEmpty) return;

    setState(() {
      _connectionStatus = 'TESTING';
      _statusMessage = '서버에 접속 시도 중...';
    });

    try {
      final baseUrl = url.endsWith('/') ? url.substring(0, url.length - 1) : url;
      // 헬스체크 대용 장비 조회 API (코드는 더미)
      final response = await http.get(Uri.parse('$baseUrl/api/funeral/building/device/code/HEALTH_CHECK'))
          .timeout(const Duration(seconds: 4));

      setState(() {
        // 401(Unauthorized)이나 404가 와도 서버 응답이 있으면 '접속 가능'으로 간주
        if (response.statusCode < 500) {
          _connectionStatus = 'SUCCESS';
          _statusMessage = '서버 연결 확인 완료';
        } else {
          _connectionStatus = 'FAIL';
          _statusMessage = '서버 응답 오류 (HTTP ${response.statusCode})';
        }
      });
    } catch (e) {
      setState(() {
        _connectionStatus = 'FAIL';
        _statusMessage = '연결 실패: 서버 주소나 네트워크를 확인하세요.';
      });
    }
  }

  Future<void> _autoDetectPublicIp() async {
    try {
      final res = await http.get(Uri.parse('https://api.ipify.org')).timeout(const Duration(seconds: 4));
      if (res.statusCode == 200) {
        setState(() { _publicIpController.text = res.body.trim(); });
      } else {
        setState(() { _publicIpController.text = '조회 실패'; });
      }
    } catch (e) {
      setState(() { _publicIpController.text = '조회 실패'; });
    }
  }

  Future<void> _autoDetectMacAddress() async {
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
              if (mac.isNotEmpty && mac != 'N/A') {
                setState(() { _macController.text = mac; });
                return;
              }
            }
          }
        }
      } catch (_) {}
    }
    // 기타 플랫폼 로직 생략 (기존과 동일)
  }

  Future<void> _autoDetectLocalIp() async {
    try {
      final interfaces = await NetworkInterface.list();
      for (var interface in interfaces) {
        for (var addr in interface.addresses) {
          if (addr.type == InternetAddressType.IPv4 && !addr.isLoopback) {
            setState(() { _ipController.text = addr.address; });
            return;
          }
        }
      }
    } catch (_) {}
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
    final bool isPortrait = widget.initialOrientation == 'PORTRAIT';

    // 상태 색상 결정
    Color statusColor = Colors.white24;
    IconData statusIcon = Icons.help_outline;
    if (_connectionStatus == 'TESTING') {
      statusColor = Colors.orangeAccent;
      statusIcon = Icons.sync;
    } else if (_connectionStatus == 'SUCCESS') {
      statusColor = Colors.greenAccent;
      statusIcon = Icons.check_circle_outline;
    } else if (_connectionStatus == 'FAIL') {
      statusColor = Colors.redAccent;
      statusIcon = Icons.error_outline;
    }

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
          physics: isPortrait ? const ClampingScrollPhysics() : const NeverScrollableScrollPhysics(),
          padding: const EdgeInsets.symmetric(horizontal: 24.0, vertical: 16.0),
          child: Container(
            constraints: BoxConstraints(maxWidth: isPortrait ? 450 : 650),
            padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 24),
            decoration: BoxDecoration(
              color: Colors.black,
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: const Color(0xFFC0A060).withOpacity(0.3), width: 1.5),
            ),
            child: Form(
              key: _formKey,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // 상단 연결 상태 표시 뱃지
                  Container(
                    padding: const EdgeInsets.symmetric(vertical: 10, horizontal: 16),
                    decoration: BoxDecoration(
                      color: statusColor.withOpacity(0.1),
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(color: statusColor.withOpacity(0.3)),
                    ),
                    child: Row(
                      children: [
                        Icon(statusIcon, color: statusColor, size: 20),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Text(_statusMessage, style: TextStyle(color: statusColor, fontSize: 13, fontWeight: FontWeight.bold)),
                        ),
                        if (_connectionStatus != 'TESTING')
                          IconButton(
                            icon: Icon(Icons.refresh, color: statusColor, size: 20),
                            onPressed: _testConnection,
                            padding: EdgeInsets.zero,
                            constraints: const BoxConstraints(),
                          ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 32),

                  Row(
                    children: [
                      Expanded(
                        child: TextFormField(
                          controller: _serverController,
                          decoration: const InputDecoration(
                            labelText: '통합 서버 주소 (Gateway)',
                            labelStyle: TextStyle(color: Colors.white60, fontSize: 13),
                            border: OutlineInputBorder(),
                            prefixIcon: Icon(Icons.lan, color: Colors.white54, size: 20),
                          ),
                          onChanged: (_) => setState(() { _connectionStatus = 'IDLE'; }),
                          validator: (v) => (v == null || v.isEmpty) ? '필수 입력' : null,
                        ),
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        child: TextFormField(
                          controller: _codeController,
                          decoration: const InputDecoration(
                            labelText: '장비 식별 코드',
                            labelStyle: TextStyle(color: Colors.white60, fontSize: 13),
                            border: OutlineInputBorder(),
                            prefixIcon: Icon(Icons.developer_board, color: Colors.white54, size: 20),
                          ),
                          validator: (v) => (v == null || v.isEmpty) ? '필수 입력' : null,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  Row(
                    children: [
                      Expanded(
                        child: TextFormField(
                          controller: _ipController,
                          readOnly: true,
                          style: const TextStyle(color: Colors.white38, fontSize: 13),
                          decoration: const InputDecoration(labelText: '사설 IP', border: OutlineInputBorder()),
                        ),
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        child: TextFormField(
                          controller: _publicIpController,
                          readOnly: true,
                          style: const TextStyle(color: Colors.white38, fontSize: 13),
                          decoration: const InputDecoration(labelText: '공인 IP', border: OutlineInputBorder()),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: _macController,
                    readOnly: true,
                    style: const TextStyle(color: Colors.white38, fontSize: 13),
                    decoration: const InputDecoration(labelText: 'MAC 주소', border: OutlineInputBorder()),
                  ),

                  const SizedBox(height: 32),

                  ElevatedButton(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFFC0A060),
                      foregroundColor: Colors.black,
                      padding: const EdgeInsets.symmetric(vertical: 16),
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
                    child: const Text('설정 저장 및 실행', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );

    return isPortrait ? RotatedBox(quarterTurns: 1, child: mainContent) : mainContent;
  }
}
