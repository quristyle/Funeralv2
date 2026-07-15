import 'package:flutter/material.dart';
import '../../models/device_models.dart';
import '../../services/api/api_service.dart';
import '../../services/player/media_player_service.dart';
import '../../services/cache/cache_manager.dart';
import '../../services/cache/local_db_service.dart';
import 'dart:convert';

/// [입구 종합 안내 컨트롤러]
/// 장례식장 입구 종합 안내판(ENTRANCE_GUIDE) 화면에 표출할 데이터와 미디어를 로드하고 관리합니다.
class EntranceGuideController extends ChangeNotifier {
  final ApiService _apiService = ApiService(); // 서버 API 서비스
  final MediaPlayerService playerService = MediaPlayerService(); // 미디어 재생 서비스
  final CacheManager _cacheManager = CacheManager(); // 미디어 캐시 매니저
  final LocalDbService _dbService = LocalDbService(); // 로컬 DB 캐시 서비스

  DeviceDto? device; // 장비 설정 정보
  List<EntranceGuideRoomDto> guideRooms = []; // 입구 안내판에 노출할 빈소/호실 목록 데이터
  bool isLoading = false; // 로딩 여부 플래그
  bool _isDisposed = false; // 메모리 릭 방지를 위한 Dispose 감지용 플래그

  // 고인 ID별 로컬 영정사진 파일 경로 매핑 테이블
  Map<String, String> deceasedPhotoPaths = {};

  /// [자원 해제]
  /// 컨트롤러가 제거될 때 자원 누수를 막기 위해 미디어 재생기도 함께 파괴합니다.
  @override
  void dispose() {
    _isDisposed = true;
    playerService.dispose();
    super.dispose();
  }

  /// [UI 갱신 전파]
  /// Dispose된 위젯에서 리스너 갱신을 전파하다 오동작(Exception)이 발생하는 것을 막기 위해 안전 장치를 얹어 재정의합니다.
  @override
  void notifyListeners() {
    if (!_isDisposed) super.notifyListeners();
  }

  /// [안내판 데이터 및 미디어 초기화 루틴 (Cache-First)]
  /// 우선 로컬 DB 캐시에서 장비 사양 및 호실 데이터를 파싱하여 즉각 화면에 표출하고 비디오를 구동합니다.
  /// 그 뒤 백그라운드 비동기로 서버 최신 정보 동기화를 수행합니다.
  Future<void> init(String serverBaseUrl, String deviceCode, Function() onVideoInitialized) async {
    if (_isDisposed) return;
    isLoading = true;
    notifyListeners();

    // 1. [Cache-First] 로컬 DB 즉시 로딩
    final cachedDevice = await _dbService.getDevice(deviceCode);
    if (cachedDevice != null && !_isDisposed) {
      device = cachedDevice;

      // 로컬 입구 안내 JSON 로드 및 역직렬화
      final cachedJson = await _dbService.getEntranceGuide(deviceCode);
      if (cachedJson != null) {
        try {
          final decoded = jsonDecode(cachedJson);
          List<dynamic> resultList = [];
          if (decoded.containsKey('data') && decoded['data'] is Map && decoded['data'].containsKey('result')) {
            resultList = decoded['data']['result'] as List;
          }
          guideRooms = resultList.map((item) => EntranceGuideRoomDto.fromJson(item)).toList();
        } catch (e) {
          print('[EntranceGuideController] 캐시 파싱 에러: $e');
        }
      }

      // 로컬 고인별 영정사진 캐시 즉시 구성
      deceasedPhotoPaths.clear();
      for (var room in guideRooms) {
        final dec = room.deceasedDetail;
        if (dec != null) {
          final photoPath = dec.memorialEditedPhotoUrl ?? dec.memorialPhotoUrl;
          if (photoPath != null && photoPath.isNotEmpty) {
            final lp = await _cacheManager.getLocalFile(photoPath);
            if (lp != null) {
              deceasedPhotoPaths[dec.id] = lp;
            }
          }
        }
      }

      // 로컬 비디오 캐시 재생 가동
      if (device!.isVideoEnabled && device!.videoId != null) {
        final cachedVideoPath = await _dbService.getSourcePath(device!.videoId!);
        if (cachedVideoPath != null) {
          final cachedLocalVideo = await _cacheManager.getLocalFile(cachedVideoPath);
          if (cachedLocalVideo != null && !_isDisposed) {
            await playerService.playVideo(cachedLocalVideo, onVideoInitialized);
          }
        }
      }

      // 로딩 해제하고 먼저 화면 렌더링
      isLoading = false;
      notifyListeners();
    }

    // 2. 백그라운드 서버 동기화 작업 기동
    _syncWithServer(serverBaseUrl, deviceCode, onVideoInitialized);
  }

  /// [백그라운드 비동기 서버 동기화 루틴]
  Future<void> _syncWithServer(String serverBaseUrl, String deviceCode, Function() onVideoInitialized) async {
    try {
      print('[EntranceGuideController] [Background Sync] 시작');
      final fetchedDevice = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
      if (fetchedDevice != null && !_isDisposed) {
        final fetchedRooms = await _apiService.fetchEntranceGuideRooms(serverBaseUrl, deviceCode);

        // 변경 사항 검출
        bool isChanged = device == null ||
            device!.id != fetchedDevice.id ||
            device!.isVideoEnabled != fetchedDevice.isVideoEnabled ||
            device!.videoId != fetchedDevice.videoId ||
            guideRooms.length != fetchedRooms.length;

        if (!isChanged) {
          // 리스트 개수가 같아도 세부 고인/방 ID 등의 매핑 상태 변경 비교
          for (int i = 0; i < guideRooms.length; i++) {
            if (guideRooms[i]?.roomId != fetchedRooms[i]?.roomId ||
                guideRooms[i]?.deceasedDetail?.id != fetchedRooms[i]?.deceasedDetail?.id ||
                guideRooms[i]?.deceasedDetail?.name != fetchedRooms[i]?.deceasedDetail?.name) {
              isChanged = true;
              break;
            }
          }
        }

        if (isChanged) {
          print('[EntranceGuideController] [Background Sync] 변경점 발견 -> UI 리프레시');
          device = fetchedDevice;
          guideRooms = fetchedRooms;

          // 고인 영정사진 캐시 다운로드 및 맵 갱신
          deceasedPhotoPaths.clear();
          for (var room in guideRooms) {
            final dec = room.deceasedDetail;
            if (dec != null) {
              final photoPath = dec.memorialEditedPhotoUrl ?? dec.memorialPhotoUrl;
              if (photoPath != null && photoPath.isNotEmpty) {
                final lp = await _cacheManager.getCachedFileByPath(serverBaseUrl, photoPath);
                if (lp != null) {
                  deceasedPhotoPaths[dec.id] = lp;
                }
              }
            }
          }

          // 비디오 재생 갱신
          if (device!.isVideoEnabled && device!.videoId != null) {
            final sourcePath = await _apiService.fetchSourcePath(serverBaseUrl, device!.videoId!);
            final localVideoPath = await _cacheManager.getCachedFileByPath(serverBaseUrl, sourcePath);
            if (localVideoPath != null && !_isDisposed) {
              await playerService.playVideo(localVideoPath, onVideoInitialized);
            }
          } else {
            await playerService.stopVideo();
          }

          notifyListeners();
        } else {
          print('[EntranceGuideController] [Background Sync] 변동 사항 없음 -> 기존 뷰 유지');
        }
      }
    } catch (e) {
      print('[EntranceGuideController] [Background Sync] 에러: $e');
    } finally {
      if (!_isDisposed) {
        isLoading = false;
        notifyListeners();
      }
    }
  }
}
