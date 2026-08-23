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
    // 로그인 후 첫 화면과 루트('/') 리다이렉트 대상.
    // 프레임워크 기본값은 '/analytics' 인데 이 포털의 첫 화면은 '/workspace' 다.
    //
    // 계정마다 다르게 두려면 이 값이 아니라 계정 프로필의 HomePath 를 쓴다
    // (scom.account_profile_details, detail_type='HomePath').
    // 그 값이 있으면 그쪽이 우선이고, 없을 때 여기 값이 쓰인다.
    defaultHomePath: '/workspace',
  },
  logo: {
    source: '/jsini.svg',
    sourceDark: '/jsini_dark.svg',
  },
  theme: {
    // 기본 제공 테마. 라이트/다크에 쓸 주 색상을 테마가 각각 들고 있으므로
    // colorPrimary 를 여기서 따로 박지 않는다.
    //   gray → 라이트 hsl(240 5.9% 10%) / 다크 hsl(0 0% 98%)
    //
    // 예전에는 colorPrimary: 'hsl(0 0% 98%)' 가 박혀 있었다. 다크 모드용 값이라
    // 라이트 모드에서 선택된 메뉴 글자(--menu-item-active-color: hsl(var(--primary)))가
    // 배경과 같은 색이 되어 보이지 않았다.
    builtinType: 'gray',
    // 시스템 기본 글자 크기(px). 프레임워크 기본값은 16 인데 이 포털은 표·그리드가
    // 많아 한 화면에 들어가는 양을 늘리려고 낮춰 쓴다.
    //
    // 이 값은 "저장된 설정이 없을 때"의 기본값이다. 사용자가 환경설정에서 한 번
    // 바꾸면 그 값이 로컬스토리지(`jsini-portal-web-preferences`)에 남고 그쪽이 우선한다.
    // 그래서 여기를 고쳐도 이미 쓰던 브라우저에는 바로 반영되지 않는다 —
    // 환경설정 창의 초기화를 누르거나 로컬스토리지를 비워야 한다.
    fontSize: 14,
    mode: 'auto',
    radius: '0.25',
  },
});
