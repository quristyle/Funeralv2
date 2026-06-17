import { defineOverridesPreferences } from '@vben/preferences';

/**
 * @description 프로젝트 설정 파일
 * 프로젝트의 일부 설정만 덮어쓰면 되며, 필요하지 않은 설정은 덮어쓸 필요 없이 자동으로 기본 설정이 사용됩니다.
 * !!! 설정을 변경한 후에는 캐시를 비워주세요. 그렇지 않으면 적용되지 않을 수 있습니다.
 */
export const overridesPreferences = defineOverridesPreferences({
  // overrides
  app: {
    name: import.meta.env.VITE_APP_TITLE,
    enableCheckUpdates: false,
    accessMode: 'backend',
  },
  logo: {
    source: '/jsini.svg',
    sourceDark: '/jsini_dark.svg',
  },
  "theme": {
    "builtinType": "gray",
    "colorPrimary": "hsl(0 0% 98%)",
    "mode": "auto",
    "radius": "0.25",
    "fontSize": 15
  }
});
