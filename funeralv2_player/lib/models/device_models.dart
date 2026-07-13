import 'dart:convert';

class DeviceDto {
  final String id;
  final String code;
  final String name;
  final String? roomId;
  final String? roomName;
  final String? floorId;
  final String? floorName;
  final String? buildingId;
  final String? buildingName;
  final String? videoId;
  final String? musicId;
  final bool isVideoEnabled;
  final bool isMusicEnabled;
  final bool isMuted;
  final String? videoName;
  final String? musicName;
  final double musicVolume;
  final bool isMemorialPhotoEnabled;
  final bool isDeceasedNameVisible;
  final bool isFamilyContactVisible;
  final String displayOrientation;
  final String portraitOrientation;
  final String videoOrientation;
  final String deviceType;
  final String photoVerticalAlignment;
  final String photoHorizontalAlignment;
  final String memorialPhotoEffect;
  final int contentIntervalSec;

  final double displayPaddingTop;
  final double displayPaddingLeft;
  final double displayPaddingRight;
  final double displayPaddingBottom;

  final double memorialPaddingTop;
  final double memorialPaddingLeft;
  final double memorialPaddingRight;
  final double memorialPaddingBottom;

  DeviceDto({
    required this.id, required this.code, required this.name,
    this.roomId, this.roomName, this.floorId, this.floorName,
    this.buildingId, this.buildingName, this.videoId, this.musicId,
    required this.isVideoEnabled, required this.isMusicEnabled, required this.isMuted,
    this.videoName, this.musicName, required this.musicVolume,
    required this.isMemorialPhotoEnabled, required this.isDeceasedNameVisible, required this.isFamilyContactVisible,
    required this.displayOrientation, required this.portraitOrientation, required this.videoOrientation,
    required this.deviceType, required this.photoVerticalAlignment, required this.photoHorizontalAlignment,
    required this.memorialPhotoEffect, required this.contentIntervalSec,
    required this.displayPaddingTop, required this.displayPaddingLeft, required this.displayPaddingRight, required this.displayPaddingBottom,
    required this.memorialPaddingTop, required this.memorialPaddingLeft, required this.memorialPaddingRight, required this.memorialPaddingBottom,
  });

  factory DeviceDto.fromJson(Map<String, dynamic> json) {
    Map<String, dynamic> data;
    if (json.containsKey('data') && json['data'] is Map && json['data'].containsKey('result')) {
      final list = json['data']['result'] as List;
      data = list.isNotEmpty ? list[0] : json;
    } else if (json.containsKey('result') && json['result'] is List) {
      final list = json['result'] as List;
      data = list.isNotEmpty ? list[0] : json;
    } else {
      data = json;
    }

    return DeviceDto(
      id: data['id'] ?? '',
      code: data['code'] ?? '',
      name: data['name'] ?? '',
      roomId: data['roomId'],
      roomName: data['roomName'],
      floorId: data['floorId'],
      floorName: data['floorName'],
      buildingId: data['buildingId'],
      buildingName: data['buildingName'],
      videoId: data['videoId'],
      musicId: data['musicId'],
      isVideoEnabled: (data['isVideoEnabled'] == 1 || data['isVideoEnabled'] == true),
      isMusicEnabled: (data['isMusicEnabled'] == 1 || data['isMusicEnabled'] == true),
      isMuted: (data['isMuted'] == 1 || data['isMuted'] == true),
      videoName: data['videoName'],
      musicName: data['musicName'],
      musicVolume: (data['musicVolume'] ?? 50).toDouble(),
      isMemorialPhotoEnabled: (data['isMemorialPhotoEnabled'] == 1 || data['isMemorialPhotoEnabled'] == true),
      isDeceasedNameVisible: (data['isDeceasedNameVisible'] == 1 || data['isDeceasedNameVisible'] == true),
      isFamilyContactVisible: (data['isFamilyContactVisible'] == 1 || data['isFamilyContactVisible'] == true),
      displayOrientation: data['displayOrientation'] ?? 'LANDSCAPE',
      portraitOrientation: data['portraitOrientation'] ?? 'HORIZONTAL',
      videoOrientation: data['videoOrientation'] ?? 'HORIZONTAL',
      photoVerticalAlignment: (data['photoVerticalAlignment'] == null || data['photoVerticalAlignment'].toString().isEmpty) ? 'CENTER' : data['photoVerticalAlignment'],
      photoHorizontalAlignment: (data['photoHorizontalAlignment'] == null || data['photoHorizontalAlignment'].toString().isEmpty) ? 'CENTER' : data['photoHorizontalAlignment'],
      deviceType: data['deviceType'] ?? 'UNKNOWN',
      memorialPhotoEffect: data['memorialPhotoEffect'] ?? 'FADE',
      contentIntervalSec: (data['contentIntervalSec'] ?? 10).toInt(),
      displayPaddingTop: (data['displayPaddingTop'] ?? 0).toDouble(),
      displayPaddingLeft: (data['displayPaddingLeft'] ?? 0).toDouble(),
      displayPaddingRight: (data['displayPaddingRight'] ?? 0).toDouble(),
      displayPaddingBottom: (data['displayPaddingBottom'] ?? 0).toDouble(),
      memorialPaddingTop: (data['memorialPaddingTop'] ?? 0).toDouble(),
      memorialPaddingLeft: (data['memorialPaddingLeft'] ?? 0).toDouble(),
      memorialPaddingRight: (data['memorialPaddingRight'] ?? 0).toDouble(),
      memorialPaddingBottom: (data['memorialPaddingBottom'] ?? 0).toDouble(),
    );
  }

  Map<String, dynamic> toMap() {
    return {
      'id': id, 'code': code, 'name': name, 'roomId': roomId, 'roomName': roomName,
      'floorId': floorId, 'floorName': floorName, 'buildingId': buildingId, 'buildingName': buildingName,
      'videoId': videoId, 'musicId': musicId, 'isVideoEnabled': isVideoEnabled ? 1 : 0,
      'isMusicEnabled': isMusicEnabled ? 1 : 0, 'isMuted': isMuted ? 1 : 0,
      'videoName': videoName, 'musicName': musicName, 'musicVolume': musicVolume,
      'isMemorialPhotoEnabled': isMemorialPhotoEnabled ? 1 : 0,
      'isDeceasedNameVisible': isDeceasedNameVisible ? 1 : 0,
      'isFamilyContactVisible': isFamilyContactVisible ? 1 : 0,
      'displayOrientation': displayOrientation, 'portraitOrientation': portraitOrientation,
      'videoOrientation': videoOrientation, 'photoVerticalAlignment': photoVerticalAlignment,
      'photoHorizontalAlignment': photoHorizontalAlignment, 'deviceType': deviceType,
      'memorialPhotoEffect': memorialPhotoEffect, 'contentIntervalSec': contentIntervalSec,
      'displayPaddingTop': displayPaddingTop, 'displayPaddingLeft': displayPaddingLeft,
      'displayPaddingRight': displayPaddingRight, 'displayPaddingBottom': displayPaddingBottom,
      'memorialPaddingTop': memorialPaddingTop, 'memorialPaddingLeft': memorialPaddingLeft,
      'memorialPaddingRight': memorialPaddingRight, 'memorialPaddingBottom': memorialPaddingBottom,
    };
  }
}

class MournerDto {
  final String? name;
  final String? relation;
  final String? relationName;
  final bool isChief;
  MournerDto({this.name, this.relation, this.relationName, required this.isChief});
  factory MournerDto.fromJson(Map<String, dynamic> json) => MournerDto(
    name: json['name'], relation: json['relation'], relationName: json['relationName'], isChief: (json['isChief'] == 1 || json['isChief'] == true),
  );
  Map<String, dynamic> toJson() => {'name': name, 'relation': relation, 'relationName': relationName, 'isChief': isChief};
}

class DeviceRibbonDto {
  final String id;
  final String deviceId;
  final String mediaSourceId;
  final String? mediaSourceUrl;
  final double positionLeft;
  final double positionTop;
  final double width;
  final double height;
  final String? remark;

  DeviceRibbonDto({required this.id, required this.deviceId, required this.mediaSourceId, this.mediaSourceUrl, required this.positionLeft, required this.positionTop, required this.width, required this.height, this.remark});

  factory DeviceRibbonDto.fromJson(Map<String, dynamic> json) => DeviceRibbonDto(
    id: json['id'] ?? '', deviceId: json['deviceId'] ?? '', mediaSourceId: json['mediaSourceId'] ?? '', mediaSourceUrl: json['mediaSourceUrl'],
    positionLeft: (json['positionLeft'] ?? 0).toDouble(), positionTop: (json['positionTop'] ?? 0).toDouble(),
    width: (json['width'] ?? 0).toDouble(), height: (json['height'] ?? 0).toDouble(),
    remark: json['remark'],
  );

  Map<String, dynamic> toJson() => {'id': id, 'deviceId': deviceId, 'mediaSourceId': mediaSourceId, 'mediaSourceUrl': mediaSourceUrl, 'positionLeft': positionLeft, 'positionTop': positionTop, 'width': width, 'height': height, 'remark': remark};

  int get rotation {
    if (remark == null || remark!.isEmpty) return 0;
    final match = RegExp(r'rotation:(\d+)').firstMatch(remark!);
    if (match != null) {
      return int.tryParse(match.group(1) ?? '0') ?? 0;
    }
    return 0;
  }
}

class DeviceTextOverlayDto {
  final String id;
  final String deviceId;
  final String textContent;
  final double fontSize;
  final String fontColor;
  final String backgroundColor;
  final String textAlign;
  final String fontWeight;
  final double positionLeft;
  final double positionTop;
  final double width;
  final double height;
  final String? remark;

  DeviceTextOverlayDto({required this.id, required this.deviceId, required this.textContent, required this.fontSize, required this.fontColor, required this.backgroundColor, required this.textAlign, required this.fontWeight, required this.positionLeft, required this.positionTop, required this.width, required this.height, this.remark});

  factory DeviceTextOverlayDto.fromJson(Map<String, dynamic> json) => DeviceTextOverlayDto(
    id: json['id'] ?? '', deviceId: json['deviceId'] ?? '', textContent: json['textContent'] ?? '',
    fontSize: (json['fontSize'] ?? 0).toDouble(), fontColor: json['fontColor'] ?? '#FFFFFF', backgroundColor: json['backgroundColor'] ?? 'transparent',
    textAlign: json['textAlign'] ?? 'center', fontWeight: json['fontWeight'] ?? 'normal',
    positionLeft: (json['positionLeft'] ?? 0).toDouble(), positionTop: (json['positionTop'] ?? 0).toDouble(),
    width: (json['width'] ?? 0).toDouble(), height: (json['height'] ?? 0).toDouble(),
    remark: json['remark'],
  );

  Map<String, dynamic> toJson() => {'id': id, 'deviceId': deviceId, 'textContent': textContent, 'fontSize': fontSize, 'fontColor': fontColor, 'backgroundColor': backgroundColor, 'textAlign': textAlign, 'fontWeight': fontWeight, 'positionLeft': positionLeft, 'positionTop': positionTop, 'width': width, 'height': height, 'remark': remark};

  int get rotation {
    if (remark == null || remark!.isEmpty) return 0;
    final match = RegExp(r'rotation:(\d+)').firstMatch(remark!);
    if (match != null) {
      return int.tryParse(match.group(1) ?? '0') ?? 0;
    }
    return 0;
  }
}

class DeceasedDto {
  final String id;
  final String name;
  final String gender;
  final int age;
  final String? religion;
  final String? deathDate;
  final String? funeralDate;
  final String? burialDate;
  final String? roomId;
  final String? roomName;
  final String? chiefMourner;
  final List<MournerDto> mourners;
  final String? memorialPhotoUrl;
  final String? memorialPhotoFileId;
  final String? memorialEditedPhotoUrl;
  final String? memorialEditedPhotoFileId;
  final List<DeviceRibbonDto> deviceRibbons;
  final List<DeviceTextOverlayDto> deviceTextOverlays;
  final List<String> familyPhotos;

  DeceasedDto({required this.id, required this.name, required this.gender, required this.age, this.religion, this.deathDate, this.funeralDate, this.burialDate, this.roomId, this.roomName, this.chiefMourner, required this.mourners, this.familyPhotos = const [], this.memorialPhotoUrl, this.memorialPhotoFileId, this.memorialEditedPhotoUrl, this.memorialEditedPhotoFileId, required this.deviceRibbons, required this.deviceTextOverlays});

  factory DeceasedDto.fromJson(Map<String, dynamic> json) {
    Map<String, dynamic> data;
    if (json.containsKey('data') && json['data'] is Map && json['data'].containsKey('result')) {
      final list = json['data']['result'] as List;
      data = list.isNotEmpty ? list[0] : json['data'];
    } else if (json.containsKey('result') && json['result'] is List) {
      final list = json['result'] as List;
      data = list.isNotEmpty ? list[0] : json;
    } else {
      data = json;
    }

    List<dynamic> _flexibleList(dynamic input) {
      if (input == null) return [];
      if (input is String) {
        try {
          final decoded = jsonDecode(input);
          return (decoded is List) ? decoded : [];
        } catch (_) {
          return [];
        }
      }
      if (input is List) return input;
      return [];
    }

    return DeceasedDto(
      id: data['id'] ?? '',
      name: data['name'] ?? '',
      gender: data['gender'] ?? '',
      age: (data['age'] ?? 0).toInt(),
      religion: data['religion'],
      deathDate: data['deathDate'],
      funeralDate: data['funeralDate'],
      burialDate: data['burialDate'],
      roomId: data['roomId'],
      roomName: data['roomName'],
      chiefMourner: data['chiefMourner'],
      mourners: _flexibleList(data['mourners']).map((i) => MournerDto.fromJson(i)).toList(),
      familyPhotos: _flexibleList(data['familyPhotos']).map((i) => i.toString()).toList(),
      memorialPhotoUrl: data['memorialPhotoUrl'],
      memorialPhotoFileId: data['memorialPhotoFileId'],
      memorialEditedPhotoUrl: data['memorialEditedPhotoUrl'],
      memorialEditedPhotoFileId: data['memorialEditedPhotoFileId'],
      deviceRibbons: _flexibleList(data['deviceRibbons']).map((i) => DeviceRibbonDto.fromJson(i)).toList(),
      deviceTextOverlays: _flexibleList(data['deviceTextOverlays']).map((i) => DeviceTextOverlayDto.fromJson(i)).toList(),
    );
  }

  Map<String, dynamic> toMap() {
    return {
      'id': id, 'name': name, 'gender': gender, 'age': age, 'religion': religion, 'deathDate': deathDate, 'funeralDate': funeralDate, 'burialDate': burialDate, 'roomId': roomId, 'roomName': roomName, 'chiefMourner': chiefMourner,
      'mourners': jsonEncode(mourners.map((e) => e.toJson()).toList()), 
      'familyPhotos': jsonEncode(familyPhotos),
      'memorialPhotoUrl': memorialPhotoUrl, 'memorialPhotoFileId': memorialPhotoFileId, 'memorialEditedPhotoUrl': memorialEditedPhotoUrl, 'memorialEditedPhotoFileId': memorialEditedPhotoFileId,
      'deviceRibbons': jsonEncode(deviceRibbons.map((e) => e.toJson()).toList()),
      'deviceTextOverlays': jsonEncode(deviceTextOverlays.map((e) => e.toJson()).toList()),
    };
  }
}

class EntranceGuideRoomDto {
  final String roomId;
  final String roomName;
  final String floorName;
  final int sortOrder;
  final DeceasedDto? deceasedDetail;
  EntranceGuideRoomDto({required this.roomId, required this.roomName, required this.floorName, required this.sortOrder, this.deceasedDetail});
  factory EntranceGuideRoomDto.fromJson(Map<String, dynamic> json) => EntranceGuideRoomDto(
    roomId: json['roomId'] ?? '', roomName: json['roomName'] ?? '', floorName: json['floorName'] ?? '', sortOrder: (json['sortOrder'] ?? 0).toInt(),
    deceasedDetail: json['deceasedDetail'] != null ? DeceasedDto.fromJson(json['deceasedDetail']) : null,
  );
}

class KioskGuideResponseDto {
  final List<EntranceGuideRoomDto> rooms;
  final List<String> buildingPhotos;
  final List<String> parkingPhotos;
  KioskGuideResponseDto({required this.rooms, required this.buildingPhotos, required this.parkingPhotos});
  factory KioskGuideResponseDto.fromJson(Map<String, dynamic> json) {
    var roomsList = json['rooms'] as List? ?? [];
    var buildingPhotosList = json['buildingPhotos'] as List? ?? [];
    var parkingPhotosList = json['parkingPhotos'] as List? ?? [];
    return KioskGuideResponseDto(
      rooms: roomsList.map((e) => EntranceGuideRoomDto.fromJson(e)).toList(),
      buildingPhotos: buildingPhotosList.map((e) => e.toString()).toList(),
      parkingPhotos: parkingPhotosList.map((e) => e.toString()).toList(),
    );
  }
}
