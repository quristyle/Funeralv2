import 'package:flutter/material.dart';
import '../../models/device_models.dart';
import '../../services/api/api_service.dart';
import '../../services/player/media_player_service.dart';
import '../../services/cache/cache_manager.dart';
import '../../services/signalr/signalr_service.dart';

class RoomGuideController extends ChangeNotifier {
  final ApiService _apiService = ApiService();
  final MediaPlayerService playerService = MediaPlayerService();
  final CacheManager _cacheManager = CacheManager();
  final SignalRService _signalRService = SignalRService();

  DeviceDto? device;
  DeceasedDto? deceased;
  String? localVideoPath;
  String? deceasedPhotoPath;
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

  Future<void> init(String serverBaseUrl, String deviceCode, Function() onRefresh) async {
    if (_isDisposed) return;
    isLoading = true;
    notifyListeners();

    device = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
    if (device != null && !_isDisposed) {
      // 즉시 반응: 비디오 비활성화 시 즉시 정지
      if (!device!.isVideoEnabled) {
        await playerService.stopVideo();
        localVideoPath = null;
      }
      notifyListeners();

      if (device!.roomId != null) {
        deceased = await _apiService.fetchDeceased(serverBaseUrl, device!.roomId!);
      }

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

      if (deceased != null) {
        final photoPath = (deceased!.memorialEditedPhotoUrl != null && deceased!.memorialEditedPhotoUrl!.isNotEmpty)
            ? deceased!.memorialEditedPhotoUrl
            : deceased!.memorialPhotoUrl;
        deceasedPhotoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, photoPath);
      }

      await _signalRService.connect(serverBaseUrl, deviceCode, () {
        if (!_isDisposed) init(serverBaseUrl, deviceCode, onRefresh);
      });
    }

    isLoading = false;
    notifyListeners();
  }
}
