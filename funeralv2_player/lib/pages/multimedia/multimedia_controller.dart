import 'dart:async';
import 'package:flutter/material.dart';
import '../../models/device_models.dart';
import '../../services/api/api_service.dart';
import '../../services/player/media_player_service.dart';
import '../../services/cache/cache_manager.dart';
import '../../services/signalr/signalr_service.dart';

class MultimediaController extends ChangeNotifier {
  final ApiService _apiService = ApiService();
  final MediaPlayerService playerService = MediaPlayerService();
  final CacheManager _cacheManager = CacheManager();
  final SignalRService _signalRService = SignalRService();

  DeviceDto? device;
  DeceasedDto? deceased;
  
  // 현재 표시 중인 사진 인덱스
  int currentPhotoIndex = 0;
  Timer? _rotationTimer;
  bool isLoading = false;
  bool _isDisposed = false;

  @override
  void dispose() {
    _isDisposed = true;
    _rotationTimer?.cancel();
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
      if (device!.roomId != null) {
        deceased = await _apiService.fetchDeceased(serverBaseUrl, deviceCode);
      }

      // 배경 영상 설정
      if (device!.isVideoEnabled && device!.videoId != null) {
        final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.videoId!);
        final localVideoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
        if (localVideoPath != null && !_isDisposed && device!.isVideoEnabled) {
          await playerService.playVideo(localVideoPath, onRefresh);
        }
      } else {
        await playerService.stopVideo();
      }

      // 배경 음악 설정 (Mute 반영)
      if (device!.isMusicEnabled && device!.musicId != null) {
        final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.musicId!);
        final localMusicPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
        if (localMusicPath != null && !_isDisposed) {
          await playerService.playMusic(localMusicPath, device!.musicVolume, isMuted: device!.isMuted);
        }
      }

      // 사진 자동 전환 타이머 시작
      _startPhotoRotation();

      // SignalR 연결
      await _signalRService.connect(
        serverUrl: serverBaseUrl,
        deviceCode: deviceCode,
        onDeviceChanged: () {
          if (!_isDisposed) init(serverBaseUrl, deviceCode, onRefresh);
        },
      );
    }

    isLoading = false;
    notifyListeners();
  }

  void _startPhotoRotation() {
    _rotationTimer?.cancel();
    if (deceased == null || deceased!.familyPhotos.isEmpty) return;

    final interval = device?.contentIntervalSec ?? 10;
    _rotationTimer = Timer.periodic(Duration(seconds: interval), (timer) {
      if (!_isDisposed && deceased!.familyPhotos.isNotEmpty) {
        currentPhotoIndex = (currentPhotoIndex + 1) % deceased!.familyPhotos.length;
        notifyListeners();
      }
    });
  }
}
