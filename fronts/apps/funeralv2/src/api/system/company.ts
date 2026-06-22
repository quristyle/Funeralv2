import { requestClient } from '#/api/request';

/**
 * 시스템 회사(System Company) 관련 API 및 타입 정의
 */
export namespace SystemCompanyApi {
  /**
   * 회사 정보 엔티티 타입
   */
  export interface SystemCompany {
    /** 회사 고유 ID (UUID) */
    id: string;
    /** 회사명 */
    name: string;
    /** 사업자 등록 번호 */
    businessNumber?: string;
    /** 대표자 성함 */
    representative?: string;
    /** 상태 (1: 활성, 0: 비활성) */
    status: number;
    /** 비고/설명 */
    remark?: string;
    /** 생성 일시 */
    createdAt: string;
  }

  /**
   * 회사 등록/수정 요청 파라미터
   */
  export interface CreateParams {
    /** 회사명 */
    name: string;
    /** 사업자 등록 번호 */
    businessNumber?: string;
    /** 대표자 성함 */
    representative?: string;
    /** 상태 (1: 활성, 0: 비활성) */
    status: number;
    /** 비고/설명 */
    remark?: string;
  }

  /**
   * 페이징된 결과 응답 타입 (백엔드 PagedResult와 일치)
   */
  export interface PagedResult<T> {
    /** 데이터 목록 */
    items: T[];
    /** 전체 데이터 건수 */
    total: number;
  }
}

/**
 * 회사 목록 조회 (페이징 지원)
 * AuthServer의 /system/companies 엔드포인트 호출
 */
async function getCompanyList() {
  // 백엔드 ApiResponse<PagedResult<CompanyDto>> 반환 구조에 따라 
  // requestClient가 data 필드(PagedResult)를 반환합니다.
  return requestClient.get<SystemCompanyApi.PagedResult<SystemCompanyApi.SystemCompany>>('/auth/system/companies');
}

/**
 * 새로운 회사 정보를 등록합니다.
 * @param params 등록할 회사 정보
 */
async function createCompany(params: SystemCompanyApi.CreateParams) {
  return requestClient.post('/auth/system/companies', params);
}

/**
 * 기존 회사 정보를 수정합니다.
 * @param id 수정할 회사의 고유 ID
 * @param params 수정할 데이터
 */
async function updateCompany(id: string, params: SystemCompanyApi.CreateParams) {
  return requestClient.put(`/auth/system/companies/${id}`, params);
}

/**
 * 회사를 삭제합니다.
 * @param id 삭제할 회사의 고유 ID
 */
async function deleteCompany(id: string) {
  return requestClient.delete(`/auth/system/companies/${id}`);
}

export {  
  createCompany,
  deleteCompany,
  getCompanyList,
  updateCompany,
};
