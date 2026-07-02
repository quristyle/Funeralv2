import 'package:flutter/material.dart';
import '../../models/device_models.dart';
import '../../services/api/api_service.dart';
import '../../services/player/media_player_service.dart';
import '../../services/signalr/signalr_service.dart';
import '../../services/cache/cache_manager.dart';

class RoomGuideController extends ChangeNotifier {
  final ApiService _apiService = ApiService();
  final MediaPlayerService playerService = MediaPlayerService();
  final SignalRService _signalRService = SignalRService();
  final CacheManager _cacheManager = CacheManager();

  DeviceDto? device;
  bool isLoading = false;

  Future<void> init(String serverBaseUrl, String deviceCode, Function() onVideoInitialized) async {
    isLoading = true;
    notifyListeners();

    device = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
    
    if (device != null) {
      // 배경 영상 동기화
      if (device!.isVideoEnabled && device!.videoId != null) {
        final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.videoId!);
        final localVideoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
        if (localVideoPath != null) {
          await playerService.playVideo(localVideoPath, onVideoInitialized);
        }
      }

      await _signalRService.connect(serverBaseUrl, deviceCode, () {
        init(serverBaseUrl, deviceCode, onVideoInitialized);
      });
    }

    isLoading = false;
    notifyListeners();
  }

  @override
  void dispose() {
    playerService.dispose();
    super.dispose();
  }
}
