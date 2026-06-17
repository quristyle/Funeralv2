import type {
  ApplicationConfig,
  VbenAdminProAppConfigRaw,
} from '@vben/types/global';

/**
 * vite-inject-app-config에서 주입한 전역 설정
 */
export function useAppConfig(
  env: Record<string, any>,
  isProduction: boolean,
): ApplicationConfig {
  // 프로덕션 환경에서는 window._VBEN_ADMIN_PRO_APP_CONF_ 전역 변수를 직접 사용합니다.
  const config = isProduction
    ? window._VBEN_ADMIN_PRO_APP_CONF_
    : (env as VbenAdminProAppConfigRaw);

  const {
    VITE_GLOB_API_URL,
    VITE_GLOB_AUTH_DINGDING_CORP_ID,
    VITE_GLOB_AUTH_DINGDING_CLIENT_ID,
  } = config;

  const applicationConfig: ApplicationConfig = {
    apiURL: VITE_GLOB_API_URL,
    auth: {},
  };
  if (VITE_GLOB_AUTH_DINGDING_CORP_ID && VITE_GLOB_AUTH_DINGDING_CLIENT_ID) {
    applicationConfig.auth.dingding = {
      clientId: VITE_GLOB_AUTH_DINGDING_CLIENT_ID,
      corpId: VITE_GLOB_AUTH_DINGDING_CORP_ID,
    };
  }

  return applicationConfig;
}
