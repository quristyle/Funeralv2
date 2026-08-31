import { requestClient } from '#/api/request';
import type { SystemRolePermissionApi } from './role-permission';

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
    /** 짧은명칭 */
    shortName?: string;
    /** 우편번호 */
    zipCode?: string;
    /** 주소 */
    address?: string;
    /** 상세주소 */
    addressDetail?: string;
    /** 승인일 */
    approvalDate?: string;
    /** 정렬 순서 */
    sortOrder: number;
    /** 생성 일시 */
    createdAt: string;
    /**
     * 이 회사에 소속된 사용자 수.
     * 목록을 받을 때 함께 온다 — 회사마다 따로 물어보지 않는다.
     */
    userCount?: number;
    /** 이 회사에 등록된 부서 수 */
    deptCount?: number;
    /**
     * 회사 사용처 — 이 회사가 쓰이는 시스템들.
     *
     * 공통코드 그룹 `COMPANY_USAGE_LOCATION` 의 `codeValue` 목록이다.
     * 여러 개일 수 있고 **빈 목록일 수도 있다**.
     */
    usageLocations?: string[];
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
    /** 짧은명칭 */
    shortName?: string;
    /** 우편번호 */
    zipCode?: string;
    /** 주소 */
    address?: string;
    /** 상세주소 */
    addressDetail?: string;
    /** 승인일 */
    approvalDate?: string;
    /** 정렬 순서 */
    sortOrder?: number;
    /**
     * 회사 사용처 (`COMPANY_USAGE_LOCATION` 의 `codeValue` 목록).
     *
     * **안 보내면 서버가 사용처를 건드리지 않는다.** 목록 화면의 셀 편집처럼
     * 일부 항목만 보내는 호출이 사용처를 지우지 않게 하려는 것이다.
     * 빈 배열(`[]`)을 보내면 '전부 해제' 다.
     */
    usageLocations?: string[];
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
/**
 * 장례식장 관리시스템의 사용처 코드.
 *
 * 회사 관리의 '사용처'(공통코드 `COMPANY_USAGE_LOCATION`)에 이 코드가 붙은 회사만
 * 장례식장 화면에 나와야 한다. 셀렉트는 `BizSelect type="funeralCompany"` 가 맡고
 * (설정은 `scom.biz_select_configs` 에 있다), 목록을 직접 부르는 화면만 이 상수를 쓴다.
 */
export const FUNERAL_USAGE_LOCATION = 'FUNERAL_HOME_MANAGEMENT_SYSTEM';

/**
 * 회사 목록.
 *
 * @param usageLocation 사용처로 좁힌다(`COMPANY_USAGE_LOCATION` 의 코드값).
 *                      비우면 전부 받는다 — 포털의 회사·조직 화면이 그렇게 쓴다.
 */
async function getCompanyList(usageLocation?: string) {
  // 백엔드 ApiResponse<PagedResult<CompanyDto>> 반환 구조에 따라 
  // requestClient가 data 필드(PagedResult)를 반환합니다.
  return requestClient.get<SystemCompanyApi.PagedResult<SystemCompanyApi.SystemCompany>>(
    '/auth/system/companies',
    usageLocation ? { params: { usageLocation } } : undefined,
  );
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

/**
 * 특정 회사 소속 사용자 목록 조회
 */
async function getCompanyUsers(companyId: string) {
  return requestClient.get<SystemRolePermissionApi.RoleUser[]>(
    `/auth/system/companies/${companyId}/users`
  );
}

/**
 * 소속 회사가 없는 사용자 목록 조회
 */
async function getEligibleCompanyUsers() {
  return requestClient.get<SystemRolePermissionApi.RoleUser[]>(
    `/auth/system/companies/eligible-users`
  );
}

/**
 * 사용자들을 회사에 할당
 */
async function assignCompanyUsers(companyId: string, userIds: string[]) {
  return requestClient.post(`/auth/system/companies/${companyId}/users`, userIds);
}

/**
 * 사용자의 회사 소속 해제
 */
async function removeCompanyUsers(userIds: string[]) {
  return requestClient.post('/auth/system/companies/users/remove', userIds);
}

export {  
  createCompany,
  deleteCompany,
  getCompanyList,
  updateCompany,
  getCompanyUsers,
  getEligibleCompanyUsers,
  assignCompanyUsers,
  removeCompanyUsers,
};
