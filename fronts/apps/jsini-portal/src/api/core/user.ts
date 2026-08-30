import type { UserInfo } from '@vben/types';

import { requestClient, requestListClient } from '#/api/request';

/**
 * 사용자 정보 가져오기
 */
export async function getUserInfoApi() {
  try {
    const response = await requestClient.get<any>('/auth/user/info');
    console.log('check response', response);

    const userInfo = response?.result?.[0] ?? ({} as UserInfo);
    console.log('userInfo', userInfo);

    return userInfo;
  } catch (error) {
    console.warn('사용자정보 가져오기 에러', error);
    return {} as UserInfo;
  }
}

/** 접속 기록 한 줄 */
export interface LoginLog {
  at?: null | string;
  /** 브라우저·기기를 줄인 것 (`Chrome · Windows`) */
  device?: null | string;
  /** 실패 이유. 성공이면 null */
  failReason?: null | string;
  ip?: null | string;
  success: boolean;
  /** 브라우저·기기 원문 */
  userAgent?: null | string;
}

/** 계정 활동 정보 */
export interface AccountActivity {
  /** 계정을 써 온 일수 */
  accountAgeDays: number;
  /** 가장 최근 실패 */
  lastFail?: LoginLog | null;
  /** 로그인 성공 횟수 */
  loginCount: number;
  /** 지난번 접속 (지금 이 접속의 바로 앞) */
  previousLogin?: LoginLog | null;
  /** 최근 접속 기록. 최신 순. 성공·실패를 섞어 담는다. */
  recent: LoginLog[];
  /** 최근 30일 안의 실패 횟수 */
  recentFailCount: number;
}

/**
 * 계정 활동 정보 가져오기.
 *
 * **자기 것만 온다.** 조회할 계정을 보내지 않고 서버가 토큰의 신원을 쓴다.
 */
export async function getAccountActivityApi(limit = 10) {
  const res = await requestClient.get<any>('/auth/user/activity', {
    params: { limit },
  });
  const raw = res?.result?.[0] ?? res?.result ?? res;
  return (raw ?? {}) as AccountActivity;
}

/**
 * 사용자 기본 프로필 수정
 */
export async function updateProfileApi(data: { realName?: string; introduction?: string; email?: string; phone?: string; avatar?: string; avatarGroupId?: string; birthDate?: string; birthDateIsLunar?: boolean }) {
  return requestClient.post('/auth/user/profile', data);
}

/**
 * 사용자 비밀번호 변경
 */
export async function changePasswordApi(data: any) {
  return requestClient.post('/auth/user/change-password', data);
}

/**
 * 사용자 설정(보안/알림) 업데이트
 */
export async function updateSettingApi(data: { fieldName: string; value: boolean }) {
  return requestClient.post('/auth/user/settings', data);
}

/**
 * 화면 환경설정 가져오기 (계정에 저장된 것)
 *
 * 기본값과 **다른 항목만** 담겨 있다. 저장된 것이 없으면 빈 객체다.
 * 브라우저 로컬스토리지가 아니라 계정에 붙어 있으므로 다른 PC 에서도 따라온다.
 */
export async function getUserPreferencesApi() {
  const response = await requestClient.get<any>('/auth/user/preferences', {
    // 곁들이는 요청이다. 실패하면 로컬 설정으로 그대로 쓰면 되므로 오류 토스트를 띄우지 않는다.
    // (백엔드를 아직 다시 띄우지 않았으면 404 가 온다 — 그때 화면마다 토스트가 뜨면 안 된다)
    skipErrorMessage: true,
  } as any);
  return (response?.result?.[0]?.payload ?? {}) as Record<string, any>;
}

/**
 * 화면 환경설정 저장 (계정에 저장)
 *
 * 전체가 아니라 기본값과의 차이만 보낸다 — 전체를 저장하면 나중에 프레임워크
 * 기본값이 바뀌어도 옛 값이 박혀 따라오지 않는다.
 */
export async function saveUserPreferencesApi(payload: Record<string, any>) {
  return requestClient.put('/auth/user/preferences', { payload }, {
    // 저장 실패도 조용히 넘긴다. 로컬스토리지에는 이미 남아 이 브라우저는 정상 동작하고,
    // 다음 변경 때 다시 시도한다. 설정을 만질 때마다 토스트가 뜨면 그게 더 방해된다.
    skipErrorMessage: true,
  } as any);
}
