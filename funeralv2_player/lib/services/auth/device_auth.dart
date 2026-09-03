import 'package:shared_preferences/shared_preferences.dart';

/// [장비 인증 키]
///
/// 서버의 익명 표출 API 는 열쇠가 장비 코드 하나였고 그 코드는 추측 가능하다
/// (docs/analysis/46 · 결정 D-M1). 게이트웨이에 장비 토큰 검증이 생겼다 —
/// **기본 꺼짐**이고, 켜면 `X-Device-Token` 헤더(웹소켓은 `?deviceToken=`)가
/// 맞아야 표출 API 를 부를 수 있다.
///
/// 이 클래스는 그 키의 저장·전송 한 곳이다. 환경 설정에서 넣고,
/// [ApiService] 와 SignalR 접속이 여기서 읽어 싣는다.
///
/// **키가 비어 있으면 아무것도 싣지 않는다** — 서버 검증이 꺼져 있는 동안은
/// 지금과 완전히 같게 동작한다. 켜는 순서는 서버 쪽 주석에 있다
/// (플레이어 전 대수에 키를 먼저 넣고, 그 다음 서버 검증을 켠다).
class DeviceAuth {
  static const String prefsKey = 'deviceAuthToken';

  static String _token = '';
  static bool _loaded = false;

  /// 저장된 키를 메모리에 올린다. 앱 시작·설정 저장 때 부른다.
  static Future<String> load() async {
    final prefs = await SharedPreferences.getInstance();
    _token = prefs.getString(prefsKey) ?? '';
    _loaded = true;
    return _token;
  }

  /// 키를 저장하고 메모리도 갱신한다. 빈 값이면 지운 것과 같다.
  static Future<void> save(String token) async {
    final prefs = await SharedPreferences.getInstance();
    _token = token.trim();
    if (_token.isEmpty) {
      await prefs.remove(prefsKey);
    } else {
      await prefs.setString(prefsKey, _token);
    }
    _loaded = true;
  }

  /// HTTP 요청에 붙일 헤더. 키가 없으면 빈 맵 — 요청이 지금과 같아진다.
  ///
  /// 동기 함수인 이유: ApiService 의 호출부 다섯 곳이 전부 동기 흐름 속에서
  /// 헤더를 만들므로, 최초 한 번만 [load] 를 거치면 그 뒤는 메모리 값으로 충분하다.
  static Map<String, String> headers() {
    if (!_loaded) {
      // load() 전에 불렸다면 키 없이 간다. 서버 검증이 꺼져 있으면 문제없고,
      // 켜져 있어도 다음 동기화(재시도 20초)가 load() 이후라 스스로 낫는다.
      return const {};
    }
    return _token.isEmpty ? const {} : {'X-Device-Token': _token};
  }

  /// SignalR 허브 주소에 키를 붙인다. 웹소켓 업그레이드는 커스텀 헤더를
  /// 싣기 어려워 게이트웨이가 쿼리로도 받는다.
  static String appendToUrl(String url) {
    if (!_loaded || _token.isEmpty) return url;
    final sep = url.contains('?') ? '&' : '?';
    return '$url$sep' 'deviceToken=${Uri.encodeQueryComponent(_token)}';
  }
}
