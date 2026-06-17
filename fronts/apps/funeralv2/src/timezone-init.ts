import { setTimezoneHandler } from '@vben/stores';

import { getTimezoneApi, getTimezoneOptionsApi, setTimezoneApi } from '#/api';

/**
 * API를 통해 타임존 설정을 저장하고 타임존 처리를 초기화합니다.
 */
export function initTimezone() {
  setTimezoneHandler({
    getTimezone() {
      return getTimezoneApi();
    },
    setTimezone(timezone: string) {
      return setTimezoneApi(timezone);
    },
    getTimezoneOptions() {
      return getTimezoneOptionsApi();
    },
  });
}
