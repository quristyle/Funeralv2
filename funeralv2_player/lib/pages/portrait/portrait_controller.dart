import 'package:flutter/material.dart';
import '../../models/device_models.dart';
import '../../services/api/api_service.dart';
import '../../services/cache/cache_manager.dart';
import '../../services/player/media_player_service.dart';

/// [영정 화면 및 제례 제어 컨트롤러]
/// 빈소 내부 제단에 놓이는 대형 영정 사이니지 화면(`FUNERAL_PORTRAIT`)에 
/// 표출할 보정 영정 사진, 리본 장식 레이어, 텍스트 오버레이, 배경 이미지, 배경 오디오(추모곡)를 로드하고 통제합니다.
class PortraitController extends ChangeNotifier {
  final ApiService _apiService = ApiService(); // 서버 API 서비스
  final CacheManager _cacheManager = CacheManager(); // 미디어 캐시 매니저
  final MediaPlayerService playerService = MediaPlayerService(); // 비디오/오디오 엔진 서비스

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

  /// [영정 화면 데이터 및 미디어 로드 핵심 루틴]
  /// 서버에서 장비 사양을 가져오고, 이에 속한 고인 정보를 동기화합니다.
  /// 그 뒤 비디오, 오디오(추모음악), 영정사진, 배경 스킨, 근조 리본 장식 파일들을 차례대로 로컬 캐싱하고,
  /// 플레이어 엔진에 기동을 위임한 뒤 SignalR 허브 채널을 오픈하여 실시간 이벤트 대기를 탑재합니다.
  Future<void> init(String serverBaseUrl, String deviceCode, Function() onVideoInitialized) async {
    if (_isDisposed) return;

    isLoading = true;
    statusMessage = '장비 정보를 불러오는 중...';
    notifyListeners();

    try {
      // 1. 장비 사양 획득
      final newDevice = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
      if (newDevice == null) {
        statusMessage = '장비 정보를 불러오지 못했습니다.';
        isLoading = false;
        notifyListeners();
        return;
      }
      device = newDevice;

      // 설정값 변경에 즉각적으로 음소거 및 비디오 정지를 물리적으로 반영합니다.
      if (!device!.isMusicEnabled) { await playerService.stopMusic(); localMusicPath = null; }
      if (!device!.isVideoEnabled) { await playerService.stopVideo(); localVideoPath = null; }
      notifyListeners();

      // 2. 해당 기기 빈소의 고인 상세 데이터 로드
      statusMessage = '고인 정보를 동기화하는 중...';
      print('[PortraitController] 고인 정보 API 호출: $deviceCode');
      deceased = await _apiService.fetchDeceased(serverBaseUrl, deviceCode);
      print('[PortraitController] 고인 정보 로드 완료: ${deceased?.name}');
      notifyListeners();

      if (_isDisposed) return;

      // 3. 다양한 종류의 연동 미디어 리소스 다운로드 및 로컬 캐싱 동기화
      // 3-1) 백그라운드 영상 경로
      String? nextVideoPath;
      if (device!.isVideoEnabled && device!.videoId != null) {
        final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.videoId!);
        nextVideoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
      }

      // 3-2) 추모 음악 경로
      String? nextMusicPath;
      if (device!.isMusicEnabled && device!.musicId != null) {
        final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.musicId!);
        nextMusicPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
      }

      // 3-3) 고인 영정사진 경로 (보정 이미지 우선 적용)
      if (device!.isMemorialPhotoEnabled && deceased != null) {
        final photoPath = (deceased!.memorialEditedPhotoUrl != null && deceased!.memorialEditedPhotoUrl!.isNotEmpty)
            ? deceased!.memorialEditedPhotoUrl
            : deceased!.memorialPhotoUrl;
        localPhotoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, photoPath);
      } else {
        localPhotoPath = null;
      }

      // 3-4) 배경 스킨 이미지 경로
      if (device!.isBackgroundImageEnabled && device!.backgroundImageUrl != null && device!.backgroundImageUrl!.isNotEmpty) {
        localBackgroundPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, device!.backgroundImageUrl);
      } else {
        localBackgroundPath = null;
      }

      // 3-5) 근조 리본 장식 이미지 경로 일괄 다운로드
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

      // 4. 캐싱된 리소스를 바탕으로 실제 플레이 가동 설정 적용
      // 4-1) 비디오 플레이 기동
      if (device!.isVideoEnabled && nextVideoPath != null) {
        if (localVideoPath != nextVideoPath) {
          localVideoPath = nextVideoPath;
          await playerService.playVideo(localVideoPath!, onVideoInitialized);
        }
      } else {
        await playerService.stopVideo();
        localVideoPath = null;
      }

      // 4-2) 배경 음악 플레이 기동
      if (device!.isMusicEnabled && nextMusicPath != null) {
        if (localMusicPath != nextMusicPath) {
          localMusicPath = nextMusicPath;
          await playerService.playMusic(localMusicPath!, device!.musicVolume, isMuted: device!.isMuted);
        }
      } else {
        await playerService.stopMusic();
        localMusicPath = null;
      }

      // 4-3) 음악이 재생 중인 상태라면 실시간 볼륨 업데이트 처리 진행
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
  }

  /// [고인 영정사진 파일 경로 Getter]
  String? get deceasedPhotoPath => localPhotoPath;
}
