import 'package:flutter/material.dart';
import '../../models/device_models.dart';
import '../../services/api/api_service.dart';
import '../../services/player/media_player_service.dart';
import '../../services/cache/cache_manager.dart';
import '../../services/signalr/signalr_service.dart';

/// [종합 안내 키오스크 컨트롤러]
/// 대고객 터치형 종합 키오스크(KIOSK) 화면에 표출할 전체 호실 현황, 약도 및 주차장 미디어 경로를 
/// 서버로부터 로드하고, 캐싱 및 실시간 동기화 상태를 제어합니다.
class KioskController extends ChangeNotifier {
  final ApiService _apiService = ApiService(); // 서버 API 서비스
  final MediaPlayerService playerService = MediaPlayerService(); // 비디오/사운드 재생 서비스
  final CacheManager _cacheManager = CacheManager(); // 미디어 캐시 매니저
  final SignalRService _signalRService = SignalRService(); // 실시간 통신 서비스

  DeviceDto? device; // 장비 설정 정보
  List<EntranceGuideRoomDto> rooms = []; // 전체 호실 정보 보관 리스트
  List<String> buildingPhotos = []; // 건물 층별 약도/층 안내 이미지 경로 리스트
  List<String> parkingPhotos = []; // 주차장 및 오시는 길 이미지 경로 리스트
  bool isLoading = false; // 데이터 조회 상태 플래그
  bool _isDisposed = false; // 위젯 생명주기 이탈 제어 플래그

  /// [자원 해제]
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

  /// [키오스크 데이터 및 미디어 초기화 루틴]
  /// 서버에서 장비 스펙을 읽은 뒤, 키오스크 안내 리소스 API를 호출하여 호실, 주차장, 건물 사진을 확보합니다.
  /// 배경 영상이 지정되어 있다면 다운로드 캐싱 후 루프 기동하며 SignalR에 장비를 온라인 등록합니다.
  Future<void> init(String serverBaseUrl, String deviceCode, Function() onVideoInitialized) async {
    if (_isDisposed) return;
    isLoading = true;
    notifyListeners();

    // 1. 장비 상세 스펙 획득
    device = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
    if (device != null && !_isDisposed) {
      // 2. 키오스크 전용 API를 통한 건물 약도, 주차장, 전체 빈소 정보 획득
      final kioskData = await _apiService.fetchKioskRooms(serverBaseUrl, deviceCode);
      rooms = kioskData.rooms;
      buildingPhotos = kioskData.buildingPhotos;
      parkingPhotos = kioskData.parkingPhotos;

      // 3. 백그라운드 영상 파일 로컬 캐싱 및 재생 요청
      if (device!.isVideoEnabled && device!.videoId != null) {
        final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.videoId!);
        final localVideoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
        if (localVideoPath != null && !_isDisposed && device!.isVideoEnabled) {
          await playerService.playVideo(localVideoPath, onVideoInitialized);
        }
      } else {
        await playerService.stopVideo();
      }

      // 4. 실시간 설정 동기화 웹소켓 소켓 연결 바인딩
      await _signalRService.connect(
        serverUrl: serverBaseUrl,
        deviceCode: deviceCode,
        onDeviceChanged: () {
          if (!_isDisposed) init(serverBaseUrl, deviceCode, onVideoInitialized);
        },
      );
    }

    isLoading = false;
    notifyListeners();
  }
}
