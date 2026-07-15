import 'package:flutter/material.dart';
import '../../models/device_models.dart';
import '../../services/api/api_service.dart';
import '../../services/cache/cache_manager.dart';
import '../../services/player/media_player_service.dart';
import '../../services/cache/local_db_service.dart';

/// [영정 화면 및 제례 제어 컨트롤러]
/// 빈소 내부 제단에 놓이는 대형 영정 사이니지 화면(`FUNERAL_PORTRAIT`)에 
/// 표출할 보정 영정 사진, 리본 장식 레이어, 텍스트 오버레이, 배경 이미지, 배경 오디오(추모곡)를 로드하고 통제합니다.
class PortraitController extends ChangeNotifier {
  final ApiService _apiService = ApiService(); // 서버 API 서비스
  final CacheManager _cacheManager = CacheManager(); // 미디어 캐시 매니저
  final MediaPlayerService playerService = MediaPlayerService(); // 비디오/오디오 엔진 서비스
  final LocalDbService _dbService = LocalDbService(); // 로컬 DB 캐시 서비스

  DeviceDto? device; // 장비 설정 정보
  DeceasedDto? deceased; // 빈소에 안치된 고인의 정보
  String? localVideoPath; // 로컬에 임시 캐싱 완료된 비디오 경로
  String? localMusicPath; // 로컬에 임시 캐싱 완료된 오디오(BGM) 경로
  String? localPhotoPath; // 로컬에 임시 캐싱 완료된 영정사진 이미지 경로
  String? localBackgroundPath; // 로컬에 임시 캐싱 완료된 제단 전용 배경 이미지 경로
  Map<String, String> ribbonPaths = {}; // 리본 장식 미디어 ID별 로컬 경로 맵

  bool isLoading = false; // 로딩 처리 플래그
  String statusMessage = '준비 중...'; // 로딩 단계별 상태 텍스트
  bool _isDisposed = false; // 메모리 릭 억제용 기동 제어 플래그

  /// [자원 해제]
  @override
  void dispose() {
    _isDisposed = true;
    playerService.dispose();
    super.dispose();
  }

  /// [UI 갱신 알림 재정의]
  @override
  void notifyListeners() {
    if (!_isDisposed) super.notifyListeners();
  }

  /// [영정 화면 데이터 및 미디어 로드 핵심 루틴 (Cache-First)]
  /// 앱 시작 즉시 로컬 DB 캐시 리소스를 메모리에 즉각 세팅하여 영정과 BGM을 구동하고 로딩을 정지시킵니다.
  /// 그 직후 백그라운드 비동기로 최신 정보를 조회하고 갱신을 확인합니다.
  Future<void> init(String serverBaseUrl, String deviceCode, Function() onVideoInitialized) async {
    if (_isDisposed) return;

    isLoading = true;
    statusMessage = '로컬 설정 조회 중...';
    notifyListeners();

    // 1. 로컬 캐시 즉시 구동 (TTV 최소화)
    final cachedDevice = await _dbService.getDevice(deviceCode);
    if (cachedDevice != null && !_isDisposed) {
      device = cachedDevice;
      deceased = await _dbService.getDeceasedByDeviceCode(deviceCode);

      // 로컬 배경 이미지 셋업
      if (device!.isBackgroundImageEnabled && device!.backgroundImageUrl != null && device!.backgroundImageUrl!.isNotEmpty) {
        localBackgroundPath = await _cacheManager.getLocalFile(device!.backgroundImageUrl);
      }

      // 로컬 영정사진 셋업
      if (device!.isMemorialPhotoEnabled && deceased != null) {
        final photoPath = (deceased!.memorialEditedPhotoUrl != null && deceased!.memorialEditedPhotoUrl!.isNotEmpty)
            ? deceased!.memorialEditedPhotoUrl
            : deceased!.memorialPhotoUrl;
        localPhotoPath = await _cacheManager.getLocalFile(photoPath);
      }

      // 로컬 근조 리본 장식 셋업
      ribbonPaths.clear();
      if (deceased != null) {
        for (var ribbon in deceased!.deviceRibbons) {
          if (ribbon.mediaSourceUrl != null) {
            final lp = await _cacheManager.getLocalFile(ribbon.mediaSourceUrl);
            if (lp != null) ribbonPaths[ribbon.id] = lp;
          }
        }
      }

      // 로컬 미디어 재생 가동
      // 비디오
      if (device!.isVideoEnabled && device!.videoId != null) {
        final cachedVideoPath = await _dbService.getSourcePath(device!.videoId!);
        if (cachedVideoPath != null) {
          final cachedLocalVideo = await _cacheManager.getLocalFile(cachedVideoPath);
          if (cachedLocalVideo != null && !_isDisposed) {
            localVideoPath = cachedLocalVideo;
            await playerService.playVideo(localVideoPath!, onVideoInitialized);
          }
        }
      }

      // 오디오(BGM)
      if (device!.isMusicEnabled && device!.musicId != null) {
        final cachedMusicPath = await _dbService.getSourcePath(device!.musicId!);
        if (cachedMusicPath != null) {
          final cachedLocalMusic = await _cacheManager.getLocalFile(cachedMusicPath);
          if (cachedLocalMusic != null && !_isDisposed) {
            localMusicPath = cachedLocalMusic;
            await playerService.playMusic(localMusicPath!, device!.musicVolume, isMuted: device!.isMuted);
          }
        }
      }

      isLoading = false;
      statusMessage = '재생 중 (로컬 캐시)';
      notifyListeners();
    }

    // 2. 백그라운드 서버 동기화 작업 기동
    _syncWithServer(serverBaseUrl, deviceCode, onVideoInitialized);
  }

  /// [백그라운드 비동기 서버 동기화 루틴]
  Future<void> _syncWithServer(String serverBaseUrl, String deviceCode, Function() onVideoInitialized) async {
    try {
      print('[PortraitController] [Background Sync] 시작');
      final fetchedDevice = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
      if (fetchedDevice != null && !_isDisposed) {
        DeceasedDto? fetchedDeceased;
        if (fetchedDevice.roomId != null) {
          fetchedDeceased = await _apiService.fetchDeceased(serverBaseUrl, deviceCode);
        }

        // 핵심 정보 변경점 대조
        bool isChanged = device == null ||
            device!.id != fetchedDevice.id ||
            device!.roomId != fetchedDevice.roomId ||
            device!.isVideoEnabled != fetchedDevice.isVideoEnabled ||
            device!.videoId != fetchedDevice.videoId ||
            device!.isMusicEnabled != fetchedDevice.isMusicEnabled ||
            device!.musicId != fetchedDevice.musicId ||
            device!.backgroundImageUrl != fetchedDevice.backgroundImageUrl ||
            deceased?.id != fetchedDeceased?.id ||
            deceased?.name != fetchedDeceased?.name;

        if (isChanged) {
          print('[PortraitController] [Background Sync] 변경점 발견 -> UI 리프레시');
          device = fetchedDevice;
          deceased = fetchedDeceased;

          // 설정 기반 미디어 드라이버 즉각 갱신
          if (!device!.isMusicEnabled) { await playerService.stopMusic(); localMusicPath = null; }
          if (!device!.isVideoEnabled) { await playerService.stopVideo(); localVideoPath = null; }

          // 비디오 캐싱 및 갱신
          if (device!.isVideoEnabled && device!.videoId != null) {
            final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.videoId!);
            final nextVideoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
            if (nextVideoPath != null && !_isDisposed && localVideoPath != nextVideoPath) {
              localVideoPath = nextVideoPath;
              await playerService.playVideo(localVideoPath!, onVideoInitialized);
            }
          }

          // 음악 캐싱 및 갱신
          if (device!.isMusicEnabled && device!.musicId != null) {
            final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.musicId!);
            final nextMusicPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
            if (nextMusicPath != null && !_isDisposed && localMusicPath != nextMusicPath) {
              localMusicPath = nextMusicPath;
              await playerService.playMusic(localMusicPath!, device!.musicVolume, isMuted: device!.isMuted);
            }
          }

          // 영정사진 갱신
          if (device!.isMemorialPhotoEnabled && deceased != null) {
            final photoPath = (deceased!.memorialEditedPhotoUrl != null && deceased!.memorialEditedPhotoUrl!.isNotEmpty)
                ? deceased!.memorialEditedPhotoUrl
                : deceased!.memorialPhotoUrl;
            localPhotoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, photoPath);
          } else {
            localPhotoPath = null;
          }

          // 배경 스킨 이미지 갱신
          if (device!.isBackgroundImageEnabled && device!.backgroundImageUrl != null && device!.backgroundImageUrl!.isNotEmpty) {
            localBackgroundPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, device!.backgroundImageUrl);
          } else {
            localBackgroundPath = null;
          }

          // 근조 리본 장식 갱신
          ribbonPaths.clear();
          if (deceased != null) {
            for (var ribbon in deceased!.deviceRibbons) {
              if (ribbon.mediaSourceUrl != null) {
                final lp = await _cacheManager.getCachedFileByPath(serverBaseUrl, ribbon.mediaSourceUrl);
                if (lp != null) ribbonPaths[ribbon.id] = lp;
              }
            }
          }

          notifyListeners();
        } else {
          print('[PortraitController] [Background Sync] 변동 사항 없음 -> 기존 뷰 유지');
          // 볼륨 및 음소거 설정 최종 갱신 대응
          if (device!.isMusicEnabled && localMusicPath != null) {
            await playerService.updateMusicVolume(device!.musicVolume, isMuted: device!.isMuted);
          }
        }
      }
    } catch (e) {
      print('[PortraitController] [Background Sync] 에러: $e');
    } finally {
      if (!_isDisposed) {
        isLoading = false;
        statusMessage = '재생 중';
        notifyListeners();
      }
    }
  }

  /// [고인 영정사진 파일 경로 Getter]
  String? get deceasedPhotoPath => localPhotoPath;
}
