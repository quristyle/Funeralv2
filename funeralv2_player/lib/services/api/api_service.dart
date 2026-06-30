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
        
        // 로컬 DB 캐싱 갱신
        await _dbService.saveDevice(device);
        return device;
      }
    } catch (e) {
      // 네트워크 예외 발생 시 로컬 캐시 조회
      print('API 호출 실패. 로컬 캐시로 Fallback 시도: $e');
    }
    
    // 오프라인 Fallback
    return await _dbService.getDevice(deviceCode);
  }

  // 호실 소속 고인 정보 패치 (실패 시 로컬 DB Fallback)
  Future<DeceasedDto?> fetchDeceased(String apiServerUrl, String roomId) async {
    final url = Uri.parse('$apiServerUrl/building/deceased/list?roomId=$roomId');
    try {
      final response = await http.get(url).timeout(const Duration(seconds: 4));
      if (response.statusCode == 200) {
        final json = jsonDecode(response.body);
        // 고인 DTO 생성
        if (json['result'] != null && json['result'] is List && (json['result'] as List).isNotEmpty) {
          final deceased = DeceasedDto.fromJson(json);
          // 로컬 DB 캐싱 갱신
          await _dbService.saveDeceased(deceased);
          return deceased;
        }
      }
    } catch (e) {
      print('API 고인 조회 실패. 로컬 캐시로 Fallback 시도: $e');
    }

    // 오프라인 Fallback
    return await _dbService.getDeceasedByRoom(roomId);
  }
}
