import 'dart:convert';
import 'package:http/http.dart' as http;
import '../../models/device_models.dart';
import '../cache/local_db_service.dart';

class ApiService {
  final LocalDbService _dbService = LocalDbService();

  // 1. 장비 정보
  Future<DeviceDto?> fetchDevice(String serverBaseUrl, String deviceCode) async {
    final baseUrl = serverBaseUrl.endsWith('/') ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) : serverBaseUrl;
    final url = Uri.parse('$baseUrl/api/funeral/building/device/code/$deviceCode');
    print('[API Request] fetchDevice: $url');
    try {
      final response = await http.get(url).timeout(const Duration(seconds: 4));
      if (response.statusCode == 200) {
        final json = jsonDecode(response.body);
        final device = DeviceDto.fromJson(json);
        await _dbService.saveDevice(device);
        return device;
      }
    } catch (e) {
      print('[API Error] fetchDevice: $e');
    }
    return await _dbService.getDevice(deviceCode);
  }

  // 2. 고인 정보
  Future<DeceasedDto?> fetchDeceased(String serverBaseUrl, String deviceCode) async {
    final baseUrl = serverBaseUrl.endsWith('/') ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) : serverBaseUrl;
    final url = Uri.parse('$baseUrl/api/funeral/building/deceased/deviceCode/$deviceCode');
    print('[API Request] fetchDeceased: $url');
    try {
      final response = await http.get(url).timeout(const Duration(seconds: 4));
      if (response.statusCode == 200) {
        final json = jsonDecode(response.body);
        bool hasData = false;
        if (json.containsKey('data') && json['data'] is Map && json['data'].containsKey('result')) {
          hasData = (json['data']['result'] as List).isNotEmpty;
        }
        if (hasData) {
          final deceased = DeceasedDto.fromJson(json);
          await _dbService.saveDeceased(deceased, deviceCode);
          return deceased;
        }
      }
    } catch (e) {
      print('[API Error] fetchDeceased: $e');
    }
    return await _dbService.getDeceasedByDeviceCode(deviceCode);
  }

  // 3. 입구 안내 호실 목록
  Future<List<EntranceGuideRoomDto>> fetchEntranceGuideRooms(String serverBaseUrl, String deviceCode) async {
    final baseUrl = serverBaseUrl.endsWith('/') ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) : serverBaseUrl;
    final url = Uri.parse('$baseUrl/api/funeral/building/deceased/guide/deviceCode/$deviceCode');
    print('[API Request] fetchEntranceGuideRooms: $url');
    try {
      final response = await http.get(url).timeout(const Duration(seconds: 4));
      if (response.statusCode == 200) {
        final body = response.body;
        final json = jsonDecode(body);
        List<dynamic> resultList = [];
        if (json.containsKey('data') && json['data'] is Map && json['data'].containsKey('result')) {
          resultList = json['data']['result'] as List;
        }
        
        // 데이터가 있다면 캐시 업데이트
        if (resultList.isNotEmpty) {
          await _dbService.saveEntranceGuide(deviceCode, body);
        }

        return resultList.map((item) => EntranceGuideRoomDto.fromJson(item)).toList();
      }
    } catch (e) {
      print('[API Error] fetchEntranceGuideRooms: $e');
    }

    // 오프라인 캐시 로드
    print('[Cache] 입구 안내 목록 오프라인 캐시 조회를 시도합니다.');
    final cachedBody = await _dbService.getEntranceGuide(deviceCode);
    if (cachedBody != null) {
      final json = jsonDecode(cachedBody);
      List<dynamic> resultList = json['data']['result'] as List;
      return resultList.map((item) => EntranceGuideRoomDto.fromJson(item)).toList();
    }
    return [];
  }

  // 4. 미디어 소스 경로
  Future<String?> fetchSourcePath(String serverBaseUrl, String sourceId) async {
    final baseUrl = serverBaseUrl.endsWith('/') ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) : serverBaseUrl;
    final url = Uri.parse('$baseUrl/api/funeral/building/source/$sourceId');
    print('[API Request] fetchSourcePath: $url');
    try {
      final response = await http.get(url).timeout(const Duration(seconds: 4));
      if (response.statusCode == 200) {
        final json = jsonDecode(response.body);
        Map<String, dynamic>? data;
        if (json.containsKey('data') && json['data'] is Map && json['data'].containsKey('result')) {
          final list = json['data']['result'] as List;
          if (list.isNotEmpty) data = list[0];
        }

        if (data != null) {
          String? path;
          if (data['hasWebm'] == true && data['webmUrl'] != null) path = data['webmUrl'];
          else if (data['hasAac'] == true && data['aacUrl'] != null) path = data['aacUrl'];
          else path = data['url'] ?? data['filePath'] ?? data['path'];

          if (path != null) {
            await _dbService.saveSourcePath(sourceId, path); // 경로 캐시 저장
            return path;
          }
        }
      }
    } catch (e) {
      print('[API Error] fetchSourcePath: $e');
    }
    
    // 오프라인 캐시 로드
    return await _dbService.getSourcePath(sourceId);
  }

  // 5. 키오스크 정보
  Future<KioskGuideResponseDto> fetchKioskRooms(String serverBaseUrl, String deviceCode) async {
    final baseUrl = serverBaseUrl.endsWith('/') ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) : serverBaseUrl;
    final url = Uri.parse('$baseUrl/api/funeral/building/deceased/kiosk/deviceCode/$deviceCode');
    print('[API Request] fetchKioskRooms: $url');
    try {
      final response = await http.get(url).timeout(const Duration(seconds: 4));
      if (response.statusCode == 200) {
        final json = jsonDecode(response.body);
        Map<String, dynamic>? targetMap;
        if (json.containsKey('data') && json['data'] is Map) {
          final dataMap = json['data'] as Map<String, dynamic>;
          if (dataMap.containsKey('result') && dataMap['result'] is List) {
            final list = dataMap['result'] as List;
            if (list.isNotEmpty && list[0] is Map) targetMap = list[0] as Map<String, dynamic>;
          }
          targetMap ??= dataMap;
        }
        if (targetMap != null) return KioskGuideResponseDto.fromJson(targetMap);
      }
    } catch (e) {
      print('[API Error] fetchKioskRooms: $e');
    }
    return KioskGuideResponseDto(rooms: [], buildingPhotos: [], parkingPhotos: []);
  }

  Future<DeviceDto?> getCachedDevice(String deviceCode) async {
    return await _dbService.getDevice(deviceCode);
  }
}
