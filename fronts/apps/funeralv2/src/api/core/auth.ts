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
  // requestClient는 응답의 'data' 필드를 자동으로 추출합니다.
  // 백엔드 응답이 { data: { result: [{ accessToken: '...' }] } } 구조를 가지므로,
  // response 변수에는 { result: [{ accessToken: '...' }] } 객체가 할당됩니다.
  const response = await requestClient.post<any>('/auth/login', data, {
    withCredentials: true,
  });

  // 새로운 응답 구조에 맞춰 accessToken을 추출합니다.
  // response.result가 배열이고 첫 번째 요소가 존재하는지 확인합니다.
  const accessToken = response?.result?.[0]?.accessToken || null;

  return { accessToken };
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
