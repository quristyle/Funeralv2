import 'dart:io' as io;
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:path/path.dart' as path;
import 'package:path_provider/path_provider.dart';

class CacheManager {
  static final CacheManager _instance = CacheManager._internal();
  factory CacheManager() => _instance;
  CacheManager._internal();

  Future<String?> getCachedFile(String fileServerUrl, String? fileId, {String ext = ''}) async {
    if (fileId == null || fileId.isEmpty) return null;

    // 주소 끝의 슬래시 처리
    final baseUrl = fileServerUrl.endsWith('/') 
        ? fileServerUrl.substring(0, fileServerUrl.length - 1) 
        : fileServerUrl;
    
    final downloadUrl = '$baseUrl/api/file/download/id/$fileId';
    print('[CacheManager] 최종 다운로드 주소: $downloadUrl');

    // Web 환경은 로컬 파일 시스템이 없으므로 URL 직접 반환
    if (kIsWeb) {
      return downloadUrl;
    }

    try {
      final docDir = await getApplicationDocumentsDirectory();
      final cacheDir = io.Directory(path.join(docDir.path, 'media_cache'));
      
      // 네이티브 환경에서만 io 관련 코드 실행
      if (!await cacheDir.exists()) {
        await cacheDir.create(recursive: true);
      }

      final fileName = ext.isEmpty ? fileId : '$fileId$ext';
      final localFilePath = path.join(cacheDir.path, fileName);
      final localFile = io.File(localFilePath);

      if (await localFile.exists() && await localFile.length() > 0) {
        return localFilePath;
      }

      final response = await http.get(Uri.parse(downloadUrl)).timeout(const Duration(seconds: 30));

      if (response.statusCode == 200) {
        await localFile.writeAsBytes(response.bodyBytes);
        return localFilePath;
      }
    } catch (e) {
      print('캐싱 에러: $e');
    }

    return null;
  }

  /// 파일의 상대 경로(예: /api/file/download/id/...)를 받아 로컬에 캐싱하거나 URL을 반환합니다.
  Future<String?> getCachedFileByPath(String serverBaseUrl, String? relativePath) async {
    if (relativePath == null || relativePath.isEmpty) return null;

    final baseUrl = serverBaseUrl.endsWith('/') 
        ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) 
        : serverBaseUrl;
    
    // 상대 경로가 /로 시작하지 않으면 추가
    final fixedPath = relativePath.startsWith('/') ? relativePath : '/$relativePath';
    final downloadUrl = '$baseUrl$fixedPath';
    
    print('[CacheManager] 경로 기반 다운로드 주소: $downloadUrl');

    if (kIsWeb) return downloadUrl;

    try {
      final docDir = await getApplicationDocumentsDirectory();
      final cacheDir = io.Directory(path.join(docDir.path, 'media_cache'));
      if (!await cacheDir.exists()) await cacheDir.create(recursive: true);

      // 경로에서 파일명 추출 (캐시 키로 사용)
      final fileName = relativePath.split('/').last;
      final localFilePath = path.join(cacheDir.path, fileName);
      final localFile = io.File(localFilePath);

      if (await localFile.exists() && await localFile.length() > 0) {
        return localFilePath;
      }

      final response = await http.get(Uri.parse(downloadUrl)).timeout(const Duration(seconds: 30));
      if (response.statusCode == 200) {
        await localFile.writeAsBytes(response.bodyBytes);
        return localFilePath;
      }
    } catch (e) {
      print('[CacheManager] 경로 캐싱 에러: $e');
    }
    return null;
  }
}
