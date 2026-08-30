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

  export interface ImageInfo {
    createdAt: string;
    inUse: boolean;
    name: string;
    sizeMb: number;
  }

  export interface ContainerLogs {
    /** 헤더를 걷어낸 로그 본문 (타임스탬프 포함) */
    log: string;
    service: string;
    tail: number;
  }

  export interface CleanupResult {
    errors: string[];
    keptRecent: number;
    removed: string[];
    spaceReclaimedMb: number;
  }

  export interface Overview {
    docker: {
      available: boolean;
      containers: Container[];
      error: null | string;
      images: ImageInfo[];
      imagesTotalMb: number;
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

/**
 * 오래된 배포 이미지 정리 — 저장소별로 사용 중 + 최근 2개 태그만 남긴다.
 * 관리자 계열만 서버가 허용한다.
 */
export async function cleanupDockerImages(): Promise<DeployStatusApi.CleanupResult> {
  const res = await requestClient.post<any>('/auth/deploy-status/cleanup');
  return res?.result?.[0] ?? res;
}

/**
 * 컨테이너 로그 조회 — 컨테이너에 들어가지 않고 화면에서 본다.
 * 관리자 계열만 서버가 허용한다.
 */
export async function getContainerLogs(
  service: string,
  tail = 200,
): Promise<DeployStatusApi.ContainerLogs> {
  const res = await requestClient.get<any>(
    `/auth/deploy-status/logs/${service}`,
    { params: { tail } },
  );
  return res?.result?.[0] ?? res;
}
