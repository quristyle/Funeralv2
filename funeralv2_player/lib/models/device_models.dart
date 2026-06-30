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
  });

  factory DeviceDto.fromJson(Map<String, dynamic> json) {
    // result 리스트 래핑 대응
    final data = json['result'] != null && json['result'] is List && (json['result'] as List).isNotEmpty
        ? json['result'][0]
        : json;

    return DeviceDto(
      id: data['id'] ?? '',
      code: data['code'] ?? '',
      name: data['name'] ?? '',
      roomId: data['roomId'],
      videoId: data['videoId'],
      musicId: data['musicId'],
      isVideoEnabled: data['isVideoEnabled'] ?? false,
      isMusicEnabled: data['isMusicEnabled'] ?? false,
      videoName: data['videoName'],
      musicName: data['musicName'],
      musicVolume: (data['musicVolume'] ?? 50).toDouble(),
      isMemorialPhotoEnabled: data['isMemorialPhotoEnabled'] ?? false,
      isDeceasedNameVisible: data['isDeceasedNameVisible'] ?? false,
      isFamilyContactVisible: data['isFamilyContactVisible'] ?? false,
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
  final String? memorialEditedPhotoUrl;

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
    this.memorialEditedPhotoUrl,
  });

  factory DeceasedDto.fromJson(Map<String, dynamic> json) {
    // result 리스트 래핑 대응
    final data = json['result'] != null && json['result'] is List && (json['result'] as List).isNotEmpty
        ? json['result'][0]
        : json;

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
      memorialEditedPhotoUrl: data['memorialEditedPhotoUrl'],
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
      'memorialEditedPhotoUrl': memorialEditedPhotoUrl,
    };
  }
}
