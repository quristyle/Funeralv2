import { ref, unref } from 'vue';

import { DEFAULT_TIME_ZONE_OPTIONS } from '@vben-core/preferences';
import {
  getCurrentTimezone,
  setCurrentTimezone,
} from '@vben-core/shared/utils';

import { acceptHMRUpdate, defineStore } from 'pinia';

interface TimezoneHandler {
  getTimezone?: () => Promise<null | string | undefined>;
  getTimezoneOptions?: () => Promise<
    {
      label: string;
      value: string;
    }[]
  >;
  setTimezone?: (timezone: string) => Promise<void>;
}

/**
 * 기본 시간대 처리 모듈
 * 시간대 저장은 Pinia 스토리지 플러그인을 기반으로 함
 */
const getDefaultTimezoneHandler = (): TimezoneHandler => {
  return {
    getTimezoneOptions: () => {
      return Promise.resolve(
        DEFAULT_TIME_ZONE_OPTIONS.map((item) => {
          return {
            label: item.label,
            value: item.timezone,
          };
        }),
      );
    },
  };
};

/**
 * 사용자 정의 시간대 처리 모듈
 */
let customTimezoneHandler: null | Partial<TimezoneHandler> = null;
const setTimezoneHandler = (handler: Partial<TimezoneHandler>) => {
  customTimezoneHandler = handler;
};

/**
 * 시간대 처리 모듈 가져오기
 */
const getTimezoneHandler = () => {
  return {
    ...getDefaultTimezoneHandler(),
    ...customTimezoneHandler,
  };
};

/**
 * 시간대 지원 스토어
 */
const useTimezoneStore = defineStore(
  'core-timezone',
  () => {
    const timezoneRef = ref(getCurrentTimezone());

    /**
     * 시간대 초기화
     */
    async function initTimezone() {
      const timezoneHandler = getTimezoneHandler();
      const timezone = await timezoneHandler.getTimezone?.();
      if (timezone) {
        timezoneRef.value = timezone;
      }
      // dayjs 기본 시간대 설정
      setCurrentTimezone(unref(timezoneRef));
    }

    /**
     * 시간대 설정
     * @param timezone 시간대 문자열
     */
    async function setTimezone(timezone: string) {
      const timezoneHandler = getTimezoneHandler();
      await timezoneHandler.setTimezone?.(timezone);
      timezoneRef.value = timezone;
      // dayjs 기본 시간대 설정
      setCurrentTimezone(timezone);
    }

    /**
     * 시간대 옵션 가져오기
     */
    async function getTimezoneOptions() {
      const timezoneHandler = getTimezoneHandler();
      return (await timezoneHandler.getTimezoneOptions?.()) || [];
    }

    initTimezone().catch((error) => {
      console.error('Failed to initialize timezone during store setup:', error);
    });

    function $reset() {
      timezoneRef.value = getCurrentTimezone();
    }

    return {
      timezone: timezoneRef,
      setTimezone,
      getTimezoneOptions,
      $reset,
    };
  },
  {
    persist: {
      // 지속성(Persistence)
      pick: ['timezone'],
    },
  },
);

export { setTimezoneHandler, useTimezoneStore };

// Hot Module Replacement(HMR) 문제 해결
const hot = import.meta.hot;
if (hot) {
  hot.accept(acceptHMRUpdate(useTimezoneStore, hot));
}
