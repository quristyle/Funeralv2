import 'dart:io';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:media_kit/media_kit.dart';
import 'package:window_manager/window_manager.dart';
import 'pages/device_dispatcher.dart';
import 'pages/settings_screen.dart'; // 분리된 환경설정 화면 임포트
import 'services/cache/cache_manager.dart';
import 'widgets/auto_hide_cursor.dart';
import 'services/display/display_mode_service.dart';
import 'package:http/http.dart' as http;

/// [앱의 시작점] main 함수
/// Flutter 앱이 구동될 때 가장 먼저 호출되는 함수입니다.
/// async 키워드는 이 함수 내부에서 비동기(기다림이 필요한) 작업을 수행함을 의미합니다.
void main() async {
  // Flutter 프레임워크가 위젯을 그릴 준비가 될 때까지 기다립니다.
  WidgetsFlutterBinding.ensureInitialized();
  
  // 동영상 재생 엔진인 MediaKit을 초기화합니다.
  MediaKit.ensureInitialized();

  print('[Main] 프로그램 구동 시작');

  // 현재 실행되는 운영체제가 Windows 또는 Linux인 경우 창 설정을 진행합니다.
  if (Platform.isWindows || Platform.isLinux) {
    try {
      // 창 관리자(window_manager)를 초기화합니다.
      await windowManager.ensureInitialized();

      // 저장된 서버 URL을 확인하여 로컬 개발 환경인지 판별합니다.
      final prefs = await SharedPreferences.getInstance();
      final serverUrl = prefs.getString('serverBaseUrl') ?? '';
      final isLocal = serverUrl.contains('localhost') || serverUrl.contains('127.0.0.1');

      WindowOptions windowOptions = const WindowOptions(
      );

      // [로컬 환경 체크] localhost가 아닐 때만 전체화면과 최상단 고정 설정을 적용합니다.
      if (!isLocal) {
        // 창의 초기 옵션을 설정합니다. (중앙 정렬, 검은 배경, 제목 표시줄 숨김 등)
        windowOptions = const WindowOptions(
          center: true,
          backgroundColor: Colors.black,
          skipTaskbar: false,
          titleBarStyle: TitleBarStyle.hidden,
        );
      }
      
      // 설정한 옵션으로 창이 준비되면 화면에 표시합니다.
      await windowManager.waitUntilReadyToShow(windowOptions, () async {
        await windowManager.show();
        await windowManager.focus();
        
        // [로컬 환경 체크] localhost가 아닐 때만 전체화면과 최상단 고정 설정을 적용합니다.
        if (!isLocal) {
          print('[Main] 상용 환경 감지: 전체화면 및 최상단 고정을 적용합니다.');
          Future.delayed(const Duration(milliseconds: 200), () async {
            await windowManager.setFullScreen(true);
            await windowManager.setAlwaysOnTop(true);
          });
        } else {
          print('[Main] 로컬 개발 환경 감지: 전체화면 설정을 건너뜁니다.');
        }
      });
    } catch (e) {
      print('[Main] 창 설정 실패: $e');
    }
  }

  // [미디어 캐시 정리 기동]
  // 장례 행사가 바뀔 때마다 새 영정/영상이 쌓이므로, TTL 과 용량 상한으로 주기 정리한다.
  CacheManager().startPeriodicGarbageCollection();

  // 실제 앱 위젯 트리를 실행합니다.
  runApp(const FuneralPlayerApp());

  // [화면 해상도 복원]
  // 설정 화면에서 선택한 화면 비율(16:9 / 16:10)을 다시 적용합니다.
  //
  // 반드시 창이 화면에 올라온 뒤에 적용해야 한다.
  // runApp 이전에 적용하면 이후 앱 표면이 생성될 때 컴포지터가 출력 모드를
  // EDID 선호 모드(4K30 등)로 되돌려버려, 로그상 "적용 완료"인데 실제로는 반영되지 않는다.
  WidgetsBinding.instance.addPostFrameCallback((_) {
    Future.delayed(const Duration(seconds: 2), () {
      DisplayModeService.applySavedOnStartup();
    });
  });
}

/// [앱 루트 위젯]
/// 앱의 전체 테마와 제목, 그리고 첫 화면을 설정합니다.
class FuneralPlayerApp extends StatelessWidget {
  const FuneralPlayerApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'JSINI', // 안드로이드 '최근 앱' 목록에 뜨는 이름
      debugShowCheckedModeBanner: false, // 오른쪽 상단 디버그 띠를 숨깁니다.
      theme: ThemeData(
        fontFamily: 'NanumGothic', // 번들 한글 폰트 (Linux 두부 현상 방지)
        brightness: Brightness.dark, // 어두운 테마 사용
        scaffoldBackgroundColor: Colors.black, // 기본 배경색은 검정
        primaryColor: const Color(0xFFC0A060), // 금색 포인트 컬러
      ),
      // 마우스 포인터는 움직일 때만 보이고 멈추면 사라집니다. (사이니지 요건)
      builder: (context, child) => AutoHideCursor(child: child ?? const SizedBox.shrink()),
      // 앱의 실제 관문 역할을 하는 MainRouter를 홈 화면으로 설정합니다.
      home: const MainRouter(),
    );
  }
}

/// [메인 라우터 위젯]
/// 저장된 설정값 유무에 따라 '설정 화면'을 보여줄지, '플레이어 화면'을 보여줄지 결정합니다.
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
  int displayRotationTurns = 0; // [추가] 화면 회전 상태 수치 (0: 0도, 1: 90도, 2: 180도, 3: 270도)
  bool isConfigured = false; // 설정 완료 여부 플래그
  bool isLoading = true;     // 데이터 로딩 중 여부 플래그

  /// 위젯이 처음 생성될 때 실행되는 초기화 함수입니다.
  @override
  void initState() {
    super.initState();
    // 저장된 설정 정보를 불러옵니다.
    _loadConfiguration();
  }

  /// 기기 로컬 저장소(SharedPreferences)에서 설정 정보를 읽어오는 함수입니다.
  Future<void> _loadConfiguration() async {
    print('[MainRouter] _loadConfiguration() 호출됨 - 로컬 SharedPreferences 스캔 시작');
    final prefs = await SharedPreferences.getInstance();
    
    setState(() {
      // 저장된 값이 없으면 기본값을 사용하거나 빈 값을 할당합니다.
      serverBaseUrl = prefs.getString('serverBaseUrl') ?? 'http://localhost:5265';
      deviceCode = prefs.getString('deviceCode');
      ipAddress = prefs.getString('ipAddress') ?? '';
      macAddress = prefs.getString('macAddress') ?? '';
      publicIpAddress = prefs.getString('publicIpAddress') ?? '';
      displayOrientation = prefs.getString('displayOrientation') ?? 'LANDSCAPE';
      
      // 로컬 회전 각도 로드 (없을 경우 displayOrientation이 PORTRAIT이면 1로 하위 호환 매핑)
      displayRotationTurns = prefs.getInt('displayRotationTurns') ?? (displayOrientation == 'PORTRAIT' ? 1 : 0);
      
      // 장비 코드가 로컬에 존재하면 이미 설정된 것으로 간주합니다.
      isConfigured = deviceCode != null && deviceCode!.isNotEmpty;
      isLoading = false; // 로딩 완료
    });
    print('[MainRouter] _loadConfiguration() 완료: isConfigured=$isConfigured, code=$deviceCode, displayOrientation=$displayOrientation, displayRotationTurns=$displayRotationTurns');
  }

  /// 사용자가 입력한 새로운 설정 정보를 로컬 저장소에 저장하는 함수입니다.
  Future<void> _saveConfiguration(String server, String code, String ip, String mac, String publicIp, int rotationTurns) async {
    print('[MainRouter] _saveConfiguration() 저장 진입: code=$code, rotationTurns=$rotationTurns');
    
    // 기존 장비코드가 존재하고, 새로 입력한 장비코드와 다를 경우 백엔드에 즉시 OFFLINE 처리 요청
    final oldCode = deviceCode;
    if (oldCode != null && oldCode.isNotEmpty && oldCode != code) {
      print('[MainRouter] 장비코드 변경 감지: $oldCode -> $code. 기존 장비 오프라인 전환 요청 전송.');
      try {
        final cleanServer = server.endsWith('/') ? server.substring(0, server.length - 1) : server;
        final url = Uri.parse('$cleanServer/api/funeral/building/device/status/$oldCode?status=OFFLINE');
        await http.put(url).timeout(const Duration(seconds: 3));
        print('[MainRouter] 기존 장비($oldCode) OFFLINE 처리 완료');
      } catch (e) {
        print('[MainRouter] 기존 장비 오프라인 처리 통신 실패: $e');
      }
    }

    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('serverBaseUrl', server);
    await prefs.setString('deviceCode', code);
    await prefs.setString('ipAddress', ip);
    await prefs.setString('macAddress', mac);
    await prefs.setString('publicIpAddress', publicIp);
    
    // 화면 회전 상태를 저장하고 이에 맞추어 displayOrientation 문자열 매핑 저장
    await prefs.setInt('displayRotationTurns', rotationTurns);
    final String mappedOrientation = (rotationTurns % 2 == 1) ? 'PORTRAIT' : 'LANDSCAPE';
    await prefs.setString('displayOrientation', mappedOrientation);
    print('[MainRouter] SharedPreferences 쓰기 완료: displayOrientation=$mappedOrientation, displayRotationTurns=$rotationTurns');

    setState(() {
      serverBaseUrl = server;
      deviceCode = code;
      ipAddress = ip;
      macAddress = mac;
      publicIpAddress = publicIp;
      displayRotationTurns = rotationTurns;
      displayOrientation = mappedOrientation;
      isConfigured = true; // 저장 완료 시 설정 상태를 true로 변경하여 화면을 전환시킵니다.
    });
  }

  /// 화면의 모습을 그리는 함수입니다. 상태가 바뀔 때마다 다시 호출됩니다.
  @override
  Widget build(BuildContext context) {
    print('[MainRouter] build() 진입: isLoading=$isLoading, isConfigured=$isConfigured, displayOrientation=$displayOrientation, displayRotationTurns=$displayRotationTurns');
    
    // 아직 로딩 중이라면 로딩 바만 중앙에 띄웁니다.
    if (isLoading) {
      return const Scaffold(body: Center(child: CircularProgressIndicator(color: Color(0xFFC0A060))));
    }

    // 설정이 안 되어 있다면 '환경 설정' 화면을 반환합니다.
    if (!isConfigured) {
      print('[MainRouter] SettingsScreen 생성 및 전달: '
            'initialOrientation=${displayOrientation ?? 'LANDSCAPE'}, '
            'initialRotationTurns=$displayRotationTurns');
      return SettingsScreen(
        initialServer: serverBaseUrl ?? 'http://localhost:5265',
        initialCode: deviceCode ?? '',
        initialIp: ipAddress ?? '',
        initialMac: macAddress ?? '',
        initialPublicIp: publicIpAddress ?? '',
        initialOrientation: displayOrientation ?? 'LANDSCAPE',
        initialRotationTurns: displayRotationTurns, // 회전 초기값 제공
        onSave: _saveConfiguration, // 저장을 누르면 실행될 동작을 전달합니다.
        onCancel: (deviceCode != null && deviceCode!.isNotEmpty) 
            ? () => setState(() => isConfigured = true) 
            : null, // 이전에 설정된 이력이 있는 경우에만 취소 버튼 활성화
      );
    }

    // 설정이 완료되었다면 '장비 디스패처'를 반환하여 실제 콘텐츠 화면을 띄웁니다.
    print('[MainRouter] DeviceDispatcher로 분기합니다. (코드: $deviceCode)');
    return DeviceDispatcher(
      serverBaseUrl: serverBaseUrl!,
      deviceCode: deviceCode!,
      ipAddress: ipAddress ?? '',
      macAddress: macAddress ?? '',
      publicIpAddress: publicIpAddress ?? '',
      onOpenSettings: () async {
        // 플레이어 구동 중에 설정을 다시 열고 싶을 때 이 함수가 실행됩니다.
        print('[MainRouter] 설정 변경 요청 수신 -> 로컬 영속 캐시 동기화 리로드');
        final prefs = await SharedPreferences.getInstance();
        final server = prefs.getString('serverBaseUrl');
        final code = prefs.getString('deviceCode');
        final ip = prefs.getString('ipAddress');
        final mac = prefs.getString('macAddress');
        final publicIp = prefs.getString('publicIpAddress');
        final orientation = prefs.getString('displayOrientation') ?? 'LANDSCAPE';
        final rotation = prefs.getInt('displayRotationTurns') ?? (orientation == 'PORTRAIT' ? 1 : 0);

        print('[MainRouter] 동기화 완료 후 설정 화면 오픈: '
              'orientation=$orientation, rotation=$rotation');

        setState(() {
          serverBaseUrl = server;
          deviceCode = code;
          ipAddress = ip;
          macAddress = mac;
          publicIpAddress = publicIp;
          displayOrientation = orientation;
          displayRotationTurns = rotation;
          isConfigured = false;
        });
      },
    );
  }
}
