/**
 * 헬프데스크 전용 요청 클라이언트.
 *
 * 헬프데스크(HelpDeskServer)는 funeralv2 의 다른 서비스와 응답 봉투가 다르다.
 *   funeralv2 : { code: 'S000', data: ... }
 *   헬프데스크 : { success: true, message, data, meta: { rowCount, totalCount, ... } }
 *
 * 서버는 JinRestApi 를 그대로 이식한 것이라 봉투를 바꾸면 살아있는 JinReception 과 어긋난다.
 * 그래서 봉투 차이는 프론트의 이 클라이언트가 흡수한다.
 */
import type { AxiosResponseHeaders, RequestClientOptions } from '@vben/request';

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

/** 게이트웨이가 헬프데스크로 라우팅하는 프리픽스 */
export const HELPDESK_PREFIX = '/helpdesk';

/**
 * 헬프데스크 응답 봉투.
 *
 * 목록 API 는 총건수를 meta 가 아니라 봉투 최상위에 `totalcount` / `totalpagecount`
 * (전부 소문자) 로 실어 보낸다. 서버의 ApiResponseBuilder 가 그렇게 만든다.
 */
export interface HelpdeskEnvelope<T = any> {
  data: T;
  message: string;
  meta?: HelpdeskMeta;
  success: boolean;
  totalcount?: null | number;
  totalpagecount?: null | number;
}

/** 헬프데스크 응답의 부가 정보 */
export interface HelpdeskMeta {
  columnCount?: null | number;
  completionTime?: string;
  duration?: string;
  requestTime?: string;
  rowCount?: null | number;
}

/** 목록 조회 결과 — 데이터와 전체 건수를 함께 돌려준다. */
export interface HelpdeskPage<T> {
  items: T[];
  totalCount: number;
  totalPageCount: number;
}

function formatToken(token: null | string) {
  return token ? `Bearer ${token}` : null;
}

function createHelpdeskClient(options?: RequestClientOptions) {
  const client = new RequestClient({
    ...options,
    baseURL: `${apiURL}${HELPDESK_PREFIX}`,
    transformResponse: (data: any, header: AxiosResponseHeaders) => {
      if (
        header.getContentType()?.toString().includes('application/json') &&
        typeof data === 'string'
      ) {
        try {
          return JSON.parse(data);
        } catch {
          return data;
        }
      }
      return data;
    },
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

  // funeralv2 로 계정을 단일화했으므로 AuthServer 가 발급한 토큰을 그대로 보낸다.
  // 헬프데스크 서버가 이 토큰을 검증하고 내부 계정으로 해석한다.
  client.addRequestInterceptor({
    fulfilled: async (config) => {
      const accessStore = useAccessStore();
      config.headers.Authorization = formatToken(accessStore.accessToken);
      config.headers['Accept-Language'] = preferences.app.locale;
      return config;
    },
  });

  // 헬프데스크 봉투 해제. success 가 false 면 message 를 담아 예외로 던진다.
  client.addResponseInterceptor({
    fulfilled: (response) => {
      const { config, data: responseData, status } = response;

      if (config.responseReturn === 'raw') return response;
      if (status < 200 || status >= 400) {
        throw Object.assign({}, response, { response });
      }
      if (config.responseReturn === 'body') return responseData;

      // 봉투가 아닌 응답(파일 다운로드 등)은 그대로 통과시킨다.
      if (
        !responseData ||
        typeof responseData !== 'object' ||
        !('success' in responseData)
      ) {
        return responseData;
      }

      const envelope = responseData as HelpdeskEnvelope;
      if (!envelope.success) {
        throw Object.assign({}, response, {
          response: {
            ...response,
            data: { message: envelope.message },
          },
        });
      }

      return envelope.data;
    },
  });

  client.addResponseInterceptor(
    authenticateResponseInterceptor({
      client,
      doReAuthenticate,
      doRefreshToken: async () => {
        // 헬프데스크는 자체 리프레시 토큰이 없다. 만료되면 funeralv2 재로그인으로 처리한다.
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

/** 헬프데스크 API 호출용 클라이언트. 응답의 data 만 돌려준다. */
export const helpdeskClient = createHelpdeskClient({
  responseReturn: 'data',
});

/** 봉투 전체(meta 포함)가 필요할 때 쓰는 클라이언트. 페이징 총건수를 읽을 때 사용한다. */
export const helpdeskRawClient = createHelpdeskClient({
  responseReturn: 'body',
});

/**
 * 목록 API 를 호출하고 데이터와 총건수를 함께 돌려준다.
 *
 * 헬프데스크의 목록 조회는 대부분 POST 로 검색 조건을 본문에 담아 보낸다
 * (DynamicFilterHelper 규약: `title_or_like`, `status`, `customer.companyId`, `sorts`, `page`, `pageSize` ...).
 */
export async function helpdeskFetchPage<T>(
  url: string,
  payload?: Record<string, any>,
): Promise<HelpdeskPage<T>> {
  const envelope = await helpdeskRawClient.post<HelpdeskEnvelope<T[]>>(
    url,
    payload ?? {},
  );

  const items = envelope?.data ?? [];
  return {
    items,
    totalCount: envelope?.totalcount ?? items.length,
    totalPageCount: envelope?.totalpagecount ?? 1,
  };
}

/** GET 방식 목록 조회용. 총건수 규약은 동일하다. */
export async function helpdeskFetchPageByGet<T>(
  url: string,
  params?: Record<string, any>,
): Promise<HelpdeskPage<T>> {
  const envelope = await helpdeskRawClient.get<HelpdeskEnvelope<T[]>>(url, {
    params,
  });

  const items = envelope?.data ?? [];
  return {
    items,
    totalCount: envelope?.totalcount ?? items.length,
    totalPageCount: envelope?.totalpagecount ?? 1,
  };
}
