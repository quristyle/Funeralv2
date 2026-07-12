import 'dart:io' as io;
import 'package:flutter/foundation.dart'; 
import 'package:path/path.dart';
import 'package:sqflite/sqflite.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:sqflite_common_ffi_web/sqflite_ffi_web.dart'; 
import '../../models/device_models.dart';

class LocalDbService {
  static final LocalDbService _instance = LocalDbService._internal();
  factory LocalDbService() => _instance;
  LocalDbService._internal();

  Database? _database;
  bool _isWebError = false;

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

  Future<Database> _initDb() async {
    if (kIsWeb) {
      try { databaseFactory = databaseFactoryFfiWeb; } catch (e) { _isWebError = true; rethrow; }
    } else if (io.Platform.isWindows || io.Platform.isLinux) {
      sqfliteFfiInit();
      databaseFactory = databaseFactoryFfi;
    }

    final dbPath = await getDatabasesPath();
    final fullPath = join(dbPath, 'funeral_signage_v2.db');

    return await openDatabase(
      fullPath,
      version: 10, // 버전 10으로 상향
      onCreate: _onCreate,
      onUpgrade: (db, oldVersion, newVersion) async {
        // 기존 업그레이드 로직 유지 및 10 버전 추가
        if (oldVersion < 10) {
          try {
            // 미디어 소스 경로 캐시 테이블
            await db.execute('CREATE TABLE IF NOT EXISTS media_sources (id TEXT PRIMARY KEY, path TEXT)');
            // 입구 안내 호실 목록 캐시 테이블
            await db.execute('CREATE TABLE IF NOT EXISTS entrance_guide (deviceCode TEXT PRIMARY KEY, jsonData TEXT)');
          } catch (_) {}
        }
      },
    );
  }

  Future<void> _onCreate(Database db, int version) async {
    await db.execute('''
      CREATE TABLE devices (
        id TEXT PRIMARY KEY, code TEXT, name TEXT, roomId TEXT, roomName TEXT,
        floorId TEXT, floorName TEXT, buildingId TEXT, buildingName TEXT,
        videoId TEXT, musicId TEXT, isVideoEnabled INTEGER, isMusicEnabled INTEGER,
        isMuted INTEGER, videoName TEXT, musicName TEXT, musicVolume REAL,
        isMemorialPhotoEnabled INTEGER, isDeceasedNameVisible INTEGER,
        isFamilyContactVisible INTEGER, displayOrientation TEXT,
        portraitOrientation TEXT, videoOrientation TEXT, displayPaddingTop REAL,
        displayPaddingLeft REAL, displayPaddingRight REAL, displayPaddingBottom REAL,
        memorialPaddingTop REAL, memorialPaddingLeft REAL, memorialPaddingRight REAL,
        memorialPaddingBottom REAL, photoVerticalAlignment TEXT,
        photoHorizontalAlignment TEXT, deviceType TEXT, memorialPhotoEffect TEXT,
        contentIntervalSec INTEGER
      )
    ''');

    await db.execute('''
      CREATE TABLE deceased (
        id TEXT PRIMARY KEY, deviceCode TEXT, name TEXT, gender TEXT, age INTEGER,
        religion TEXT, deathDate TEXT, funeralDate TEXT, burialDate TEXT,
        roomId TEXT, roomName TEXT, chiefMourner TEXT, mourners TEXT,
        memorialPhotoUrl TEXT, memorialPhotoFileId TEXT, memorialEditedPhotoUrl TEXT,
        memorialEditedPhotoFileId TEXT, deviceRibbons TEXT, deviceTextOverlays TEXT
      )
    ''');

    await db.execute('CREATE TABLE media_sources (id TEXT PRIMARY KEY, path TEXT)');
    await db.execute('CREATE TABLE entrance_guide (deviceCode TEXT PRIMARY KEY, jsonData TEXT)');
  }

  // --- 장비 정보 ---
  Future<void> saveDevice(DeviceDto deviceDto) async {
    final db = await database;
    if (db != null) await db.insert('devices', deviceDto.toMap(), conflictAlgorithm: ConflictAlgorithm.replace);
  }
  Future<DeviceDto?> getDevice(String code) async {
    final db = await database;
    if (db == null) return null;
    final List<Map<String, dynamic>> maps = await db.query('devices', where: 'code = ?', whereArgs: [code]);
    return maps.isEmpty ? null : DeviceDto.fromJson(maps.first);
  }

  // --- 고인 정보 ---
  Future<void> saveDeceased(DeceasedDto deceased, String deviceCode) async {
    final db = await database;
    if (db == null) return;
    final map = deceased.toMap();
    map['deviceCode'] = deviceCode;
    await db.insert('deceased', map, conflictAlgorithm: ConflictAlgorithm.replace);
  }
  Future<DeceasedDto?> getDeceasedByDeviceCode(String deviceCode) async {
    final db = await database;
    if (db == null) return null;
    final List<Map<String, dynamic>> maps = await db.query('deceased', where: 'deviceCode = ?', whereArgs: [deviceCode]);
    return maps.isEmpty ? null : DeceasedDto.fromJson(maps.first);
  }

  // --- 미디어 소스 경로 ---
  Future<void> saveSourcePath(String sourceId, String path) async {
    final db = await database;
    if (db != null) await db.insert('media_sources', {'id': sourceId, 'path': path}, conflictAlgorithm: ConflictAlgorithm.replace);
  }
  Future<String?> getSourcePath(String sourceId) async {
    final db = await database;
    if (db == null) return null;
    final List<Map<String, dynamic>> maps = await db.query('media_sources', where: 'id = ?', whereArgs: [sourceId]);
    return maps.isEmpty ? null : maps.first['path'] as String;
  }

  // --- 입구 안내 호실 목록 ---
  Future<void> saveEntranceGuide(String deviceCode, String json) async {
    final db = await database;
    if (db != null) await db.insert('entrance_guide', {'deviceCode': deviceCode, 'jsonData': json}, conflictAlgorithm: ConflictAlgorithm.replace);
  }
  Future<String?> getEntranceGuide(String deviceCode) async {
    final db = await database;
    if (db == null) return null;
    final List<Map<String, dynamic>> maps = await db.query('entrance_guide', where: 'deviceCode = ?', whereArgs: [deviceCode]);
    return maps.isEmpty ? null : maps.first['jsonData'] as String;
  }
}
