/**
 * 프로젝트관리 전용 요청 클라이언트.
 *
 * ProjMngServer 는 다른 서비스와 응답 봉투가 다르다.
 *   포털/장례식장 : `{ code: 'S000', data: ... }`
 *   헬프데스크     : `{ success: true, data: ... }`
 *   프로젝트관리   : `{ code: 0, message, res, cols, data }`  ← 숫자 코드, 음수면 실패
 *
 * 서버 코드는 ProjMngWasm(Blazor) 시절 그대로 이식한 것이라 봉투를 바꾸면
 * DB 프로시저 쪽 규약까지 건드려야 한다. 그래서 차이는 이 클라이언트가 흡수한다.
 *
 * 또한 `cols`(컬럼 메타)가 응답 최상위에 실려 오는데 화면이 그걸 반드시 봐야 하므로,
 * 다른 클라이언트처럼 `data` 만 꺼내 주면 안 된다. 봉투 전체를 돌려준다.
 */
import type { RequestClientOptions } from '@vben/request';

import type { ProjMngRequest, ProjMngResult, ProjMngRow } from './types';

import { useAppConfig } from '@vben/hooks';
import { preferences } from '@vben/preferences';
import {
  authenticateResponseInterceptor,
  errorMessageResponseInterceptor,
  RequestClient,
} from '@vben/request';
import { useAccessStore } from '@vben/stores';

import { message } from 'ant-design-vue';

import { useAuthStore } from '#/store';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

/** 게이트웨이가 프로젝트관리로 라우팅하는 프리픽스 */
export const PROJMNG_PREFIX = '/projmng';

/** 서비스 내부 엔드포인트. 게이트웨이가 프리픽스를 떼고 `/api` 를 다시 붙인다. */
export const PROJMNG_URL = {
  /** 업무 프로시저 (`sp_*`) 와 다건 저장 */
  proj: '/Proj',
  /** 캐시를 타지 않아야 하는 시스템 조회 */
  projSys: '/Proj/sys',
  /** 개발 도구 — 프로젝트 DB 메타 조회 */
  dev: '/Dev',
  /** 개발 도구 — 직접 쿼리 실행 (서버에서 역할을 한 번 더 확인한다) */
  devSql: '/Dev/sql',
  /** 서버측 파일 스캔 (`md_*`) */
  media: '/Media',
  /** 서버 캐시 초기화 */
  sys: '/Sys',
} as const;

function formatToken(token: null | string) {
  return token ? `Bearer ${token}` : null;
}

function createProjMngClient(options?: RequestClientOptions) {
  const client = new RequestClient({
    ...options,
    baseURL: `${apiURL}${PROJMNG_PREFIX}`,
  });

  async function doReAuthenticate() {
    const accessStore = useAccessStore();
    const authStore = useAuthStore();
    accessStore.setAccessToken(null);
    if (
      preferences.app.loginExpiredMode === 'modal' &&
      accessStore.isAccessChecked
    ) {
      accessStore.setLoginExpired(true);
    } else {
      await authStore.logout();
    }
  }

  client.addRequestInterceptor({
    fulfilled: async (config) => {
      const accessStore = useAccessStore();
      config.headers.Authorization = formatToken(accessStore.accessToken);
      config.headers['Accept-Language'] = preferences.app.locale;
      return config;
    },
  });

  // 봉투를 벗기지 않고 그대로 넘긴다. cols 를 화면이 써야 하기 때문이다.
  // 대신 code < 0 은 여기서 예외로 바꿔 호출한 쪽이 try/catch 로 다룰 수 있게 한다.
  client.addResponseInterceptor({
    fulfilled: (response) => {
      const { config, data: responseData, status } = response;

      if (config.responseReturn === 'raw') return response;
      if (status < 200 || status >= 400) {
        throw Object.assign({}, response, { response });
      }

      if (
        !responseData ||
        typeof responseData !== 'object' ||
        !('code' in responseData)
      ) {
        return responseData;
      }

      const envelope = responseData as ProjMngResult<any>;
      if (typeof envelope.code === 'number' && envelope.code < 0) {
        throw Object.assign({}, response, {
          response: {
            ...response,
            data: { message: envelope.message },
          },
        });
      }

      return envelope;
    },
  });

  client.addResponseInterceptor(
    authenticateResponseInterceptor({
      client,
      doReAuthenticate,
      doRefreshToken: async () => {
        // 프로젝트관리는 자체 리프레시가 없다. 만료되면 포털 재로그인으로 처리한다.
        await doReAuthenticate();
        return '';
      },
      enableRefreshToken: false,
      formatToken,
    }),
  );

  client.addResponseInterceptor(
    errorMessageResponseInterceptor((msg: string, error) => {
      const responseData = error?.response?.data ?? {};
      const errorMessage = responseData?.message ?? responseData?.error ?? '';
      message.error(errorMessage || msg);
    }),
  );

  return client;
}

const projmngClient = createProjMngClient({ responseReturn: 'body' });

/** 빈 결과. 호출이 실패했을 때 화면이 그대로 그릴 수 있도록 모양을 맞춰 준다. */
export function emptyResult<T = ProjMngRow>(message = ''): ProjMngResult<T> {
  return { code: 0, message, cols: {}, data: [] };
}

/**
 * ProjMngServer 호출의 단일 진입점.
 *
 * 실패해도 예외를 위로 던지지 않는다. 오류 메시지는 인터셉터가 이미 토스트로 띄우고,
 * 화면은 빈 결과를 받아 계속 그린다 — 이식 전 Blazor 쪽 동작과 같다.
 */
export async function projmngPost<T = ProjMngRow>(
  url: string,
  payload: ProjMngRequest,
): Promise<ProjMngResult<T>> {
  try {
    const result = await projmngClient.post<ProjMngResult<T>>(url, {
      ProcType: 'srch',
      IsFast: false,
      IsProjDb: false,
      MainParam: {},
      MultyData: [],
      ...payload,
    });
    // 서버가 data/cols 를 null 로 줄 수 있다. 화면에서 매번 확인하지 않도록 여기서 채운다.
    return {
      ...result,
      cols: result?.cols ?? {},
      data: result?.data ?? [],
    };
  } catch {
    return emptyResult<T>('요청이 실패했습니다.');
  }
}

/**
 * 봉투가 아닌 평범한 딕셔너리를 보내는 경로용.
 *
 * `/Dev/sql` 하나가 여기 해당한다. 이 엔드포인트는 `RequestDto` 가 아니라
 * `{ query, db_nick, isBreakCnt }` 를 그대로 받는다(이식 전 서버 구현 그대로).
 */
export async function projmngPostPlain<T = ProjMngRow>(
  url: string,
  payload: Record<string, string>,
): Promise<ProjMngResult<T>> {
  try {
    const result = await projmngClient.post<ProjMngResult<T>>(url, payload);
    return {
      ...result,
      cols: result?.cols ?? {},
      data: result?.data ?? [],
    };
  } catch {
    return emptyResult<T>('요청이 실패했습니다.');
  }
}
