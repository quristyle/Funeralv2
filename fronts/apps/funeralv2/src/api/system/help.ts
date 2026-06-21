import { requestClient } from '#/api/request';

export namespace SystemHelpApi {
  export interface HelpDoc {
    id: string;
    title: string;
    category: string; // SYSTEM, BUILDING, BILLING 등
    content: string;
    sortOrder: number;
    status: 'PUBLISHED' | 'DRAFT';
    updatedAt: string;
  }
}

/**
 * 도움말 목록 조회
 */
export async function getHelpDocs() {
  return requestClient.get<SystemHelpApi.HelpDoc[]>('/system/help/list');
}

/**
 * 도움말 생성
 */
export async function createHelpDoc(data: Omit<SystemHelpApi.HelpDoc, 'id' | 'updatedAt'>) {
  return requestClient.post('/system/help', data);
}

/**
 * 도움말 수정
 */
export async function updateHelpDoc(id: string, data: Omit<SystemHelpApi.HelpDoc, 'id' | 'updatedAt'>) {
  return requestClient.put(`/system/help/${id}`, data);
}

/**
 * 도움말 삭제
 */
export async function deleteHelpDoc(id: string) {
  return requestClient.delete(`/system/help/${id}`);
}
