import { updatePreferences, usePreferences } from '@vben/preferences';
/**
 * 본문 영역 최대화
 */
export function useContentMaximize() {
  const { contentIsMaximize } = usePreferences();

  function toggleMaximize() {
    const isMaximize = contentIsMaximize.value;

    updatePreferences({
      header: {
        hidden: !isMaximize,
      },
      sidebar: {
        hidden: !isMaximize,
      },
    });
  }
  return {
    contentIsMaximize,
    toggleMaximize,
  };
}
