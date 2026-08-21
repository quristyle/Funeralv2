import { requestClient } from '#/api/request';

export namespace BizSelectConfigApi {
  export interface BizSelectConfig {
    id: string;
    bizType: string;
    apiUrl: string;
    httpMethod: string;
    labelField: string;
    valueField: string;
    resultPath?: string;
    processorType?: string;
    remark?: string;
    createdAt?: string;
  }
}

/**
 * BizSelect 설정 전체 목록 조회
 */
export async function getBizSelectConfigs() {
  return requestClient.get<BizSelectConfigApi.BizSelectConfig[]>('/auth/system/biz-select/configs');
}

/**
 * BizSelect 설정 등록
 */
export async function createBizSelectConfig(data: Omit<BizSelectConfigApi.BizSelectConfig, 'id' | 'createdAt'>) {
  return requestClient.post<BizSelectConfigApi.BizSelectConfig>('/auth/system/biz-select/config', data);
}

/**
 * BizSelect 설정 수정
 */
export async function updateBizSelectConfig(id: string, data: Partial<Omit<BizSelectConfigApi.BizSelectConfig, 'id' | 'createdAt'>>) {
  return requestClient.put<boolean>(`/auth/system/biz-select/config/${id}`, data);
}

/**
 * BizSelect 설정 삭제
 */
export async function deleteBizSelectConfig(id: string) {
  return requestClient.delete<boolean>(`/auth/system/biz-select/config/${id}`);
}
