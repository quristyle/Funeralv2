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
  
  // 리본 장식 로컬 경로 보관용 (Key: Ribbon ID, Value: Local Path)
  Map<String, String> ribbonPaths = {};

  bool isLoading = false;
  String statusMessage = '준비 중...';
  bool _isDisposed = false;

  @override
  void dispose() {
    _isDisposed = true;
    playerService.dispose();
    super.dispose();
  }

  @override
  void notifyListeners() {
    if (!_isDisposed) {
      super.notifyListeners();
    }
  }

  Future<void> init(
    String serverBaseUrl, 
    String deviceCode,
    Function() onVideoInitialized,
  ) async {
    if (_isDisposed) return;

    isLoading = true;
    statusMessage = '장비 정보를 불러오는 중...';
    notifyListeners();

    final newDevice = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
    if (newDevice == null || _isDisposed) {
      statusMessage = '장비 정보를 불러오지 못했습니다 (오프라인 상태)';
      isLoading = false;
      notifyListeners();
      return;
    }
    device = newDevice;

    if (!device!.isMusicEnabled) {
      await playerService.stopMusic();
      localMusicPath = null;
    }
    if (!device!.isVideoEnabled) {
      await playerService.stopVideo();
      localVideoPath = null;
    }
    notifyListeners();

    if (device!.roomId != null && device!.roomId!.isNotEmpty) {
      statusMessage = '고인 정보를 동기화하는 중...';
      deceased = await _apiService.fetchDeceased(serverBaseUrl, deviceCode);
      notifyListeners(); 
    } else {
      deceased = null;
      notifyListeners();
    }

    if (_isDisposed) return;

    statusMessage = '미디어 자원을 동기화하는 중...';
    notifyListeners();

    // 1. 비디오 동기화
    String? nextVideoPath;
    if (device!.isVideoEnabled && device!.videoId != null) {
      final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.videoId!);
      nextVideoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
    }

    // 2. 음악 동기화
    String? nextMusicPath;
    if (device!.isMusicEnabled && device!.musicId != null) {
      final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.musicId!);
      nextMusicPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
    }

    // 3. 영정사진 동기화
    if (device!.isMemorialPhotoEnabled && deceased != null) {
      final photoPath = (deceased!.memorialEditedPhotoUrl != null && deceased!.memorialEditedPhotoUrl!.isNotEmpty)
          ? deceased!.memorialEditedPhotoUrl
          : deceased!.memorialPhotoUrl;
      localPhotoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, photoPath);
    } else {
      localPhotoPath = null;
    }

    // 4. 리본 장식 동기화 (추가됨)
    ribbonPaths.clear();
    if (deceased != null && deceased!.deviceRibbons.isNotEmpty) {
      for (var ribbon in deceased!.deviceRibbons) {
        if (ribbon.mediaSourceUrl != null) {
          final lp = await _cacheManager.getCachedFileByPath(serverBaseUrl, ribbon.mediaSourceUrl);
          if (lp != null) ribbonPaths[ribbon.id] = lp;
        }
      }
    }

    if (_isDisposed) return;

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
        await playerService.playMusic(localMusicPath!, device!.musicVolume, isMuted: device!.isMuted);
      }
    } else {
      await playerService.stopMusic();
      localMusicPath = null;
    }

    if (device!.isMusicEnabled && localMusicPath != null) {
       await playerService.updateMusicVolume(device!.musicVolume, isMuted: device!.isMuted);
    }

    await _signalRService.connect(
      serverUrl: serverBaseUrl,
      deviceCode: deviceCode,
      onDeviceChanged: () {
        if (!_isDisposed) init(serverBaseUrl, deviceCode, onVideoInitialized);
      },
    );

    isLoading = false;
    statusMessage = '재생 중';
    notifyListeners();
  }

  String? get deceasedPhotoPath => localPhotoPath;
}
