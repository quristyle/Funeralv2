import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';

import { preferences } from '@vben/preferences';

function useContentSpinner() {
  const spinning = ref(false);
  const startTime = ref(0);
  const router = useRouter();
  const minShowTime = 500; // 최소 표시 시간
  const enableLoading = computed(() => preferences.transition.loading);

  // 로딩 애니메이션 종료
  const onEnd = () => {
    if (!enableLoading.value) {
      return;
    }
    const processTime = performance.now() - startTime.value;
    if (processTime < minShowTime) {
      setTimeout(() => {
        spinning.value = false;
      }, minShowTime - processTime);
    } else {
      spinning.value = false;
    }
  };

  // 라우터 가드 (BeforeEach)
  router.beforeEach((to) => {
    if (to.meta.loaded || !enableLoading.value || to.meta.iframeSrc) {
      return true;
    }
    startTime.value = performance.now();
    spinning.value = true;
    return true;
  });

  // 라우터 가드 (AfterEach)
  router.afterEach((to) => {
    if (to.meta.loaded || !enableLoading.value || to.meta.iframeSrc) {
      return true;
    }
    onEnd();
    return true;
  });

  return { spinning };
}

export { useContentSpinner };
