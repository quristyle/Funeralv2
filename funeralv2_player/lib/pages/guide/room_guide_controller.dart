import 'package:flutter/material.dart';
import '../../models/device_models.dart';
import '../../services/api/api_service.dart';
import '../../services/player/media_player_service.dart';
import '../../services/cache/cache_manager.dart';

/// [호실 입구 안내 컨트롤러]
/// 개별 호실(빈소) 입구에 배치되는 사이니지 안내판(ROOM_GUIDE)에 데이터를 바인딩하고
/// 미디어(배경 비디오 및 고인 영정 사진)의 로컬 캐시 다운로드 및 실시간 화면 갱신을 주도합니다.
class RoomGuideController extends ChangeNotifier {
  final ApiService _apiService = ApiService(); // 서버 API 서비스
  final MediaPlayerService playerService = MediaPlayerService(); // 미디어 재생 서비스
  final CacheManager _cacheManager = CacheManager(); // 미디어 캐시 매니저

  DeviceDto? device; // 장비 설정 정보
  DeceasedDto? deceased; // 빈소에 모셔진 고인의 정보
  String? localVideoPath; // 로컬에 저장된 배경 영상 파일 물리 경로
  String? deceasedPhotoPath; // 로컬에 저장된 고인 영정사진 파일 물리 경로
  bool isLoading = false; // 데이터 조회 로딩 상태
  bool _isDisposed = false; // 위젯 트리에서 이탈했는지 여부 (메모리 누수 차단)

  /// [자원 해제]
  /// 컨트롤러 파괴 시 함께 물려 있는 미디어 리소스를 안전하게 해제합니다.
  @override
  void dispose() {
    _isDisposed = true;
    playerService.dispose();
    super.dispose();
  }

  /// [UI 갱신 알림 재정의]
  @override
  void notifyListeners() {
    if (!_isDisposed) super.notifyListeners();
  }

  /// [컨트롤러 초기화 루틴]
  /// 장비 및 고인 정보를 API 서버에서 비동기로 긁어오며,
  /// 백그라운드 영상 및 보정 영정 사진을 로컬 스토리지에 캐시 보관하고 화면 리프레시를 제어합니다.
  /// 실시간 신호(SignalR)에 세션을 맺어 백엔드 갱신 발생 시 자동 리로드합니다.
  Future<void> init(String serverBaseUrl, String deviceCode, Function() onRefresh) async {
    if (_isDisposed) return;
    isLoading = true;
    notifyListeners();

    // 1. 장비 사양 및 환경 로드
    device = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
    if (device != null && !_isDisposed) {
      // 비디오 사용 설정이 꺼져 있을 경우 즉각 비디오 드라이버 정지 처리
      if (!device!.isVideoEnabled) {
        await playerService.stopVideo();
        localVideoPath = null;
      }
      notifyListeners();

      // 2. 호실 고유 ID가 존재하는 경우 고인 및 행사 정보 로드
      if (device!.roomId != null) {
        deceased = await _apiService.fetchDeceased(serverBaseUrl, deviceCode);
      }

      // 3. 백그라운드 루프 비디오 로드 및 로컬 캐싱 적용
      if (device!.isVideoEnabled && device!.videoId != null) {
        final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.videoId!);
        localVideoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
        if (localVideoPath != null && !_isDisposed && device!.isVideoEnabled) {
          await playerService.playVideo(localVideoPath!, onRefresh);
        }
      } else {
        await playerService.stopVideo();
        localVideoPath = null;
      }

      // 4. 고인의 보정된 영정사진을 우선으로 가져와 로컬 캐싱 다운로드 진행
      if (deceased != null) {
        final photoPath = (deceased!.memorialEditedPhotoUrl != null && deceased!.memorialEditedPhotoUrl!.isNotEmpty)
            ? deceased!.memorialEditedPhotoUrl
            : deceased!.memorialPhotoUrl;
        deceasedPhotoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, photoPath);
      }
    }

    isLoading = false;
    notifyListeners();
  }
}
