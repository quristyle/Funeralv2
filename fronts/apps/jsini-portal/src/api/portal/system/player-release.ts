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

/**
 * 응답 봉투에서 **객체 하나**를 꺼낸다.
 *
 * AuthServer 의 공통 래퍼(`AddApiResponseWrapper`)는 단건도 목록처럼 싸서 보낸다 —
 * `{ result: [ {…} ], page: { total: 1 } }`. 그래서 `res.result` 를 그대로 쓰면
 * **배열**을 받고, `status.configured` 같은 것이 전부 `undefined` 가 된다
 * (실제로 그래서 운영 화면의 입력칸과 발행 단추가 모두 잠겨 있었다).
 *
 * 봉투가 바뀌어도 견디도록 세 모양을 다 받는다 — 배열이면 첫 항목,
 * `result` 가 객체면 그것, 봉투가 이미 벗겨져 있으면 그대로.
 */
function one<T>(res: any): T {
  const inner = res?.result ?? res;
  return (Array.isArray(inner) ? inner[0] : inner) as T;
}

/** 화면 첫 그림에 필요한 정보 */
export async function getPlayerReleaseStatus() {
  const res = await requestClient.get<any>('/auth/system/player-release/status');
  return one<PlayerReleaseApi.Status>(res);
}

/**
 * 릴리스 발행 — 버전 태그를 만들어 빌드를 깨운다.
 *
 * **되돌리기 어렵다.** 태그와 릴리스를 지울 수는 있지만 이미 받아 간 사람에게는
 * 되돌릴 수 없다. 화면이 한 번 더 확인한 뒤에 부른다.
 */
export async function createPlayerRelease(version: string, notes?: string) {
  const res = await requestClient.post<any>('/auth/system/player-release', {
    notes,
    version,
  });
  return one<PlayerReleaseApi.Result>(res);
}

/** 진행 상황. 화면이 몇 초 간격으로 부른다. */
export async function getPlayerReleaseRun(tag: string) {
  const res = await requestClient.get<any>(
    `/auth/system/player-release/runs/${encodeURIComponent(tag)}`,
  );
  return one<PlayerReleaseApi.Run>(res);
}
