import 'package:flutter/material.dart';
import '../../models/device_models.dart';
import '../../services/api/api_service.dart';
import '../../services/player/media_player_service.dart';
import '../../services/cache/cache_manager.dart';
import '../../services/signalr/signalr_service.dart';

/// [입구 종합 안내 컨트롤러]
/// 장례식장 입구 종합 안내판(ENTRANCE_GUIDE) 화면에 표출할 데이터와 미디어를 로드하고 관리합니다.
class EntranceGuideController extends ChangeNotifier {
  final ApiService _apiService = ApiService(); // 서버 API 서비스
  final MediaPlayerService playerService = MediaPlayerService(); // 미디어 재생 서비스
  final CacheManager _cacheManager = CacheManager(); // 미디어 캐시 매니저
  final SignalRService _signalRService = SignalRService(); // 실시간 알림 서비스

  DeviceDto? device; // 장비 설정 정보
  List<EntranceGuideRoomDto> guideRooms = []; // 입구 안내판에 노출할 빈소/호실 목록 데이터
  bool isLoading = false; // 로딩 여부 플래그
  bool _isDisposed = false; // 메모리 릭 방지를 위한 Dispose 감지용 플래그

  /// [자원 해제]
  /// 컨트롤러가 제거될 때 자원 누수를 막기 위해 미디어 재생기도 함께 파괴합니다.
  @override
  void dispose() {
    _isDisposed = true;
    playerService.dispose();
    super.dispose();
  }

  /// [UI 갱신 전파]
  /// Dispose된 위젯에서 리스너 갱신을 전파하다 오동작(Exception)이 발생하는 것을 막기 위해 안전 장치를 얹어 재정의합니다.
  @override
  void notifyListeners() {
    if (!_isDisposed) super.notifyListeners();
  }

  /// [안내판 데이터 및 미디어 초기화 루틴]
  /// 서버로부터 장비 정보([deviceCode]) 및 소속된 모든 호실 정보([guideRooms])를 불러와 메모리에 적재합니다.
  /// 백그라운드 영상이 지정되어 있다면 로컬 경로에 캐싱한 후 재생을 요청하고, SignalR 실시간 신호를 구독합니다.
  Future<void> init(String serverBaseUrl, String deviceCode, Function() onVideoInitialized) async {
    if (_isDisposed) return;
    isLoading = true;
    notifyListeners();

    // 1. 장비 상세 사양 로드
    device = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
    if (device != null && !_isDisposed) {
      // 2. 층/건물 내 호실 및 고인/상주 안내 종합 데이터 로드
      guideRooms = await _apiService.fetchEntranceGuideRooms(serverBaseUrl, deviceCode);

      // 3. 백그라운드 영상이 활성화 상태라면 캐싱 후 비디오 드라이버에 전달
      if (device!.isVideoEnabled && device!.videoId != null) {
        final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.videoId!);
        final localVideoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
        if (localVideoPath != null && !_isDisposed) {
          await playerService.playVideo(localVideoPath, onVideoInitialized);
        }
      }

      // 4. 실시간 설정 변경 및 데이터 업데이트 알림 이벤트 등록
      await _signalRService.connect(
        serverUrl: serverBaseUrl,
        deviceCode: deviceCode,
        onDeviceChanged: () {
          // 서버에서 데이터 변경 노티가 오면 본 초기화 함수를 처음부터 다시 호출해 갱신을 진행합니다.
          if (!_isDisposed) init(serverBaseUrl, deviceCode, onVideoInitialized);
        },
      );
    }

    isLoading = false;
    notifyListeners();
  }
}
