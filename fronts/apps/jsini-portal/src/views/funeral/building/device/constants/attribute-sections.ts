/**
 * [화면 표시] 탭의 섹션 목록과 장비 유형 매핑 (49번 문서 D-DV3).
 *
 * 예전 속성 탭은 섹션 여섯을 장비 유형과 무관하게 항상 세로로 쌓았다. 영정사진
 * DID 를 보면서도 키오스크 · 층별 안내 설정이 다 나와서, 40개 남짓한 칸 중
 * 실제로 쓰는 열 개를 찾아 스크롤해야 했다.
 *
 * 여기서 유형별로 갈라 두고, 해당하는 섹션만 펼친다. **무관한 섹션은 숨기지 않고
 * 접어서 아래로 내린다** — 유형을 잘못 넣은 장비를 고칠 길은 남겨야 하기 때문이다.
 */
export interface AttributeSection {
  key: string;
  label: string;
  icon: string;
  /** 이 섹션이 의미 있는 장비 유형. 비우면 모든 유형에 해당한다. */
  deviceTypes?: string[];
  /**
   * 펼치는 대신 편집기로 들어가는 섹션. 리본 · 문구 편집기는 3단 구성이라
   * 접이식 안에 넣으면 눌린다.
   */
  drillIn?: boolean;
  /**
   * 이 섹션이 담는 속성 칸. 섹션 머리에 「N 변경」을 붙이는 데 쓴다.
   * (리본은 별도 표라 여기 없다.)
   */
  fields?: string[];
}

export const ATTRIBUTE_SECTIONS: AttributeSection[] = [
  {
    key: 'layout',
    label: '화면 배치',
    icon: 'lucide:monitor-cog',
    fields: [
      'displayOrientation', 'portraitOrientation',
      'displayPaddingTop', 'displayPaddingBottom', 'displayPaddingLeft', 'displayPaddingRight',
      'contentIntervalSec', 'isScreensaverEnabled', 'screensaverTimeoutSec',
    ],
  },
  {
    key: 'memorial',
    label: '영정사진 · 추모',
    icon: 'lucide:image',
    deviceTypes: ['FUNERAL_PORTRAIT'],
    fields: [
      'isMemorialPhotoEnabled', 'memorialPhotoEffect',
      'photoVerticalAlignment', 'photoHorizontalAlignment',
      'isDeceasedNameVisible', 'isFamilyContactVisible', 'isMemorialPhotoKeepAspectRatio',
      'memorialPaddingTop', 'memorialPaddingBottom', 'memorialPaddingLeft', 'memorialPaddingRight',
    ],
  },
  {
    key: 'media',
    label: '사진 · 영상 · 음악',
    icon: 'lucide:play-circle',
    // 배경 이미지는 영정사진 장비도 쓴다.
    deviceTypes: ['MULTIMEDIA', 'FUNERAL_PORTRAIT'],
    fields: [
      'isVideoEnabled', 'isMusicEnabled', 'isBackgroundImageEnabled',
      'videoId', 'musicId', 'backgroundImageId',
      'videoOrientation', 'backgroundOrientation',
      'isMuted', 'musicVolume', 'isMediaLoop',
    ],
  },
  {
    key: 'ribbon',
    label: '장식 · 문구',
    icon: 'lucide:layout',
    deviceTypes: ['FUNERAL_PORTRAIT', 'MULTIMEDIA'],
    drillIn: true,
  },
  {
    key: 'floorGuide',
    label: '층별 안내판',
    icon: 'lucide:layout-list',
    deviceTypes: ['ROOM_GUIDE'],
    fields: [
      'isFloorGuideEnabled', 'isRoomAssignmentVisible', 'isActiveRoomsOnly', 'floorGuideRefreshSec',
    ],
  },
  {
    key: 'kiosk',
    label: '입구 정보 · 키오스크',
    icon: 'lucide:door-open',
    deviceTypes: ['KIOSK', 'ENTRANCE_GUIDE'],
    fields: [
      'isTouchEnabled', 'isQrCodeVisible', 'isBuildingMapVisible',
      'isNoticeVisible', 'noticeScrollSpeed', 'entranceGreeting',
    ],
  },
  {
    key: 'remark',
    label: '비고',
    icon: 'lucide:file-text',
    fields: ['remark'],
  },
];

/** 이 섹션이 그 장비 유형에 해당하는가. */
export function isSectionRelevant(section: AttributeSection, deviceType?: string) {
  if (!section.deviceTypes) return true;
  return section.deviceTypes.includes(deviceType ?? '');
}

/** 유형에 해당하는 섹션이 앞, 무관한 섹션이 뒤. 각 무리 안의 순서는 위 정의를 따른다. */
export function sectionsForDevice(deviceType?: string) {
  const relevant: AttributeSection[] = [];
  const others: AttributeSection[] = [];
  for (const section of ATTRIBUTE_SECTIONS) {
    (isSectionRelevant(section, deviceType) ? relevant : others).push(section);
  }
  return { relevant, others };
}
