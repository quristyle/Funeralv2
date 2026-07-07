import { requestClient } from '#/api/request';

export namespace BuildingApi {
  export interface Building {
    id: string;
    companyId: string;
    name: string;
    shortName?: string;
    abbreviation?: string;
    address?: string;
    zipCode?: string;
    addressDetail?: string;
    remark?: string;
    buildingPhotoGroupId?: string;
    parkingPhotoGroupId?: string;
    createdAt: string;
  }

  export interface Floor {
    id: string;
    buildingId: string;
    buildingName?: string;
    name: string;
    sortOrder: number;
    remark?: string;
  }

  export interface Room {
    id: string;
    buildingId?: string;
    floorId?: string;
    floorName?: string;
    name: string;
    shortName?: string;
    roomType: string; // 빈소, 안치실, 참관실 등
    status: 'ACTIVE' | 'INACTIVE';
    remark?: string;
  }

  export interface Device {
    id: string;
    name: string;
    shortName?: string;
    code: string;
    deviceType: string; // DID, 키오스크, 현판 등
    ipAddress?: string;
    macAddress?: string;
    status: 'ONLINE' | 'OFFLINE' | 'UNKNOWN';
    sortOrder: number;
    companyId?: string;
    buildingId?: string;
    floorId?: string;
    roomId?: string;
    roomName?: string;
    buildingShortName?: string;
    floorShortName?: string;
    roomShortName?: string;
    videoId?: string;
    musicId?: string;
    isVideoEnabled?: boolean;
    isMusicEnabled?: boolean;
    videoName?: string;
    musicName?: string;
  }

  export interface DeviceConfig {
    id: string;
    deviceId: string;
    deviceName?: string;
    volume: number;
    brightness: number;
    rebootTime?: string;
    isAutoPower: boolean;
    powerOnTime?: string;
    powerOffTime?: string;
  }

  export interface MediaSource {
    id: string;
    name: string;
    sourceType: 'VIDEO' | 'AUDIO' | 'IMAGE';
    url: string;
    fileSize?: number;
    remark?: string;
    shortName?: string;
    sortOrder?: number;
    thumbnailUrl?: string;
    thumbnailFileId?: string | null;
    originalFileId?: string | null;
  }

  export interface Deceased {
    id: string;
    name: string;
    gender: 'MALE' | 'FEMALE';
    age: number;
    religion?: string;
    deathDate: string;
    funeralDate?: string;
    burialDate?: string;
    roomId?: string;
    roomName?: string;
    status: 'IN_HOSPITAL' | 'DISCHARGED' | 'COMPLETED' | 'FUNERAL_DEPARTURE_COMPLETED';
  }

  export interface DeceasedMourner {
    id?: string;
    name: string;
    relation: string;
    contact: string;
    email?: string;
    address?: string;
    isChief: boolean;
    sortOrder: number;
  }

  export interface DeceasedContractor {
    name: string;
    contact: string;
    relation?: string;
    address?: string;
    remark?: string;
    signatureFileId?: string;
  }

  export interface DeceasedManager {
    directorName?: string;
    directorContact?: string;
    mutualAidCompany?: string;
    staffName?: string;
    staffContact?: string;
  }

  export interface DeceasedFacility {
    id?: string;
    facilityType: string;
    startTime?: string;
    endTime?: string;
    useHours: number;
    unitPrice: number;
    totalPrice: number;
    remark?: string;
  }

  export interface DeceasedRoom {
    id?: string;
    roomId: string;
    roomName?: string;
    startTime: string;
    endTime?: string;
  }

  export interface DeceasedDetail {
    id: string;
    name: string;
    gender: 'MALE' | 'FEMALE';
    age: number;
    religion?: string;
    deathDate: string;
    funeralDate?: string;
    burialDate?: string;
    roomId?: string;
    roomName?: string;
    status: 'IN_HOSPITAL' | 'DISCHARGED' | 'COMPLETED' | 'FUNERAL_DEPARTURE_COMPLETED';
    remark?: string;
    ssn?: string;
    causeOfDeath?: string;
    burialPlot?: string;
    memorialPhotoUrl?: string;
    memorialPhotoFileId?: string;
    memorialEditedPhotoUrl?: string;
    memorialEditedPhotoFileId?: string;
    familyPhotoGroupId?: string;
    chiefMourner?: string;

    mourners: DeceasedMourner[];
    contractor?: DeceasedContractor;
    manager?: DeceasedManager;
    facilities: DeceasedFacility[];
    rooms: DeceasedRoom[];
  }

  export interface DeviceAttribute {
    id: string;
    deviceId: string;
    // 공통 표시 설정
    displayOrientation: 'LANDSCAPE' | 'PORTRAIT';
    portraitOrientation: 'HORIZONTAL' | 'VERTICAL_LEFT' | 'VERTICAL_RIGHT' | 'INVERTED';
    videoOrientation: 'HORIZONTAL' | 'VERTICAL';
    displayPaddingTop: number;
    displayPaddingLeft: number;
    displayPaddingRight: number;
    displayPaddingBottom: number;
    contentIntervalSec: number;
    isScreensaverEnabled: boolean;
    screensaverTimeoutSec: number;
    // 영정사진/추모 콘텐츠 설정
    isMemorialPhotoEnabled: boolean;
    memorialPhotoEffect: 'FADE' | 'SLIDE' | 'NONE';
    /** 사진 세로 정렬: 상단(기본값) / 중앙 / 하단 */
    photoVerticalAlignment: 'TOP' | 'CENTER' | 'BOTTOM';
    /** 사진 가로 정렬: 좌측 / 중앙(기본값) / 우측 */
    photoHorizontalAlignment: 'LEFT' | 'CENTER' | 'RIGHT';
    isDeceasedNameVisible: boolean;
    isFamilyContactVisible: boolean;
    memorialPaddingTop: number;
    memorialPaddingLeft: number;
    memorialPaddingRight: number;
    memorialPaddingBottom: number;
    // 멀티미디어 콘텐츠 설정
    isVideoEnabled: boolean;
    isMusicEnabled: boolean;
    videoId: string | null;
    musicId: string | null;
    musicVolume: number | null;
    isMediaLoop: boolean;
    isMuted: boolean;
    // 층별 안내 설정
    isFloorGuideEnabled: boolean;
    isRoomAssignmentVisible: boolean;
    isActiveRoomsOnly: boolean;
    floorGuideRefreshSec: number;
    // 입구 정보/키오스크 설정
    isTouchEnabled: boolean;
    isQrCodeVisible: boolean;
    isBuildingMapVisible: boolean;
    entranceGreeting: string | null;
    isNoticeVisible: boolean;
    noticeScrollSpeed: number;
    remark: string | null;
  }

  export interface DeviceRibbon {
    id: string;
    deviceId: string;
    mediaSourceId: string;
    // 장식 이미지 정보 (조인)
    mediaSourceName?: string;
    mediaSourceUrl?: string;
    mediaSourceThumbnailUrl?: string;
    // 위치 및 크기 (%, 소수점 3자리)
    positionLeft: number;
    positionTop: number;
    width: number;
    height: number;
    sortOrder: number;
    remark?: string;
  }

  export interface DeviceRibbonUpsert {
    deviceId: string;
    mediaSourceId: string;
    positionLeft: number;
    positionTop: number;
    width: number;
    height: number;
    sortOrder: number;
    remark?: string;
  }

  export interface DeviceRibbonBulkSave {
    deviceId: string;
    ribbons: DeviceRibbonUpsert[];
  }

  // === 텍스트 오버레이 ===
  export interface DeviceTextOverlay {
    id: string;
    deviceId: string;
    textContent: string;
    fontSize: number;
    fontColor: string;
    backgroundColor: string;
    textAlign: 'left' | 'center' | 'right';
    fontWeight: 'normal' | 'bold';
    positionLeft: number;
    positionTop: number;
    width: number;
    height: number;
    sortOrder: number;
    remark?: string;
  }

  export interface DeviceTextOverlayUpsert {
    deviceId: string;
    textContent: string;
    fontSize: number;
    fontColor: string;
    backgroundColor: string;
    textAlign: 'left' | 'center' | 'right';
    fontWeight: 'normal' | 'bold';
    positionLeft: number;
    positionTop: number;
    width: number;
    height: number;
    sortOrder: number;
    remark?: string;
  }

  export interface DeviceTextOverlayBulkSave {
    deviceId: string;
    overlays: DeviceTextOverlayUpsert[];
  }
}

// === 건물 API ===
export async function getBuildings(companyId?: string) {
  return requestClient.get<BuildingApi.Building[]>('/funeral/building/info/list', { params: { companyId } });
}
export async function createBuilding(data: Omit<BuildingApi.Building, 'id' | 'createdAt'>) {
  return requestClient.post('/funeral/building/info', data);
}
export async function updateBuilding(id: string, data: Omit<BuildingApi.Building, 'id' | 'createdAt'>) {
  return requestClient.put(`/funeral/building/info/${id}`, data);
}
export async function deleteBuilding(id: string) {
  return requestClient.delete(`/funeral/building/info/${id}`);
}

// === 층 API ===
export async function getFloors(buildingId?: string) {
  return requestClient.get<BuildingApi.Floor[]>('/funeral/building/floor/list', { params: { buildingId } });
}
export async function createFloor(data: Omit<BuildingApi.Floor, 'id'>) {
  return requestClient.post('/funeral/building/floor', data);
}
export async function updateFloor(id: string, data: Omit<BuildingApi.Floor, 'id'>) {
  return requestClient.put(`/funeral/building/floor/${id}`, data);
}
export async function deleteFloor(id: string) {
  return requestClient.delete(`/funeral/building/floor/${id}`);
}

// === 호실 API ===
export async function getRooms(params?: {
  companyId?: string;
  buildingId?: string;
  floorId?: string;
}) {
  return requestClient.get<BuildingApi.Room[]>('/funeral/building/room/list', {
    params,
  });
}
export async function createRoom(data: Omit<BuildingApi.Room, 'id'>) {
  return requestClient.post('/funeral/building/room', data);
}
export async function updateRoom(id: string, data: Omit<BuildingApi.Room, 'id'>) {
  return requestClient.put(`/funeral/building/room/${id}`, data);
}
export async function deleteRoom(id: string) {
  return requestClient.delete(`/funeral/building/room/${id}`);
}

// === 장비 API ===
export async function getDevices(params?: {
  companyId?: string;
  buildingId?: string;
  floorId?: string;
  roomId?: string;
}) {
  return requestClient.get<BuildingApi.Device[]>('/funeral/building/device/list', { params });
}
export async function getDevice(id: string) {
  return requestClient.get<BuildingApi.Device>(`/funeral/building/device/${id}`);
}
export async function createDevice(data: Omit<BuildingApi.Device, 'id'>) {
  return requestClient.post('/funeral/building/device', data);
}
export async function updateDevice(id: string, data: Partial<BuildingApi.Device>) {
  return requestClient.put<BuildingApi.Device>(`/funeral/building/device/${id}`, data);
}
export async function deleteDevice(id: string) {
  return requestClient.delete(`/funeral/building/device/${id}`);
}

// === 장비설정 API ===
export async function getDeviceConfigs(params?: { deviceId?: string }) {
  return requestClient.get<BuildingApi.DeviceConfig[]>('/funeral/building/device-config/list', { params });
}
export async function getDeviceConfig(deviceId: string) {
  return requestClient.get<BuildingApi.DeviceConfig>(`/funeral/building/device-config/${deviceId}`);
}
export async function upsertDeviceConfig(data: Omit<BuildingApi.DeviceConfig, 'id' | 'deviceName'>) {
  return requestClient.put<BuildingApi.DeviceConfig>('/funeral/building/device-config/', data);
}
export async function updateDeviceConfig(id: string, data: Omit<BuildingApi.DeviceConfig, 'id' | 'deviceName'>) {
  return requestClient.put(`/funeral/building/device-config/${id}`, data);
}

// === 미디어 소스/영상/음원 API ===
export async function getMediaSources(type?: 'VIDEO' | 'AUDIO' | 'IMAGE') {
  return requestClient.get<BuildingApi.MediaSource[]>('/funeral/building/source/list', { params: { type } });
}
export async function createMediaSource(data: Omit<BuildingApi.MediaSource, 'id'>) {
  return requestClient.post('/funeral/building/source', data);
}
export async function updateMediaSource(id: string, data: Omit<BuildingApi.MediaSource, 'id'>) {
  return requestClient.put(`/funeral/building/source/${id}`, data);
}
export async function deleteMediaSource(id: string) {
  return requestClient.delete(`/funeral/building/source/${id}`);
}

export async function retryThumbnail(id: string) {
  return requestClient.post(`/funeral/building/source/${id}/retry/thumbnail`);
}

export async function retryWebm(id: string) {
  return requestClient.post(`/funeral/building/source/${id}/retry/webm`);
}

export async function retryAudio(id: string) {
  return requestClient.post(`/funeral/building/source/${id}/retry/audio`);
}

// === 고인 API ===
export async function getDeceasedList(params?: Record<string, any>) {
  return requestClient.get<BuildingApi.Deceased[]>('/funeral/building/deceased/list', {
    params
  });
}
export async function createDeceased(data: Omit<BuildingApi.Deceased, 'id'>) {
  return requestClient.post('/funeral/building/deceased', data);
}
export async function updateDeceased(id: string, data: Omit<BuildingApi.Deceased, 'id'>) {
  return requestClient.put(`/funeral/building/deceased/${id}`, data);
}
export async function deleteDeceased(id: string) {
  return requestClient.delete(`/funeral/building/deceased/${id}`);
}
export async function getDeceasedDetail(id: string) {
  return requestClient.get<BuildingApi.DeceasedDetail>(`/funeral/building/deceased/${id}/detail`);
}
export async function saveDeceasedDetail(id: string, data: BuildingApi.DeceasedDetail) {
  const url = id ? `/funeral/building/deceased/${id}/detail` : '/funeral/building/deceased/detail';
  return requestClient.put<BuildingApi.DeceasedDetail>(url, data);
}
export async function cancelDeceasedDeparture(id: string) {
  return requestClient.put<boolean>(`/funeral/building/deceased/${id}/cancel-departure`);
}

// === 장비 속성 API ===
export async function getDeviceAttribute(deviceId: string) {
  return requestClient.get<BuildingApi.DeviceAttribute>(`/funeral/building/device-attribute/${deviceId}`);
}
export async function upsertDeviceAttribute(data: Omit<BuildingApi.DeviceAttribute, 'id'>) {
  return requestClient.put<BuildingApi.DeviceAttribute>('/funeral/building/device-attribute/', data);
}
export async function deleteDeviceAttribute(deviceId: string) {
  return requestClient.delete(`/funeral/building/device-attribute/${deviceId}`);
}

// === 장비 리본 설정 API ===
export async function getDeviceRibbons(deviceId: string) {
  return requestClient.get<BuildingApi.DeviceRibbon[]>(`/funeral/building/device-ribbon/by-device/${deviceId}`);
}
export async function createDeviceRibbon(data: BuildingApi.DeviceRibbonUpsert) {
  return requestClient.post<BuildingApi.DeviceRibbon>('/funeral/building/device-ribbon/', data);
}
export async function updateDeviceRibbon(id: string, data: BuildingApi.DeviceRibbonUpsert) {
  return requestClient.put<BuildingApi.DeviceRibbon>(`/funeral/building/device-ribbon/${id}`, data);
}
export async function deleteDeviceRibbon(id: string) {
  return requestClient.delete(`/funeral/building/device-ribbon/${id}`);
}
export async function bulkSaveDeviceRibbons(data: BuildingApi.DeviceRibbonBulkSave) {
  return requestClient.put<BuildingApi.DeviceRibbon[]>('/funeral/building/device-ribbon/bulk-save', data);
}

// === 텍스트 오버레이 API ===
export async function getDeviceTextOverlays(deviceId: string) {
  return requestClient.get<BuildingApi.DeviceTextOverlay[]>(`/funeral/building/device-text-overlay/by-device/${deviceId}`);
}
export async function bulkSaveDeviceTextOverlays(data: BuildingApi.DeviceTextOverlayBulkSave) {
  return requestClient.put<BuildingApi.DeviceTextOverlay[]>('/funeral/building/device-text-overlay/bulk-save', data);
}
export async function deleteDeviceTextOverlay(id: string) {
  return requestClient.delete(`/funeral/building/device-text-overlay/${id}`);
}
