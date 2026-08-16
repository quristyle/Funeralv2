import 'dart:io';
import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// [디스플레이 해상도 프리셋]
/// 사이니지 패널은 대부분 16:9 이지만, 일부 현장은 16:10 패널을 사용한다.
/// 두 비율에 맞는 출력 해상도를 프리셋으로 제공한다.
enum DisplayAspect {
  ratio16x9('16:9', 1920, 1080),
  ratio16x10('16:10', 1920, 1200);

  const DisplayAspect(this.label, this.width, this.height);

  final String label;
  final int width;
  final int height;

  /// wlr-randr 에 넘길 모드 문자열 (예: 1920x1080@60)
  String get modeString => '${width}x$height@60';

  /// SharedPreferences 에 저장할 값
  String get storageValue => label;

  static DisplayAspect fromStorage(String? value) {
    return DisplayAspect.values.firstWhere(
      (e) => e.storageValue == value,
      orElse: () => DisplayAspect.ratio16x9,
    );
  }
}

/// [디스플레이 출력 모드 제어 서비스]
///
/// 라즈베리파이(Wayland/cage) 환경에서 출력 해상도를 바꾼다.
/// wlroots 계열 컴포지터는 커널의 video= 파라미터를 무시하고 EDID 의 선호 모드를 쓰기 때문에,
/// 컴포지터가 뜬 뒤 wlr-randr 로 모드를 지정해야 한다.
///
/// Windows 등 다른 플랫폼에서는 창 크기를 OS 가 관리하므로 이 서비스는 동작하지 않고
/// [isSupported] 가 false 를 반환한다.
class DisplayModeService {
  static const String prefsKey = 'displayAspectRatio';

  /// 기본 출력 커넥터. 환경변수 PLAYER_OUTPUT 으로 덮어쓸 수 있다.
  static String get _defaultOutput =>
      Platform.environment['PLAYER_OUTPUT'] ?? 'HDMI-A-1';

  /// 이 플랫폼에서 해상도 전환을 지원하는지 여부
  static bool get isSupported => !kIsWeb && Platform.isLinux;

  /// 저장된 비율 설정을 읽는다. 저장값이 없으면 16:9.
  static Future<DisplayAspect> loadSaved() async {
    final prefs = await SharedPreferences.getInstance();
    return DisplayAspect.fromStorage(prefs.getString(prefsKey));
  }

  /// 비율 설정을 저장한다.
  static Future<void> save(DisplayAspect aspect) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(prefsKey, aspect.storageValue);
  }

  /// 실제 출력 커넥터 이름을 조회한다. 실패 시 기본값을 돌려준다.
  /// wlr-randr 의 첫 줄이 커넥터 이름으로 시작한다. (예: `HDMI-A-1 "..."`)
  static Future<String> detectOutput() async {
    if (!isSupported) return _defaultOutput;
    try {
      final result = await Process.run('wlr-randr', const []);
      if (result.exitCode == 0) {
        for (final line in (result.stdout as String).split('\n')) {
          if (line.isEmpty || line.startsWith(' ')) continue;
          final name = line.split(' ').first.trim();
          if (name.isNotEmpty) return name;
        }
      }
    } catch (e) {
      print('[DisplayMode] 커넥터 조회 실패: $e');
    }
    return _defaultOutput;
  }

  /// 지정한 비율의 해상도를 즉시 적용한다.
  /// 반환값은 (성공 여부, 사용자에게 보여줄 메시지).
  static Future<(bool, String)> apply(DisplayAspect aspect) async {
    if (!isSupported) {
      return (false, '이 플랫폼에서는 해상도 전환을 지원하지 않습니다.');
    }

    final output = await detectOutput();
    try {
      final result = await Process.run(
        'wlr-randr',
        ['--output', output, '--mode', aspect.modeString],
      );

      if (result.exitCode == 0) {
        print('[DisplayMode] 적용 완료: $output ${aspect.modeString}');
        return (true, '${aspect.label} (${aspect.width}x${aspect.height}) 적용됨');
      }

      // 패널이 해당 해상도를 지원하지 않으면 wlr-randr 가 실패한다.
      final err = (result.stderr as String).trim();
      print('[DisplayMode] 적용 실패: $err');
      return (false, '적용 실패: 패널이 ${aspect.width}x${aspect.height} 를 지원하지 않을 수 있습니다.');
    } on ProcessException catch (e) {
      print('[DisplayMode] wlr-randr 실행 불가: $e');
      return (false, 'wlr-randr 가 설치되어 있지 않습니다.');
    } catch (e) {
      print('[DisplayMode] 예외: $e');
      return (false, '적용 중 오류가 발생했습니다: $e');
    }
  }

  /// 앱 기동 시 저장된 비율을 다시 적용한다.
  /// (cage 런처가 기본 모드를 잡아두므로, 저장값이 있으면 앱이 덮어쓴다)
  static Future<void> applySavedOnStartup() async {
    if (!isSupported) return;
    try {
      final aspect = await loadSaved();
      await apply(aspect);
    } catch (e) {
      print('[DisplayMode] 기동 시 적용 실패: $e');
    }
  }
}
