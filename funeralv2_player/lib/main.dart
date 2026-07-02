import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:media_kit/media_kit.dart';
import 'pages/device_dispatcher.dart'; // 수정됨

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  MediaKit.ensureInitialized();
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
    setState(() {
      serverBaseUrl = prefs.getString('serverBaseUrl') ?? 'http://localhost:5265';
      deviceCode = prefs.getString('deviceCode')??'JSI-06-0001';
      
      isConfigured = deviceCode != null && deviceCode!.isNotEmpty;
      isLoading = false;
    });
  }

  // 설정 저장 처리
  Future<void> _saveConfiguration(String server, String code) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('serverBaseUrl', server);
    await prefs.setString('deviceCode', code);

    setState(() {
      serverBaseUrl = server;
      deviceCode = code;
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
        onSave: _saveConfiguration,
      );
    }

    return DeviceDispatcher(
      serverBaseUrl: serverBaseUrl!,
      deviceCode: deviceCode!,
      onOpenSettings: () {
        setState(() {
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
  final Function(String server, String code) onSave;

  const SettingsScreen({
    super.key,
    required this.initialServer,
    required this.initialCode,
    required this.onSave,
  });

  @override
  State<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends State<SettingsScreen> {
  final _formKey = GlobalKey<FormState>();
  late TextEditingController _serverController;
  late TextEditingController _codeController;

  @override
  void initState() {
    super.initState();
    _serverController = TextEditingController(text: widget.initialServer);
    _codeController = TextEditingController(text: widget.initialCode);
  }

  @override
  void dispose() {
    _serverController.dispose();
    _codeController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF121212),
      appBar: AppBar(
        title: const Text('사이니지 플레이어 환경 설정'),
        backgroundColor: Colors.black,
        elevation: 0,
        centerTitle: true,
      ),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(32.0),
          child: Container(
            constraints: const BoxConstraints(maxWidth: 500),
            padding: const EdgeInsets.all(40),
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
                    size: 64,
                  ),
                  const SizedBox(height: 24),
                  const Text(
                    '통합 서버 초기화',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 22,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    '게이트웨이 주소와 장비코드를 입력해 주십시오.',
                    textAlign: TextAlign.center,
                    style: TextStyle(color: Colors.white38, fontSize: 13),
                  ),
                  const SizedBox(height: 32),

                  // 통합 서버 주소
                  TextFormField(
                    controller: _serverController,
                    decoration: const InputDecoration(
                      labelText: '통합 서버 주소 (Gateway)',
                      labelStyle: TextStyle(color: Colors.white60),
                      border: OutlineInputBorder(),
                      prefixIcon: Icon(Icons.lan, color: Colors.white54),
                    ),
                    validator: (v) => (v == null || v.isEmpty) ? '서버 주소를 입력해 주십시오.' : null,
                  ),
                  const SizedBox(height: 20),

                  // 장비 식별 코드
                  TextFormField(
                    controller: _codeController,
                    decoration: const InputDecoration(
                      labelText: '장비 식별 코드',
                      labelStyle: TextStyle(color: Colors.white60),
                      border: OutlineInputBorder(),
                      prefixIcon: Icon(Icons.developer_board, color: Colors.white54),
                    ),
                    validator: (v) => (v == null || v.isEmpty) ? '장비코드를 입력해 주십시오.' : null,
                  ),
                  const SizedBox(height: 36),

                  // 저장 버튼
                  ElevatedButton(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFFC0A060),
                      foregroundColor: Colors.black,
                      padding: const EdgeInsets.symmetric(vertical: 16),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(8),
                      ),
                      textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                    ),
                    onPressed: () {
                      if (_formKey.currentState!.validate()) {
                        widget.onSave(
                          _serverController.text.trim(),
                          _codeController.text.trim(),
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
  }
}
