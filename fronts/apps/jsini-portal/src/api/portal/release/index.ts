import { requestClient } from '#/api/request';

/**
 * 배포(릴리즈) API.
 *
 * 예전에는 헬프데스크가 자기 시스템 배포 화면을 들고 있었다.
 * JSini 관리 포털이 여러 MSA 를 관장하므로 이쪽으로 옮겼고,
 * 배포 대상은 화면에 박지 않고 서버 설정(Release:Targets)에서 받아 온다.
 *
 * ## 진행 상황을 어떻게 받나
 *
 * 예전에는 서버가 큐에 넣고 잊었고, 화면이 `setTimeout` 으로 단계를 만들어 내
 * 초록색 [SUCCESS] 를 찍었다. 실패한 배포도 전부 초록으로 보였다.
 *
 * 이제 요청 한 건이 서버에 **run** 으로 남는다. 화면은 그 `runId` 를 폴링하고,
 * 배포 장비의 래퍼가 스크립트의 실제 stdout 을 되돌려 보고한다.
 * **화면에 보이는 줄은 전부 실제로 일어난 일이다.**
 */
export namespace ReleaseApi {
  /**
   * 실행 상태.
   *
   * - `queued` 큐에 넣었고 아직 아무도 집어가지 않았다 (예전에 감춰져 있던 상태)
   * - `running` 배포 장비가 집어가서 돌고 있다
   * - `succeeded` 스크립트가 0 으로 끝났다
   * - `failed` 0 이 아닌 코드로 끝났다
   * - `timeout` 제한 시간을 넘겨도 소식이 없다 (소비자가 죽었거나 없다)
   * - `dispatched` 보고를 하지 않는 대상에 요청만 보냈다 — **결과는 알 수 없다**
   */
  export type RunStatus =
    | 'dispatched'
    | 'failed'
    | 'queued'
    | 'running'
    | 'succeeded'
    | 'timeout';

  /** 진행 로그 한 줄 */
  export interface RunEvent {
    /** 서버가 받은 시각 */
    at: string;
    level: 'error' | 'info' | 'result' | 'stdout' | 'step' | 'warn';
    message: string;
    /** `level` 이 step 인 경우의 단계 이름 */
    step?: null | string;
    seq: number;
  }

  /** 실행 한 건 */
  export interface Run {
    /** 마지막으로 지나간 단계 (스크립트가 `##STEP ...` 을 찍은 경우) */
    currentStep?: null | string;
    /** 대상이 배포 후 스스로 알려 준 버전. VersionUrl 을 둔 대상만 찬다. */
    deployedVersion?: null | string;
    /** 요청한 구간의 로그. 상태만 물었으면 빈 목록이다. */
    events: RunEvent[];
    /** 스크립트 종료 코드. 0 이면 성공. */
    exitCode?: null | number;
    finishedAt?: null | string;
    id: string;
    /** 더 폴링할 필요가 있나 */
    isFinal: boolean;
    /** 받은 마지막 로그 순번. 다음 요청의 `sinceSeq` 로 그대로 돌려준다. */
    lastSeq: number;
    /** 사람이 읽을 한 줄. 실패 사유가 여기 들어간다. */
    message?: null | string;
    /** 이 대상이 보고를 하기로 되어 있었나. `dispatched` 의 뜻을 정확히 쓰는 데 필요하다. */
    reportsProgress: boolean;
    requestedAt: string;
    requestedBy?: null | string;
    scriptPath?: null | string;
    startedAt?: null | string;
    status: RunStatus;
    targetKey: string;
    targetName: string;
  }

  /** 배포 대상 */
  export interface Target {
    /** 지금 돌고 있는 실행. 화면을 새로 열어도 이어 볼 수 있다. */
    activeRunId?: null | string;
    /** 무엇을 배포하는지 */
    description?: null | string;
    /**
     * 대략의 소요 시간(초). "보통 N초쯤 걸립니다" 안내에만 쓴다.
     *
     * **진행률을 만들어 내는 데 쓰지 않는다.** 예전 화면이 이 값으로 가짜 단계를
     * 초록색으로 찍고 있었다.
     */
    estimatedSeconds: number;
    /** 호출에 쓰는 식별자 */
    key: string;
    /** 가장 최근 실행 요약 */
    lastRun?: null | Run;
    /** 화면에 보일 이름 */
    name: string;
    /**
     * 이 대상이 진행 상황을 되돌려 보고하나.
     *
     * 꺼져 있으면 화면은 "요청을 보냈다" 까지만 말한다. 성공/실패를 아는 척하지 않는다.
     */
    reportsProgress: boolean;
    timeoutSeconds: number;
  }

  /** 대상 목록 응답 */
  export interface TargetList {
    /** 배포를 실행할 수 있나 (`/portal/release` 의 can_cust1). 서버가 판정한 값이다. */
    canRelease: boolean;
    /** 보고를 켠 대상이 있는데 콜백 주소가 비어 있는 등, 설정이 반쪽일 때 찬다. */
    configWarning?: null | string;
    items: Target[];
  }

  /** 배포 요청 결과 */
  export interface TriggerResult {
    message: string;
    queued: boolean;
    /** 화면이 이 값으로 진행 상황을 폴링한다. 거절되면 없다. */
    runId?: null | string;
    targetKey: string;
  }
}

/**
 * AuthServer 의 응답 필터는 단건 객체도 `{ result: [ ... ], page }` 로 감싸 보낸다.
 * 배열로 와도 객체로 와도 하나를 꺼내 준다 (자료실 API 와 같은 방식).
 */
function pickOne<T>(res: any): T | undefined {
  const raw = res?.result ?? res?.data?.result ?? res;
  return (Array.isArray(raw) ? raw[0] : raw) as T | undefined;
}

function pickMany<T>(res: any): T[] {
  const raw = res?.result ?? res?.data?.result ?? res;
  return Array.isArray(raw) ? (raw as T[]) : [];
}

/** 배포 대상 목록. 진행 중인 실행과 최근 실행 요약이 함께 온다. */
export async function getReleaseTargets() {
  const res = await requestClient.get<any>('/auth/release/targets');

  return (
    pickOne<ReleaseApi.TargetList>(res) ?? {
      canRelease: false,
      configWarning: null,
      items: [],
    }
  );
}

/**
 * 배포 실행 요청.
 *
 * 성공하면 `runId` 가 온다. 그 값으로 {@link getReleaseRun} 을 폴링한다.
 * 같은 대상이 이미 돌고 있으면 서버가 409 를 준다 — 두 사람이 동시에 눌러
 * 같은 체크아웃에서 스크립트 둘이 도는 것을 막는다.
 */
export async function triggerRelease(key: string) {
  const res = await requestClient.post<any>(`/auth/release/${key}`, {});
  return pickOne<ReleaseApi.TriggerResult>(res);
}

/**
 * 실행 한 건의 상태와 `sinceSeq` 이후의 로그.
 *
 * 받은 `lastSeq` 를 다음 요청의 `sinceSeq` 로 돌려주면 같은 줄을 두 번 받지 않는다.
 */
export async function getReleaseRun(runId: string, sinceSeq = 0) {
  const res = await requestClient.get<any>(`/auth/release/runs/${runId}`, {
    params: { sinceSeq },
  });
  return pickOne<ReleaseApi.Run>(res);
}

/** 최근 실행 이력. 누가 언제 무엇을 배포했는지 남는다. */
export async function getReleaseRuns(take = 20) {
  const res = await requestClient.get<any>('/auth/release/runs', {
    params: { take },
  });
  return pickMany<ReleaseApi.Run>(res);
}
