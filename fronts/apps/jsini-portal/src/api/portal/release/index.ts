import { requestClient } from '#/api/request';

/**
 * 배포(릴리즈) API.
 *
 * 예전에는 헬프데스크가 자기 시스템 배포 화면을 들고 있었다.
 * JSini 관리 포털이 여러 MSA 를 관장하므로 이쪽으로 옮겼고,
 * 배포 대상은 화면에 박지 않고 서버 설정(Release:Targets)에서 받아 온다.
 */
export namespace ReleaseApi {
  /** 배포 대상 */
  export interface Target {
    /** 무엇을 배포하는지 */
    description?: null | string;
    /** 대략의 소요 시간(초). 진행 안내에 쓴다. */
    estimatedSeconds: number;
    /** 호출에 쓰는 식별자 */
    key: string;
    /** 화면에 보일 이름 */
    name: string;
  }

  /** 배포 요청 결과 */
  export interface Result {
    message: string;
    queued: boolean;
    targetKey: string;
  }
}

/** 배포 대상 목록 */
export async function getReleaseTargets() {
  const res = await requestClient.get<any>('/auth/release/targets');
  if (Array.isArray(res)) return res as ReleaseApi.Target[];
  if (Array.isArray(res?.result)) return res.result as ReleaseApi.Target[];
  if (Array.isArray(res?.data?.result)) {
    return res.data.result as ReleaseApi.Target[];
  }
  return [] as ReleaseApi.Target[];
}

/**
 * 배포 실행 요청.
 *
 * 큐에 요청을 넣는 것까지가 서버의 일이다. 스크립트가 실제로 끝났는지는
 * 알 수 없으므로 화면도 그렇게 안내한다.
 */
export async function triggerRelease(key: string) {
  return requestClient.post<ReleaseApi.Result>(`/auth/release/${key}`, {});
}
