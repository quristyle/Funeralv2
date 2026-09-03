/**
 * 프로젝트관리 전용 요청 클라이언트.
 *
 * [2026-09-04 · D-A1] ProjMngServer 의 와이어 봉투가 표준으로 바뀌었다.
 *   지금 와이어  : `{ success, code: 'S000', message,
 *                    data: { result: { rows, res, cols, procCode }, page } }`
 *   옛 와이어    : `{ code: 0, message, res, cols, data }`  ← 숫자 코드, 음수면 실패
 *
 * 화면 20여 곳은 옛 모양(`ProjMngResult` — code·res·cols·data)을 그대로 쓰므로,
 * 이 클라이언트가 **와이어에서 그 모양을 재구성**한다. 화면은 바뀐 것이 없다.
 * (`cols` 컬럼 메타를 화면이 반드시 봐야 해서 `data` 만 꺼내 주면 안 되는 사정도 그대로다.)
 *
 * 배포 순서와 무관하게 동작하도록 **옛 봉투도 계속 받는다** — `success` 가 없고
 * 숫자 `code` 가 있으면 옛 서버다. 서버가 먼저 올라가든 화면이 먼저 올라가든 깨지지 않는다.
 *
 * 헬프데스크(`{ success, data, meta }`)는 여전히 다르다 — 살아 있는 JinReception 이
 * 그 봉투를 실사용 중이라, DB 이관 전에는 맞추지 않는다(사용자 지시).
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

/**
 * 프로시저 파라미터는 전부 문자열로 넘긴다. null/undefined 는 빈 문자열이 된다.
 *
 * 서버의 `MainParam` 은 `Dictionary<string, string>` 이라 숫자·불리언을 그대로 보내면
 * 역직렬화에서 400 이 난다. 이건 요청 규약이므로 호출부가 아니라 여기서 지킨다.
 */
export function toParam(
  dic?: null | Record<string, unknown>,
): Record<string, string> {
  const out: Record<string, string> = {};
  if (!dic) return out;
  for (const [key, value] of Object.entries(dic)) {
    if (value === null || value === undefined) {
      out[key] = '';
    } else if (value instanceof Date) {
      out[key] = formatDateTime(value);
    } else if (typeof value === 'boolean') {
      out[key] = value ? 'true' : 'false';
    } else {
      out[key] = String(value);
    }
  }
  return out;
}

function formatDateTime(d: Date) {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}

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

  // 와이어 봉투(표준 ApiResponse)에서 화면이 쓰는 옛 모양(ProjMngResult)을 재구성한다.
  // 실패(success:false · 옛 음수 code)는 예외로 바꿔 호출한 쪽이 try/catch 로 다룬다.
  client.addResponseInterceptor({
    fulfilled: (response) => {
      const { config, data: responseData, status } = response;

      if (config.responseReturn === 'raw') return response;
      if (status < 200 || status >= 400) {
        throw Object.assign({}, response, { response });
      }

      if (!responseData || typeof responseData !== 'object') {
        return responseData;
      }

      // ── 지금 와이어: 표준 봉투 (D-A1) ──────────────────────────────
      if ('success' in responseData) {
        const std = responseData as {
          code?: string;
          data?: {
            page?: { total?: number };
            result?: {
              cols?: null | Record<string, string>;
              procCode?: number;
              res?: null | Record<string, unknown>;
              rows?: unknown[];
            };
          };
          message?: string;
          success: boolean;
        };

        if (!std.success) {
          throw Object.assign({}, response, {
            response: { ...response, data: { message: std.message } },
          });
        }

        const inner = std.data?.result;
        return {
          code: inner?.procCode ?? 0,
          message: std.message ?? '',
          res: inner?.res ?? undefined,
          cols: inner?.cols ?? {},
          data: inner?.rows ?? [],
        } satisfies ProjMngResult<any>;
      }

      // ── 옛 와이어 (배포 사이 · 옛 서버) ───────────────────────────
      if (!('code' in responseData)) {
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
 * BizSelect 메타데이터가 지시하는 임의 호출.
 *
 * 경로·메서드·본문이 전부 DB(`scom.biz_select_configs`)에서 오므로 프로시저 래퍼를
 * 거치지 않는다. 봉투는 그대로 돌려준다 — 메타데이터의 `result_path` 가 그 안에서
 * 목록을 찾는다.
 */
export async function projmngRequest<T = ProjMngRow>(
  url: string,
  method: string,
  payload?: Record<string, any>,
): Promise<ProjMngResult<T>> {
  if (method.toUpperCase() === 'GET') {
    try {
      return await projmngClient.get<ProjMngResult<T>>(url, {
        params: payload,
      });
    } catch {
      return emptyResult<T>('요청이 실패했습니다.');
    }
  }

  // MainParam 은 문자열 사전이다. 메타데이터를 타고 온 값은 숫자일 수 있어(프로젝트 코드 등)
  // 여기서 규약에 맞춰 준다 — 프로시저 래퍼(dbCont)가 하던 일과 같다.
  const body = { ...payload };
  if (body.MainParam && typeof body.MainParam === 'object') {
    body.MainParam = toParam(body.MainParam as Record<string, unknown>);
  }

  return projmngPost<T>(url, body as ProjMngRequest);
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
