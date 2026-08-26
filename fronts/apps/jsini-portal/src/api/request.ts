/**
 * 이 파일은 비즈니스 로직에 따라 자유롭게 조정할 수 있습니다.
 */
import type { AxiosResponseHeaders, RequestClientOptions } from '@vben/request';

import { useAppConfig } from '@vben/hooks';
import { preferences } from '@vben/preferences';
import {
  authenticateResponseInterceptor,
  defaultResponseInterceptor,
  errorMessageResponseInterceptor,
  RequestClient,
} from '@vben/request';
import { useAccessStore } from '@vben/stores';
import { cloneDeep } from '@vben/utils';

import { message } from 'ant-design-vue';
import JSONBigInt from 'json-bigint';

import { useAuthStore } from '#/store';

import { refreshTokenApi } from './core';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

/**
 * 비밀번호 만료 안내를 이미 띄웠는지.
 *
 * 한 화면이 API 를 여러 개 부르면 전부 403 으로 막히므로,
 * 안내를 요청 수만큼 띄우지 않도록 클라이언트 사이에서 함께 본다.
 */
let passwordExpiredNotified = false;

function createRequestClient(
  baseURL: string,
  options?: RequestClientOptions & { dataField?: string },
) {
  const client = new RequestClient({
    ...options,
    baseURL,
    transformResponse: (data: any, header: AxiosResponseHeaders) => {
      // storeAsString은 BigInt를 문자열로 저장할지 여부를 나타내며, false로 설정하면 내장 BigInt 유형으로 저장됩니다.
      if (
        header.getContentType()?.toString().includes('application/json') &&
        typeof data === 'string'
      ) {
        return cloneDeep(
          JSONBigInt({ storeAsString: true, strict: true }).parse(data),
        );
      }
      return data;
    },
  });

  /**
   * 재인증 로직
   */
  async function doReAuthenticate() {
    console.warn('Access token or refresh token is invalid or expired. ');
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

  /**
   * 토큰 갱신 로직
   */
  async function doRefreshToken() {
    const accessStore = useAccessStore();
    const resp = await refreshTokenApi();
    const newToken = resp.data;
    accessStore.setAccessToken(newToken);
    return newToken;
  }

  function formatToken(token: null | string) {
    return token ? `Bearer ${token}` : null;
  }

  // 요청 헤더 처리
  client.addRequestInterceptor({
    fulfilled: async (config) => {
      const accessStore = useAccessStore();

      config.headers.Authorization = formatToken(accessStore.accessToken);
      config.headers['Accept-Language'] = preferences.app.locale;
      return config;
    },
  });

  // 반환된 응답 데이터 형식 처리
  client.addResponseInterceptor({
    fulfilled: (response) => {
      const { data } = response;
      if (data && data.code && data.code !== 'S000') {
        console.warn(
          `[Business Error] Code: ${data.code}, Message: ${data.message}`,
          data,
        );
      }
      return response;
    },
  });

  client.addResponseInterceptor(
    defaultResponseInterceptor({
      codeField: 'code',
      dataField: options?.dataField || 'data',
      successCode: 'S000',
    }),
  );

  // ── 비밀번호 사용 기간 만료(게이트웨이 차단) 처리 ──────────
  //
  // 90일이 지나면 게이트웨이가 비밀번호 변경에 필요한 경로만 통과시키고
  // 나머지는 403 + code `E403_PWD_EXPIRED` 로 막는다.
  //
  // 로그인 직후는 auth 스토어가 안내하고 보내지만, 이미 로그인해 둔 탭에서
  // 그대로 쓰다가(또는 새로고침으로) 만료 시점을 넘기는 경우가 있다.
  // 그때 화면마다 빨간 토스트만 쌓이면 무슨 일인지 알 수 없으므로,
  // 안내를 한 번만 띄우고 비밀번호 변경 화면으로 보낸다.
  //
  // **토큰 만료 처리보다 앞에 둔다.** 뒤에 두면 아래 인터셉터가 먼저 응답을 소비한다.
  client.addResponseInterceptor({
    rejected: async (error: any) => {
      if (error?.response?.data?.code !== 'E403_PWD_EXPIRED') {
        throw error;
      }

      if (!passwordExpiredNotified) {
        passwordExpiredNotified = true;
        message.warning(
          error.response.data.message ??
            '비밀번호를 변경한 뒤 이용해 주세요.',
        );
        // 라우터를 정적으로 가져오면 순환 참조가 된다
        // (router → guard → store → api/request → router). 쓸 때 가져온다.
        const { router } = await import('#/router');
        const current = router.currentRoute.value;
        if (current.path !== '/profile' || current.query.tab !== 'password') {
          await router.push({ path: '/profile', query: { tab: 'password' } });
        }
        // 같은 안내를 계속 띄우지 않되, 비밀번호를 바꾸고 나면 다시 알릴 수 있게 풀어 준다.
        setTimeout(() => {
          passwordExpiredNotified = false;
        }, 10_000);
      }

      throw error;
    },
  });

  // 토큰 만료 처리
  client.addResponseInterceptor(
    authenticateResponseInterceptor({
      client,
      doReAuthenticate,
      doRefreshToken,
      enableRefreshToken: preferences.app.enableRefreshToken,
      formatToken,
    }),
  );

  // 일반적인 오류 처리, 위의 오류 처리 로직에 진입하지 않으면 여기로 들어옵니다.
  client.addResponseInterceptor(
    errorMessageResponseInterceptor((msg: string, error) => {
      // ── 곁들이는 요청은 조용히 실패시킨다 ──────────────────
      //
      // 화면이 그려지는 데 필수가 아닌 요청이 있다(예: 계정에 저장된 환경설정).
      // 실패해도 기본값·로컬 설정으로 그대로 쓸 수 있으므로 사용자에게 알릴 것이 없다.
      // 그런데 이 인터셉터는 **부르는 쪽이 catch 하기 전에** 토스트를 띄우므로,
      // `.catch()` 만으로는 막을 수 없다. 그래서 요청 쪽에서 표시를 끄게 한다.
      //
      //   requestClient.get(url, { skipErrorMessage: true } as any)
      if ((error?.config as any)?.skipErrorMessage) {
        return;
      }

      // 이곳은 비즈니스에 따라 맞춤형으로 구현할 수 있습니다. error 내의 정보를 가져와서 맞춤형 처리를 할 수 있으며, 서로 다른 code에 따라 다른 메시지를 표시할 수 있습니다. 단순히 message.error를 사용하여 msg를 표시하는 대신 말이죠.
      // 현재 mock 인터페이스에서 반환하는 오류 필드는 error 또는 message입니다.
      const responseData = error?.response?.data ?? {};
      const errorMessage = responseData?.error ?? responseData?.message ?? '';
      // 오류 정보가 없으면 상태 코드에 따라 안내 메시지를 표시합니다.
      message.error(errorMessage || msg);
    }),
  );

  return client;
}

export const requestClient = createRequestClient(apiURL, {
  dataField: 'data',
  responseReturn: 'data',
});

export const requestListClient = createRequestClient(apiURL, {
  dataField: 'data.result',
  responseReturn: 'data',
});

export const baseRequestClient = new RequestClient({ baseURL: apiURL });

export interface PageFetchParams {
  [key: string]: any;
  pageNo?: number;
  pageSize?: number;
}
