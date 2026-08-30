import { initPreferences } from '@vben/preferences';
import { unmountGlobalLoading } from '@vben/utils';

import { jsiniPreferencesExtension, overridesPreferences } from './preferences';
import { setupPwa } from './pwa';

/**
 * 애플리케이션 초기화가 완료된 후 페이지 로드 및 렌더링을 진행합니다
 */
async function initApplication() {
  // name은 프로젝트의 고유 식별자를 지정하는 데 사용됩니다
  // 다른 프로젝트의 환경 설정, 저장된 데이터의 키 접두사 및 격리가 필요한 기타 데이터를 구분하는 데 사용됩니다
  const env = import.meta.env.PROD ? 'prod' : 'dev';
  const appVersion = import.meta.env.VITE_APP_VERSION;
  const namespace = `${import.meta.env.VITE_APP_NAMESPACE}-${appVersion}-${env}`;

  // 앱 환경 설정 초기화
  //
  // `extension` 은 우리가 더한 항목이다(지금은 AI 모델 선택 하나).
  // 환경설정 창에 탭으로 붙고, 값은 계정에 저장된다.
  await initPreferences({
    namespace,
    overrides: overridesPreferences,
    extension: jsiniPreferencesExtension,
  });

  // 앱 실행 및 마운트
  // vue 앱의 주요 로직 및 뷰
  const { bootstrap } = await import('./bootstrap');
  await bootstrap(namespace);

  // 로딩 제거 및 종료
  unmountGlobalLoading();

  // PWA 서비스워커 — 설치(홈 화면 추가)와 웹푸시 수신을 맡는다.
  // 앱 마운트 뒤에 등록해 첫 화면 로딩과 경쟁하지 않게 한다.
  setupPwa();
}

initApplication();
