<script setup lang="ts">
import type { CSSProperties } from 'vue';

import {
  computed,
  onBeforeUnmount,
  onMounted,
  onUpdated,
  ref,
  watchEffect,
} from 'vue';

import { VbenTooltip } from '@vben-core/shadcn-ui';

import { useElementSize } from '@vueuse/core';

interface Props {
  /**
   * 클릭 시 텍스트 전체 펼치기 활성화 여부
   * @default false
   */
  expand?: boolean;
  /**
   * 텍스트 최대 행 수
   * @default 1
   */
  line?: number;
  /**
   * 텍스트 최대 너비
   * @default '100%'
   */
  maxWidth?: number | string;
  /**
   * 툴팁 위치
   * @default 'top'
   */
  placement?: 'bottom' | 'left' | 'right' | 'top';
  /**
   * 텍스트 툴팁 활성화 여부
   * @default true
   */
  tooltip?: boolean;
  /**
   * 텍스트가 생략될 때만 툴팁 표시 여부
   * @default false
   */
  tooltipWhenEllipsis?: boolean;
  /**
   * 텍스트 생략 감지를 위한 픽셀 차이 임계값 (값이 클수록 엄격하게 판단)
   * @default 3
   */
  ellipsisThreshold?: number;
  /**
   * 툴팁 배경 색상 (overlayStyle보다 우선순위가 높음)
   */
  tooltipBackgroundColor?: string;
  /**
   * 툴팁 텍스트 색상 (overlayStyle보다 우선순위가 높음)
   */
  tooltipColor?: string;
  /**
   * 툴팁 텍스트 글꼴 크기 (px 단위, overlayStyle보다 우선순위가 높음)
   */
  tooltipFontSize?: number;
  /**
   * 툴팁 내용의 최대 너비 (px 단위, 설정하지 않으면 표시되는 텍스트 너비와 자동으로 일치함)
   */
  tooltipMaxWidth?: number;
  /**
   * 툴팁 내용 영역 스타일
   * @default { textAlign: 'justify' }
   */
  tooltipOverlayStyle?: CSSProperties;
}

const props = withDefaults(defineProps<Props>(), {
  expand: false,
  line: 1,
  maxWidth: '100%',
  placement: 'top',
  tooltip: true,
  tooltipWhenEllipsis: false,
  ellipsisThreshold: 3,
  tooltipBackgroundColor: '',
  tooltipColor: '',
  tooltipFontSize: 14,
  tooltipMaxWidth: undefined,
  tooltipOverlayStyle: () => ({ textAlign: 'justify' }),
});

const emit = defineEmits<{ expandChange: [boolean] }>();

const textMaxWidth = computed(() => {
  if (typeof props.maxWidth === 'number') {
    return `${props.maxWidth}px`;
  }
  return props.maxWidth;
});
const ellipsis = ref();
const isExpand = ref(false);
const defaultTooltipMaxWidth = ref();
const isEllipsis = ref(false);

const { width: eleWidth } = useElementSize(ellipsis);

// 텍스트 생략 여부 확인
const checkEllipsis = () => {
  if (!ellipsis.value || !props.tooltipWhenEllipsis) return;

  const element = ellipsis.value;

  const originalText = element.textContent || '';
  const originalTrimmed = originalText.trim();

  // 빈 텍스트인 경우 false 반환
  if (!originalTrimmed) {
    isEllipsis.value = false;
    return;
  }

  const widthDiff = element.scrollWidth - element.clientWidth;
  const heightDiff = element.scrollHeight - element.clientHeight;

  // 충분히 큰 차이 임계값을 사용하여 실제 생략된 텍스트에만 툴팁 표시
  isEllipsis.value =
    props.line === 1
      ? widthDiff > props.ellipsisThreshold
      : heightDiff > props.ellipsisThreshold;
};

// ResizeObserver를 사용하여 크기 변화 감시
let resizeObserver: null | ResizeObserver = null;

onMounted(() => {
  if (typeof ResizeObserver !== 'undefined' && props.tooltipWhenEllipsis) {
    resizeObserver = new ResizeObserver(() => {
      checkEllipsis();
    });

    if (ellipsis.value) {
      resizeObserver.observe(ellipsis.value);
    }
  }

  // 초기 확인
  checkEllipsis();
});

// onUpdated 훅을 사용하여 내용 변화 확인
onUpdated(() => {
  if (props.tooltipWhenEllipsis) {
    checkEllipsis();
  }
});

onBeforeUnmount(() => {
  if (resizeObserver) {
    resizeObserver.disconnect();
    resizeObserver = null;
  }
});

watchEffect(
  () => {
    if (props.tooltip && eleWidth.value) {
      defaultTooltipMaxWidth.value =
        props.tooltipMaxWidth ?? eleWidth.value + 24;
    }
  },
  { flush: 'post' },
);

function onExpand() {
  isExpand.value = !isExpand.value;
  emit('expandChange', isExpand.value);
  if (props.tooltipWhenEllipsis) {
    checkEllipsis();
  }
}

function handleExpand() {
  props.expand && onExpand();
}
</script>
<template>
  <div>
    <VbenTooltip
      :content-style="{
        ...tooltipOverlayStyle,
        maxWidth: `${defaultTooltipMaxWidth}px`,
        fontSize: `${tooltipFontSize}px`,
        color: tooltipColor,
        backgroundColor: tooltipBackgroundColor,
      }"
      :disabled="
        !props.tooltip || isExpand || (props.tooltipWhenEllipsis && !isEllipsis)
      "
      :side="placement"
    >
      <slot name="tooltip">
        <slot></slot>
      </slot>

      <template #trigger>
        <div
          ref="ellipsis"
          :class="{
            'cursor-pointer!': expand,
            ['block truncate']: line === 1,
            [$style.ellipsisMultiLine]: line > 1,
          }"
          :style="{
            '-webkit-line-clamp': isExpand ? '' : line,
            'max-width': textMaxWidth,
          }"
          class="cursor-text overflow-hidden"
          @click="handleExpand"
          v-bind="$attrs"
        >
          <slot></slot>
        </div>
      </template>
    </VbenTooltip>
  </div>
</template>

<style module>
.ellipsisMultiLine {
  display: -webkit-box;
  -webkit-box-orient: vertical;
}
</style>
