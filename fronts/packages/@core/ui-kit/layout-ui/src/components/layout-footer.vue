<script setup lang="ts">
import type { CSSProperties } from 'vue';

import { computed } from 'vue';

interface Props {
  /**
   * 하단 고정 여부
   */
  fixed?: boolean;
  height: number;
  /**
   * 표시 여부
   * @default true
   */
  show?: boolean;
  width: string;
  zIndex: number;
}

const props = withDefaults(defineProps<Props>(), {
  show: true,
});

const style = computed((): CSSProperties => {
  const { fixed, height, show, width, zIndex } = props;
  return {
    height: `${height}px`,
    marginBottom: show ? '0' : `-${height}px`,
    position: fixed ? 'fixed' : 'static',
    transform: show ? 'translateY(0)' : 'translateY(100%)',
    width,
    zIndex,
  };
});
</script>

<template>
  <footer
    :style="style"
    class="bottom-0 w-full shrink-0 bg-background-deep transition-all duration-200"
  >
    <slot></slot>
  </footer>
</template>
