import type { Watermark, WatermarkOptions } from 'watermark-js-plus';

import { nextTick, onUnmounted, readonly, ref } from 'vue';

const watermark = ref<Watermark>();
const unmountedHooked = ref<boolean>(false);
const cachedOptions = ref<Partial<WatermarkOptions>>({
  advancedStyle: {
    colorStops: [
      {
        color: 'gray',
        offset: 0,
      },
      {
        color: 'gray',
        offset: 1,
      },
    ],
    type: 'linear',
  },
  // fontSize: '20px',
  content: '',
  contentType: 'multi-line-text',
  globalAlpha: 0.25,
  gridLayoutOptions: {
    cols: 2,
    gap: [20, 20],
    matrix: [
      [1, 0],
      [0, 1],
    ],
    rows: 2,
  },
  height: 200,
  layout: 'grid',
  rotate: 30,
  width: 160,
});

export function useWatermark() {
  async function initWatermark(options: Partial<WatermarkOptions>) {
    const { Watermark } = await import('watermark-js-plus');

    cachedOptions.value = {
      ...cachedOptions.value,
      ...options,
    };
    watermark.value = new Watermark(cachedOptions.value);
    await watermark.value?.create();
  }

  async function updateWatermark(options: Partial<WatermarkOptions>) {
    if (watermark.value) {
      await nextTick();
      await watermark.value?.changeOptions({
        ...cachedOptions.value,
        ...options,
      });
    } else {
      await initWatermark(options);
    }
  }

  function destroyWatermark() {
    if (watermark.value) {
      watermark.value.destroy();
      watermark.value = undefined;
    }
  }

  // 워터마크가 라우트 전환 시 파괴되는 것을 방지하기 위해 첫 번째 호출 시에만 언마운트 훅을 등록합니다.
  if (!unmountedHooked.value) {
    unmountedHooked.value = true;
    onUnmounted(() => {
      destroyWatermark();
    });
  }

  return {
    destroyWatermark,
    updateWatermark,
    watermark: readonly(watermark),
  };
}
