import 'dart:convert';
import 'package:http/http.dart' as http;
import '../../models/device_models.dart';
import '../cache/local_db_service.dart';

class ApiService {
  final LocalDbService _dbService = LocalDbService();

  // 장비 정보 패치 (실패 시 로컬 DB Fallback)
  Future<DeviceDto?> fetchDevice(String serverBaseUrl, String deviceCode) async {
    final baseUrl = serverBaseUrl.endsWith('/') ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) : serverBaseUrl;
    final url = Uri.parse('$baseUrl/api/funeral/building/device/code/$deviceCode');
    print('[API Request] fetchDevice: $url');
    try {
      final response = await http.get(url).timeout(const Duration(seconds: 4));
      print('[API Response] fetchDevice Status: ${response.statusCode}');
      
      if (response.statusCode == 200) {
        print('[API Response] body: ${response.body}');
        final json = jsonDecode(response.body);
        final device = DeviceDto.fromJson(json);
        
        try {
          await _dbService.saveDevice(device);
        } catch (e) {
          print('[Cache] 장비 저장 실패 (무시): $e');
        }
        return device;
      }
    } catch (e) {
      print('[API Error] fetchDevice: $e');
    }
    
    try {
      final cached = await _dbService.getDevice(deviceCode);
      if (cached != null) print('[Cache] 로컬 캐시 데이터 반환 성공');
      return cached;
    } catch (e) {
      return null;
    }
  }

  // 호실 소속 고인 정보 패치
  Future<DeceasedDto?> fetchDeceased(String serverBaseUrl, String deviceCode) async {
    final baseUrl = serverBaseUrl.endsWith('/') ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) : serverBaseUrl;
    final url = Uri.parse('$baseUrl/api/funeral/building/deceased/deviceCode/$deviceCode');
    print('[API Request] fetchDeceased: $url');
    try {
      final response = await http.get(url).timeout(const Duration(seconds: 4));
      print('[API Response] fetchDeceased Status: ${response.statusCode}');
      
      if (response.statusCode == 200) {
        print('[API Response] body: ${response.body}');
        final json = jsonDecode(response.body);
        
        bool hasData = false;
        if (json.containsKey('data') && json['data'] is Map && json['data'].containsKey('result')) {
          hasData = (json['data']['result'] as List).isNotEmpty;
        } else if (json.containsKey('result') && json['result'] is List) {
          hasData = (json['result'] as List).isNotEmpty;
        }

        if (hasData) {
          final deceased = DeceasedDto.fromJson(json);
          try {
            await _dbService.saveDeceased(deceased);
          } catch (e) {
            print('[Cache] 고인 저장 실패 (무시): $e');
          }
          return deceased;
        }
      }
    } catch (e) {
      print('[API Error] fetchDeceased: $e');
    }

    try {
      return await _dbService.getDeceasedByRoom(deviceCode);
    } catch (e) {
      return null;
    }
  }

  // 소스(미디어) 정보 패치 (비디오/음악 경로 확인용)
  Future<String?> fetchSourcePath(String serverBaseUrl, String sourceId) async {
    final baseUrl = serverBaseUrl.endsWith('/') ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) : serverBaseUrl;
    final url = Uri.parse('$baseUrl/api/funeral/building/source/$sourceId');
    print('[API Request] fetchSourcePath: $url');
    try {
      final response = await http.get(url).timeout(const Duration(seconds: 4));
      if (response.statusCode == 200) {
        final json = jsonDecode(response.body);
        print('[API Response] fetchSourcePath body: ${response.body}');

        Map<String, dynamic>? data;
        if (json.containsKey('data') && json['data'] is Map && json['data'].containsKey('result')) {
          final list = json['data']['result'] as List;
          if (list.isNotEmpty) data = list[0];
        } else if (json.containsKey('result') && json['result'] is List) {
          final list = json['result'] as List;
          if (list.isNotEmpty) data = list[0];
        } else {
          data = json['data'] ?? json;
        }

        if (data == null) return null;

        // 1. 동영상 처리: webm 우선순위
        if (data['hasWebm'] == true && data['webmUrl'] != null) {
          return data['webmUrl'];
        }

        // 2. 음악 처리: aac 우선순위
        if (data['hasAac'] == true && data['aacUrl'] != null) {
          return data['aacUrl'];
        }

        // 3. 공통 폴백 (기본 url 또는 경로)
        return data['url'] ?? data['filePath'] ?? data['path'];
      }
    } catch (e) {
      print('[API Error] fetchSourcePath: $e');
    }
    return null;
  }

  // 건물/층 입구 안내용 호실 목록과 고인/상주 정보 패치
  Future<List<EntranceGuideRoomDto>> fetchEntranceGuideRooms(String serverBaseUrl, String deviceCode) async {
    final baseUrl = serverBaseUrl.endsWith('/') ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) : serverBaseUrl;
    final url = Uri.parse('$baseUrl/api/funeral/building/deceased/guide/deviceCode/$deviceCode');
    print('[API Request] fetchEntranceGuideRooms: $url');
    try {
      final response = await http.get(url).timeout(const Duration(seconds: 4));
      print('[API Response] fetchEntranceGuideRooms Status: ${response.statusCode}');
      
      if (response.statusCode == 200) {
        final json = jsonDecode(response.body);
        print('[API Response] fetchEntranceGuideRooms body: ${response.body}');
        
        List<dynamic> resultList = [];
        if (json.containsKey('data') && json['data'] is Map && json['data'].containsKey('result')) {
          resultList = json['data']['result'] as List;
        } else if (json.containsKey('result') && json['result'] is List) {
          resultList = json['result'] as List;
        } else if (json.containsKey('data') && json['data'] is List) {
          resultList = json['data'] as List;
        } else if (json is List) {
          resultList = json;
        }

        return resultList.map((item) => EntranceGuideRoomDto.fromJson(item)).toList();
      }
    } catch (e) {
      print('[API Error] fetchEntranceGuideRooms: $e');
    }
    return [];
  }
}
