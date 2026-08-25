import { requestClient } from '#/api/request';

export namespace BizSelectConfigApi {
  export interface BizSelectConfig {
    id: string;
    bizType: string;
    /**
     * 호출 대상 MSA — auth · funeral · helpdesk · projmng · file · ai.
     * 게이트웨이 프리픽스이면서 동시에 응답 봉투를 벗길 요청 클라이언트를 고르는 키다.
     */
    serviceCode: string;
    /** MSA 프리픽스를 뺀 서비스 내부 경로 */
    apiUrl: string;
    httpMethod: string;
    labelField: string;
    valueField: string;
    resultPath?: string;
    processorType?: string;
    /** 호출 시 항상 함께 보내는 고정 파라미터 (JSON 객체 문자열) */
    staticParams?: string;
    /** 런타임 파라미터를 넣을 본문 내 경로 (점 표기). 비면 최상위 */
    paramPath?: string;
    remark?: string;
    createdAt?: string;
  }
}

/** 메타데이터에서 고를 수 있는 MSA 목록. 게이트웨이 라우트 프리픽스와 같다. */
export const BIZ_SELECT_SERVICES = [
  { label: '포털/인증 (auth)', value: 'auth' },
  { label: '장례식장 (funeral)', value: 'funeral' },
  { label: '헬프데스크 (helpdesk)', value: 'helpdesk' },
  { label: '프로젝트관리 (projmng)', value: 'projmng' },
  { label: '파일 (file)', value: 'file' },
  { label: 'AI (ai)', value: 'ai' },
] as const;

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
