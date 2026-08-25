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
export async function updateProfileApi(data: { realName?: string; introduction?: string; email?: string; phone?: string; avatar?: string; avatarGroupId?: string }) {
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
