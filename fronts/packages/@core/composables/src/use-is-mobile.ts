import { computed } from 'vue';
import { breakpointsTailwind, useBreakpoints } from '@vueuse/core';

export function useIsMobile() {
  const breakpoints = useBreakpoints(breakpointsTailwind);
  const isMobile = computed(() => breakpoints.smaller('md').value);
  return { isMobile };
}
