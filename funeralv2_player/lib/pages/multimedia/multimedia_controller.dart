import 'dart:async';
import 'package:flutter/material.dart';
import '../../models/device_models.dart';
import '../../services/api/api_service.dart';
import '../../services/player/media_player_service.dart';
import '../../services/cache/cache_manager.dart';

/// [멀티미디어 추모 롤링 컨트롤러]
/// 빈소 유족들이 업로드한 가족/추모 사진 목록([familyPhotos])을 순차적으로 롤링(슬라이드)하고,
/// 추모 음원(오디오 BGM) 및 배경 영상을 자동 플레이하도록 제어합니다.
class MultimediaController extends ChangeNotifier {
  final ApiService _apiService = ApiService(); // 서버 API 서비스
  final MediaPlayerService playerService = MediaPlayerService(); // 미디어 재생 서비스
  final CacheManager _cacheManager = CacheManager(); // 캐시 관리 매니저

  DeviceDto? device; // 장비 설정 정보
  DeceasedDto? deceased; // 고인 상세 정보
  
  // 현재 화면에 롤링 표출 중인 사진 리스트의 인덱스
  int currentPhotoIndex = 0;
  // 사진 자동 전환을 위한 타이머
  Timer? _rotationTimer;
  bool isLoading = false; // 로딩 중 플래그
  bool _isDisposed = false; // 메모리 해제 확인 플래그

  // 로컬에 캐싱된 사진 경로들의 리스트
  List<String> localPhotoPaths = [];

  /// [자원 해제]
  /// 컨트롤러 파괴 시 슬라이드 루프 타이머를 멈추고 재생 자원을 반납합니다.
  @override
  void dispose() {
    _isDisposed = true;
    _rotationTimer?.cancel();
    playerService.dispose();
    super.dispose();
  }

  /// [UI 갱신 알림 재정의]
  @override
  void notifyListeners() {
    if (!_isDisposed) super.notifyListeners();
  }

  /// [멀티미디어 화면 초기화 루틴]
  /// 서버에서 장비 정보, 고인 정보 및 가족 추모 사진 리스트를 가져와 준비합니다.
  /// 배경 영상과 배경 음악(볼륨 및 음소거 설정 포함)을 다운로드하여 동시에 구동하며,
  /// 사진 슬라이더 회전을 개시하고 웹소켓 변경 노티를 구독합니다.
  Future<void> init(String serverBaseUrl, String deviceCode, Function() onRefresh) async {
    if (_isDisposed) return;
    isLoading = true;
    notifyListeners();

    // 1. 장비 스펙 획득
    device = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
    if (device != null && !_isDisposed) {
      // 2. 장비가 속한 빈소의 고인 상세 데이터 획득 (가족 추모 사진 목록 포함)
      if (device!.roomId != null) {
        deceased = await _apiService.fetchDeceased(serverBaseUrl, deviceCode);
      }

      // 3. 백그라운드 영상 물리 경로 캐싱 및 루프 구동
      if (device!.isVideoEnabled && device!.videoId != null) {
        final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.videoId!);
        final localVideoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
        if (localVideoPath != null && !_isDisposed && device!.isVideoEnabled) {
          await playerService.playVideo(localVideoPath, onRefresh);
        }
      } else {
        await playerService.stopVideo();
      }

      // 4. 백그라운드 오디오 음원(BGM) 캐싱 및 재생 시작 (음소거 및 볼륨 조절 적용)
      if (device!.isMusicEnabled && device!.musicId != null) {
        final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.musicId!);
        final localMusicPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
        if (localMusicPath != null && !_isDisposed) {
          await playerService.playMusic(localMusicPath, device!.musicVolume, isMuted: device!.isMuted);
        }
      }

      // 5. 가족 사진 캐싱 동기화
      localPhotoPaths.clear();
      if (deceased != null) {
        for (var photoPath in deceased!.familyPhotos) {
          final lp = await _cacheManager.getCachedFileByPath(serverBaseUrl, photoPath);
          if (lp != null) {
            localPhotoPaths.add(lp);
          } else {
            localPhotoPaths.add('');
          }
        }
      }

      // 6. 사진 자동 롤링 슬라이드 타이머 기동
      _startPhotoRotation();
    }

    isLoading = false;
    notifyListeners();
  }

  /// [사진 자동 회전/롤링 기동]
  /// 장비 설정의 롤링 주기([contentIntervalSec], 기본 10초) 주기로 
  /// 가족사진 인덱스를 순환 갱신하여 상위 뷰를 다시 그리도록 노티합니다.
  void _startPhotoRotation() {
    _rotationTimer?.cancel();
    if (deceased == null || deceased!.familyPhotos.isEmpty) return;

    final interval = device?.contentIntervalSec ?? 10;
    _rotationTimer = Timer.periodic(Duration(seconds: interval), (timer) {
      if (!_isDisposed && deceased!.familyPhotos.isNotEmpty) {
        // 인덱스를 0부터 사진 최대 크기까지 1씩 증가시키며 순환
        currentPhotoIndex = (currentPhotoIndex + 1) % deceased!.familyPhotos.length;
        notifyListeners();
      }
    });
  }
}
