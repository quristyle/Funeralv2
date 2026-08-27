import { currentAiModel, currentAiProvider } from '#/api/portal/ai/provider';
import { requestClient } from '#/api/request';

/**
 * 공통코드 그룹 정보
 */
export interface CommonCodeGroup {
  id: string;
  groupCode: string;
  groupName: string;
  isHierarchical: boolean;
  sortOrder: number;
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
  sortOrder: number;
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
 * 공통코드 그룹 수정
 */
export function updateCommonCodeGroup(id: string, params: CommonCodeGroupParams) {
  return requestClient.put(`/auth/system/common-code/groups/${id}`, params);
}

/**
 * 공통코드 그룹 삭제
 */
export function deleteCommonCodeGroup(id: string) {
  return requestClient.delete(`/auth/system/common-code/groups/${id}`);
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

/**
 * AI 기반 공통코드 영문 추천
 * @param word 한글 명칭
 */
export function suggestCommonCodeByAI(word: string, natural: boolean = false) {
  // Gateway의 YARP 룰에 따라 /api/ai/ai/suggest-code 형태로 호출되거나, 
  // ApiGateway 설정이 /api/ai -> / 경로로 매핑되므로 /auth/ai 가 아니라 바로 게이트웨이를 바라보는 /ai 로 호출
  // 프로젝트 프록시 설정에 따라 다를 수 있으나, 기존 auth 호출처럼 prefix를 맞춥니다.
  // provider — 사용자가 환경설정에서 고른 AI 모델(#/api/portal/ai/provider.ts).
  return requestClient.get<unknown>('/ai/suggest-code', {
    params: { word, natural, provider: currentAiProvider(), model: currentAiModel() },
  });
}
