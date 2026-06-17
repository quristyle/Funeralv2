import type { TabsProps } from './types';

import { nextTick, onMounted, onUnmounted, ref, watch } from 'vue';

import { VbenScrollbar } from '@vben-core/shadcn-ui';

import { useDebounceFn } from '@vueuse/core';

type DomElement = Element | null | undefined;

export function useTabsViewScroll(props: TabsProps) {
  let resizeObserver: null | ResizeObserver = null;
  let mutationObserver: MutationObserver | null = null;
  let tabItemCount = 0;
  const scrollbarRef = ref<InstanceType<typeof VbenScrollbar> | null>(null);
  const scrollViewportEl = ref<DomElement>(null);
  const showScrollButton = ref(false);
  const scrollIsAtLeft = ref(true);
  const scrollIsAtRight = ref(false);

  function getScrollClientWidth() {
    const scrollbarEl = scrollbarRef.value?.$el;
    if (!scrollbarEl || !scrollViewportEl.value) return {};

    const scrollbarWidth = scrollbarEl.clientWidth;
    const scrollViewWidth = scrollViewportEl.value.clientWidth;

    return {
      scrollbarWidth,
      scrollViewWidth,
    };
  }

  function scrollDirection(
    direction: 'left' | 'right',
    distance: number = 150,
  ) {
    const { scrollbarWidth, scrollViewWidth } = getScrollClientWidth();

    if (!scrollbarWidth || !scrollViewWidth) return;

    if (scrollbarWidth > scrollViewWidth) return;

    scrollViewportEl.value?.scrollBy({
      behavior: 'smooth',
      left:
        direction === 'left'
          ? -(scrollbarWidth - distance)
          : +(scrollbarWidth - distance),
    });
  }

  async function initScrollbar() {
    await nextTick();

    const scrollbarEl = scrollbarRef.value?.$el;
    if (!scrollbarEl) {
      return;
    }

    const viewportEl = scrollbarEl?.querySelector(
      'div[data-reka-scroll-area-viewport]',
    );

    scrollViewportEl.value = viewportEl;
    calcShowScrollbarButton();

    await nextTick();
    scrollToActiveIntoView();

    // 크기 변화 감지
    resizeObserver?.disconnect();
    resizeObserver = new ResizeObserver(
      useDebounceFn((_entries: ResizeObserverEntry[]) => {
        calcShowScrollbarButton();
        scrollToActiveIntoView();
      }, 100),
    );
    resizeObserver.observe(viewportEl);

    tabItemCount = props.tabs?.length || 0;
    mutationObserver?.disconnect();
    // MutationObserver를 사용하여 자식 노드 수의 변화만 감지
    mutationObserver = new MutationObserver(() => {
      const count = viewportEl.querySelectorAll(
        `div[data-tab-item="true"]`,
      ).length;

      if (count > tabItemCount) {
        scrollToActiveIntoView();
      }

      if (count !== tabItemCount) {
        calcShowScrollbarButton();
        tabItemCount = count;
      }
    });

    // 자식 노드의 추가 및 제거만 감지하도록 설정
    mutationObserver.observe(viewportEl, {
      attributes: false,
      childList: true,
      subtree: true,
    });
  }

  async function scrollToActiveIntoView() {
    if (!scrollViewportEl.value) {
      return;
    }
    await nextTick();
    const viewportEl = scrollViewportEl.value;
    const { scrollbarWidth } = getScrollClientWidth();
    const { scrollWidth } = viewportEl;

    if (scrollbarWidth >= scrollWidth) {
      return;
    }

    requestAnimationFrame(() => {
      const activeItem = viewportEl?.querySelector('.is-active');
      activeItem?.scrollIntoView({ behavior: 'smooth', inline: 'start' });
    });
  }

  /**
   * 탭 너비를 계산하여 좌우 스크롤 버튼 표시 여부를 판단합니다.
   */
  async function calcShowScrollbarButton() {
    if (!scrollViewportEl.value) {
      return;
    }

    const { scrollbarWidth } = getScrollClientWidth();

    showScrollButton.value =
      scrollViewportEl.value.scrollWidth > scrollbarWidth;
  }

  const handleScrollAt = useDebounceFn(({ left, right }) => {
    scrollIsAtLeft.value = left;
    scrollIsAtRight.value = right;
  }, 100);

  function handleWheel({ deltaY }: WheelEvent) {
    scrollViewportEl.value?.scrollBy({
      // behavior: 'smooth',
      left: deltaY * 3,
    });
  }

  watch(
    () => props.active,
    async () => {
      // 탭 전환 애니메이션이 완료될 때까지 대기하기 위한 200ms
      // setTimeout(() => {
      scrollToActiveIntoView();
      // }, 300);
    },
    {
      flush: 'post',
    },
  );

  // watch(
  //   () => props.tabs?.length,
  //   async () => {
  //     await nextTick();
  //     calcShowScrollbarButton();
  //   },
  //   {
  //     flush: 'post',
  //   },
  // );

  watch(
    () => props.styleType,
    () => {
      initScrollbar();
    },
  );

  onMounted(initScrollbar);

  onUnmounted(() => {
    resizeObserver?.disconnect();
    mutationObserver?.disconnect();
    resizeObserver = null;
    mutationObserver = null;
  });

  return {
    handleScrollAt,
    handleWheel,
    initScrollbar,
    scrollbarRef,
    scrollDirection,
    scrollIsAtLeft,
    scrollIsAtRight,
    showScrollButton,
  };
}
