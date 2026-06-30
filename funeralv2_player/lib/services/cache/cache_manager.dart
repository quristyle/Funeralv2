import 'dart:io';
import 'package:http/http.dart' as http;
import 'package:path/path.dart' as path;
import 'package:path_provider/path_provider.dart';

class CacheManager {
  static final CacheManager _instance = CacheManager._internal();
  factory CacheManager() => _instance;
  CacheManager._internal();

  /// 지정한 미디어 ID(비디오/오디오/이미지)를 로컬 디스크에 다운로드받고 그 로컬 절대경로를 반환합니다.
  /// 파일 서버 URL 예시: http://{fileServerUrl}/api/file/download/{fileId}
  Future<String?> getCachedFile(String fileServerUrl, String? fileId, {String ext = ''}) async {
    if (fileId == null || fileId.isEmpty) return null;

    try {
      final docDir = await getApplicationDocumentsDirectory();
      final cacheDir = Directory(path.join(docDir.path, 'media_cache'));
      if (!await cacheDir.exists()) {
        await cacheDir.create(recursive: true);
      }

      // 로컬 파일명 결정
      final fileName = ext.isEmpty ? fileId : '$fileId$ext';
      final localFilePath = path.join(cacheDir.path, fileName);
      final localFile = File(localFilePath);

      // 이미 파일이 디바이스 로컬 저장소에 존재한다면 다운로드 생략하고 로컬 경로 반환
      if (await localFile.exists() && await localFile.length() > 0) {
        print('로컬 캐시 파일 발견: $localFilePath');
        return localFilePath;
      }

      // 인터넷이 연결되어 있을 때 다운로드 시도
      final downloadUrl = '$fileServerUrl/api/file/download/$fileId';
      print('미디어 다운로드 시작: $downloadUrl');
      final response = await http.get(Uri.parse(downloadUrl)).timeout(const Duration(seconds: 30));

      if (response.statusCode == 200) {
        await localFile.writeAsBytes(response.bodyBytes);
        print('미디어 파일 다운로드 및 캐시 저장 성공: $localFilePath');
        return localFilePath;
      } else {
        print('다운로드 실패 (HTTP ${response.statusCode})');
      }
    } catch (e) {
      print('미디어 파일 다운로드 캐싱 중 에러 (오프라인 상태일 수 있음): $e');
    }

    // 오프라인 상태거나 에러 시, 이전에 다운로드받았던 로컬 파일이 존재한다면 최선책으로 리턴
    final docDir = await getApplicationDocumentsDirectory();
    final localFilePath = path.join(docDir.path, 'media_cache', ext.isEmpty ? fileId : '$fileId$ext');
    final localFile = File(localFilePath);
    if (await localFile.exists()) {
      print('네트워크 오류로 기존 로컬 파일 리턴: $localFilePath');
      return localFilePath;
    }

    return null;
  }
}
