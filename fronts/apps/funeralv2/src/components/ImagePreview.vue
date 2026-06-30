<script lang="ts" setup>
import { Image as AImage } from 'ant-design-vue';

/**
 * ImagePreview Props 정의
 */
interface Props {
  /** 이미지 URL 경로 */
  src?: string | null;
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

withDefaults(defineProps<Props>(), {
  src: null,
  previewSrc: null,
  width: 40,
  height: 40,
  fallbackText: '📷',
  alt: 'Preview',
});
</script>

<template>
  <div class="flex items-center justify-center p-0.5">
    <!-- 이미지가 존재하는 경우 Ant Design Vue의 Image 컴포넌트로 미리보기 지원 -->
    <AImage
      v-if="src"
      :src="src"
      :width="width"
      :height="height"
      class="object-contain rounded shadow border cursor-zoom-in"
      :alt="alt"
      :preview="previewSrc ? { src: previewSrc } : true"
    />
    <!-- 이미지가 없는 경우 폴백 UI 표출 -->
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
