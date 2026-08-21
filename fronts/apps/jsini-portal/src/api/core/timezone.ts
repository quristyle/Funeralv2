import { requestClient } from '#/api/request';

/**
 * 시스템에서 지원하는 타임존 목록 가져오기
 */
export async function getTimezoneOptionsApi() {
  return await requestClient.get<
    {
      label: string;
      value: string;
    }[]
  >('/auth/timezone/getTimezoneOptions');
}
/**
 * 사용자 타임존 가져오기
 */
export async function getTimezoneApi(): Promise<null | string | undefined> {
  return requestClient.get<null | string | undefined>('/auth/timezone/getTimezone');
}
/**
 * 사용자 타임존 설정
 * @param timezone 타임존
 */
export async function setTimezoneApi(timezone: string): Promise<void> {
  return requestClient.post('/auth/timezone/setTimezone', { timezone });
}
