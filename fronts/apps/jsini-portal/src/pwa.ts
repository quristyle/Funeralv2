/**
 * PWA 서비스워커 등록.
 *
 * vben 의 vite-config 가 vite-plugin-pwa 를 `injectRegister: false` 로 켜 두므로
 * 등록은 앱이 직접 한다 (이 함수를 main.ts 가 부른다).
 *
 * `virtual:pwa-register` 를 쓰지 않고 **직접 등록한다** — 그 가상 모듈은
 * workbox-window 의존이 하나 더 붙고, 개발 서버에서 동적 로드가 종종 깨졌다.
 * 워크박스가 만든 서비스워커의 파일 이름은 플러그인 규약으로 고정이다:
 *   · 빌드: /sw.js
 *   · 개발: /dev-sw.js?dev-sw (devOptions.enabled 가 켜 둔다)
 * registerType 이 autoUpdate 라 서비스워커 쪽(skipWaiting · clientsClaim)이
 * 새 버전을 스스로 활성화한다 — 여기서는 등록과 주기적 갱신 확인만 한다.
 *
 * 서비스워커는 두 가지 일을 한다:
 *   1. 설치 가능(PWA) 요건 — 매니페스트와 함께 portal.jsini.co.kr 을
 *      모바일 홈 화면에 앱으로 얹을 수 있게 한다.
 *   2. 웹푸시 수신 — public/push-sw.js (워크박스 sw 가 importScripts 로 실행)가
 *      NotificationServer 가 보낸 알림을 띄운다. 구독은 [알림 설정] 화면이 한다.
 *
 * 등록 실패는 삼킨다 — 지원하지 않는 브라우저(구형 · http 비보안 오리진)에서
 * 앱 자체가 죽으면 안 된다. localhost 는 보안 오리진으로 취급돼 개발에서도 돈다.
 */
export async function setupPwa() {
  if (!('serviceWorker' in navigator)) return;

  const swUrl = import.meta.env.DEV ? '/dev-sw.js?dev-sw' : '/sw.js';

  try {
    const registration = await navigator.serviceWorker.register(swUrl, {
      scope: '/',
    });

    // 오래 켜 두는 업무 화면이 많다 — 탭이 다시 보일 때 새 버전을 확인한다.
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') {
        registration.update().catch(() => {});
      }
    });
  } catch (error) {
    console.warn('[PWA] 서비스워커 등록 실패', error);
  }
}
