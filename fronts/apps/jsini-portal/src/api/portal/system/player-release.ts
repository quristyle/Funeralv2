import { requestClient } from '#/api/request';

/**
 * 플레이어 릴리스.
 *
 * **GitHub 을 화면에서 직접 부르지 않는다.** 태그를 만들려면 `repo` 권한 토큰이
 * 필요한데, 브라우저에 실으면 누구든 꺼내 갈 수 있다(저장소가 공개라 더 그렇다).
 * 토큰은 AuthServer 에만 두고 여기서는 그 서버만 부른다.
 */
export namespace PlayerReleaseApi {
  /** 화면을 처음 그릴 때 필요한 것 */
  export interface Status {
    /** 태그를 붙일 브랜치 */
    branch: string;
    /** 이 사용자가 릴리스를 낼 수 있는가 */
    canRelease: boolean;
    /** 서버에 GitHub 설정(특히 토큰)이 갖춰져 있는가 */
    configured: boolean;
    /** 최신 커밋 메시지 첫 줄 */
    headMessage?: null | string;
    /** 브랜치의 최신 커밋 */
    headSha?: null | string;
    /** 가장 최근 릴리스 태그. 아직 없으면 null */
    latestRelease?: null | string;
    /** `owner/repo` */
    repository: string;
    /** 설정이 없을 때 띄울 안내 */
    setupHint?: null | string;
    /** 다음 버전 제안 */
    suggestedVersion?: null | string;
    /** 이미 나가 있는 태그 */
    tags: string[];
    /** 조회 중 생긴 문제 */
    warning?: null | string;
  }

  /** 워크플로 갈래 하나 */
  export interface Job {
    conclusion?: null | string;
    /** 지금 돌고 있는 단계. 끝났으면 null */
    currentStep?: null | string;
    name: string;
    status?: null | string;
  }

  /** 진행 상황 */
  export interface Run {
    conclusion?: null | string;
    htmlUrl?: null | string;
    jobs: Job[];
    /** 아직 실행을 못 찾았다. 태그 직후 몇 초 동안 그렇다 */
    pending: boolean;
    releaseUrl?: null | string;
    runNumber?: null | number;
    status?: null | string;
    tag: string;
  }

  /** 발행 결과 */
  export interface Result {
    message: string;
    sha: string;
    tag: string;
  }
}

/** 화면 첫 그림에 필요한 정보 */
export async function getPlayerReleaseStatus() {
  return requestClient.get<PlayerReleaseApi.Status>(
    '/auth/system/player-release/status',
  );
}

/**
 * 릴리스 발행 — 버전 태그를 만들어 빌드를 깨운다.
 *
 * **되돌리기 어렵다.** 태그와 릴리스를 지울 수는 있지만 이미 받아 간 사람에게는
 * 되돌릴 수 없다. 화면이 한 번 더 확인한 뒤에 부른다.
 */
export async function createPlayerRelease(version: string, notes?: string) {
  return requestClient.post<PlayerReleaseApi.Result>(
    '/auth/system/player-release',
    { notes, version },
  );
}

/** 진행 상황. 화면이 몇 초 간격으로 부른다. */
export async function getPlayerReleaseRun(tag: string) {
  return requestClient.get<PlayerReleaseApi.Run>(
    `/auth/system/player-release/runs/${encodeURIComponent(tag)}`,
  );
}
