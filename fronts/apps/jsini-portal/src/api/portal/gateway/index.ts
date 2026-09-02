import { unwrapOne } from '#/api/envelope';
import { requestClient } from '#/api/request';

/**
 * 게이트웨이 자체가 제공하는 API.
 *
 * 다른 API 들이 게이트웨이를 "거쳐" 각 마이크로서비스로 가는 것과 달리,
 * 이 엔드포인트는 게이트웨이에서 직접 처리된다.
 */
export namespace GatewayApi {
  /** 개별 서비스(목적지)의 상태 */
  /**
   * 서비스가 딸린 것 하나를 점검한 결과.
   *
   * 서비스가 스스로 점검해 `/health` 본문에 담아 보낸다
   * (`JSini.Shared.Infrastructure/HealthChecks`). 게이트웨이는 읽어 올리기만 한다 —
   * LLM 주소·모델명은 그 서비스의 설정이라 게이트웨이가 알 필요가 없다.
   */
  export interface DependencyStatus {
    /** 점검 이름 (예: `llm`, `database`, `release-queue`) */
    name: string;
    /**
     * `Healthy` 정상 / `Degraded` 딸린 것이 죽어 제 일을 못 한다 /
     * `Unhealthy` 서비스 자체가 처리 불가
     */
    status: string;
    /** 사람이 읽을 설명. 그대로 보여 준다. */
    description?: null | string;
    /** 점검에 걸린 시간(ms) */
    durationMs?: number;
    /** 주소·모델명·응답시간 등 곁들이는 값. 비밀은 담기지 않는다. */
    data?: null | Record<string, any>;
  }

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
    /**
     * 왜 이 상태인지 한 줄.
     *
     * 서비스가 스스로 보고한 설명이다 (예: "LLM 장비가 3초 안에 응답하지 않습니다").
     * 상태만 보여 주면 "왜?" 를 알 수 없어 결국 서버에 들어가 봐야 한다.
     */
    reason?: null | string;
    /**
     * 딸린 것들 (LLM · DB · 큐 · 저장소 …).
     *
     * **이 값이 이 화면의 핵심이다.** 프로세스가 살아 있는 것과 서비스가 제 일을 하는 것은
     * 다르다. AiAgentServer 는 LLM 장비가 꺼져 있으면 아무 일도 못 하는데,
     * 예전에는 프로세스만 보고 '정상' 으로 보여 주어 오해를 만들었다.
     */
    dependencies?: DependencyStatus[];
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

/**
 * LLM 정밀 확인 — 실제로 응답을 만들어 내는지 확인한다.
 *
 * 자동 점검(`/health`)은 **접속과 모델 목록까지만** 본다. 그것으로 '장비 꺼짐' 과
 * '모델 미로딩' 은 잡히지만, 실제로 토큰을 만들어 내는지는 알 수 없다.
 *
 * 생성까지 확인하려면 GPU 를 쓰고 모델이 메모리에 없으면 수십 초가 걸린다.
 * 그래서 자동 점검에 넣지 않고 사람이 누를 때만 부른다.
 *
 * **실패해도 200 으로 온다** — '점검이 실패했다' 는 것 자체가 정상적인 응답이고,
 * 화면이 그 내용을 읽어 보여 주어야 한다.
 *
 * @param provider 확인할 AI 공급자(`jsini` · `groq`). 비우면 서버의 기본 공급자.
 *                 **공급자별로 따로 확인해야 한다** — 로컬 장비가 꺼져 있을 때
 *                 Groq 는 멀쩡한지 보는 것이 이 버튼의 주 용도다.
 */
export async function deepCheckLlm(provider?: string) {
  const res = await requestClient.post<any>('/ai/health/deep', null, {
    params: provider ? { provider } : undefined,
    // 모델 로드가 걸리면 오래 걸린다. 기본 타임아웃으로는 끊긴다.
    timeout: 120_000,
  });
  const raw = unwrapOne<any>(res);
  return {
    generated: !!raw?.generated,
    latencyMs: Number(raw?.latencyMs ?? 0),
    message: String(raw?.message ?? '결과를 읽지 못했습니다.'),
    ok: !!raw?.ok,
    /** 한도 초과는 고장이 아니다. 화면이 다른 색으로 보여 준다. */
    rateLimited: !!raw?.rateLimited,
    provider: String(raw?.provider ?? provider ?? ''),
    model: String(raw?.model ?? ''),
  };
}
