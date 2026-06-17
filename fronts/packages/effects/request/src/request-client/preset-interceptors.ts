import type { RequestClient } from './request-client';
import type { MakeErrorMessageFn, ResponseInterceptorConfig } from './types';

import { $t } from '@vben/locales';
import { isFunction } from '@vben/utils';

import axios from 'axios';

export const defaultResponseInterceptor = ({
  codeField = 'code',
  dataField = 'data',
  successCode = 0,
}: {
  /** 응답 데이터에서 요청 결과를 나타내는 필드명 */
  codeField: string;
  /** 응답 데이터에서 실제 데이터를 담고 있는 필드명, 또는 응답 데이터에서 반환할 데이터를 파싱하는 함수 제공 */
  dataField: ((response: any) => any) | string;
  /** codeField가 지정한 필드 값이 successCode와 같을 때 API 요청이 성공했음을 나타냅니다. 함수를 제공할 경우 true를 반환하면 API 요청 성공을 의미합니다. */
  successCode: ((code: any) => boolean) | number | string;
}): ResponseInterceptorConfig => {
  return {
    fulfilled: (response) => {
      const { config, data: responseData, status } = response;

      if (config.responseReturn === 'raw') {
        return response;
      }

      if (status >= 200 && status < 400) {
        if (config.responseReturn === 'body') {
          return responseData;
        } else if (
          isFunction(successCode)
            ? successCode(responseData[codeField])
            : responseData[codeField] === successCode
        ) {
          return isFunction(dataField)
            ? dataField(responseData)
            : responseData[dataField];
        }
      }
      throw Object.assign({}, response, { response });
    },
  };
};

export const authenticateResponseInterceptor = ({
  client,
  doReAuthenticate,
  doRefreshToken,
  enableRefreshToken,
  formatToken,
}: {
  client: RequestClient;
  doReAuthenticate: () => Promise<void>;
  doRefreshToken: () => Promise<string>;
  enableRefreshToken: boolean;
  formatToken: (token: string) => null | string;
}): ResponseInterceptorConfig => {
  return {
    rejected: async (error) => {
      const { config, response } = error;
      // 401 오류가 아니면 직접 예외 발생
      if (response?.status !== 401) {
        throw error;
      }
      // refreshToken 기능이 활성화되어 있는지 확인합니다.
      // 활성화되어 있지 않거나 이미 재시도한 요청인 경우, 바로 다시 로그인하도록 이동합니다.
      if (!enableRefreshToken || config.__isRetryRequest) {
        await doReAuthenticate();
        throw error;
      }
      // 토큰을 갱신 중인 경우, 요청을 대기열에 추가하고 갱신이 완료되기를 기다립니다.
      if (client.isRefreshing) {
        return new Promise((resolve) => {
          client.refreshTokenQueue.push((newToken: string) => {
            config.headers.Authorization = formatToken(newToken);
            resolve(client.request(config.url, { ...config }));
          });
        });
      }

      // 토큰 갱신 시작을 표시합니다.
      client.isRefreshing = true;
      // 무한 루프를 방지하기 위해 현재 요청을 재시도 요청으로 표시합니다.
      config.__isRetryRequest = true;

      try {
        const newToken = await doRefreshToken();

        // 대기열의 요청을 처리합니다.
        client.refreshTokenQueue.forEach((callback) => callback(newToken));
        // 대기열을 비웁니다.
        client.refreshTokenQueue = [];

        return client.request(error.config.url, { ...error.config });
      } catch (refreshError) {
        // 토큰 갱신에 실패하면 오류를 처리합니다(예: 강제 로그아웃 또는 로그인 페이지로 이동).
        client.refreshTokenQueue.forEach((callback) => callback(''));
        client.refreshTokenQueue = [];
        console.error('Refresh token failed, please login again.');
        await doReAuthenticate();

        throw refreshError;
      } finally {
        client.isRefreshing = false;
      }
    },
  };
};

export const errorMessageResponseInterceptor = (
  makeErrorMessage?: MakeErrorMessageFn,
): ResponseInterceptorConfig => {
  return {
    rejected: (error: any) => {
      if (axios.isCancel(error)) {
        return Promise.reject(error);
      }

      const err: string = error?.toString?.() ?? '';
      let errMsg = '';
      if (err?.includes('Network Error')) {
        errMsg = $t('ui.fallback.http.networkError');
      } else if (error?.message?.includes?.('timeout')) {
        errMsg = $t('ui.fallback.http.requestTimeout');
      }
      if (errMsg) {
        makeErrorMessage?.(errMsg, error);
        return Promise.reject(error);
      }

      let errorMessage: string;
      const status = error?.response?.status;

      switch (status) {
        case 400: {
          errorMessage = $t('ui.fallback.http.badRequest');
          break;
        }
        case 401: {
          errorMessage = $t('ui.fallback.http.unauthorized');
          break;
        }
        case 403: {
          errorMessage = $t('ui.fallback.http.forbidden');
          break;
        }
        case 404: {
          errorMessage = $t('ui.fallback.http.notFound');
          break;
        }
        case 408: {
          errorMessage = $t('ui.fallback.http.requestTimeout');
          break;
        }
        default: {
          errorMessage = '[ '+status+' ]'+ $t('ui.fallback.http.internalServerError');
        }
      }
      makeErrorMessage?.(errorMessage, error);
      return Promise.reject(error);
    },
  };
};
