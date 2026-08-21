import { requestClient } from '#/api/request';

export namespace CompanyAdminApi {
  export interface CompanyAdmin {
    id: string;
    companyId: string;
    companyName?: string;
    userId: string;
    userName: string;
    loginId: string;
    status: 'ACTIVE' | 'INACTIVE';
    email?: string;
    phone?: string;
    remark?: string;
    createdAt: string;
  }
}

/**
 * 회사 관리자 목록 조회
 */
export async function getCompanyAdmins(companyId?: string) {
  return requestClient.get<CompanyAdminApi.CompanyAdmin[]>('/system/company-admin/list', {
    params: { companyId },
  });
}

/**
 * 회사 관리자 등록
 */
export async function createCompanyAdmin(data: Omit<CompanyAdminApi.CompanyAdmin, 'id' | 'createdAt'>) {
  return requestClient.post('/system/company-admin', data);
}

/**
 * 회사 관리자 정보 수정
 */
export async function updateCompanyAdmin(id: string, data: Partial<Omit<CompanyAdminApi.CompanyAdmin, 'id' | 'createdAt'>>) {
  return requestClient.put(`/system/company-admin/${id}`, data);
}

/**
 * 회사 관리자 해제/삭제
 */
export async function deleteCompanyAdmin(id: string) {
  return requestClient.delete(`/system/company-admin/${id}`);
}
