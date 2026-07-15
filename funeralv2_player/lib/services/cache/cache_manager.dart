import 'dart:io' as io;
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:path/path.dart' as path;
import 'package:path_provider/path_provider.dart';

/// [미디어 캐시 관리 매니저]
/// 서버의 대용량 비디오, 사진, 폰트 등의 파일을 로컬 스토리지에 캐싱하여 네트워크 트래픽을 아끼고
/// 오프라인 모드에서도 재생이 중단되지 않도록 관리하는 싱글톤 도구 클래스입니다.
class CacheManager {
  // 싱글톤 인스턴스
  static final CacheManager _instance = CacheManager._internal();
  factory CacheManager() => _instance;
  CacheManager._internal();

  /// [파일 ID 기반 캐싱 및 파일 경로 획득]
  /// 서버 주소([fileServerUrl])와 파일 식별 아이디([fileId])를 전달받아
  /// 로컬에 이미 다운로드된 파일이 있는지 확인하고, 없으면 서버에서 다운로드한 후 로컬 경로를 반환합니다.
  /// Web 환경([kIsWeb])의 경우, 로컬 파일 작성이 불가하므로 다운로드 URL 자체를 리턴합니다.
  Future<String?> getCachedFile(String fileServerUrl, String? fileId, {String ext = ''}) async {
    if (fileId == null || fileId.isEmpty) return null;

    final baseUrl = fileServerUrl.endsWith('/') 
        ? fileServerUrl.substring(0, fileServerUrl.length - 1) 
        : fileServerUrl;
    
    // 파일 다운로드용 표준 엔드포인트 URL
    final downloadUrl = '$baseUrl/api/file/download/id/$fileId';
    print('[CacheManager] 최종 다운로드 주소: $downloadUrl');

    if (kIsWeb) {
      return downloadUrl;
    }

    try {
      final docDir = await getApplicationDocumentsDirectory();
      // 앱 문서 폴더 아래에 'media_cache' 폴더 생성
      final cacheDir = io.Directory(path.join(docDir.path, 'media_cache'));
      
      if (!await cacheDir.exists()) {
        await cacheDir.create(recursive: true);
      }

      // 확장자 지정 여부에 따른 파일명 구성
      final fileName = ext.isEmpty ? fileId : '$fileId$ext';
      final localFilePath = path.join(cacheDir.path, fileName);
      final localFile = io.File(localFilePath);

      // 이미 존재하고 비어있지 않은 파일이 있다면 네트워크 다운로드 없이 기존 로컬 파일 사용
      if (await localFile.exists() && await localFile.length() > 0) {
        return localFilePath;
      }

      // 서버에서 30초 타임아웃을 두고 다운로드 실행
      final response = await http.get(Uri.parse(downloadUrl)).timeout(const Duration(seconds: 30));

      if (response.statusCode == 200) {
        // 로컬 바이트 저장
        await localFile.writeAsBytes(response.bodyBytes);
        return localFilePath;
      }
    } catch (e) {
      print('캐싱 에러: $e');
    }

    return null;
  }

  /// [상대 경로 기반 캐싱 및 파일 경로 획득]
  /// 파일의 고유 ID가 아닌 서버 상의 리얼 상대 경로([relativePath])를 주입하여
  /// 로컬에 캐싱하거나 Web 환경에 대응되는 전체 주소를 획득합니다.
  Future<String?> getCachedFileByPath(String serverBaseUrl, String? relativePath) async {
    if (relativePath == null || relativePath.isEmpty) return null;

    final baseUrl = serverBaseUrl.endsWith('/') 
        ? serverBaseUrl.substring(0, serverBaseUrl.length - 1) 
        : serverBaseUrl;
    
    // 상대 경로 구분 슬래시 보정
    final fixedPath = relativePath.startsWith('/') ? relativePath : '/$relativePath';
    final downloadUrl = '$baseUrl$fixedPath';
    
    print('[CacheManager] 경로 기반 다운로드 주소: $downloadUrl');

    if (kIsWeb) return downloadUrl;

    try {
      final docDir = await getApplicationDocumentsDirectory();
      final cacheDir = io.Directory(path.join(docDir.path, 'media_cache'));
      if (!await cacheDir.exists()) await cacheDir.create(recursive: true);

      // 전체 파일경로의 맨 마지막 단어(파일명)를 캐시 키로 획득
      final fileName = relativePath.split('/').last;
      final localFilePath = path.join(cacheDir.path, fileName);
      final localFile = io.File(localFilePath);

      // 로컬 파일 검사
      if (await localFile.exists() && await localFile.length() > 0) {
        return localFilePath;
      }

      // 서버 다운로드 진행 (타임아웃을 30초에서 4초로 단축하여 지연 최소화)
      final response = await http.get(Uri.parse(downloadUrl)).timeout(const Duration(seconds: 4));
      if (response.statusCode == 200) {
        await localFile.writeAsBytes(response.bodyBytes);
        return localFilePath;
      }
    } catch (e) {
      print('[CacheManager] 경로 캐싱 에러: $e');
    }
    return null;
  }

  /// [네트워크 요청이 없는 순수 로컬 파일 즉시 검사]
  /// 서버 통신 없이 오직 로컬 디렉터리에 기보관된 캐시 파일이 존재하는지만 판단하여 경로를 리턴합니다. (지연 시간 0초 보장)
  Future<String?> getLocalFile(String? relativePath) async {
    if (relativePath == null || relativePath.isEmpty) return null;
    try {
      final docDir = await getApplicationDocumentsDirectory();
      final cacheDir = io.Directory(path.join(docDir.path, 'media_cache'));
      final fileName = relativePath.split('/').last;
      final localFilePath = path.join(cacheDir.path, fileName);
      final localFile = io.File(localFilePath);

      if (await localFile.exists() && await localFile.length() > 0) {
        return localFilePath;
      }
    } catch (_) {}
    return null;
  }
}
