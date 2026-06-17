import { requestClient } from '#/api/request';

export namespace SystemCompanyApi {
  export interface SystemCompany {
    id: string;
    name: string;
    businessNumber?: string;
    representative?: string;
    status: number;
    remark?: string;
    createdAt: string;
  }

  export interface CreateParams {
    name: string;
    businessNumber?: string;
    representative?: string;
    status: number;
    remark?: string;
  }
}

/**
 * 회사 목록 조회
 */
async function getCompanyList() {
  return requestClient.get<SystemCompanyApi.SystemCompany[]>('/auth/system/companies');
}

/**
 * 회사 등록
 */
async function createCompany(params: SystemCompanyApi.CreateParams) {
  return requestClient.post('/auth/system/companies', params);
}

/**
 * 회사 수정
 */
async function updateCompany(id: string, params: SystemCompanyApi.CreateParams) {
  return requestClient.put(`/auth/system/companies/${id}`, params);
}

/**
 * 회사 삭제
 */
async function deleteCompany(id: string) {
  return requestClient.delete(`/auth/system/companies/${id}`);
}

export {  
  SystemCompanyApi,
  createCompany,
  deleteCompany,
  getCompanyList,
  updateCompany,
};
