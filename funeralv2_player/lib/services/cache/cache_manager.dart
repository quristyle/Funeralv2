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

    final downloadUrl = '$fileServerUrl/api/file/download/$fileId';

    // Web 환경은 URL 직접 반환
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
}
