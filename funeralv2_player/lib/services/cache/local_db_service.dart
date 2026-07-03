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
  bool _isWebError = false; // 웹 워커 에러 발생 여부 체크

  Future<Database?> get database async {
    if (kIsWeb && _isWebError) return null; // 웹 에러 상태면 바로 null 반환
    if (_database != null) return _database!;
    
    try {
      _database = await _initDb();
      return _database;
    } catch (e) {
      print('[DB] Database access failed: $e');
      if (kIsWeb) _isWebError = true;
      return null;
    }
  }

  Future<Database> _initDb() async {
    if (kIsWeb) {
      try {
        // 웹 워커 설정을 시도하지만 실패해도 치명적 에러가 되지 않도록 함
        databaseFactory = databaseFactoryFfiWeb;
      } catch (e) {
        _isWebError = true;
        print('[DB] Web SQL initialization failed, running without cache.');
        rethrow;
      }
    } else if (io.Platform.isWindows || io.Platform.isLinux) {
      sqfliteFfiInit();
      databaseFactory = databaseFactoryFfi;
    }

    final dbPath = await getDatabasesPath();
    final path = join(dbPath, 'funeral_signage.db');

    return await openDatabase(
      path,
      version: 6,
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
        if (oldVersion < 4) {
          try {
            await db.execute('ALTER TABLE devices ADD COLUMN photoVerticalAlignment TEXT');
          } catch (_) {}
        }
        if (oldVersion < 5) {
          try {
            await db.execute('ALTER TABLE devices ADD COLUMN photoHorizontalAlignment TEXT');
          } catch (_) {}
        }
        if (oldVersion < 6) {
          try {
            await db.execute('ALTER TABLE deceased ADD COLUMN mourners TEXT');
          } catch (_) {}
        }
      },
    );
  }

  Future<void> _onCreate(Database db, int version) async {
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
        memorialPaddingBottom REAL,
        photoVerticalAlignment TEXT,
        photoHorizontalAlignment TEXT,
        deviceType TEXT
      )
    ''');

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
        mourners TEXT,
        memorialPhotoUrl TEXT,
        memorialPhotoFileId TEXT,
        memorialEditedPhotoUrl TEXT,
        memorialEditedPhotoFileId TEXT
      )
    ''');
  }

  Future<void> saveDevice(DeviceDto deviceDto) async {
    final db = await database;
    if (db == null) return;
    await db.insert('devices', deviceDto.toMap(), conflictAlgorithm: ConflictAlgorithm.replace);
  }

  Future<DeviceDto?> getDevice(String code) async {
    final db = await database;
    if (db == null) return null;
    final List<Map<String, dynamic>> maps = await db.query('devices', where: 'code = ?', whereArgs: [code]);
    if (maps.isEmpty) return null;
    return DeviceDto.fromJson(maps.first);
  }

  Future<void> saveDeceased(DeceasedDto deceased) async {
    final db = await database;
    if (db == null) return;
    await db.insert('deceased', deceased.toMap(), conflictAlgorithm: ConflictAlgorithm.replace);
  }

  Future<DeceasedDto?> getDeceasedByRoom(String deviceCode) async {
    final db = await database;
    if (db == null) return null;
    final List<Map<String, dynamic>> maps = await db.query('deceased', where: 'deviceCode = ?', whereArgs: [deviceCode]);
    if (maps.isEmpty) return null;
    return DeceasedDto.fromJson(maps.first);
  }
}
