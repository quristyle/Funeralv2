import { requestClient } from '#/api/request';

/**
 * 게이트웨이 자체가 제공하는 API.
 *
 * 다른 API 들이 게이트웨이를 "거쳐" 각 마이크로서비스로 가는 것과 달리,
 * 이 엔드포인트는 게이트웨이에서 직접 처리된다.
 */
export namespace GatewayApi {
  /** 개별 서비스(목적지)의 상태 */
  export interface ServiceStatus {
    /** YARP 클러스터 ID (예: funeral-cluster) */
    cluster: string;
    /** 목적지 ID (예: destination1) */
    destination: string;
    /** 실제 주소 (예: http://localhost:5320) */
    address: string;
    /** UP: 정상 / DEGRADED: 응답은 하나 /health 가 비정상 / DOWN: 연결 불가 */
    status: 'DEGRADED' | 'DOWN' | 'UP';
    /** 프로브 응답 코드. 연결 실패 시 null */
    httpStatus: null | number;
    /** 프로브 왕복 시간(ms) */
    latencyMs: number;
    /** 실패 사유 */
    error: null | string;
  }

  export interface StatusResponse {
    gateway: {
      status: string;
      checkedAt: string;
    };
    services: ServiceStatus[];
  }
}

/**
 * 게이트웨이와 모든 마이크로서비스의 상태를 한 번에 조회한다.
 *
 * 게이트웨이가 각 목적지의 /health 를 직접 호출해 결과를 모아 준다.
 * 이 요청 자체가 실패하면 게이트웨이가 죽은 것으로 판단하면 된다.
 */
export async function getGatewayStatus() {
  return requestClient.get<GatewayApi.StatusResponse>('/gateway/status');
}
