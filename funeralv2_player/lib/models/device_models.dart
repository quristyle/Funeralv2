class DeviceDto {
  final String id;
  final String code;
  final String name;
  final String? roomId;
  final String? videoId;
  final String? musicId;
  final bool isVideoEnabled;
  final bool isMusicEnabled;
  final String? videoName;
  final String? musicName;
  final double musicVolume;
  final bool isMemorialPhotoEnabled;
  final bool isDeceasedNameVisible;
  final bool isFamilyContactVisible;
  final String displayOrientation;
  final String portraitOrientation;
  final String videoOrientation;
  final String photoVerticalAlignment;
  final String photoHorizontalAlignment; // 추가

  final double displayPaddingTop;
  final double displayPaddingLeft;
  final double displayPaddingRight;
  final double displayPaddingBottom;

  final double memorialPaddingTop;
  final double memorialPaddingLeft;
  final double memorialPaddingRight;
  final double memorialPaddingBottom;

  DeviceDto({
    required this.id,
    required this.code,
    required this.name,
    this.roomId,
    this.videoId,
    this.musicId,
    required this.isVideoEnabled,
    required this.isMusicEnabled,
    this.videoName,
    this.musicName,
    required this.musicVolume,
    required this.isMemorialPhotoEnabled,
    required this.isDeceasedNameVisible,
    required this.isFamilyContactVisible,
    required this.displayOrientation,
    required this.portraitOrientation,
    required this.videoOrientation,
    required this.photoVerticalAlignment,
    required this.photoHorizontalAlignment,
    required this.displayPaddingTop,
    required this.displayPaddingLeft,
    required this.displayPaddingRight,
    required this.displayPaddingBottom,
    required this.memorialPaddingTop,
    required this.memorialPaddingLeft,
    required this.memorialPaddingRight,
    required this.memorialPaddingBottom,
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
      videoId: data['videoId'],
      musicId: data['musicId'],
      isVideoEnabled: (data['isVideoEnabled'] == 1 || data['isVideoEnabled'] == true),
      isMusicEnabled: (data['isMusicEnabled'] == 1 || data['isMusicEnabled'] == true),
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
      'id': id,
      'code': code,
      'name': name,
      'roomId': roomId,
      'videoId': videoId,
      'musicId': musicId,
      'isVideoEnabled': isVideoEnabled ? 1 : 0,
      'isMusicEnabled': isMusicEnabled ? 1 : 0,
      'videoName': videoName,
      'musicName': musicName,
      'musicVolume': musicVolume,
      'isMemorialPhotoEnabled': isMemorialPhotoEnabled ? 1 : 0,
      'isDeceasedNameVisible': isDeceasedNameVisible ? 1 : 0,
      'isFamilyContactVisible': isFamilyContactVisible ? 1 : 0,
      'displayOrientation': displayOrientation,
      'portraitOrientation': portraitOrientation,
      'videoOrientation': videoOrientation,
      'photoVerticalAlignment': photoVerticalAlignment,
      'photoHorizontalAlignment': photoHorizontalAlignment,
      'displayPaddingTop': displayPaddingTop,
      'displayPaddingLeft': displayPaddingLeft,
      'displayPaddingRight': displayPaddingRight,
      'displayPaddingBottom': displayPaddingBottom,
      'memorialPaddingTop': memorialPaddingTop,
      'memorialPaddingLeft': memorialPaddingLeft,
      'memorialPaddingRight': memorialPaddingRight,
      'memorialPaddingBottom': memorialPaddingBottom,
    };
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
  final String? memorialPhotoUrl;
  final String? memorialPhotoFileId;
  final String? memorialEditedPhotoUrl;
  final String? memorialEditedPhotoFileId;

  DeceasedDto({
    required this.id,
    required this.name,
    required this.gender,
    required this.age,
    this.religion,
    this.deathDate,
    this.funeralDate,
    this.burialDate,
    this.roomId,
    this.roomName,
    this.chiefMourner,
    this.memorialPhotoUrl,
    this.memorialPhotoFileId,
    required this.memorialEditedPhotoUrl,
    required this.memorialEditedPhotoFileId,
  });

  factory DeceasedDto.fromJson(Map<String, dynamic> json) {
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

    return DeceasedDto(
      id: data['id'] ?? '',
      name: data['name'] ?? '',
      gender: data['gender'] ?? '',
      age: data['age'] ?? 0,
      religion: data['religion'],
      deathDate: data['deathDate'],
      funeralDate: data['funeralDate'],
      burialDate: data['burialDate'],
      roomId: data['roomId'],
      roomName: data['roomName'],
      chiefMourner: data['chiefMourner'],
      memorialPhotoUrl: data['memorialPhotoUrl'],
      memorialPhotoFileId: data['memorialPhotoFileId'],
      memorialEditedPhotoUrl: data['memorialEditedPhotoUrl'] ?? '',
      memorialEditedPhotoFileId: data['memorialEditedPhotoFileId'] ?? '',
    );
  }

  Map<String, dynamic> toMap() {
    return {
      'id': id,
      'name': name,
      'gender': gender,
      'age': age,
      'religion': religion,
      'deathDate': deathDate,
      'funeralDate': funeralDate,
      'burialDate': burialDate,
      'roomId': roomId,
      'roomName': roomName,
      'chiefMourner': chiefMourner,
      'memorialPhotoUrl': memorialPhotoUrl,
      'memorialPhotoFileId': memorialPhotoFileId,
      'memorialEditedPhotoUrl': memorialEditedPhotoUrl,
      'memorialEditedPhotoFileId': memorialEditedPhotoFileId,
    };
  }
}
