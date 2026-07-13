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

  print('[Main] 프로그램 구동 시작');

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
      print('[Main] 창 설정 실패: $e');
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
    print('[MainRouter] 설정 정보 로드 중...');
    final prefs = await SharedPreferences.getInstance();
    
    setState(() {
      serverBaseUrl = prefs.getString('serverBaseUrl') ?? 'http://localhost:5265';
      deviceCode = prefs.getString('deviceCode');
      ipAddress = prefs.getString('ipAddress') ?? '';
      macAddress = prefs.getString('macAddress') ?? '';
      publicIpAddress = prefs.getString('publicIpAddress') ?? '';
      displayOrientation = prefs.getString('displayOrientation') ?? 'LANDSCAPE';
      
      isConfigured = deviceCode != null && deviceCode!.isNotEmpty;
      isLoading = false;
    });
    print('[MainRouter] 로드 완료: isConfigured=$isConfigured, code=$deviceCode');
  }

  Future<void> _saveConfiguration(String server, String code, String ip, String mac, String publicIp) async {
    print('[MainRouter] 설정 저장: code=$code');
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
    print('[MainRouter] build() 진입: isLoading=$isLoading, isConfigured=$isConfigured');
    
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

    print('[MainRouter] DeviceDispatcher로 분기합니다.');
    return DeviceDispatcher(
      serverBaseUrl: serverBaseUrl!,
      deviceCode: deviceCode!,
      ipAddress: ipAddress ?? '',
      macAddress: macAddress ?? '',
      publicIpAddress: publicIpAddress ?? '',
      onOpenSettings: () {
        print('[MainRouter] 설정 변경 요청 수신');
        setState(() => isConfigured = false);
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

  String _connectionStatus = 'IDLE'; 
  String _statusMessage = '서버 연결 상태를 확인해 주십시오.';

  @override
  void initState() {
    super.initState();
    _serverController = TextEditingController(text: widget.initialServer);
    _codeController = TextEditingController(text: widget.initialCode);
    _ipController = TextEditingController(text: widget.initialIp);
    _macController = TextEditingController(text: widget.initialMac);
    _publicIpController = TextEditingController(text: '대기 중...');
    
    WidgetsBinding.instance.addPostFrameCallback((_) => _testConnection());
  }

  Future<void> _testConnection() async {
    final url = _serverController.text.trim();
    if (url.isEmpty) return;

    print('[Settings] 서버 연결 테스트 시도: $url');
    setState(() {
      _connectionStatus = 'TESTING';
      _statusMessage = '서버 연결 확인 중...';
    });

    try {
      final baseUrl = url.endsWith('/') ? url.substring(0, url.length - 1) : url;
      final response = await http.get(Uri.parse('$baseUrl/api/funeral/building/device/code/HEALTH_CHECK'))
          .timeout(const Duration(seconds: 4));

      setState(() {
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

  @override
  Widget build(BuildContext context) {
    final bool isPortrait = widget.initialOrientation == 'PORTRAIT';
    Color statusColor = _connectionStatus == 'SUCCESS' ? Colors.greenAccent : (_connectionStatus == 'FAIL' ? Colors.redAccent : Colors.orangeAccent);

    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(title: const Text('환경 설정'), centerTitle: true, backgroundColor: Colors.black),
      body: Center(
        child: Container(
          constraints: const BoxConstraints(maxWidth: 600),
          padding: const EdgeInsets.all(32),
          decoration: BoxDecoration(
            border: Border.all(color: const Color(0xFFC0A060).withOpacity(0.3)),
            borderRadius: BorderRadius.circular(16),
          ),
          child: Form(
            key: _formKey,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                // 연결 상태 뱃지
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(color: statusColor.withOpacity(0.1), borderRadius: BorderRadius.circular(8), border: Border.all(color: statusColor.withOpacity(0.3))),
                  child: Row(
                    children: [
                      Icon(_connectionStatus == 'SUCCESS' ? Icons.check_circle : Icons.error, color: statusColor, size: 20),
                      const SizedBox(width: 12),
                      Expanded(child: Text(_statusMessage, style: TextStyle(color: statusColor, fontWeight: FontWeight.bold))),
                      IconButton(icon: const Icon(Icons.refresh), onPressed: _testConnection, color: statusColor),
                    ],
                  ),
                ),
                const SizedBox(height: 24),
                TextFormField(
                  controller: _serverController,
                  decoration: const InputDecoration(labelText: '통합 서버 주소', border: OutlineInputBorder()),
                  onChanged: (_) => setState(() => _connectionStatus = 'IDLE'),
                  validator: (v) => (v == null || v.isEmpty) ? '서버 주소 필수' : null,
                ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: _codeController,
                  decoration: const InputDecoration(labelText: '장비 코드', border: OutlineInputBorder()),
                  validator: (v) => (v == null || v.isEmpty) ? '장비 코드 필수' : null,
                ),
                const SizedBox(height: 32),
                ElevatedButton(
                  style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFFC0A060), foregroundColor: Colors.black, minimumSize: const Size(double.infinity, 50)),
                  onPressed: () {
                    if (_formKey.currentState!.validate()) {
                      widget.onSave(_serverController.text.trim(), _codeController.text.trim(), '', '', '');
                    }
                  },
                  child: const Text('저장 및 실행', style: TextStyle(fontWeight: FontWeight.bold)),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
