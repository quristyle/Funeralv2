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
    /**
     * 비밀번호 사용 기간(기본 90일)이 지났는지.
     *
     * true 라도 토큰은 정상 발급된다 — 비밀번호를 바꾸려면 로그인 상태여야 하기 때문이다.
     * 대신 게이트웨이가 비밀번호 변경에 필요한 경로만 통과시키므로,
     * 화면은 이 값을 보고 곧바로 비밀번호 변경으로 안내해야 한다.
     */
    passwordExpired: boolean;
    /** 만료 기준 일수. 정책이 꺼져 있으면 null 이다. */
    passwordExpiryDays: null | number;
    /** 만료까지 남은 일수. 이미 지났으면 0, 정책이 꺼져 있으면 null 이다. */
    passwordDaysRemaining: null | number;
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
  const result = response?.result?.[0];

  return {
    accessToken: result?.accessToken || null,
    // 비밀번호 사용 기간. 만료면 게이트웨이가 다른 요청을 막으므로
    // 로그인 직후 곧바로 비밀번호 변경 화면으로 보내야 한다.
    passwordExpired: !!result?.passwordExpired,
    passwordExpiryDays: result?.passwordExpiryDays ?? null,
    passwordDaysRemaining: result?.passwordDaysRemaining ?? null,
  };
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
  return requestClient.post('/auth/logout', null, {
    withCredentials: true,
  });
}

/**
 * 사용자 권한 코드 가져오기
 */
export async function getAccessCodesApi() {
  return requestClient.get<string[]>('/auth/codes');
}
