import 'package:flutter/foundation.dart';

/// [장비 설정 변경 브로드캐스트 버스]
/// SignalR로 설정 변경 신호(DeviceChanged)가 도착했을 때, 현재 화면에 떠 있는 컨트롤러가
/// 뷰를 재생성하지 않고 "제자리"에서 서버와 재동기화하도록 신호를 전파하는 전역 이벤트 버스입니다.
///
/// 뷰(UniqueKey) 재생성 방식과 달리 위젯 트리를 헐지 않으므로 화면 깜빡임이 없으며,
/// 각 컨트롤러가 자신의 변경 감지(_syncWithServer) 로직으로 필요한 부분만 갱신합니다.
class DeviceUpdateBus extends ChangeNotifier {
  DeviceUpdateBus._();
  static final DeviceUpdateBus instance = DeviceUpdateBus._();

  /// 활성 컨트롤러들에게 "지금 서버와 재동기화하라"는 신호를 브로드캐스트합니다.
  void ping() => notifyListeners();
}

/// [자동 재동기화 믹스인]
/// 화면 컨트롤러(ChangeNotifier)에 얹어, 전역 [DeviceUpdateBus] 신호를 받으면
/// 컨트롤러 자신의 서버 동기화 루틴을 다시 실행하도록 연결해 주는 믹스인입니다.
///
/// 사용법:
///  1) 클래스 선언에 `with DeviceAutoSync` 추가
///  2) init() 말미에 `bindAutoSync(serverBaseUrl, deviceCode, onRefresh)` 호출
///  3) dispose()에서 `unbindAutoSync()` 호출
///  4) `runAutoSync`를 오버라이드하여 자신의 `_syncWithServer(...)`를 호출
mixin DeviceAutoSync on ChangeNotifier {
  bool _autoBound = false; // 버스 구독 여부
  bool _autoSyncing = false; // 재동기화 진행 중 여부 (중복 실행 방지)
  String? _syncServer;
  String? _syncCode;
  Function()? _syncCallback;

  /// [자동 동기화 등록]
  /// 동기화에 필요한 파라미터를 기억하고 전역 버스를 구독합니다. (init 말미에서 호출)
  void bindAutoSync(String serverBaseUrl, String deviceCode, Function() onRefresh) {
    _syncServer = serverBaseUrl;
    _syncCode = deviceCode;
    _syncCallback = onRefresh;
    if (_autoBound) return;
    _autoBound = true;
    DeviceUpdateBus.instance.addListener(_handleBusPing);
  }

  /// [자동 동기화 해제]
  /// 전역 버스 구독을 해제합니다. (dispose에서 호출)
  void unbindAutoSync() {
    if (!_autoBound) return;
    _autoBound = false;
    DeviceUpdateBus.instance.removeListener(_handleBusPing);
  }

  /// [컨트롤러별 재동기화 구현 지점]
  /// 각 컨트롤러가 자신의 `_syncWithServer(server, code, callback)`를 호출하도록 구현합니다.
  Future<void> runAutoSync(String serverBaseUrl, String deviceCode, Function() onRefresh);

  /// 버스 신호 수신 시 실제 재동기화를 트리거합니다. (중복 실행 가드 포함)
  Future<void> _handleBusPing() async {
    if (_syncServer == null || _autoSyncing) return;
    _autoSyncing = true;
    try {
      await runAutoSync(_syncServer!, _syncCode!, _syncCallback ?? () {});
    } finally {
      _autoSyncing = false;
    }
  }
}
