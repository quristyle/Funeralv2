import 'dart:convert';
import 'package:http/http.dart' as http;
import '../../models/device_models.dart';
import '../cache/local_db_service.dart';

class ApiService {
  final LocalDbService _dbService = LocalDbService();

  // 장비 정보 패치 (실패 시 로컬 DB Fallback)
  Future<DeviceDto?> fetchDevice(String apiServerUrl, String deviceCode) async {
    final url = Uri.parse('$apiServerUrl/building/device/code/$deviceCode');
    try {
      final response = await http.get(url).timeout(const Duration(seconds: 4));
      if (response.statusCode == 200) {
        final json = jsonDecode(response.body);
        final device = DeviceDto.fromJson(json);
        
        // 로컬 DB 캐싱 갱신 (실패해도 앱 실행은 계속됨)
        try {
          await _dbService.saveDevice(device);
        } catch (e) {
          print('캐시 저장 실패 (무시): $e');
        }
        return device;
      }
    } catch (e) {
      print('API 호출 실패: $e');
    }
    
    // 오프라인 Fallback (DB 에러 발생 시 null 반환)
    try {
      return await _dbService.getDevice(deviceCode);
    } catch (e) {
      return null;
    }
  }

  // 호실 소속 고인 정보 패치 (실패 시 로컬 DB Fallback)
  Future<DeceasedDto?> fetchDeceased(String apiServerUrl, String roomId) async {
    final url = Uri.parse('$apiServerUrl/building/deceased/list?roomId=$roomId');
    try {
      final response = await http.get(url).timeout(const Duration(seconds: 4));
      if (response.statusCode == 200) {
        final json = jsonDecode(response.body);
        
        // 데이터 존재 여부 확인 (data.result 또는 result)
        bool hasData = false;
        if (json.containsKey('data') && json['data'] is Map && json['data'].containsKey('result')) {
          hasData = (json['data']['result'] as List).isNotEmpty;
        } else if (json.containsKey('result') && json['result'] is List) {
          hasData = (json['result'] as List).isNotEmpty;
        }

        if (hasData) {
          final deceased = DeceasedDto.fromJson(json);
          // 로컬 DB 캐싱 갱신 (실패해도 진행)
          try {
            await _dbService.saveDeceased(deceased);
          } catch (e) {
            print('고인 캐시 저장 실패 (무시): $e');
          }
          return deceased;
        }
      }
    } catch (e) {
      print('API 고인 조회 실패: $e');
    }

    // 오프라인 Fallback
    try {
      return await _dbService.getDeceasedByRoom(roomId);
    } catch (e) {
      return null;
    }
  }
}
