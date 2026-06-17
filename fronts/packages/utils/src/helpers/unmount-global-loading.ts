/**
 * 로딩을 제거하고 해제합니다.
 * index.html의 app 태그 내에 두지 않고 여기에 두는 이유는 부자연스러움을 줄이기 위함이며, 렌더링이 너무 빠르면 깜빡임이 발생할 수 있기 때문입니다.
 * 먼저 CSS 애니메이션을 추가하여 숨긴 후, 애니메이션이 끝나면 loading 노드를 제거하여 사용자 경험을 개선합니다.
 * 단점은 코드량이 약간 증가한다는 것입니다.
 * 커스텀 로딩은 다음을 참조하세요: https://doc.vben.pro/guide/in-depth/loading.html
 */
export function unmountGlobalLoading() {
  // 전역 로딩 요소 찾기
  const loadingElement = document.querySelector('#__app-loading__');

  if (loadingElement) {
    // hidden 클래스를 추가하여 트랜지션 애니메이션 트리거
    loadingElement.classList.add('hidden');

    // 제거해야 할 주입된 모든 로딩 요소 찾기
    const injectLoadingElements = document.querySelectorAll(
      '[data-app-loading^="inject"]',
    );

    // 트랜지션 애니메이션이 끝나면 로딩 요소와 주입된 모든 로딩 요소를 제거합니다.
    loadingElement.addEventListener(
      'transitionend',
      () => {
        loadingElement.remove(); // 로딩 요소 제거
        injectLoadingElements.forEach((el) => el.remove()); // 주입된 모든 로딩 요소 제거
      },
      { once: true },
    ); // 이벤트가 한 번만 트리거되도록 설정
  }
}
