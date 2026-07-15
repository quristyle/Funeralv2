import 'package:flutter/material.dart';
import '../../models/device_models.dart';
import '../../services/api/api_service.dart';
import '../../services/player/media_player_service.dart';
import '../../services/cache/cache_manager.dart';
import '../../services/cache/local_db_service.dart';
import 'dart:convert';

/// [종합 안내 키오스크 뷰 컨트롤러]
/// 대고객 터치형 종합 키오스크(KIOSK) 화면에 표출할 전체 호실 현황, 약도 및 주차장 미디어 경로를 
/// 서버로부터 로드하고, 캐싱 및 실시간 동기화 상태를 제어합니다.
class KioskController extends ChangeNotifier {
  final ApiService _apiService = ApiService(); // 서버 API 서비스
  final MediaPlayerService playerService = MediaPlayerService(); // 비디오/사운드 재생 서비스
  final CacheManager _cacheManager = CacheManager(); // 미디어 캐시 매니저
  final LocalDbService _dbService = LocalDbService(); // 로컬 DB 캐시 서비스

  DeviceDto? device; // 장비 설정 정보
  List<EntranceGuideRoomDto> rooms = []; // 전체 호실 정보 보관 리스트
  List<String> buildingPhotos = []; // 건물 층별 약도/층 안내 이미지 경로 리스트
  List<String> parkingPhotos = []; // 주차장 및 오시는 길 이미지 경로 리스트
  bool isLoading = false; // 데이터 조회 상태 플래그
  bool _isDisposed = false; // 위젯 생명주기 이탈 제어 플래그

  // 로컬 캐시 미디어 파일 경로 목록
  Map<String, String> deceasedPhotoPaths = {};
  List<String> localBuildingPhotos = [];
  List<String> localParkingPhotos = [];

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

  /// [키오스크 데이터 및 미디어 초기화 루틴 (Cache-First)]
  /// 시동 즉시 로컬 캐시 DB에서 키오스크 레이아웃 및 약도 데이터를 불러와 렌더링하고 비디오를 실행합니다.
  /// 그 뒤 백그라운드 비동기로 최신 정보 동기화를 수행합니다.
  Future<void> init(String serverBaseUrl, String deviceCode, Function() onVideoInitialized) async {
    if (_isDisposed) return;
    isLoading = true;
    notifyListeners();

    // 1. [Cache-First] 로컬 DB에서 즉시 데이터 로딩
    final cachedDevice = await _dbService.getDevice(deviceCode);
    if (cachedDevice != null && !_isDisposed) {
      device = cachedDevice;

      // 로컬 키오스크 안내 데이터 로드
      final cachedJson = await _dbService.getKioskGuide(deviceCode);
      if (cachedJson != null) {
        try {
          final decoded = jsonDecode(cachedJson);
          Map<String, dynamic>? targetMap;
          if (decoded.containsKey('data') && decoded['data'] is Map) {
            final dataMap = decoded['data'] as Map<String, dynamic>;
            if (dataMap.containsKey('result') && dataMap['result'] is List) {
              final list = dataMap['result'] as List;
              if (list.isNotEmpty && list[0] is Map) {
                targetMap = list[0] as Map<String, dynamic>;
              }
            }
            targetMap ??= dataMap;
          }
          if (targetMap != null) {
            final kioskData = KioskGuideResponseDto.fromJson(targetMap);
            rooms = kioskData.rooms;
            buildingPhotos = kioskData.buildingPhotos;
            parkingPhotos = kioskData.parkingPhotos;
          }
        } catch (e) {
          print('[KioskController] 캐시 파싱 에러: $e');
        }
      }

      // 로컬 영정사진 캐시 즉시 구성
      deceasedPhotoPaths.clear();
      for (var room in rooms) {
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

      // 약도 및 주차장 사진 로컬 캐시 즉시 구성
      localBuildingPhotos.clear();
      for (var photoPath in buildingPhotos) {
        final lp = await _cacheManager.getLocalFile(photoPath);
        localBuildingPhotos.add(lp ?? '');
      }

      localParkingPhotos.clear();
      for (var photoPath in parkingPhotos) {
        final lp = await _cacheManager.getLocalFile(photoPath);
        localParkingPhotos.add(lp ?? '');
      }

      // 로컬 비디오 캐시 즉시 구동
      if (device!.isVideoEnabled && device!.videoId != null) {
        final cachedVideoPath = await _dbService.getSourcePath(device!.videoId!);
        if (cachedVideoPath != null) {
          final cachedLocalVideo = await _cacheManager.getLocalFile(cachedVideoPath);
          if (cachedLocalVideo != null && !_isDisposed) {
            await playerService.playVideo(cachedLocalVideo, onVideoInitialized);
          }
        }
      }

      isLoading = false;
      notifyListeners();
    }

    // 2. 백그라운드 서버 동기화 작업 기동
    _syncWithServer(serverBaseUrl, deviceCode, onVideoInitialized);
  }

  /// [백그라운드 비동기 서버 동기화 루틴]
  Future<void> _syncWithServer(String serverBaseUrl, String deviceCode, Function() onVideoInitialized) async {
    try {
      print('[KioskController] [Background Sync] 시작');
      final fetchedDevice = await _apiService.fetchDevice(serverBaseUrl, deviceCode);
      if (fetchedDevice != null && !_isDisposed) {
        final kioskData = await _apiService.fetchKioskRooms(serverBaseUrl, deviceCode);

        // 변경점 검사
        bool isChanged = device == null ||
            device!.id != fetchedDevice.id ||
            device!.isVideoEnabled != fetchedDevice.isVideoEnabled ||
            device!.videoId != fetchedDevice.videoId ||
            rooms.length != kioskData.rooms.length ||
            parkingPhotos.length != kioskData.parkingPhotos.length;

        if (!isChanged) {
          // 세부 탭 매핑 상태 비교
          for (int i = 0; i < rooms.length; i++) {
            if (rooms[i]?.roomId != kioskData.rooms[i]?.roomId ||
                rooms[i]?.deceasedDetail?.id != kioskData.rooms[i]?.deceasedDetail?.id ||
                rooms[i]?.deceasedDetail?.name != kioskData.rooms[i]?.deceasedDetail?.name) {
              isChanged = true;
              break;
            }
          }
        }

        if (isChanged) {
          print('[KioskController] [Background Sync] 변경점 발견 -> UI 리프레시');
          device = fetchedDevice;
          rooms = kioskData.rooms;
          buildingPhotos = kioskData.buildingPhotos;
          parkingPhotos = kioskData.parkingPhotos;

          // 고인 영정사진 캐시 다운로드 및 맵 갱신
          deceasedPhotoPaths.clear();
          for (var room in rooms) {
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

          // 약도 및 주차장 사진 캐시 갱신
          localBuildingPhotos.clear();
          for (var photoPath in buildingPhotos) {
            final lp = await _cacheManager.getCachedFileByPath(serverBaseUrl, photoPath);
            localBuildingPhotos.add(lp ?? '');
          }

          localParkingPhotos.clear();
          for (var photoPath in parkingPhotos) {
            final lp = await _cacheManager.getCachedFileByPath(serverBaseUrl, photoPath);
            localParkingPhotos.add(lp ?? '');
          }

          // 비디오 갱신
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
          print('[KioskController] [Background Sync] 변동 사항 없음 -> 기존 뷰 유지');
        }
      }
    } catch (e) {
      print('[KioskController] [Background Sync] 에러: $e');
    } finally {
      if (!_isDisposed) {
        isLoading = false;
        notifyListeners();
      }
    }
  }
}
