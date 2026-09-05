import type { MaybeRefOrGetter, Ref } from 'vue';

import { computed, ref, toValue } from 'vue';

import { useResizeObserver } from '@vueuse/core';

/**
 * 칸 수를 **내용 수에 맞춰** 고른다.
 *
 * CSS `auto-fit` 만으로는 줄이 고르지 않다. 폭이 1300px 이고 최소 칸이 200px 이면
 * 여섯 칸이 잡히고, 호실이 일곱이면 `6 + 1` 이 된다 — 둘째 줄에 한 칸만 남는
 * 모양이 화면에서 제일 어색하다.
 *
 * 그래서 두 단계로 센다.
 *   1. 폭이 허용하는 최대 칸 수를 구한다 (`floor((w + gap) / (min + gap))`)
 *   2. 그 수로 필요한 줄 수를 구한 뒤, **줄 수를 유지한 채 칸 수를 다시 나눈다**
 *      (`ceil(n / rows)`). 일곱을 두 줄에 담아야 하면 `6 + 1` 대신 `4 + 3` 이 된다.
 *
 * 빈소는 보통 6~7 개다. 1920px 데스크탑에서는 1단계가 이미 7 이상을 주므로
 * 한 줄로 놓이고, 좁아지면 고르게 접힌다.
 */
export function useBalancedColumns(
  target: Ref<HTMLElement | null | undefined>,
  count: Ref<number>,
  // `min` 은 반응형을 받는다 — 휴대폰과 데스크탑의 최소 칸폭이 다르고,
  // 창을 좁히거나 화면을 돌리면 그 자리에서 바뀌어야 한다.
  options: { gap?: number; min?: MaybeRefOrGetter<number> } = {},
) {
  const gap = options.gap ?? 8;

  const width = ref(0);
  useResizeObserver(target, (entries) => {
    width.value = entries[0]?.contentRect.width ?? 0;
  });

  const columns = computed(() => {
    const n = count.value;
    if (n <= 0) return 1;
    // 아직 못 잰 동안(첫 렌더)은 내용 수를 그대로 쓴다. 재고 나면 곧 줄어든다.
    if (width.value <= 0) return n;

    const min = toValue(options.min) ?? 200;
    const fit = Math.max(1, Math.floor((width.value + gap) / (min + gap)));
    if (fit >= n) return n;

    const rows = Math.ceil(n / fit);
    return Math.ceil(n / rows);
  });

  /** 인라인 스타일로 준다 — 칸 수가 동적이라 tailwind 클래스로는 못 쓴다. */
  const gridStyle = computed(() => ({
    display: 'grid',
    gridTemplateColumns: `repeat(${columns.value}, minmax(0, 1fr))`,
    gap: `${gap}px`,
  }));

  return { columns, gridStyle, width };
}
