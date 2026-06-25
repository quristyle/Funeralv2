import { requestClient } from '#/api/request';

export namespace BuildingApi {
  export interface Building {
    id: string;
    companyId: string;
    name: string;
    shortName?: string;
    address?: string;
    zipCode?: string;
    addressDetail?: string;
    remark?: string;
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
    buildingId: string;
    floorId: string;
    floorName?: string;
    name: string;
    roomType: string; // 빈소, 안치실, 참관실 등
    status: 'ACTIVE' | 'INACTIVE';
    remark?: string;
  }

  export interface Device {
    id: string;
    name: string;
    code: string;
    deviceType: string; // DID, 키오스크, 현판 등
    ipAddress?: string;
    macAddress?: string;
    status: 'ONLINE' | 'OFFLINE' | 'UNKNOWN';
    roomId?: string;
    roomName?: string;
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
    status: 'IN_HOSPITAL' | 'DISCHARGED' | 'COMPLETED';
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
export async function createDevice(data: Omit<BuildingApi.Device, 'id'>) {
  return requestClient.post('/funeral/building/device', data);
}
export async function updateDevice(id: string, data: Omit<BuildingApi.Device, 'id'>) {
  return requestClient.put(`/funeral/building/device/${id}`, data);
}
export async function deleteDevice(id: string) {
  return requestClient.delete(`/funeral/building/device/${id}`);
}

// === 장비설정 API ===
export async function getDeviceConfigs(params?: { deviceId?: string }) {
  return requestClient.get<BuildingApi.DeviceConfig[]>('/building/device-config/list', { params });
}
export async function updateDeviceConfig(id: string, data: Partial<BuildingApi.DeviceConfig>) {
  return requestClient.put(`/building/device-config/${id}`, data);
}

// === 미디어 소스/영상/음원 API ===
export async function getMediaSources(type?: 'VIDEO' | 'AUDIO' | 'IMAGE') {
  return requestClient.get<BuildingApi.MediaSource[]>('/building/source/list', { params: { type } });
}
export async function createMediaSource(data: Omit<BuildingApi.MediaSource, 'id'>) {
  return requestClient.post('/building/source', data);
}
export async function deleteMediaSource(id: string) {
  return requestClient.delete(`/building/source/${id}`);
}

// === 고인 API ===
export async function getDeceasedList() {
  return requestClient.get<BuildingApi.Deceased[]>('/building/deceased/list');
}
export async function createDeceased(data: Omit<BuildingApi.Deceased, 'id'>) {
  return requestClient.post('/building/deceased', data);
}
export async function updateDeceased(id: string, data: Omit<BuildingApi.Deceased, 'id'>) {
  return requestClient.put(`/building/deceased/${id}`, data);
}
export async function deleteDeceased(id: string) {
  return requestClient.delete(`/building/deceased/${id}`);
}
