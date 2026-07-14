import 'dart:io' as io;
import 'package:flutter/foundation.dart'; 
import 'package:path/path.dart';
import 'package:sqflite/sqflite.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:sqflite_common_ffi_web/sqflite_ffi_web.dart'; 
import '../../models/device_models.dart';

/// [로컬 캐시 데이터베이스 서비스]
/// 오프라인 상황에서도 장례식장 사이니지가 정상 작동할 수 있도록 장비 설정, 고인 정보, 입구 안내 판 정보, 
/// 미디어 파일 경로 등을 로컬 SQLite 파일에 캐싱하고 조회하는 싱글톤 서비스입니다.
class LocalDbService {
  // 싱글톤 인스턴스 생성 및 리턴용 팩토리 생성자
  static final LocalDbService _instance = LocalDbService._internal();
  factory LocalDbService() => _instance;
  LocalDbService._internal();

  Database? _database;
  bool _isWebError = false; // Web 환경에서의 초기화 에러 여부

  /// [Database 인스턴스 Getter]
  /// 비동기 방식으로 데이터베이스 커넥션을 안전하게 열거나, 이미 열려 있는 세션을 재사용합니다.
  Future<Database?> get database async {
    if (kIsWeb && _isWebError) return null;
    if (_database != null) return _database!;
    try {
      _database = await _initDb();
      return _database;
    } catch (e) {
      if (kIsWeb) _isWebError = true;
      return null;
    }
  }

  /// [데이터베이스 초기화 및 플랫폼 설정]
  /// 실행 중인 기기의 운영체제(Web, Windows, Linux 등)에 알맞은 SQLite FFI 팩토리를 설정하고
  /// 'funeral_signage_v2.db' 파일을 불러옵니다.
  Future<Database> _initDb() async {
    if (kIsWeb) {
      try { databaseFactory = databaseFactoryFfiWeb; } catch (e) { _isWebError = true; rethrow; }
    } else if (io.Platform.isWindows || io.Platform.isLinux) {
      // 데스크톱 환경에서는 FFI를 사용해 SQLite 엔진을 기동합니다.
      sqfliteFfiInit();
      databaseFactory = databaseFactoryFfi;
    }

    final dbPath = await getDatabasesPath();
    final fullPath = join(dbPath, 'funeral_signage_v2.db');

    return await openDatabase(
      fullPath,
      version: 14, // 데이터베이스 버전 관리 (최신 마이그레이션 반영)
      onCreate: _onCreate,
      onUpgrade: (db, oldVersion, newVersion) async {
        // 기존 테이블에 순차적으로 신규 스키마 컬럼을 주입하는 마이그레이션 과정
        if (oldVersion < 9) {
          try { await db.execute('ALTER TABLE deceased ADD COLUMN deviceCode TEXT'); } catch (_) {}
        }
        if (oldVersion < 10) {
          try {
            await db.execute('CREATE TABLE IF NOT EXISTS media_sources (id TEXT PRIMARY KEY, path TEXT)');
            await db.execute('CREATE TABLE IF NOT EXISTS entrance_guide (deviceCode TEXT PRIMARY KEY, jsonData TEXT)');
          } catch (_) {}
        }
        if (oldVersion < 11) {
          try {
            // 누락된 familyPhotos 컬럼 추가
            await db.execute('ALTER TABLE deceased ADD COLUMN familyPhotos TEXT');
          } catch (_) {}
        }
        if (oldVersion < 12) {
          try {
            // 배경 이미지 레이어 제어를 위한 컬럼 추가
            await db.execute('ALTER TABLE devices ADD COLUMN isBackgroundImageEnabled INTEGER DEFAULT 0');
            await db.execute('ALTER TABLE devices ADD COLUMN backgroundImageId TEXT');
            await db.execute('ALTER TABLE devices ADD COLUMN backgroundImageName TEXT');
            await db.execute('ALTER TABLE devices ADD COLUMN backgroundImageUrl TEXT');
          } catch (_) {}
        }
        if (oldVersion < 13) {
          try {
            // 배경 이미지 방향성 설정 추가
            await db.execute('ALTER TABLE devices ADD COLUMN backgroundOrientation TEXT DEFAULT "HORIZONTAL"');
          } catch (_) {}
        }
        if (oldVersion < 14) {
          try {
            // 영정사진 비율 유지 여부 설정 추가
            await db.execute('ALTER TABLE devices ADD COLUMN isMemorialPhotoKeepAspectRatio INTEGER DEFAULT 1');
          } catch (_) {}
        }
      },
    );
  }

  /// [테이블 최초 생성 로직]
  /// 데이터베이스가 신규 생성될 때 `devices`, `deceased`, `media_sources`, `entrance_guide` 테이블을 정의합니다.
  Future<void> _onCreate(Database db, int version) async {
    // 장비 상세 정보를 저장할 테이블 생성
    await db.execute('''
      CREATE TABLE devices (
        id TEXT PRIMARY KEY, code TEXT, name TEXT, roomId TEXT, roomName TEXT,
        floorId TEXT, floorName TEXT, buildingId TEXT, buildingName TEXT,
        videoId TEXT, musicId TEXT, isVideoEnabled INTEGER, isMusicEnabled INTEGER,
        isMuted INTEGER, videoName TEXT, musicName TEXT, musicVolume REAL,
        isMemorialPhotoEnabled INTEGER, isDeceasedNameVisible INTEGER,
        isFamilyContactVisible INTEGER, isMemorialPhotoKeepAspectRatio INTEGER, displayOrientation TEXT,
        portraitOrientation TEXT, videoOrientation TEXT, displayPaddingTop REAL,
        displayPaddingLeft REAL, displayPaddingRight REAL, displayPaddingBottom REAL,
        memorialPaddingTop REAL, memorialPaddingLeft REAL, memorialPaddingRight REAL,
        memorialPaddingBottom REAL, photoVerticalAlignment TEXT,
        photoHorizontalAlignment TEXT, deviceType TEXT, memorialPhotoEffect TEXT,
        contentIntervalSec INTEGER,
        isBackgroundImageEnabled INTEGER,
        backgroundImageId TEXT,
        backgroundImageName TEXT,
        backgroundImageUrl TEXT,
        backgroundOrientation TEXT
      )
    ''');

    // 고인 정보 및 화면 오버레이(리본, 텍스트)를 저장할 테이블 생성
    await db.execute('''
      CREATE TABLE deceased (
        id TEXT PRIMARY KEY,
        deviceCode TEXT,
        name TEXT,
        gender TEXT,
        age INTEGER,
        religion TEXT,
        deathDate TEXT,
        funeralDate TEXT,
        burialDate TEXT,
        roomId TEXT,
        roomName TEXT,
        chiefMourner TEXT,
        mourners TEXT,
        familyPhotos TEXT,
        memorialPhotoUrl TEXT,
        memorialPhotoFileId TEXT,
        memorialEditedPhotoUrl TEXT,
        memorialEditedPhotoFileId TEXT,
        deviceRibbons TEXT,
        deviceTextOverlays TEXT
      )
    ''');

    // 미디어 리소스의 로컬 임시/서버 경로 매핑 테이블
    await db.execute('CREATE TABLE media_sources (id TEXT PRIMARY KEY, path TEXT)');
    // 입구 안내 전체 목록 응답 문자열 캐싱 테이블
    await db.execute('CREATE TABLE entrance_guide (deviceCode TEXT PRIMARY KEY, jsonData TEXT)');
  }

  /// [장비 정보 캐시 저장]
  Future<void> saveDevice(DeviceDto deviceDto) async {
    final db = await database;
    if (db != null) await db.insert('devices', deviceDto.toMap(), conflictAlgorithm: ConflictAlgorithm.replace);
  }

  /// [장비 정보 캐시 조회]
  Future<DeviceDto?> getDevice(String code) async {
    final db = await database;
    if (db == null) return null;
    final List<Map<String, dynamic>> maps = await db.query('devices', where: 'code = ?', whereArgs: [code]);
    return maps.isEmpty ? null : DeviceDto.fromJson(maps.first);
  }

  /// [고인 정보 캐시 저장]
  Future<void> saveDeceased(DeceasedDto deceased, String deviceCode) async {
    final db = await database;
    if (db == null) return;
    try {
      final map = deceased.toMap();
      map['deviceCode'] = deviceCode; // 이 장비에 대응되는 고인 데이터로 바인딩
      await db.insert('deceased', map, conflictAlgorithm: ConflictAlgorithm.replace);
    } catch (e) {
      print('[DB Cache] saveDeceased 에러: $e');
    }
  }

  /// [장비 코드로 고인 정보 캐시 조회]
  Future<DeceasedDto?> getDeceasedByDeviceCode(String deviceCode) async {
    final db = await database;
    if (db == null) return null;
    try {
      final List<Map<String, dynamic>> maps = await db.query('deceased', where: 'deviceCode = ?', whereArgs: [deviceCode]);
      if (maps.isEmpty) return null;
      return DeceasedDto.fromJson(maps.first);
    } catch (e) {
      print('[DB Cache] getDeceasedByDeviceCode 에러: $e');
      return null;
    }
  }

  /// [미디어 경로 캐시 저장]
  Future<void> saveSourcePath(String sourceId, String path) async {
    final db = await database;
    if (db != null) await db.insert('media_sources', {'id': sourceId, 'path': path}, conflictAlgorithm: ConflictAlgorithm.replace);
  }

  /// [미디어 경로 캐시 조회]
  Future<String?> getSourcePath(String sourceId) async {
    final db = await database;
    if (db == null) return null;
    final List<Map<String, dynamic>> maps = await db.query('media_sources', where: 'id = ?', whereArgs: [sourceId]);
    return maps.isEmpty ? null : maps.first['path'] as String;
  }

  /// [입구 안내 JSON 응답 캐시 저장]
  Future<void> saveEntranceGuide(String deviceCode, String json) async {
    final db = await database;
    if (db != null) await db.insert('entrance_guide', {'deviceCode': deviceCode, 'jsonData': json}, conflictAlgorithm: ConflictAlgorithm.replace);
  }

  /// [입구 안내 JSON 응답 캐시 조회]
  Future<String?> getEntranceGuide(String deviceCode) async {
    final db = await database;
    if (db == null) return null;
    final List<Map<String, dynamic>> maps = await db.query('entrance_guide', where: 'deviceCode = ?', whereArgs: [deviceCode]);
    return maps.isEmpty ? null : maps.first['jsonData'] as String;
  }
}
