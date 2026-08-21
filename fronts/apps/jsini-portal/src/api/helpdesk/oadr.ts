/**
 * 한주(고객사) OADR 시스템 API.
 *
 * 설비 모니터링·리포트 화면들이 읽는 외부 시스템이다. 헬프데스크 백엔드를 거치지 않고
 * 게이트웨이의 `/api/oadr` 라우트가 `https://nums.hanjucorp.co.kr/oadr` 로 중계한다.
 * (브라우저에서 직접 호출하면 CORS 에 막힌다)
 */
import type { RequestClientOptions } from '@vben/request';

import { useAppConfig } from '@vben/hooks';
import { RequestClient } from '@vben/request';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

/** 저장 프로시저 실행 파라미터 */
export interface OadrProcedureParameter {
  name: string;
  value: boolean | number | string;
}

function createOadrClient(options?: RequestClientOptions) {
  const client = new RequestClient({
    ...options,
    baseURL: `${apiURL}/oadr`,
  });

  // 외부 시스템이라 봉투 규약이 없다. 본문을 그대로 돌려준다.
  client.addResponseInterceptor({
    fulfilled: (response) => response.data,
  });

  return client;
}

const oadrClient = createOadrClient();

/**
 * OADR 저장 프로시저를 실행하고 결과를 돌려준다.
 * 리포트 화면 대부분이 이 하나의 엔드포인트에 QueryType 만 바꿔 호출한다.
 */
export async function executeProcedure<T = any>(
  procedureName: string,
  parameters: OadrProcedureParameter[] = [],
): Promise<T> {
  return oadrClient.post<T>('/api/procedure/execute', {
    parameters,
    procedureName,
  });
}

/** 서버 리포트 조회 — QueryType 만 다른 P_QURI_SERVER_REPORT 호출을 감싼다. */
export async function getServerReport<T = any>(queryType: string): Promise<T> {
  return executeProcedure<T>('P_QURI_SERVER_REPORT', [
    { name: '@QueryType', value: queryType },
  ]);
}

/** OADR 헬스체크 */
export async function getOadrHealth() {
  return oadrClient.get<any>('/health');
}

/** 임의 경로 GET — 화면별 개별 엔드포인트용 */
export async function oadrGet<T = any>(
  path: string,
  params?: Record<string, any>,
): Promise<T> {
  return oadrClient.get<T>(path, { params });
}

/** 임의 경로 POST */
export async function oadrPost<T = any>(
  path: string,
  data?: Record<string, any>,
): Promise<T> {
  return oadrClient.post<T>(path, data);
}
