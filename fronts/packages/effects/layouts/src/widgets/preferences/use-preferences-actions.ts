import { computed } from 'vue';

import { $t, loadLocaleMessages } from '@vben/locales';
import {
  clearCache,
  preferences,
  resetPreferences,
  usePreferences,
} from '@vben/preferences';

import { globalShareState } from '@vben-core/shared/global-state';

import { useClipboard } from '@vueuse/core';

/**
 * 환경설정 화면의 동작들 — 초기화 · 설정 복사 · 캐시 삭제.
 *
 * 드로어(헤더 톱니)와 `/setting/environment` 페이지가 같은 동작을 해야 하므로
 * 껍데기와 분리해 둔다. 한쪽에만 고쳐지는 일을 막는다.
 *
 * 탭·설정 항목 자체는 `preferences-panel.vue` 가 갖는다.
 * 여기 있는 것은 **껍데기가 쓰는 것**(툴바 버튼·바닥 버튼)뿐이다.
 */
export function usePreferencesActions(onClearAndLogout: () => void) {
  const message = globalShareState.getMessage();
  const { copy } = useClipboard({ legacy: true });
  const { diffCustomPreference, diffPreference } = usePreferences();

  /**
   * 기본값과 다른 부분만 모은 것.
   *
   * 초기화·복사 버튼을 켤지 끌지 판단하는 근거다.
   * 사용자별로 서버에 저장하는 값도 이것이다 — 전체가 아니라 **차이만** 저장한다.
   * 전체를 저장하면 나중에 프레임워크 기본값이 바뀌어도 옛 값이 박혀 따라오지 않는다.
   */
  const mergedDiffPreference = computed(() => {
    const result: Record<string, unknown> = {};

    if (diffPreference.value) {
      Object.assign(result, diffPreference.value);
    }

    if (diffCustomPreference.value) {
      result.custom = diffCustomPreference.value;
    }

    return Object.keys(result).length > 0 ? result : undefined;
  });

  async function handleCopy() {
    await copy(JSON.stringify(mergedDiffPreference.value, null, 2));

    message.copyPreferencesSuccess?.(
      $t('preferences.copyPreferencesSuccessTitle'),
      $t('preferences.copyPreferencesSuccess'),
    );
  }

  async function handleClearCache() {
    await resetPreferences();
    await clearCache();
    onClearAndLogout();
  }

  async function handleReset() {
    if (!mergedDiffPreference.value) {
      return;
    }
    await resetPreferences();
    await loadLocaleMessages(preferences.app.locale);
  }

  return {
    handleClearCache,
    handleCopy,
    handleReset,
    mergedDiffPreference,
  };
}
