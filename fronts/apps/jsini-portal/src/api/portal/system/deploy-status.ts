import { requestClient } from '#/api/request';

/**
 * 상태관리 > 배포 현황.
 *
 * GitHub 를 화면이 직접 부르지 않는다 — 토큰이 브라우저에 노출되기 때문이다.
 * AuthServer 의 /auth/deploy-status 가 GitHub Actions 와 운영서버 Docker 를
 * 한 번에 모아 준다 (토큰은 서버의 appsettings.Local.json 에만 있다).
 */
export namespace DeployStatusApi {
  export interface WorkflowRun {
    actor: null | string;
    branch: string;
    conclusion: null | string;
    durationSec: null | number;
    event: string;
    htmlUrl: string;
    id: number;
    name: string;
    sha: string;
    startedAt: null | string;
    /** queued | in_progress | completed */
    status: string;
    title: null | string;
    updatedAt: string;
  }

  export interface Runner {
    busy: boolean;
    labels: string[];
    name: string;
    /** online | offline */
    status: string;
  }

  export interface Container {
    createdAt: string;
    image: string;
    project: null | string;
    service: string;
    /** running | exited | ... */
    state: string;
    /** "Up 3 hours" 같은 사람용 문구 */
    status: string;
    tag: string;
  }

  export interface Overview {
    docker: {
      available: boolean;
      containers: Container[];
      error: null | string;
    };
    generatedAt: string;
    github: {
      error: null | string;
      runners: Runner[];
      runs: WorkflowRun[];
    };
    repo: string;
  }
}

export async function getDeployStatus(): Promise<DeployStatusApi.Overview> {
  // requestClient 는 봉투의 `data` 까지만 벗긴다. 이 API 의 data 는
  // { result: [overview] } 꼴이라 한 번 더 꺼내야 한다 (account.ts 와 같은 사정).
  const res = await requestClient.get<any>('/auth/deploy-status');
  return res?.result?.[0] ?? res;
}
