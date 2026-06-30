import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'pages/portrait/portrait_page.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
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
  String? apiServerUrl;
  String? fileServerUrl;
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
      apiServerUrl = prefs.getString('apiServerUrl') ?? 'http://10.0.2.2:5000'; // 에뮬레이터 기본 백엔드 루프백
      fileServerUrl = prefs.getString('fileServerUrl') ?? 'http://10.0.2.2:5001'; // 에뮬레이터 기본 파일서버 루프백
      deviceCode = prefs.getString('deviceCode');
      
      // 장비 코드가 입력되어 있으면 정상 재생 모드로 진입
      isConfigured = deviceCode != null && deviceCode!.isNotEmpty;
      isLoading = false;
    });
  }

  // 설정 저장 처리
  Future<void> _saveConfiguration(String api, String file, String code) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('apiServerUrl', api);
    await prefs.setString('fileServerUrl', file);
    await prefs.setString('deviceCode', code);

    setState(() {
      apiServerUrl = api;
      fileServerUrl = file;
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
        initialApi: apiServerUrl ?? 'http://10.0.2.2:5000',
        initialFile: fileServerUrl ?? 'http://10.0.2.2:5001',
        initialCode: deviceCode ?? '',
        onSave: _saveConfiguration,
      );
    }

    return PortraitPage(
      apiServerUrl: apiServerUrl!,
      fileServerUrl: fileServerUrl!,
      deviceCode: deviceCode!,
      onOpenSettings: () {
        setState(() {
          isConfigured = false; // 설정을 열기 위해 분기 전환
        });
      },
    );
  }
}

/// 플레이어 환경 설정 화면 (장비 코드 및 서버 주소 변경용)
class SettingsScreen extends StatefulWidget {
  final String initialApi;
  final String initialFile;
  final String initialCode;
  final Function(String api, String file, String code) onSave;

  const SettingsScreen({
    super.key,
    required this.initialApi,
    required this.initialFile,
    required this.initialCode,
    required this.onSave,
  });

  @override
  State<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends State<SettingsScreen> {
  final _formKey = GlobalKey<FormState>();
  late TextEditingController _apiController;
  late TextEditingController _fileController;
  late TextEditingController _codeController;

  @override
  void initState() {
    super.initState();
    _apiController = TextEditingController(text: widget.initialApi);
    _fileController = TextEditingController(text: widget.initialFile);
    _codeController = TextEditingController(text: widget.initialCode);
  }

  @override
  void dispose() {
    _apiController.dispose();
    _fileController.dispose();
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
                    '디바이스 및 서버 초기화',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 22,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    '사이니지 구동에 필요한 주소와 장비코드를 입력해 주십시오.',
                    textAlign: TextAlign.center,
                    style: TextStyle(color: Colors.white38, fontSize: 13),
                  ),
                  const SizedBox(height: 32),

                  // 1. API 서버 주소
                  TextFormField(
                    controller: _apiController,
                    decoration: const InputDecoration(
                      labelText: 'API 서버 주소 (REST/SignalR)',
                      labelStyle: TextStyle(color: Colors.white60),
                      border: OutlineInputBorder(),
                      prefixIcon: Icon(Icons.link, color: Colors.white50),
                    ),
                    validator: (v) => (v == null || v.isEmpty) ? 'API 주소를 입력해 주십시오.' : null,
                  ),
                  const SizedBox(height: 20),

                  // 2. 파일 서버 주소
                  TextFormField(
                    controller: _fileController,
                    decoration: const InputDecoration(
                      labelText: '미디어 파일 서버 주소',
                      labelStyle: TextStyle(color: Colors.white60),
                      border: OutlineInputBorder(),
                      prefixIcon: Icon(Icons.cloud_download, color: Colors.white50),
                    ),
                    validator: (v) => (v == null || v.isEmpty) ? '파일 서버 주소를 입력해 주십시오.' : null,
                  ),
                  const SizedBox(height: 20),

                  // 3. 장비 식별 코드
                  TextFormField(
                    controller: _codeController,
                    decoration: const InputDecoration(
                      labelText: '장비 식별 코드 (예: DID-001)',
                      labelStyle: TextStyle(color: Colors.white60),
                      border: OutlineInputBorder(),
                      prefixIcon: Icon(Icons.developer_board, color: Colors.white50),
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
                          _apiController.text.trim(),
                          _fileController.text.trim(),
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
