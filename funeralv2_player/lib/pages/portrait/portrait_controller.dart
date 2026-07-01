import 'package:flutter/material.dart';
import '../../models/device_models.dart';
import '../../services/api/api_service.dart';
import '../../services/cache/cache_manager.dart';
import '../../services/player/media_player_service.dart';
import '../../services/signalr/signalr_service.dart';

class PortraitController extends ChangeNotifier {
  final ApiService _apiService = ApiService();
  final CacheManager _cacheManager = CacheManager();
  final SignalRService _signalRService = SignalRService();
  final MediaPlayerService playerService = MediaPlayerService();

  DeviceDto? device;
  DeceasedDto? deceased;
  String? localVideoPath;
  String? localMusicPath;
  String? localPhotoPath;

  bool isLoading = false;
  String statusMessage = '준비 중...';

  // 메인 데이터 로딩 및 동기화 루프
  Future<void> init(
    String serverBaseUrl, 
    String deviceCode,
    Function() onVideoInitialized,
  ) async {
    isLoading = true;
    statusMessage = '장비 정보를 불러오는 중...';
    notifyListeners();

    // 1. 장비 상세 데이터 로드
    final newDevice = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
    if (newDevice == null) {
      statusMessage = '장비 정보를 불러오지 못했습니다 (오프라인 상태)';
      isLoading = false;
      notifyListeners();
      return;
    }
    device = newDevice;
    print('[Controller] 장비 설정 로드 완료: orientation=${device!.portraitOrientation}, display=${device!.displayOrientation}');

    // 즉시 반응: 설정에서 꺼진 기능은 데이터 동기화 전이라도 즉시 정지
    if (!device!.isMusicEnabled) {
      await playerService.stopMusic();
      localMusicPath = null;
    }
    if (!device!.isVideoEnabled) {
      await playerService.stopVideo();
      localVideoPath = null;
    }
    notifyListeners();

    // 2. 고인 정보 로드
    if (device!.roomId != null && device!.roomId!.isNotEmpty) {
      statusMessage = '고인 정보를 동기화하는 중...';
      deceased = await _apiService.fetchDeceased(serverBaseUrl, device!.roomId!);
      notifyListeners(); 
    } else {
      deceased = null;
      notifyListeners();
    }

    // 3. 미디어 파일 동기화 처리
    statusMessage = '미디어 자원을 동기화하는 중...';
    notifyListeners();

    // 비디오 동기화
    String? nextVideoPath;
    if (device!.isVideoEnabled && device!.videoId != null) {
      final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.videoId!);
      nextVideoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
    }

    // 음악 동기화 (musicId를 사용하도록 수정)
    String? nextMusicPath;
    if (device!.isMusicEnabled && device!.musicId != null) {
      final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.musicId!);
      nextMusicPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
    }

    // 영정사진 동기화 (EditedUrl 우선순위 적용)
    if (device!.isMemorialPhotoEnabled && deceased != null) {
      final photoPath = (deceased!.memorialEditedPhotoUrl != null && deceased!.memorialEditedPhotoUrl!.isNotEmpty)
          ? deceased!.memorialEditedPhotoUrl
          : deceased!.memorialPhotoUrl;
      
      print('[Photo] 영정사진 경로 확인: $photoPath');
      localPhotoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, photoPath);
    } else {
      localPhotoPath = null;
    }

    // 4. 최종 재생 상태 적용 (변경 시에만 재생 컨트롤)
    if (device!.isVideoEnabled && nextVideoPath != null) {
      if (localVideoPath != nextVideoPath) {
        localVideoPath = nextVideoPath;
        await playerService.playVideo(localVideoPath!, onVideoInitialized);
      }
    } else {
      await playerService.stopVideo();
      localVideoPath = null;
    }

    if (device!.isMusicEnabled && nextMusicPath != null) {
      if (localMusicPath != nextMusicPath) {
        localMusicPath = nextMusicPath;
        await playerService.playMusic(localMusicPath!, device!.musicVolume);
      }
    } else {
      await playerService.stopMusic();
      localMusicPath = null;
    }

    // 5. SignalR 실시간 변경 통신 소켓 연결
    statusMessage = '실시간 이벤트 서버 연결 중...';
    notifyListeners();
    await _signalRService.connect(serverBaseUrl, deviceCode, () {
      init(serverBaseUrl, deviceCode, onVideoInitialized);
    });

    isLoading = false;
    statusMessage = '재생 중';
    notifyListeners();
  }

  String? get deceasedPhotoPath => localPhotoPath;

  @override
  void dispose() {
    playerService.dispose();
    super.dispose();
  }
}
