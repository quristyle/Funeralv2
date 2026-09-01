<script lang="ts" setup>
import { computed, ref, watch } from 'vue';
import { Image as AImage } from 'ant-design-vue';

/**
 * ImagePreview Props 정의
 */
interface Props {
  /** 이미지 URL 경로 */
  src?: string | null;
  /**
   * `src` 를 못 받아왔을 때 대신 시도할 URL 경로.
   *
   * 축소본 경로(`/api/file/thumbnail/...`)를 먼저 쓰고 원본 경로를 예비로 두는 화면이 있다.
   * 축소본은 메타데이터의 `is_image` 판정에 걸려 400 이 날 수 있는데, 그때 아무것도 안
   * 보이는 대신 원본으로 한 번 더 시도한다.
   */
  fallbackSrc?: string | null;
  /** 클릭 시 크게 볼 원본 이미지 URL 경로 */
  previewSrc?: string | null;
  /** 이미지 너비 (기본값: 40) */
  width?: number | string;
  /** 이미지 높이 (기본값: 40) */
  height?: number | string;
  /** 이미지가 없을 때 노출될 기본 텍스트/이모지 */
  fallbackText?: string;
  /** 대체 텍스트 (alt) */
  alt?: string;
}

const props = withDefaults(defineProps<Props>(), {
  src: null,
  fallbackSrc: null,
  previewSrc: null,
  width: 40,
  height: 40,
  fallbackText: '📷',
  alt: 'Preview',
});

/**
 * 지금 몇 번째 후보를 시도하고 있는지. 0 = `src`, 1 = `fallbackSrc`,
 * 후보를 다 쓰면 폴백 UI 를 그린다.
 */
const attempt = ref(0);

/** 시도할 URL 후보 목록 (빈 값과 중복은 제외) */
const candidates = computed(() =>
  [props.src, props.fallbackSrc].filter(
    (url, index, list): url is string => !!url && list.indexOf(url) === index,
  ),
);

const currentSrc = computed(() => candidates.value[attempt.value] ?? null);

// 행이 바뀌어 src 가 갈리면 다시 처음 후보부터 시도한다.
watch(
  () => [props.src, props.fallbackSrc],
  () => {
    attempt.value = 0;
  },
);

/**
 * 이미지를 못 받아왔을 때. 다음 후보가 있으면 그것으로 넘어가고,
 * 없으면 폴백 UI 를 그린다 — 깨진 이미지 아이콘만 남는 것을 막는다.
 */
function handleError() {
  attempt.value += 1;
}
</script>

<template>
  <div class="flex items-center justify-center p-0.5">
    <!-- 이미지가 존재하는 경우 Ant Design Vue의 Image 컴포넌트로 미리보기 지원 -->
    <AImage
      v-if="currentSrc"
      :key="currentSrc"
      :src="currentSrc"
      :width="width"
      :height="height"
      class="object-contain rounded shadow border cursor-zoom-in"
      :alt="alt"
      :preview="previewSrc ? { src: previewSrc } : true"
      @error="handleError"
    />
    <!-- 이미지가 없거나 전부 실패한 경우 폴백 UI 표출 -->
    <div
      v-else
      :style="{
        width: typeof width === 'number' ? `${width}px` : width,
        height: typeof height === 'number' ? `${height}px` : height,
      }"
      class="bg-muted rounded flex items-center justify-center text-xs text-muted-foreground border select-none font-mono"
    >
      <slot name="fallback">
        <span>{{ fallbackText }}</span>
      </slot>
    </div>
  </div>
</template>
