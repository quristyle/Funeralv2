import 'dart:convert';
import 'package:http/http.dart' as http;
import '../../models/device_models.dart';
import '../cache/local_db_service.dart';
import '../auth/device_auth.dart';

/// [API 서비스 클래스]
/// 장례식장 백엔드 서버와 통신하여 실시간 데이터를 조회하고, 오프라인 작동을 지원하기 위한 로컬 캐시 처리를 수행합니다.
class ApiService {
  // 로컬 데이터베이스 캐시 접근을 위한 서비스 인스턴스
  final LocalDbService _dbService = LocalDbService();

  /// [장비 정보 조회]
  /// 서버로부터 장비 식별코드([deviceCode])에 해당하는 장비 상세 설정 정보를 조회합니다.
  /// 조회 성공 시 로컬 캐시를 업데이트하고, 실패 시 로컬 DB에 보관된 최신 설정 정보를 반환합니다.
  Future<DeviceDto?> fetchDevice(String serverBaseUrl, String deviceCode) async {
    // URL 끝의 '/'를 제거하여 표준 주소 형식을 맞춥니다.
    final baseUrl = serverBaseUrl.endsWith('/') ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) : serverBaseUrl;
    final url = Uri.parse('$baseUrl/api/funeral/building/device/code/$deviceCode');
    print('[API Request] fetchDevice: $url');
    try {
      final response = await http.get(url, headers: DeviceAuth.headers()).timeout(const Duration(seconds: 15));
      if (response.statusCode == 200) {
        final json = jsonDecode(response.body);
        final device = DeviceDto.fromJson(json);
        
        // 조회 성공 시 로컬 DB에 장비 정보 캐싱
        await _dbService.saveDevice(device);
        return device;
      }
    } catch (e) {
      print('[API Error] fetchDevice: $e');
    }
    
    // 네트워크 오류 등으로 예외 발생 시 로컬 캐싱된 데이터 반환 (오프라인 지원)
    return await _dbService.getDevice(deviceCode);
  }

  /// [고인 정보 조회]
  /// 장비 코드([deviceCode])에 대응되는 빈소(호실)의 고인 상세 행사 정보를 조회합니다.
  /// 성공 시 로컬 DB를 갱신하며, 네트워크 오류 시 로컬 캐싱된 데이터를 리턴합니다.
  Future<DeceasedDto?> fetchDeceased(String serverBaseUrl, String deviceCode) async {
    final baseUrl = serverBaseUrl.endsWith('/') ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) : serverBaseUrl;
    final url = Uri.parse('$baseUrl/api/funeral/building/deceased/deviceCode/$deviceCode');
    print('[API Request] fetchDeceased: $url');
    try {
      final response = await http.get(url, headers: DeviceAuth.headers()).timeout(const Duration(seconds: 15));
      if (response.statusCode == 200) {
        final json = jsonDecode(response.body);
        bool hasData = false;
        
        // 공통 API DTO 구조(data.result)에 실질적인 데이터가 포함되어 있는지 확인
        if (json.containsKey('data') && json['data'] is Map && json['data'].containsKey('result')) {
          hasData = (json['data']['result'] as List).isNotEmpty;
        }
        
        if (hasData) {
          final deceased = DeceasedDto.fromJson(json);
          // 로컬 캐시 DB에 고인 정보 및 매핑 관계 저장
          await _dbService.saveDeceased(deceased, deviceCode);
          return deceased;
        }
      }
    } catch (e) {
      print('[API Error] fetchDeceased: $e');
    }
    
    // 오프라인 대응: 로컬 캐시 반환
    return await _dbService.getDeceasedByDeviceCode(deviceCode);
  }

  /// [입구 안내 호실 목록 조회]
  /// 장례식장 입구 종합 안내판용으로 전체 빈소의 행사 일정 및 고인 요약 목록을 일괄 조회합니다.
  /// 마찬가지로 성공 시 전체 JSON 문자열을 그대로 DB에 캐싱하고, 실패 시 저장된 캐시 본문을 파싱하여 반환합니다.
  Future<List<EntranceGuideRoomDto>> fetchEntranceGuideRooms(String serverBaseUrl, String deviceCode) async {
    final baseUrl = serverBaseUrl.endsWith('/') ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) : serverBaseUrl;
    final url = Uri.parse('$baseUrl/api/funeral/building/deceased/guide/deviceCode/$deviceCode');
    print('[API Request] fetchEntranceGuideRooms: $url');
    try {
      final response = await http.get(url, headers: DeviceAuth.headers()).timeout(const Duration(seconds: 15));
      if (response.statusCode == 200) {
        final body = response.body;
        final json = jsonDecode(body);
        List<dynamic> resultList = [];
        if (json.containsKey('data') && json['data'] is Map && json['data'].containsKey('result')) {
          resultList = json['data']['result'] as List;
        }
        
        // 수신된 리스트가 유효하다면 로컬 DB에 응답 문자열 전체를 캐싱
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

  /// [미디어 소스 경로(URL) 조회]
  /// 이미지, 비디오, 음원 등 서버에 등록된 미디어 리소스의 실제 물리 URL 경로를 조회합니다.
  /// 파일의 인코딩 형식(WebM, AAC 등) 유무에 따라 우선순위 경로를 판단하여 가져오며, 캐싱합니다.
  Future<String?> fetchSourcePath(String serverBaseUrl, String sourceId) async {
    final baseUrl = serverBaseUrl.endsWith('/') ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) : serverBaseUrl;
    final url = Uri.parse('$baseUrl/api/funeral/building/source/$sourceId');
    print('[API Request] fetchSourcePath: $url');
    try {
      final response = await http.get(url, headers: DeviceAuth.headers()).timeout(const Duration(seconds: 15));
      if (response.statusCode == 200) {
        final json = jsonDecode(response.body);
        Map<String, dynamic>? data;
        if (json.containsKey('data') && json['data'] is Map && json['data'].containsKey('result')) {
          final list = json['data']['result'] as List;
          if (list.isNotEmpty) data = list[0];
        }

        if (data != null) {
          String? path;
          // 스트리밍 포맷 우선 매핑
          if (data['hasWebm'] == true && data['webmUrl'] != null) path = data['webmUrl'];
          else if (data['hasAac'] == true && data['aacUrl'] != null) path = data['aacUrl'];
          else path = data['url'] ?? data['filePath'] ?? data['path'];

          if (path != null) {
            // 경로 캐시 테이블에 저장
            await _dbService.saveSourcePath(sourceId, path);
            return path;
          }
        }
      }
    } catch (e) {
      print('[API Error] fetchSourcePath: $e');
    }
    
    // 오프라인 상태일 경우 캐싱되어 있던 경로 반환
    return await _dbService.getSourcePath(sourceId);
  }

  /// [키오스크 안내 정보 조회]
  /// 종합 안내용 터치 키오스크에서 필요로 하는 빈소 현황 및 약도/주차 정보 등을 일괄 조회합니다.
  Future<KioskGuideResponseDto> fetchKioskRooms(String serverBaseUrl, String deviceCode) async {
    final baseUrl = serverBaseUrl.endsWith('/') ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) : serverBaseUrl;
    final url = Uri.parse('$baseUrl/api/funeral/building/deceased/kiosk/deviceCode/$deviceCode');
    print('[API Request] fetchKioskRooms: $url');
    try {
      final response = await http.get(url, headers: DeviceAuth.headers()).timeout(const Duration(seconds: 15));
      if (response.statusCode == 200) {
        final body = response.body;
        final json = jsonDecode(body);
        Map<String, dynamic>? targetMap;
        if (json.containsKey('data') && json['data'] is Map) {
          final dataMap = json['data'] as Map<String, dynamic>;
          if (dataMap.containsKey('result') && dataMap['result'] is List) {
            final list = dataMap['result'] as List;
            if (list.isNotEmpty && list[0] is Map) {
              targetMap = list[0] as Map<String, dynamic>;
            }
          }
          targetMap ??= dataMap;
        }

        if (targetMap != null) {
          // 조회 성공 시 로컬 캐시 DB에 저장
          await _dbService.saveKioskGuide(deviceCode, body);
          return KioskGuideResponseDto.fromJson(targetMap);
        }
      }
    } catch (e) {
      print('[API Error] fetchKioskRooms: $e');
    }

    // 오프라인 캐시 로드
    print('[Cache] 키오스크 안내 목록 오프라인 캐시 조회를 시도합니다.');
    final cachedBody = await _dbService.getKioskGuide(deviceCode);
    if (cachedBody != null) {
      try {
        final json = jsonDecode(cachedBody);
        Map<String, dynamic>? targetMap;
        if (json.containsKey('data') && json['data'] is Map) {
          final dataMap = json['data'] as Map<String, dynamic>;
          if (dataMap.containsKey('result') && dataMap['result'] is List) {
            final list = dataMap['result'] as List;
            if (list.isNotEmpty && list[0] is Map) {
              targetMap = list[0] as Map<String, dynamic>;
            }
          }
          targetMap ??= dataMap;
        }
        if (targetMap != null) {
          return KioskGuideResponseDto.fromJson(targetMap);
        }
      } catch (e) {
        print('[Cache Error] 키오스크 캐시 디코딩 에러: $e');
      }
    }

    // 실패 시 빈 정보 구조 반환
    return KioskGuideResponseDto(rooms: [], buildingPhotos: [], parkingPhotos: []);
  }

  /// [캐싱된 장비 정보 반환]
  /// 네트워크 요청 없이 로컬 DB에 저장되어 있는 장비 설정 정보만을 반환합니다.
  Future<DeviceDto?> getCachedDevice(String deviceCode) async {
    return await _dbService.getDevice(deviceCode);
  }
}
