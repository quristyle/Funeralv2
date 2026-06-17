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

function createRequestClient(baseURL: string, options?: RequestClientOptions) {
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
  client.addResponseInterceptor(
    defaultResponseInterceptor({
      codeField: 'code',
      dataField: 'data',
      successCode: 0,
    }),
  );

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
  responseReturn: 'data',
});

export const baseRequestClient = new RequestClient({ baseURL: apiURL });

export interface PageFetchParams {
  [key: string]: any;
  pageNo?: number;
  pageSize?: number;
}
