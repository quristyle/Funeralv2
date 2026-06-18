import { requestClient } from '#/api/request';

/**
 * 공통코드 그룹 정보
 */
export interface CommonCodeGroup {
  id: string;
  groupCode: string;
  groupName: string;
  isHierarchical: boolean;
  remark?: string;
  createdAt: string;
}

/**
 * 공통코드 정보
 */
export interface CommonCode {
  id: string;
  groupId: string;
  parentId?: string;
  codeValue: string;
  codeName: string;
  i18nKey?: string;
  sortOrder: number;
  level: number;
  isLeaf: boolean;
  status: number;
  remark?: string;
  children?: CommonCode[];
}

/**
 * 그룹 생성 파라미터
 */
export interface CommonCodeGroupParams {
  groupCode: string;
  groupName: string;
  isHierarchical: boolean;
  remark?: string;
}

/**
 * 코드 생성 파라미터
 */
export interface CommonCodeParams {
  groupId: string;
  parentId?: string;
  codeValue: string;
  codeName: string;
  i18nKey?: string;
  sortOrder: number;
  status: number;
  remark?: string;
}

/**
 * 공통코드 그룹 목록 조회
 */
export function getCommonCodeGroups() {
  return requestClient.get<CommonCodeGroup[]>('/auth/system/common-code/groups');
}

/**
 * 공통코드 그룹 생성
 */
export function createCommonCodeGroup(params: CommonCodeGroupParams) {
  return requestClient.post('/auth/system/common-code/groups', params);
}

/**
 * 특정 그룹의 코드 목록 조회
 * @param groupCode 그룹 코드
 * @param hierarchical 계층 구조 여부
 */
export function getCommonCodes(groupCode: string, hierarchical: boolean = false) {
  return requestClient.get<CommonCode[]>(`/auth/system/common-code/${groupCode}`, {
    params: { hierarchical },
  });
}

/**
 * 공통코드 생성
 */
export function createCommonCode(params: CommonCodeParams) {
  return requestClient.post('/auth/system/common-code', params);
}

/**
 * 공통코드 수정
 */
export function updateCommonCode(id: string, params: CommonCodeParams) {
  return requestClient.put(`/auth/system/common-code/${id}`, params);
}

/**
 * 공통코드 삭제
 */
export function deleteCommonCode(id: string) {
  return requestClient.delete(`/auth/system/common-code/${id}`);
}
