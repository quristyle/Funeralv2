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
  String? localBackgroundPath;
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
    if (!_isDisposed) super.notifyListeners();
  }

  Future<void> init(String serverBaseUrl, String deviceCode, Function() onVideoInitialized) async {
    if (_isDisposed) return;

    isLoading = true;
    statusMessage = '장비 정보를 불러오는 중...';
    notifyListeners();

    try {
      // 1. 장비 상세 데이터 로드
      final newDevice = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
      if (newDevice == null) {
        statusMessage = '장비 정보를 불러오지 못했습니다.';
        isLoading = false;
        notifyListeners();
        return;
      }
      device = newDevice;

      // 즉시 반응 (음소거 및 비디오 정지)
      if (!device!.isMusicEnabled) { await playerService.stopMusic(); localMusicPath = null; }
      if (!device!.isVideoEnabled) { await playerService.stopVideo(); localVideoPath = null; }
      notifyListeners();

      // 2. 고인 정보 로드
      statusMessage = '고인 정보를 동기화하는 중...';
      print('[PortraitController] 고인 정보 API 호출: $deviceCode');
      deceased = await _apiService.fetchDeceased(serverBaseUrl, deviceCode);
      print('[PortraitController] 고인 정보 로드 완료: ${deceased?.name}');
      notifyListeners();

      if (_isDisposed) return;

      // 3. 미디어 파일 동기화
      // 비디오
      String? nextVideoPath;
      if (device!.isVideoEnabled && device!.videoId != null) {
        final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.videoId!);
        nextVideoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
      }

      // 음악
      String? nextMusicPath;
      if (device!.isMusicEnabled && device!.musicId != null) {
        final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.musicId!);
        nextMusicPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
      }

      // 영정사진
      if (device!.isMemorialPhotoEnabled && deceased != null) {
        final photoPath = (deceased!.memorialEditedPhotoUrl != null && deceased!.memorialEditedPhotoUrl!.isNotEmpty)
            ? deceased!.memorialEditedPhotoUrl
            : deceased!.memorialPhotoUrl;
        localPhotoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, photoPath);
      } else {
        localPhotoPath = null;
      }

      // 배경 이미지
      if (device!.isBackgroundImageEnabled && device!.backgroundImageUrl != null && device!.backgroundImageUrl!.isNotEmpty) {
        localBackgroundPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, device!.backgroundImageUrl);
      } else {
        localBackgroundPath = null;
      }

      // 리본 장식
      ribbonPaths.clear();
      if (deceased != null) {
        for (var ribbon in deceased!.deviceRibbons) {
          if (ribbon.mediaSourceUrl != null) {
            final lp = await _cacheManager.getCachedFileByPath(serverBaseUrl, ribbon.mediaSourceUrl);
            if (lp != null) ribbonPaths[ribbon.id] = lp;
          }
        }
      }

      if (_isDisposed) return;

      // 4. 재생 상태 적용
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

    } catch (e) {
      print('[PortraitController] 오류 발생: $e');
      statusMessage = '데이터 로딩 중 오류가 발생했습니다.';
    } finally {
      isLoading = false;
      statusMessage = '재생 중';
      notifyListeners();
    }

    // SignalR 연결 (성공할 때까지 시도)
    await _signalRService.connect(
      serverUrl: serverBaseUrl,
      deviceCode: deviceCode,
      onDeviceChanged: () {
        if (!_isDisposed) init(serverBaseUrl, deviceCode, onVideoInitialized);
      },
    );
  }

  String? get deceasedPhotoPath => localPhotoPath;
}
