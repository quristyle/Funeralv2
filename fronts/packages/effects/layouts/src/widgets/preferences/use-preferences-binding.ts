import { computed } from 'vue';

import { loadLocaleMessages } from '@vben/locales';
import { preferences, updatePreferences } from '@vben/preferences';
import { capitalizeFirstLetter } from '@vben/utils';

/**
 * 환경설정 스토어를 컴포넌트 props · listener 로 바꿔 주는 바인딩.
 *
 * `preferences.widget.fullscreen` →  prop `widgetFullscreen`
 *                                    listener `update:widgetFullscreen`
 *
 * **항목을 하나하나 나열하지 않는다.** 스토어를 훑어 만들기 때문에
 * 설정 항목이 늘어도 이 파일은 고치지 않는다.
 *
 * 원래 `preferences.vue` 안에 있던 것을 빼냈다. 같은 바인딩을 두 곳이 쓴다.
 *   - 헤더 톱니 → 드로어  (`preferences.vue`)
 *   - `/setting/environment` 페이지 (`preferences-view.vue`)
 * 한쪽에만 새 설정이 붙는 일이 없도록 구현을 하나로 둔다.
 */
export function usePreferencesBinding() {
  const attrs = computed(() => {
    const result: Record<string, any> = {};
    for (const [key, value] of Object.entries(preferences)) {
      for (const [subKey, subValue] of Object.entries(value)) {
        result[`${key}${capitalizeFirstLetter(subKey)}`] = subValue;
      }
    }
    return result;
  });

  const listen = computed(() => {
    const result: Record<string, any> = {};
    for (const [key, value] of Object.entries(preferences)) {
      if (typeof value === 'object') {
        for (const subKey of Object.keys(value)) {
          result[`update:${key}${capitalizeFirstLetter(subKey)}`] = (
            val: any,
          ) => {
            updatePreferences({ [key]: { [subKey]: val } });
            if (key === 'app' && subKey === 'locale') {
              loadLocaleMessages(val);
            }
          };
        }
      } else {
        result[key] = value;
      }
    }
    return result;
  });

  return { attrs, listen };
}
