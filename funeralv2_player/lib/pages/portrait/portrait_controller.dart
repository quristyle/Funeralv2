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
    String apiServerUrl, 
    String fileServerUrl, 
    String deviceCode,
    Function() onVideoInitialized,
  ) async {
    isLoading = true;
    statusMessage = '장비 정보를 불러오는 중...';
    notifyListeners();

    // 1. 장비 상세 데이터 로드 (오프라인 캐싱 대응)
    device = await _apiService.fetchDevice(apiServerUrl, deviceCode);
    if (device == null) {
      statusMessage = '장비 정보를 불러오지 못했습니다 (오프라인 상태)';
      isLoading = false;
      notifyListeners();
      return;
    }

    // 2. 고인 정보 로드 (호실이 존재할 때)
    if (device!.roomId != null && device!.roomId!.isNotEmpty) {
      statusMessage = '고인 정보를 동기화하는 중...';
      notifyListeners();
      deceased = await _apiService.fetchDeceased(apiServerUrl, device!.roomId!);
    } else {
      deceased = null;
    }

    // 3. 미디어 파일 로컬 캐싱 처리 (오프라인 대비 선다운로드)
    statusMessage = '미디어 자원을 동기화하는 중...';
    notifyListeners();

    // 비디오 캐싱
    if (device!.isVideoEnabled && device!.videoId != null) {
      localVideoPath = await _cacheManager.getCachedFile(fileServerUrl, device!.videoId);
    } else {
      localVideoPath = null;
    }

    // 음악 캐싱
    if (device!.isMusicEnabled && device!.musicId != null) {
      localMusicPath = await _cacheManager.getCachedFile(fileServerUrl, device!.musicId);
    } else {
      localMusicPath = null;
    }

    // 영정사진 캐싱
    if (device!.isMemorialPhotoEnabled && deceased != null) {
      final photoId = deceased!.memorialEditedPhotoFileId ?? deceased!.memorialPhotoFileId;
      // 파일명이 Guid 형태이므로 확장자는 이미지에 맞게 빈 값으로 둠
      localPhotoPath = await _cacheManager.getCachedFile(fileServerUrl, photoId);
    } else {
      localPhotoPath = null;
    }

    // 4. 무한 반복 재생 구동
    if (localVideoPath != null) {
      await playerService.playVideo(localVideoPath!, onVideoInitialized);
    } else {
      await playerService.stopVideo();
    }

    if (localMusicPath != null) {
      await playerService.playMusic(localMusicPath!, device!.musicVolume);
    } else {
      await playerService.stopMusic();
    }

    // 5. SignalR 실시간 변경 통신 소켓 연결
    statusMessage = '실시간 이벤트 서버 연결 중...';
    notifyListeners();
    await _signalRService.connect(apiServerUrl, deviceCode, () {
      // 알림 수신 시 동적 재호출
      init(apiServerUrl, fileServerUrl, deviceCode, onVideoInitialized);
    });

    isLoading = false;
    statusMessage = '재생 중';
    notifyListeners();
  }

  // 확장 메서드 백업 대응용 도우미 getter
  String? get deceasedPhotoPath => localPhotoPath;

  @override
  void dispose() {
    playerService.dispose();
    super.dispose();
  }
}

// DTO 내부 필드 유실 방지용 확장 Helper
extension DeceasedDtoHelper on DeceasedDto {
  String? get memorialPhotoFileId {
    if (memorialPhotoUrl == null) return null;
    final uri = Uri.parse(memorialPhotoUrl!);
    if (uri.pathSegments.isNotEmpty) {
      return uri.pathSegments.last;
    }
    return null;
  }

  String? get memorialEditedPhotoFileId {
    if (memorialEditedPhotoUrl == null) return null;
    final uri = Uri.parse(memorialEditedPhotoUrl!);
    if (uri.pathSegments.isNotEmpty) {
      return uri.pathSegments.last;
    }
    return null;
  }
}
