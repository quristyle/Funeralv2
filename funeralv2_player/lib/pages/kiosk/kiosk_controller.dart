import 'package:flutter/material.dart';
import '../../models/device_models.dart';
import '../../services/api/api_service.dart';
import '../../services/player/media_player_service.dart';
import '../../services/cache/cache_manager.dart';
import '../../services/signalr/signalr_service.dart';

class KioskController extends ChangeNotifier {
  final ApiService _apiService = ApiService();
  final MediaPlayerService playerService = MediaPlayerService();
  final CacheManager _cacheManager = CacheManager();
  final SignalRService _signalRService = SignalRService();

  DeviceDto? device;
  bool isLoading = false;
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
    notifyListeners();

    device = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
    if (device != null && !_isDisposed) {
      // 배경 영상 설정
      if (device!.isVideoEnabled && device!.videoId != null) {
        final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.videoId!);
        final localVideoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
        if (localVideoPath != null && !_isDisposed) {
          await playerService.playVideo(localVideoPath, onVideoInitialized);
        }
      }

      // SignalR 연결 (키오스크는 상시 연결 유지)
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
