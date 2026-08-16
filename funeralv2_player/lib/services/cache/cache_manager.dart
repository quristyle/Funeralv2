import 'dart:async';
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

  // ---------------------------------------------------------------------------
  // 캐시 정리 정책
  //
  // 사이니지는 24시간 켜진 채로 장례 행사가 계속 바뀌므로, 새 영정 사진과 영상이
  // 무한정 쌓인다. 정리 로직이 없으면 라즈베리파이의 저장장치 용량이 고갈되고
  // 플래시 쓰기 수명도 깎인다. 마지막 사용 시각 기준 TTL + 총 용량 상한으로 관리한다.
  // ---------------------------------------------------------------------------

  /// 이 기간 동안 한 번도 쓰이지 않은 캐시 파일은 삭제한다.
  static const Duration cacheTtl = Duration(days: 30);

  /// 캐시 폴더 총 용량 상한. 초과하면 오래 안 쓴 파일부터 지운다.
  static const int maxCacheBytes = 2 * 1024 * 1024 * 1024; // 2GB

  /// 정리 작업 주기
  static const Duration gcInterval = Duration(hours: 6);

  bool _gcRunning = false;

  /// [캐시 디렉터리 확보]
  Future<io.Directory> _cacheDir() async {
    final docDir = await getApplicationDocumentsDirectory();
    final dir = io.Directory(path.join(docDir.path, 'media_cache'));
    if (!await dir.exists()) await dir.create(recursive: true);
    return dir;
  }

  /// [캐시 적중 표시]
  /// 파일을 실제로 사용할 때 수정 시각을 현재로 갱신해 LRU 판단 근거로 삼는다.
  /// (일부 파일시스템은 atime 을 갱신하지 않으므로 mtime 을 직접 쓴다)
  Future<void> _touch(io.File file) async {
    try {
      await file.setLastModified(DateTime.now());
    } catch (_) {
      // 갱신 실패는 치명적이지 않다. 최악의 경우 조기 삭제 후 재다운로드된다.
    }
  }

  /// [캐시 정리 실행]
  /// 1) TTL 을 넘긴 파일 삭제
  /// 2) 그래도 용량 상한을 넘으면 오래 안 쓴 파일부터 삭제
  ///
  /// 삭제 대상은 캐시 폴더 안의 파일뿐이며, 재생 중인 파일이 지워지더라도
  /// 다음 조회 시 자동으로 다시 내려받으므로 데이터 유실은 없다.
  Future<void> runGarbageCollection() async {
    if (kIsWeb || _gcRunning) return;
    _gcRunning = true;

    try {
      final dir = await _cacheDir();
      final entries = <({io.File file, int size, DateTime used})>[];

      await for (final entity in dir.list(followLinks: false)) {
        if (entity is! io.File) continue;
        try {
          final stat = await entity.stat();
          entries.add((file: entity, size: stat.size, used: stat.modified));
        } catch (_) {
          // 통계 조회 실패한 항목은 건너뛴다.
        }
      }

      final now = DateTime.now();
      int removed = 0;
      int freed = 0;

      // 1) TTL 초과분 제거
      final survivors = <({io.File file, int size, DateTime used})>[];
      for (final e in entries) {
        if (now.difference(e.used) > cacheTtl) {
          try {
            await e.file.delete();
            removed++;
            freed += e.size;
            continue;
          } catch (_) {}
        }
        survivors.add(e);
      }

      // 2) 용량 상한 초과분 제거 (오래 안 쓴 것부터)
      var total = survivors.fold<int>(0, (sum, e) => sum + e.size);
      if (total > maxCacheBytes) {
        survivors.sort((a, b) => a.used.compareTo(b.used));
        for (final e in survivors) {
          if (total <= maxCacheBytes) break;
          try {
            await e.file.delete();
            removed++;
            freed += e.size;
            total -= e.size;
          } catch (_) {}
        }
      }

      if (removed > 0) {
        final mb = (freed / 1024 / 1024).toStringAsFixed(1);
        print('[CacheManager] 캐시 정리: $removed개 삭제, ${mb}MB 확보 '
              '(잔여 ${(total / 1024 / 1024).toStringAsFixed(1)}MB)');
      }
    } catch (e) {
      print('[CacheManager] 캐시 정리 실패: $e');
    } finally {
      _gcRunning = false;
    }
  }

  /// [주기적 캐시 정리 기동]
  /// 앱 시작 시 한 번 실행하고, 이후 [gcInterval] 마다 반복한다.
  void startPeriodicGarbageCollection() {
    if (kIsWeb) return;
    runGarbageCollection();
    Timer.periodic(gcInterval, (_) => runGarbageCollection());
  }

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
        await _touch(localFile); // LRU 판단용 최근 사용 시각 갱신
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
        await _touch(localFile); // LRU 판단용 최근 사용 시각 갱신
        return localFilePath;
      }

      // 서버 다운로드 진행.
      // 영상은 수십 MB 에 달해 4초로는 받다 말고 실패한다(빈 캐시 → 매번 재시도).
      // 첫 표출이 늦어지더라도 캐시에 실제로 남는 편이 사이니지에는 유리하다.
      final response = await http.get(Uri.parse(downloadUrl)).timeout(const Duration(seconds: 60));
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
        await _touch(localFile); // LRU 판단용 최근 사용 시각 갱신
        return localFilePath;
      }
    } catch (_) {}
    return null;
  }
}
