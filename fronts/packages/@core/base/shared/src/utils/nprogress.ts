import type NProgress from 'nprogress';

// NProgress 인스턴스 변수 생성, 초기값은 null
let nProgressInstance: null | typeof NProgress = null;

/**
 * NProgress 라이브러리를 동적으로 로드하고 설정합니다.
 * 이 함수는 먼저 NProgress 라이브러리가 이미 로드되었는지 확인하고, 이미 로드된 경우 NProgress 인스턴스를 직접 반환합니다.
 * 그렇지 않으면 NProgress 라이브러리를 동적으로 가져와서 설정한 후 NProgress 인스턴스를 반환합니다.
 *
 * @returns NProgress 인스턴스의 Promise 객체.
 */
async function loadNprogress() {
  if (nProgressInstance) {
    return nProgressInstance;
  }
  nProgressInstance = await import('nprogress');
  nProgressInstance.configure({
    showSpinner: true,
    speed: 300,
  });
  return nProgressInstance;
}

/**
 * 프로그레스 바 표시를 시작합니다.
 * 이 함수는 먼저 NProgress 라이브러리를 로드한 후 NProgress의 start 메서드를 호출하여 프로그레스 바 표시를 시작합니다.
 */
async function startProgress() {
  const nprogress = await loadNprogress();
  nprogress?.start();
}

/**
 * 프로그레스 바 표시를 중단하고 숨깁니다.
 * 이 함수는 먼저 NProgress 라이브러리를 로드한 후 NProgress의 done 메서드를 호출하여 프로그레스 바를 중단하고 숨깁니다.
 */
async function stopProgress() {
  const nprogress = await loadNprogress();
  nprogress?.done();
}

export { startProgress, stopProgress };
