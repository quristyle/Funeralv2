import 'dart:convert';
import 'package:flutter/foundation.dart';

/// [장비 정보 DTO]
/// 서버로부터 수신한 디스플레이 장비(사이니지)의 상세 설정 정보를 저장하는 데이터 모델입니다.
class DeviceDto {
  final String id;
  final String code; // 장비 코드
  final String name; // 장비 명칭
  final String? roomId; // 배치된 빈소/호실 ID
  final String? roomName; // 호실 명칭
  final String? floorId; // 층 ID
  final String? floorName; // 층 명칭
  final String? buildingId; // 건물 ID
  final String? buildingName; // 건물 명칭
  final String? videoId; // 설정된 비디오 미디어 ID
  final String? musicId; // 설정된 음원 미디어 ID
  final bool isVideoEnabled; // 비디오 재생 여부
  final bool isMusicEnabled; // 음원 재생 여부
  final bool isMuted; // 음소거 여부
  final bool isBackgroundImageEnabled; // 배경 이미지 사용 여부
  final String? videoName; // 비디오 파일명
  final String? musicName; // 음원 파일명
  final String? backgroundImageId; // 배경 이미지 미디어 ID
  final String? backgroundImageName; // 배경 이미지 파일명
  final String? backgroundImageUrl; // 배경 이미지 URL
  final String backgroundOrientation; // 배경 방향 (HORIZONTAL, VERTICAL 등)
  final double musicVolume; // 음원 볼륨
  final bool isMemorialPhotoEnabled; // 모바일 영정(기념) 사진 활성화 여부
  final bool isDeceasedNameVisible; // 고인명 노출 여부
  final bool isFamilyContactVisible; // 유족 연락처 노출 여부
  final bool isMemorialPhotoKeepAspectRatio; // 영정사진 비율 유지 여부
  final String displayOrientation; // 디스플레이 방향 (LANDSCAPE, PORTRAIT 등)
  final String portraitOrientation; // 영정 사진 방향
  final String videoOrientation; // 비디오 방향
  final String deviceType; // 장비 유형 (ENTRANCE, ROOM, KIOSK, PORTRAIT, MULTIMEDIA 등)
  final String photoVerticalAlignment; // 사진 수직 정렬 방식 (TOP, CENTER, BOTTOM)
  final String photoHorizontalAlignment; // 사진 수평 정렬 방식 (LEFT, CENTER, RIGHT)
  final String memorialPhotoEffect; // 영정 사진 화면 효과 (FADE, SLIDE 등)
  final int contentIntervalSec; // 콘텐츠 전환 간격 (초 단위)

  // 디스플레이 여백 설정 (상하좌우)
  final double displayPaddingTop;
  final double displayPaddingLeft;
  final double displayPaddingRight;
  final double displayPaddingBottom;

  // 영정 영역 여백 설정 (상하좌우)
  final double memorialPaddingTop;
  final double memorialPaddingLeft;
  final double memorialPaddingRight;
  final double memorialPaddingBottom;

  DeviceDto({
    required this.id, required this.code, required this.name,
    this.roomId, this.roomName, this.floorId, this.floorName,
    this.buildingId, this.buildingName, this.videoId, this.musicId,
    required this.isVideoEnabled, required this.isMusicEnabled, required this.isMuted,
    required this.isBackgroundImageEnabled,
    this.videoName, this.musicName, this.backgroundImageId, this.backgroundImageName, this.backgroundImageUrl,
    required this.backgroundOrientation,
    required this.musicVolume,
    required this.isMemorialPhotoEnabled, required this.isDeceasedNameVisible, required this.isFamilyContactVisible,
    required this.isMemorialPhotoKeepAspectRatio,
    required this.displayOrientation, required this.portraitOrientation, required this.videoOrientation,
    required this.deviceType, required this.photoVerticalAlignment, required this.photoHorizontalAlignment,
    required this.memorialPhotoEffect, required this.contentIntervalSec,
    required this.displayPaddingTop, required this.displayPaddingLeft, required this.displayPaddingRight, required this.displayPaddingBottom,
    required this.memorialPaddingTop, required this.memorialPaddingLeft, required this.memorialPaddingRight, required this.memorialPaddingBottom,
  });

  /// [JSON 역직렬화 팩토리 메서드]
  /// API 응답 구조가 단일 객체이거나, 공통 API 응답 규격(data.result 배열 패턴)에 감싸져 있는 경우를 
  /// 모두 유연하게 처리하여 DeviceDto 객체를 생성합니다.
  factory DeviceDto.fromJson(Map<String, dynamic> json) {
    Map<String, dynamic> data;
    // API 응답 데이터가 'data' 키 및 'result' 배열에 감싸져 있는 경우 첫 번째 요소를 추출합니다.
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
      isBackgroundImageEnabled: (data['isBackgroundImageEnabled'] == 1 || data['isBackgroundImageEnabled'] == true),
      videoName: data['videoName'],
      musicName: data['musicName'],
      backgroundImageId: data['backgroundImageId'],
      backgroundImageName: data['backgroundImageName'],
      backgroundImageUrl: data['backgroundImageUrl'],
      backgroundOrientation: data['backgroundOrientation'] ?? 'HORIZONTAL',
      musicVolume: (data['musicVolume'] ?? 50).toDouble(),
      isMemorialPhotoEnabled: (data['isMemorialPhotoEnabled'] == 1 || data['isMemorialPhotoEnabled'] == true),
      isDeceasedNameVisible: (data['isDeceasedNameVisible'] == 1 || data['isDeceasedNameVisible'] == true),
      isFamilyContactVisible: (data['isFamilyContactVisible'] == 1 || data['isFamilyContactVisible'] == true),
      isMemorialPhotoKeepAspectRatio: (data['isMemorialPhotoKeepAspectRatio'] == null) ? true : (data['isMemorialPhotoKeepAspectRatio'] == 1 || data['isMemorialPhotoKeepAspectRatio'] == true),
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

  /// [Map 직렬화 메서드]
  /// 로컬 데이터베이스(SQLite 등)에 객체를 키-값 형태로 영속화하기 위해 사용합니다.
  Map<String, dynamic> toMap() {
    return {
      'id': id, 'code': code, 'name': name, 'roomId': roomId, 'roomName': roomName,
      'floorId': floorId, 'floorName': floorName, 'buildingId': buildingId, 'buildingName': buildingName,
      'videoId': videoId, 'musicId': musicId, 'isVideoEnabled': isVideoEnabled ? 1 : 0,
      'isMusicEnabled': isMusicEnabled ? 1 : 0, 'isMuted': isMuted ? 1 : 0,
      'isBackgroundImageEnabled': isBackgroundImageEnabled ? 1 : 0,
      'videoName': videoName, 'musicName': musicName, 'backgroundImageId': backgroundImageId,
      'backgroundImageName': backgroundImageName, 'backgroundImageUrl': backgroundImageUrl,
      'backgroundOrientation': backgroundOrientation,
      'musicVolume': musicVolume,
      'isMemorialPhotoEnabled': isMemorialPhotoEnabled ? 1 : 0,
      'isDeceasedNameVisible': isDeceasedNameVisible ? 1 : 0,
      'isFamilyContactVisible': isFamilyContactVisible ? 1 : 0,
      'isMemorialPhotoKeepAspectRatio': isMemorialPhotoKeepAspectRatio ? 1 : 0,
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

  /// [사이니지 렌더링 상태 동등성 비교]
  /// 화면 표출에 영향을 주는 모든 설정 필드를 한 번에 비교합니다.
  /// 개별 필드를 나열해 비교하다 특정 필드(예: videoId)를 빠뜨려 변경이 반영되지 않는 문제를 방지하기 위해,
  /// 직렬화 결과(toMap)를 통째로 비교하여 어떤 속성이 바뀌어도 확실히 감지되도록 합니다.
  bool signageEquals(DeviceDto other) => mapEquals(toMap(), other.toMap());
}

/// [상주 정보 DTO]
/// 고인의 유족(상주) 정보를 나타냅니다.
class MournerDto {
  final String? name; // 상주 성함
  final String? relation; // 관계 코드
  final String? relationName; // 관계 명칭 (예: 자, 녀, 사위 등)
  final bool isChief; // 대표 상주(상주 대표) 여부

  MournerDto({this.name, this.relation, this.relationName, required this.isChief});

  /// [JSON 역직렬화 팩토리 메서드]
  factory MournerDto.fromJson(Map<String, dynamic> json) => MournerDto(
    name: json['name'], relation: json['relation'], relationName: json['relationName'], isChief: (json['isChief'] == 1 || json['isChief'] == true),
  );

  /// [JSON 직렬화 메서드]
  Map<String, dynamic> toJson() => {'name': name, 'relation': relation, 'relationName': relationName, 'isChief': isChief};
}

/// [장비 리본 장식 DTO]
/// 화면상에 배치되는 가상의 근조 리본이나 디자인 레이어(이미지 소스)의 위치 및 크기 정보입니다.
class DeviceRibbonDto {
  final String id;
  final String deviceId; // 해당 장비 ID
  final String mediaSourceId; // 이미지 리소스 ID
  final String? mediaSourceUrl; // 이미지 리소스 URL
  final double positionLeft; // 좌측 오프셋 비율/좌표
  final double positionTop; // 상단 오프셋 비율/좌표
  final double width; // 너비
  final double height; // 높이
  final String? remark; // 비고 (각도 등 커스텀 속성 파싱용)

  DeviceRibbonDto({required this.id, required this.deviceId, required this.mediaSourceId, this.mediaSourceUrl, required this.positionLeft, required this.positionTop, required this.width, required this.height, this.remark});

  /// [JSON 역직렬화 팩토리 메서드]
  factory DeviceRibbonDto.fromJson(Map<String, dynamic> json) => DeviceRibbonDto(
    id: json['id'] ?? '', deviceId: json['deviceId'] ?? '', mediaSourceId: json['mediaSourceId'] ?? '', mediaSourceUrl: json['mediaSourceUrl'],
    positionLeft: (json['positionLeft'] ?? 0).toDouble(), positionTop: (json['positionTop'] ?? 0).toDouble(),
    width: (json['width'] ?? 0).toDouble(), height: (json['height'] ?? 0).toDouble(),
    remark: json['remark'],
  );

  /// [JSON 직렬화 메서드]
  Map<String, dynamic> toJson() => {'id': id, 'deviceId': deviceId, 'mediaSourceId': mediaSourceId, 'mediaSourceUrl': mediaSourceUrl, 'positionLeft': positionLeft, 'positionTop': positionTop, 'width': width, 'height': height, 'remark': remark};

  /// [회전 각도 Getter]
  /// remark 필드에 들어있는 'rotation:90' 포맷의 정보를 정규식으로 파싱해 각도 값을 리턴합니다.
  int get rotation {
    if (remark == null || remark!.isEmpty) return 0;
    final match = RegExp(r'rotation:(\d+)').firstMatch(remark!);
    if (match != null) {
      return int.tryParse(match.group(1) ?? '0') ?? 0;
    }
    return 0;
  }
}

/// [장비 텍스트 오버레이 DTO]
/// 화면상 특정 좌표에 동적으로 그려지는 텍스트 컴포넌트의 스타일 및 내용 정보입니다.
class DeviceTextOverlayDto {
  final String id;
  final String deviceId; // 해당 장비 ID
  final String textContent; // 표시할 내용
  final double fontSize; // 폰트 크기
  final String fontColor; // 폰트 색상 (HEX)
  final String backgroundColor; // 배경 색상
  final String textAlign; // 정렬 방식 (left, center, right)
  final String fontWeight; // 폰트 두께 (normal, bold)
  final double positionLeft; // 좌측 오프셋
  final double positionTop; // 상단 오프셋
  final double width; // 너비
  final double height; // 높이
  final String? remark; // 비고 (각도 등 커스텀 속성 파싱용)

  DeviceTextOverlayDto({required this.id, required this.deviceId, required this.textContent, required this.fontSize, required this.fontColor, required this.backgroundColor, required this.textAlign, required this.fontWeight, required this.positionLeft, required this.positionTop, required this.width, required this.height, this.remark});

  /// [JSON 역직렬화 팩토리 메서드]
  factory DeviceTextOverlayDto.fromJson(Map<String, dynamic> json) => DeviceTextOverlayDto(
    id: json['id'] ?? '', deviceId: json['deviceId'] ?? '', textContent: json['textContent'] ?? '',
    fontSize: (json['fontSize'] ?? 0).toDouble(), fontColor: json['fontColor'] ?? '#FFFFFF', backgroundColor: json['backgroundColor'] ?? 'transparent',
    textAlign: json['textAlign'] ?? 'center', fontWeight: json['fontWeight'] ?? 'normal',
    positionLeft: (json['positionLeft'] ?? 0).toDouble(), positionTop: (json['positionTop'] ?? 0).toDouble(),
    width: (json['width'] ?? 0).toDouble(), height: (json['height'] ?? 0).toDouble(),
    remark: json['remark'],
  );

  /// [JSON 직렬화 메서드]
  Map<String, dynamic> toJson() => {'id': id, 'deviceId': deviceId, 'textContent': textContent, 'fontSize': fontSize, 'fontColor': fontColor, 'backgroundColor': backgroundColor, 'textAlign': textAlign, 'fontWeight': fontWeight, 'positionLeft': positionLeft, 'positionTop': positionTop, 'width': width, 'height': height, 'remark': remark};

  /// [회전 각도 Getter]
  /// remark 필드에 들어있는 'rotation:90' 포맷의 정보를 정규식으로 파싱해 각도 값을 리턴합니다.
  int get rotation {
    if (remark == null || remark!.isEmpty) return 0;
    final match = RegExp(r'rotation:(\d+)').firstMatch(remark!);
    if (match != null) {
      return int.tryParse(match.group(1) ?? '0') ?? 0;
    }
    return 0;
  }
}

/// [고인 정보 DTO]
/// 특정 빈소(호실)에 안치된 고인의 신원, 유족, 행사일정(발인, 장지), 영정 이미지 및 레이아웃 설정 일체를 저장하는 핵심 모델입니다.
class DeceasedDto {
  final String id;
  final String name; // 고인 성함
  final String gender; // 성별
  final int age; // 연세
  final String? religion; // 종교
  final String? deathDate; // 사망 일시
  final String? funeralDate; // 입관/발인 일시
  final String? burialDate; // 장지
  final String? roomId; // 호실 ID
  final String? roomName; // 호실 명칭
  final String? chiefMourner; // 대표 상주 성함
  final List<MournerDto> mourners; // 상주 목록
  final String? memorialPhotoUrl; // 원본 영정 사진 URL
  final String? memorialPhotoFileId; // 원본 영정 사진 파일 ID
  final String? memorialEditedPhotoUrl; // 보정/수정된 영정 사진 URL
  final String? memorialEditedPhotoFileId; // 보정/수정된 영정 사진 파일 ID
  final List<DeviceRibbonDto> deviceRibbons; // 화면에 얹어질 근조 리본 장식 리스트
  final List<DeviceTextOverlayDto> deviceTextOverlays; // 화면에 얹어질 텍스트 레이아웃 리스트
  final List<String> familyPhotos; // 추모용 가족 사진 URL 리스트

  DeceasedDto({required this.id, required this.name, required this.gender, required this.age, this.religion, this.deathDate, this.funeralDate, this.burialDate, this.roomId, this.roomName, this.chiefMourner, required this.mourners, this.familyPhotos = const [], this.memorialPhotoUrl, this.memorialPhotoFileId, this.memorialEditedPhotoUrl, this.memorialEditedPhotoFileId, required this.deviceRibbons, required this.deviceTextOverlays});

  /// [JSON 역직렬화 팩토리 메서드]
  /// JSON 데이터에서 고인 상세 정보를 파싱하며, 문자열 형식으로 들어온 배열 데이터나
  /// API 응답 공통 래핑 패턴(data.result)에 유연하게 대처합니다.
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

    /// JSON이 문자열 형식으로 들어올 수 있는 유연한 리스트 변환 헬퍼 함수
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

  /// [Map 직렬화 메서드]
  /// 로컬 데이터베이스에 객체 데이터를 영속화하거나 인코딩할 때 사용합니다.
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

/// [입구 안내용 호실 정보 DTO]
/// 장례식장 입구 종합 안내판(ENTRANCE)에 노출할 개별 호실 및 상주/고인 정보의 쌍을 의미합니다.
class EntranceGuideRoomDto {
  final String roomId;
  final String roomName;
  final String floorName;
  final int sortOrder; // 정렬 순서
  final DeceasedDto? deceasedDetail; // 해당 빈소에 안치된 고인의 세부 정보

  EntranceGuideRoomDto({required this.roomId, required this.roomName, required this.floorName, required this.sortOrder, this.deceasedDetail});

  /// [JSON 역직렬화 팩토리 메서드]
  factory EntranceGuideRoomDto.fromJson(Map<String, dynamic> json) => EntranceGuideRoomDto(
    roomId: json['roomId'] ?? '', roomName: json['roomName'] ?? '', floorName: json['floorName'] ?? '', sortOrder: (json['sortOrder'] ?? 0).toInt(),
    deceasedDetail: json['deceasedDetail'] != null ? DeceasedDto.fromJson(json['deceasedDetail']) : null,
  );
}

/// [종합 키오스크 안내 응답 DTO]
/// 종합 안내용 키오스크(KIOSK)에서 필요로 하는 전체 빈소 목록, 건물 안내 사진, 주차장 안내 사진의 패키지 데이터입니다.
class KioskGuideResponseDto {
  final List<EntranceGuideRoomDto> rooms; // 빈소별 행사 안내 리스트
  final List<String> buildingPhotos; // 층별/건물 소개 이미지 파일 리스트
  final List<String> parkingPhotos; // 주차장 찾아오시는 길 이미지 파일 리스트

  KioskGuideResponseDto({required this.rooms, required this.buildingPhotos, required this.parkingPhotos});

  /// [JSON 역직렬화 팩토리 메서드]
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
