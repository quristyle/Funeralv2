import { requestClient } from '#/api/request';

export namespace BuildingAdminApi {
  export interface BuildingAdmin {
    id: string;
    buildingId: string;
    buildingName?: string;
    userId: string;
    userName: string;
    loginId: string;
    status: 'ACTIVE' | 'INACTIVE';
    phone?: string;
    remark?: string;
    createdAt: string;
  }
}

/**
 * 건물 관리자 목록 조회
 */
export async function getBuildingAdmins(buildingId?: string) {
  return requestClient.get<BuildingAdminApi.BuildingAdmin[]>('/system/building-admin/list', {
    params: { buildingId },
  });
}

/**
 * 건물 관리자 등록
 */
export async function createBuildingAdmin(data: Omit<BuildingAdminApi.BuildingAdmin, 'id' | 'createdAt'>) {
  return requestClient.post('/system/building-admin', data);
}

/**
 * 건물 관리자 정보 수정
 */
export async function updateBuildingAdmin(id: string, data: Partial<Omit<BuildingAdminApi.BuildingAdmin, 'id' | 'createdAt'>>) {
  return requestClient.put(`/system/building-admin/${id}`, data);
}

/**
 * 건물 관리자 삭제
 */
export async function deleteBuildingAdmin(id: string) {
  return requestClient.delete(`/system/building-admin/${id}`);
}
