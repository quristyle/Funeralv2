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
  /**
   * 이미지 높이 (기본값: 40)
   *
   * **`'auto'` 를 주면 가로만 맞추고 세로는 원본 비율대로 둔다.**
   * 가로·세로를 모두 주면 그 사각형 안에 넣어지므로(`object-contain`),
   * 가로로 긴 이미지는 실제로 보이는 크기가 지정한 값보다 훨씬 작아진다.
   *
   * antd `Image` 는 높이를 **img 인라인 스타일**로 박아 넣기 때문에 CSS 로는
   * 덮을 수 없다. 그래서 높이를 아예 넘기지 않는 길을 따로 둔다.
   */
  height?: number | string;
  /** 이미지가 없을 때 노출될 기본 텍스트/이모지 */
  fallbackText?: string;
  /** 대체 텍스트 (alt) */
  alt?: string;
  /**
   * 주어진 자리를 어떻게 채울지.
   *
   * - `contain`(기본) — 자리 안에 다 들어가게. 비율이 다르면 여백이 생긴다
   * - `cover` — 자리를 **꽉 채운다.** 비율은 그대로고 넘치는 가장자리가 잘린다
   *
   * 표의 칸을 빈틈없이 채우려면 `cover` 다. `contain` 으로는 칸과 이미지의
   * 비율이 다른 만큼 반드시 여백이 남는다.
   */
  fit?: 'contain' | 'cover';
  /**
   * 테두리 · 그림자 · 안쪽 여백을 없애고 자리를 그대로 채운다.
   *
   * 표의 칸에 빈틈없이 넣을 때 쓴다 — 기본 모양에는 `p-0.5` 여백과 테두리가
   * 있어서 칸 안에 넣으면 사방에 선이 겹쳐 보인다.
   */
  frameless?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  src: null,
  fallbackSrc: null,
  previewSrc: null,
  width: 40,
  height: 40,
  fallbackText: '📷',
  alt: 'Preview',
  fit: 'contain',
  frameless: false,
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

/** 세로를 원본 비율대로 둘지. 이때는 antd 에 높이를 넘기지 않는다. */
const autoHeight = computed(() => props.height === 'auto');

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
  <!--
    `frameless` 일 때 세로만 물려받고 **가로는 내용이 정하게** 둔다.
    한 칸에 여러 장을 가로로 늘어놓는 화면(건물 사진)에서 `w-full` 이면
    첫 장이 칸을 다 먹어 버린다. 한 장만 넣는 화면은 `width="100%"` 를 주므로
    안쪽에서 어차피 칸을 채운다.
  -->
  <div
    class="flex items-center justify-center"
    :class="frameless ? 'h-full' : 'p-0.5'"
  >
    <!-- 이미지가 존재하는 경우 Ant Design Vue의 Image 컴포넌트로 미리보기 지원 -->
    <AImage
      v-if="currentSrc"
      :key="currentSrc"
      :src="currentSrc"
      :width="width"
      :height="autoHeight ? undefined : height"
      class="cursor-zoom-in"
      :class="[
        fit === 'cover' ? 'object-cover' : 'object-contain',
        frameless ? 'size-full' : 'rounded shadow border',
      ]"
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
      class="bg-muted flex items-center justify-center text-xs text-muted-foreground select-none font-mono"
      :class="[
        { 'min-h-8': autoHeight },
        frameless ? 'size-full' : 'rounded border',
      ]"
    >
      <slot name="fallback">
        <span>{{ fallbackText }}</span>
      </slot>
    </div>
  </div>
</template>
