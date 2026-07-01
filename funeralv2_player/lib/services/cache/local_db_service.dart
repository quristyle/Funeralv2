import 'dart:io' as io;
import 'package:flutter/foundation.dart'; 
import 'package:path/path.dart';
import 'package:sqflite/sqflite.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart'; // Windows용 추가
import 'package:sqflite_common_ffi_web/sqflite_ffi_web.dart'; 
import '../../models/device_models.dart';

class LocalDbService {
  static final LocalDbService _instance = LocalDbService._internal();
  factory LocalDbService() => _instance;
  LocalDbService._internal();

  Database? _database;

  Future<Database> get database async {
    if (_database != null) return _database!;
    _database = await _initDb();
    return _database!;
  }

  Future<Database> _initDb() async {
    try {
      if (kIsWeb) {
        databaseFactory = databaseFactoryFfiWeb;
      } else if (io.Platform.isWindows || io.Platform.isLinux) {
        // Windows/Linux용 FFI 초기화
        sqfliteFfiInit();
        databaseFactory = databaseFactoryFfi;
      }

      final dbPath = await getDatabasesPath();
      final path = join(dbPath, 'funeral_signage.db');

      return await openDatabase(
        path,
        version: 3,
        onCreate: _onCreate,
        onUpgrade: (db, oldVersion, newVersion) async {
          if (oldVersion < 2) {
            try {
              await db.execute('ALTER TABLE devices ADD COLUMN displayOrientation TEXT');
              await db.execute('ALTER TABLE devices ADD COLUMN portraitOrientation TEXT');
              await db.execute('ALTER TABLE devices ADD COLUMN videoOrientation TEXT');
            } catch (_) {}
          }
          if (oldVersion < 3) {
            try {
              await db.execute('ALTER TABLE devices ADD COLUMN displayPaddingTop REAL');
              await db.execute('ALTER TABLE devices ADD COLUMN displayPaddingLeft REAL');
              await db.execute('ALTER TABLE devices ADD COLUMN displayPaddingRight REAL');
              await db.execute('ALTER TABLE devices ADD COLUMN displayPaddingBottom REAL');
              await db.execute('ALTER TABLE devices ADD COLUMN memorialPaddingTop REAL');
              await db.execute('ALTER TABLE devices ADD COLUMN memorialPaddingLeft REAL');
              await db.execute('ALTER TABLE devices ADD COLUMN memorialPaddingRight REAL');
              await db.execute('ALTER TABLE devices ADD COLUMN memorialPaddingBottom REAL');
            } catch (_) {}
          }
        },
      );
    } catch (e) {
      print('DB 초기화 실패 (웹 환경 또는 바이너리 누락): $e');
      // DB 초기화 실패 시 더미(In-memory) DB 또는 예외를 던져 API 서비스에서 처리하도록 함
      rethrow;
    }
  }

  Future<void> _onCreate(Database db, int version) async {
    // 장비 테이블 생성
    await db.execute('''
      CREATE TABLE devices (
        id TEXT PRIMARY KEY,
        code TEXT,
        name TEXT,
        roomId TEXT,
        videoId TEXT,
        musicId TEXT,
        isVideoEnabled INTEGER,
        isMusicEnabled INTEGER,
        videoName TEXT,
        musicName TEXT,
        musicVolume REAL,
        isMemorialPhotoEnabled INTEGER,
        isDeceasedNameVisible INTEGER,
        isFamilyContactVisible INTEGER,
        displayOrientation TEXT,
        portraitOrientation TEXT,
        videoOrientation TEXT,
        displayPaddingTop REAL,
        displayPaddingLeft REAL,
        displayPaddingRight REAL,
        displayPaddingBottom REAL,
        memorialPaddingTop REAL,
        memorialPaddingLeft REAL,
        memorialPaddingRight REAL,
        memorialPaddingBottom REAL
      )
    ''');

    // 고인 테이블 생성
    await db.execute('''
      CREATE TABLE deceased (
        id TEXT PRIMARY KEY,
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
        memorialPhotoUrl TEXT,
        memorialEditedPhotoUrl TEXT
      )
    ''');
  }

  // 장비 캐시 저장
  Future<void> saveDevice(DeviceDto device) async {
    final db = await database;
    await db.insert(
      'devices',
      device.toMap(),
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  // 장비 캐시 조회
  Future<DeviceDto?> getDevice(String code) async {
    final db = await database;
    final List<Map<String, dynamic>> maps = await db.query(
      'devices',
      where: 'code = ?',
      whereArgs: [code],
    );

    if (maps.isEmpty) return null;
    return DeviceDto.fromJson(maps.first);
  }

  // 고인 캐시 저장
  Future<void> saveDeceased(DeceasedDto deceased) async {
    final db = await database;
    await db.insert(
      'deceased',
      deceased.toMap(),
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  // 고인 캐시 조회 (룸 ID 기반)
  Future<DeceasedDto?> getDeceasedByRoom(String roomId) async {
    final db = await database;
    final List<Map<String, dynamic>> maps = await db.query(
      'deceased',
      where: 'roomId = ?',
      whereArgs: [roomId],
    );

    if (maps.isEmpty) return null;
    return DeceasedDto.fromJson(maps.first);
  }
}
