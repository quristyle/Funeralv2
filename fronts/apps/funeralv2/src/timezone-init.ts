import { setTimezoneHandler } from '@vben/stores';

import { getTimezoneApi, getTimezoneOptionsApi, setTimezoneApi } from '#/api';

/**
 * API를 통해 타임존 설정을 저장하고 타임존 처리를 초기화합니다.
 */
export function initTimezone() {
  setTimezoneHandler({
    async getTimezone() {
      const res = await getTimezoneApi();
      const timezone = (res as any)?.result?.[0] ?? (res as any)?.result ?? res;
      return typeof timezone === 'string' ? timezone : null;
    },
    setTimezone(timezone: string) {
      return setTimezoneApi(timezone);
    },
    async getTimezoneOptions() {
      const res = await getTimezoneOptionsApi();
      const list = (res as any)?.result ?? res;
      return Array.isArray(list) ? list : [];
    },
  });
}
