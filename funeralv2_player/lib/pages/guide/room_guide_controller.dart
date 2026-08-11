import 'package:flutter/material.dart';
import '../../models/device_models.dart';
import '../../services/api/api_service.dart';
import '../../services/player/media_player_service.dart';
import '../../services/cache/cache_manager.dart';
import '../../services/cache/local_db_service.dart';
import '../../services/device_update_bus.dart';

/// [호실 입구 안내 컨트롤러]
/// 개별 호실(빈소) 입구에 배치되는 사이니지 안내판(ROOM_GUIDE)에 데이터를 바인딩하고
/// 미디어(배경 비디오 및 고인 영정 사진)의 로컬 캐시 다운로드 및 실시간 화면 갱신을 주도합니다.
class RoomGuideController extends ChangeNotifier with DeviceAutoSync {
  final ApiService _apiService = ApiService(); // 서버 API 서비스
  final MediaPlayerService playerService = MediaPlayerService(); // 미디어 재생 서비스
  final CacheManager _cacheManager = CacheManager(); // 미디어 캐시 매니저
  final LocalDbService _dbService = LocalDbService(); // 로컬 캐시 DB 서비스

  DeviceDto? device; // 장비 설정 정보
  DeceasedDto? deceased; // 빈소에 모셔진 고인의 정보
  String? localVideoPath; // 로컬에 저장된 배경 영상 파일 물리 경로
  String? deceasedPhotoPath; // 로컬에 저장된 고인 영정사진 파일 물리 경로
  bool isLoading = false; // 데이터 조회 로딩 상태
  bool _isDisposed = false; // 위젯 트리에서 이탈했는지 여부 (메모리 누수 차단)

  /// [자원 해제]
  /// 컨트롤러 파괴 시 함께 물려 있는 미디어 리소스를 안전하게 해제합니다.
  @override
  void dispose() {
    _isDisposed = true;
    unbindAutoSync(); // 전역 설정 변경 버스 구독 해제
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

  /// [컨트롤러 초기화 루틴 (Offline-First)]
  /// 먼저 로컬 DB 캐시를 즉각 읽어와 화면을 구성하고 비디오를 로드하여 시동 속도를 극대화합니다.
  /// 이후 백그라운드 스레드에서 백엔드 서버 동기화를 호출합니다.
  Future<void> init(String serverBaseUrl, String deviceCode, Function() onRefresh) async {
    if (_isDisposed) return;
    isLoading = true;
    notifyListeners();

    // 1. [Cache-First] 로컬 DB에서 장비 정보 즉각 조회 및 셋업
    final cachedDevice = await _dbService.getDevice(deviceCode);
    if (cachedDevice != null && !_isDisposed) {
      device = cachedDevice;
      
      // 로컬 고인 행사 정보 로드
      final cachedDeceased = await _dbService.getDeceasedByDeviceCode(deviceCode);
      deceased = cachedDeceased;

      // 로컬 비디오 캐시 즉시 재생
      if (device!.isVideoEnabled && device!.videoId != null) {
        final cachedVideoPath = await _dbService.getSourcePath(device!.videoId!);
        if (cachedVideoPath != null) {
          final cachedLocalVideo = await _cacheManager.getLocalFile(cachedVideoPath);
          if (cachedLocalVideo != null && !_isDisposed) {
            localVideoPath = cachedLocalVideo;
            await playerService.playVideo(localVideoPath!, onRefresh);
          }
        }
      }

      // 로컬 영정사진 캐시 즉시 매핑
      if (deceased != null) {
        final photoPath = (deceased!.memorialEditedPhotoUrl != null && deceased!.memorialEditedPhotoUrl!.isNotEmpty)
            ? deceased!.memorialEditedPhotoUrl
            : deceased!.memorialPhotoUrl;
        if (photoPath != null) {
          deceasedPhotoPath = await _cacheManager.getLocalFile(photoPath);
        }
      }

      // 즉각 로딩 해제 후 먼저 로컬 UI 표출
      isLoading = false;
      notifyListeners();
    }

    // 2. 백그라운드 서버 비동기 동기화 시작 (UI 동기화 차단 해제)
    _syncWithServer(serverBaseUrl, deviceCode, onRefresh);

    // 3. 전역 설정 변경 버스 구독 (SignalR 수신 시 뷰 재생성 없이 제자리 재동기화)
    bindAutoSync(serverBaseUrl, deviceCode, onRefresh);
  }

  /// [백그라운드 서버 동기화 루틴]
  Future<void> _syncWithServer(String serverBaseUrl, String deviceCode, Function() onRefresh) async {
    try {
      print('[RoomGuideController] [Background Sync] 시작');
      final fetchedDevice = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
      if (fetchedDevice != null && !_isDisposed) {
        DeceasedDto? fetchedDeceased;
        if (fetchedDevice.roomId != null) {
          fetchedDeceased = await _apiService.fetchDeceased(serverBaseUrl, deviceCode);
        }

        // 기존 캐시 상태와 백엔드 최신 상태 간에 변경 사항이 존재하는지 비교 (장비 렌더링 속성 전체 + 고인 정보)
        bool isChanged = device == null ||
            !device!.signageEquals(fetchedDevice) ||
            deceased?.id != fetchedDeceased?.id ||
            deceased?.name != fetchedDeceased?.name ||
            deceased?.burialDate != fetchedDeceased?.burialDate;

        if (isChanged) {
          print('[RoomGuideController] [Background Sync] 변경점 발견 -> UI 업데이트 수행');
          device = fetchedDevice;
          deceased = fetchedDeceased;

          // 백그라운드 비디오 갱신
          if (device!.isVideoEnabled && device!.videoId != null) {
            final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.videoId!);
            final nextVideoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
            if (nextVideoPath != null && !_isDisposed && localVideoPath != nextVideoPath) {
              localVideoPath = nextVideoPath;
              await playerService.playVideo(localVideoPath!, onRefresh);
            }
          } else {
            await playerService.stopVideo();
            localVideoPath = null;
          }

          // 영정사진 갱신
          if (deceased != null) {
            final photoPath = (deceased!.memorialEditedPhotoUrl != null && deceased!.memorialEditedPhotoUrl!.isNotEmpty)
                ? deceased!.memorialEditedPhotoUrl
                : deceased!.memorialPhotoUrl;
            deceasedPhotoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, photoPath);
          } else {
            deceasedPhotoPath = null;
          }

          notifyListeners();
        } else {
          print('[RoomGuideController] [Background Sync] 변동 사항 없음 -> 기존 뷰 유지');
        }
      }
    } catch (e) {
      print('[RoomGuideController] [Background Sync] 에러 발생: $e');
    } finally {
      if (!_isDisposed) {
        isLoading = false;
        notifyListeners();
      }
    }
  }
}
