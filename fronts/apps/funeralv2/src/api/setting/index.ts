import { requestClient } from '#/api/request';

export namespace SettingApi {
  export interface EnvironmentSetting {
    key: string;
    value: string;
    groupName: string; // SYSTEM, NOTIFICATION, SECURITY 등
    description?: string;
    updatedAt: string;
  }
}

/**
 * 환경 설정 전체 목록 조회
 */
export async function getEnvironmentSettings() {
  return requestClient.get<SettingApi.EnvironmentSetting[]>('/setting/environment/list');
}

/**
 * 환경 설정 키 값 수정
 */
export async function updateEnvironmentSetting(key: string, value: string) {
  return requestClient.put(`/setting/environment/${key}`, { value });
}
