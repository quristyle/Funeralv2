import { baseRequestClient, requestClient } from '#/api/request';

export namespace AuthApi {
  /** 로그인 API 파라미터 */
  export interface LoginParams {
    password?: string;
    username?: string;
  }

  /** 로그인 API 반환 값 */
  export interface LoginResult {
    accessToken: string;
  }

  export interface RefreshTokenResult {
    data: string;
    status: number;
  }
}

/**
 * 로그인
 */
export async function loginApi(data: AuthApi.LoginParams) {
  return requestClient.post<AuthApi.LoginResult>('/auth/login', data, {
    withCredentials: true,
  });
}

/**
 * accessToken 갱신
 */
export async function refreshTokenApi() {
  return baseRequestClient.post<AuthApi.RefreshTokenResult>(
    '/auth/refresh',
    null,
    {
      withCredentials: true,
    },
  );
}

/**
 * 로그아웃
 */
export async function logoutApi() {
  return baseRequestClient.post('/auth/logout', null, {
    withCredentials: true,
  });
}

/**
 * 사용자 권한 코드 가져오기
 */
export async function getAccessCodesApi() {
  return requestClient.get<string[]>('/auth/codes');
}
