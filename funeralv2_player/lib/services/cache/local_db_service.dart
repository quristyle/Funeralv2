using 'package:path/path.dart';
using 'package:sqflite/sqflite.dart';
using '../../models/device_models.dart';

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
    final dbPath = await getDatabasesPath();
    final path = join(dbPath, 'funeral_signage.db');

    return await openDatabase(
      path,
      version: 1,
      onCreate: (db, version) async {
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
            isFamilyContactVisible INTEGER
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
      },
    );
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
