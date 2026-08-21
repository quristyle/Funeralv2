import { ref } from 'vue';
import { defineStore } from 'pinia';
import { getBizSelectConfigs } from '#/api/portal/system/biz-select-config';
import type { BizSelectConfigApi } from '#/api/portal/system/biz-select-config';

export const useBizSelectStore = defineStore('biz-select-config', () => {
  const configs = ref<BizSelectConfigApi.BizSelectConfig[]>([]);
  const isLoaded = ref(false);
  const loading = ref(false);

  /**
   * 전체 BizSelect 설정을 조회하여 캐싱합니다.
   * 이미 캐시가 존재하면 즉시 반환하여 네트워크 요청을 방지합니다.
   * forceRefresh가 true인 경우 강제로 재요청합니다.
   */
  async function loadConfigs(forceRefresh = false) {
    if (isLoaded.value && !forceRefresh) {
      return configs.value;
    }

    loading.value = true;
    try {
      const response = await getBizSelectConfigs();
      const rawList = (response as any)?.result ?? response;
      configs.value = Array.isArray(rawList) ? rawList : [];
      isLoaded.value = true;
    } catch (error) {
      console.error('Failed to load BizSelect configs from metadata db:', error);
      throw error;
    } finally {
      loading.value = false;
    }

    return configs.value;
  }

  /**
   * 특정 비즈니스 타입의 설정을 찾습니다.
   */
  async function getConfigByType(bizType: string) {
    if (!isLoaded.value) {
      await loadConfigs();
    }
    return configs.value.find((c) => c.bizType === bizType) || null;
  }

  /**
   * 캐시를 무효화(초기화)합니다.
   */
  function clearCache() {
    configs.value = [];
    isLoaded.value = false;
  }

  return {
    configs,
    isLoaded,
    loading,
    loadConfigs,
    getConfigByType,
    clearCache,
  };
});
