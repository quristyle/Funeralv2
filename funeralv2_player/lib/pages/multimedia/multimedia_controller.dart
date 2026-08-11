import 'dart:async';
import 'package:flutter/material.dart';
import '../../models/device_models.dart';
import '../../services/api/api_service.dart';
import '../../services/player/media_player_service.dart';
import '../../services/cache/cache_manager.dart';
import '../../services/cache/local_db_service.dart';
import '../../services/device_update_bus.dart';

/// [멀티미디어 추모 롤링 컨트롤러]
/// 빈소 유족들이 업로드한 가족/추모 사진 목록([familyPhotos])을 순차적으로 롤링(슬라이드)하고,
/// 추모 음원(오디오 BGM) 및 배경 영상을 자동 플레이하도록 제어합니다.
class MultimediaController extends ChangeNotifier with DeviceAutoSync {
  final ApiService _apiService = ApiService(); // 서버 API 서비스
  final MediaPlayerService playerService = MediaPlayerService(); // 미디어 재생 서비스
  final CacheManager _cacheManager = CacheManager(); // 미디어 캐시 매니저
  final LocalDbService _dbService = LocalDbService(); // 로컬 DB 캐시 서비스

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
    unbindAutoSync(); // 전역 설정 변경 버스 구독 해제
    _rotationTimer?.cancel();
    playerService.dispose();
    super.dispose();
  }

  /// [자동 재동기화 진입점] 전역 버스 신호 수신 시 자신의 서버 동기화 루틴을 재실행합니다.
  @override
  Future<void> runAutoSync(String serverBaseUrl, String deviceCode, Function() onRefresh) =>
      _syncWithServer(serverBaseUrl, deviceCode, onRefresh);

  /// [UI 갱신 알림 재정의]
  @override
  void notifyListeners() {
    if (!_isDisposed) super.notifyListeners();
  }

  /// [멀티미디어 화면 초기화 루틴 (Cache-First)]
  /// 앱 구동 즉시 로컬 DB 캐시 리소스를 로드하여 사진 슬라이드 롤러와 비디오/BGM을 가동합니다.
  /// 그 뒤 백그라운드 비동기로 최신 미디어를 동기화합니다.
  Future<void> init(String serverBaseUrl, String deviceCode, Function() onRefresh) async {
    if (_isDisposed) return;
    isLoading = true;
    notifyListeners();

    // 1. [Cache-First] 로컬 DB에서 즉각 데이터 복구
    final cachedDevice = await _dbService.getDevice(deviceCode);
    if (cachedDevice != null && !_isDisposed) {
      device = cachedDevice;
      deceased = await _dbService.getDeceasedByDeviceCode(deviceCode);

      // 로컬 비디오 캐시 구동
      if (device!.isVideoEnabled && device!.videoId != null) {
        final cachedVideoPath = await _dbService.getSourcePath(device!.videoId!);
        if (cachedVideoPath != null) {
          final cachedLocalVideo = await _cacheManager.getCachedFileByPath(serverBaseUrl, cachedVideoPath);
          if (cachedLocalVideo != null && !_isDisposed) {
            await playerService.playVideo(cachedLocalVideo, onRefresh);
          }
        }
      }

      // 로컬 오디오(BGM) 캐시 구동
      if (device!.isMusicEnabled && device!.musicId != null) {
        final cachedMusicPath = await _dbService.getSourcePath(device!.musicId!);
        if (cachedMusicPath != null) {
          final cachedLocalMusic = await _cacheManager.getCachedFileByPath(serverBaseUrl, cachedMusicPath);
          if (cachedLocalMusic != null && !_isDisposed) {
            await playerService.playMusic(cachedLocalMusic, device!.musicVolume, isMuted: device!.isMuted);
          }
        }
      }

      // 로컬 가족사진 캐시 매핑 (getLocalFile을 사용하여 네트워크 대기 차단)
      localPhotoPaths.clear();
      if (deceased != null) {
        for (var photoPath in deceased!.familyPhotos) {
          final lp = await _cacheManager.getLocalFile(photoPath);
          localPhotoPaths.add(lp ?? '');
        }
      }

      // 사진 자동 롤링 슬라이드 타이머 즉시 구동
      _startPhotoRotation();

      // UI 렌더링 시작
      isLoading = false;
      notifyListeners();
    }

    // 2. 백그라운드 서버 동기화 작업 기동
    _syncWithServer(serverBaseUrl, deviceCode, onRefresh);

    // 3. 전역 설정 변경 버스 구독 (SignalR 수신 시 뷰 재생성 없이 제자리 재동기화)
    bindAutoSync(serverBaseUrl, deviceCode, onRefresh);
  }

  /// [백그라운드 비동기 서버 동기화 루틴]
  Future<void> _syncWithServer(String serverBaseUrl, String deviceCode, Function() onRefresh) async {
    try {
      print('[MultimediaController] [Background Sync] 시작');
      final fetchedDevice = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
      if (fetchedDevice != null && !_isDisposed) {
        DeceasedDto? fetchedDeceased;
        if (fetchedDevice.roomId != null) {
          fetchedDeceased = await _apiService.fetchDeceased(serverBaseUrl, deviceCode);
        }

        // 변경점 검사 (장비 렌더링 속성 전체 + 고인/가족사진)
        bool isChanged = device == null ||
            !device!.signageEquals(fetchedDevice) ||
            deceased?.id != fetchedDeceased?.id ||
            deceased?.familyPhotos.length != fetchedDeceased?.familyPhotos.length;

        if (!isChanged && deceased != null && fetchedDeceased != null) {
          // 세부 이미지 매핑 목록 비교
          for (int i = 0; i < deceased!.familyPhotos.length; i++) {
            if (deceased!.familyPhotos[i] != fetchedDeceased.familyPhotos[i]) {
              isChanged = true;
              break;
            }
          }
        }

        if (isChanged) {
          print('[MultimediaController] [Background Sync] 변경점 발견 -> UI 리프레시');
          device = fetchedDevice;
          deceased = fetchedDeceased;

          // 비디오 재생 갱신
          if (device!.isVideoEnabled && device!.videoId != null) {
            final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.videoId!);
            final localVideoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
            if (localVideoPath != null && !_isDisposed) {
              await playerService.playVideo(localVideoPath, onRefresh);
            }
          } else {
            await playerService.stopVideo();
          }

          // 오디오 BGM 갱신
          if (device!.isMusicEnabled && device!.musicId != null) {
            final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.musicId!);
            final localMusicPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
            if (localMusicPath != null && !_isDisposed) {
              await playerService.playMusic(localMusicPath, device!.musicVolume, isMuted: device!.isMuted);
            }
          } else {
            await playerService.stopMusic();
          }

          // 가족 사진 캐싱 동기화
          localPhotoPaths.clear();
          if (deceased != null) {
            for (var photoPath in deceased!.familyPhotos) {
              final lp = await _cacheManager.getCachedFileByPath(serverBaseUrl, photoPath);
              localPhotoPaths.add(lp ?? '');
            }
          }

          _startPhotoRotation();
          notifyListeners();
        } else {
          print('[MultimediaController] [Background Sync] 변동 사항 없음 -> 기존 뷰 유지');
          // 볼륨 및 음소거 설정 최종 갱신 대응
          if (device!.isMusicEnabled) {
            await playerService.updateMusicVolume(device!.musicVolume, isMuted: device!.isMuted);
          }
        }
      }
    } catch (e) {
      print('[MultimediaController] [Background Sync] 에러: $e');
    } finally {
      if (!_isDisposed) {
        isLoading = false;
        notifyListeners();
      }
    }
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
