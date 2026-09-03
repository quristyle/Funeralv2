<script setup lang="ts">
import type { CSSProperties } from 'vue';

import { computed, onUnmounted, shallowRef, useSlots, watchEffect } from 'vue';

import { useScrollLock } from '@vben-core/composables';
import { VbenScrollbar } from '@vben-core/shadcn-ui';

import { useSidebarDrag } from '../hooks/use-sidebar-drag';
import { SidebarCollapseButton, SidebarFixedButton } from './widgets';

interface Props {
  /**
   * 접힌 영역 높이
   * @default 42
   */
  collapseHeight?: number;
  /**
   * 접힌 상태의 너비
   * @default 48
   */
  collapseWidth?: number;
  /**
   * 숨겨진 DOM의 표시 여부
   * @default true
   */
  domVisible?: boolean;
  /**
   * 标准侧栏展开宽度
   */
  expandedWidth?: number;
  /**
   * 扩展区域extra-title的高度
   */
  extraTitleHeight?: number;
  /**
   * 扩展区域宽度
   */
  extraWidth: number;
  /**
   * 확장 영역 고정
   * @default false
   */
  fixedExtra?: boolean;
  /**
   * 헤더 높이
   */
  headerHeight: number;
  /**
   * 是否移动端抽屉模式
   * @default false
   */
  isMobile?: boolean;
  /**
   * 是否侧边混合模式
   * @default false
   */
  isSidebarMixed?: boolean;
  /**
   * 상단 마진
   * @default 60
   */
  marginTop?: number;
  /**
   * 혼합 메뉴 너비
   * @default 80
   */
  mixedWidth?: number;
  /**
   * 상단 패딩
   * @default 60
   */
  paddingTop?: number;
  /**
   * 표시 여부
   * @default true
   */
  show?: boolean;
  /**
   * 접기 버튼 표시
   * @default true
   */
  showCollapseButton?: boolean;
  /**
   * 고정 버튼 표시
   * @default true
   */
  showFixedButton?: boolean;
  /**
   * 테마
   */
  theme: string;
  /**
   * 서브 테마
   */
  themeSub: string;
  /**
   * 너비
   */
  width: number;
  /**
   * zIndex
   * @default 0
   */
  zIndex?: number;
  }


const props = withDefaults(defineProps<Props>(), {
  collapseHeight: 42,
  collapseWidth: 48,
  domVisible: true,
  expandedWidth: 180,
  extraTitleHeight: undefined,
  fixedExtra: false,
  isMobile: false,
  isSidebarMixed: false,
  marginTop: 0,
  mixedWidth: 70,
  paddingTop: 0,
  show: true,
  showCollapseButton: true,
  showFixedButton: true,
  zIndex: 0,
});

const emit = defineEmits<{ leave: []; 'update:width': [value: number] }>();
const draggable = defineModel<boolean>('draggable');
const collapse = defineModel<boolean>('collapse');
const extraCollapse = defineModel<boolean>('extraCollapse');
const expandOnHovering = defineModel<boolean>('expandOnHovering');
const expandOnHover = defineModel<boolean>('expandOnHover');
const extraVisible = defineModel<boolean>('extraVisible');

const isLocked = useScrollLock({ immediate: false });
const slots = useSlots();

const asideRef = shallowRef<HTMLElement | null>(null);
const dragBarRef = shallowRef<HTMLElement | null>(null);

const hiddenSideStyle = computed((): CSSProperties => {
  const widthValue = props.show ? getMenuWidthValue(true) : '0px';
  return {
    flexBasis: widthValue,
    flexGrow: 0,
    flexShrink: 0,
    overflow: 'hidden',
  };
});

const sidebarVisualWidth = computed(() => {
  const currentWidth = Number.parseFloat(getMenuWidthValue(false));
  return !props.isMobile && !props.isSidebarMixed
    ? Math.max(currentWidth, props.expandedWidth)
    : currentWidth;
});

const dragBarStyle = computed((): CSSProperties => {
  const currentWidth = Number.parseFloat(getMenuWidthValue(false));
  return {
    right: `${Math.max(0, sidebarVisualWidth.value - currentWidth)}px`,
  };
});

const style = computed((): CSSProperties => {
  const { isSidebarMixed, marginTop, paddingTop, zIndex } = props;

  return {
    '--scroll-shadow': 'var(--sidebar)',
    ...calcMenuWidthStyle(),
    height: `calc(100% - ${marginTop}px)`,
    marginTop: `${marginTop}px`,
    paddingTop: `${paddingTop}px`,
    zIndex,
    ...(isSidebarMixed && extraVisible.value ? { transition: 'none' } : {}),
  };
});

const extraStyle = computed((): CSSProperties => {
  const { extraWidth, show, width, zIndex } = props;

  return {
    left: `${width}px`,
    width: extraVisible.value && show ? `${extraWidth}px` : 0,
    zIndex,
  };
});

const extraTitleStyle = computed((): CSSProperties => {
  const { extraTitleHeight, headerHeight } = props;

  return {
    height: `${extraTitleHeight ?? headerHeight - 1}px`,
  };
});

const contentWidthStyle = computed((): CSSProperties => {
  const { fixedExtra, isSidebarMixed, mixedWidth } = props;
  if (isSidebarMixed && fixedExtra) {
    return { width: `${mixedWidth}px` };
  }
  return {};
});

const contentStyle = computed((): CSSProperties => {
  const { collapseHeight, headerHeight } = props;

  return {
    height: `calc(100% - ${headerHeight + collapseHeight}px)`,
    ...contentWidthStyle.value,
  };
});

const headerStyle = computed((): CSSProperties => {
  const { headerHeight, isSidebarMixed } = props;

  return {
    ...(isSidebarMixed ? { display: 'flex', justifyContent: 'center' } : {}),
    height: `${headerHeight - 1}px`,
    ...contentWidthStyle.value,
  };
});

const extraContentStyle = computed((): CSSProperties => {
  const { collapseHeight, extraTitleHeight, headerHeight } = props;
  const titleHeight = extraTitleHeight ?? headerHeight;
  return {
    height: `calc(100% - ${titleHeight + collapseHeight}px)`,
  };
});

const collapseStyle = computed((): CSSProperties => {
  return {
    height: `${props.collapseHeight}px`,
  };
});

watchEffect(() => {
  extraVisible.value = props.fixedExtra ? true : extraVisible.value;
});

function getMenuWidthValue(isHiddenDom: boolean) {
  const {
    collapseWidth,
    extraWidth,
    mixedWidth,
    fixedExtra,
    isSidebarMixed,
    width,
  } = props;

  let widthValue =
    width === 0
      ? '0px'
      : `${width + (isSidebarMixed && fixedExtra && extraVisible.value ? extraWidth : 0)}px`;

  if (isHiddenDom && expandOnHovering.value && !expandOnHover.value) {
    widthValue = isSidebarMixed ? `${mixedWidth}px` : `${collapseWidth}px`;
  }
  return widthValue;
}

function calcMenuWidthStyle(): CSSProperties {
  const widthValue = getMenuWidthValue(false);
  const currentWidth = Number.parseFloat(widthValue);
  const clippedWidth = Math.max(0, sidebarVisualWidth.value - currentWidth);
  let transform: CSSProperties['transform'];

  if (props.isMobile) {
    transform = undefined;
  } else if (props.show) {
    transform = 'translate3d(0, 0, 0)';
  } else {
    transform = 'translate3d(-100%, 0, 0)';
  }

  return {
    ...(widthValue === '0px' ? { overflow: 'hidden' } : {}),
    clipPath: `inset(0 ${clippedWidth}px 0 0)`,
    transform,
    width: `${sidebarVisualWidth.value}px`,
  };
}

function handleMouseenter(e: MouseEvent) {
  if (e?.offsetX < 10) {
    return;
  }

  // 활성화되지 않았거나 접히지 않은 상태에서는 적용되지 않음
  if (expandOnHover.value) {
    return;
  }
  if (!expandOnHovering.value) {
    collapse.value = false;
  }
  if (props.isSidebarMixed) {
    isLocked.value = true;
  }
  expandOnHovering.value = true;
}

function handleMouseleave() {
  emit('leave');
  if (props.isSidebarMixed) {
    isLocked.value = false;
  }
  if (expandOnHover.value) {
    return;
  }

  expandOnHovering.value = false;
  collapse.value = true;
  extraVisible.value = false;
}

const { startDrag, endDrag } = useSidebarDrag();

const handleDragSidebar = (e: MouseEvent) => {
  const { isSidebarMixed, collapseWidth, width } = props;
  const minLimit = isSidebarMixed ? width + collapseWidth : collapseWidth;
  const maxLimit = isSidebarMixed ? width + 320 : 320;

  startDrag(
    e,
    {
      min: minLimit,
      max: maxLimit,
    },
    {
      target: asideRef.value,
      dragBar: dragBarRef.value,
    },
    (newWidth) => {
      if (isSidebarMixed) {
        emit('update:width', newWidth - width);
        extraCollapse.value = collapse.value =
          newWidth - width <= collapseWidth;
      } else {
        emit('update:width', newWidth);
        collapse.value = extraCollapse.value = newWidth <= collapseWidth;
      }
    },
  );
};

onUnmounted(() => {
  endDrag();
});
</script>

<template>
  <div
    v-if="domVisible"
    :class="theme"
    :style="hiddenSideStyle"
    class="h-full"
  ></div>
  <Transition name="mobile-sidebar">
    <aside
      v-if="!isMobile || !collapse"
      ref="asideRef"
      data-layout-region="sidebar"
      :inert="!show || width === 0"
      :style="style"
      class="fixed left-0 top-0 h-full"
      :class="[
        theme,
        {
          'border-r border-border bg-sidebar transition-[clip-path,transform] duration-300 ease-out':
            !isMobile && !isSidebarMixed,
          'transition-transform duration-300 ease-out':
            !isMobile && isSidebarMixed,
        },
      ]"
      @mouseenter="handleMouseenter"
      @mouseleave="handleMouseleave"
    >
      <div
        class="h-full"
        :class="[
          {
            'bg-sidebar-deep': isSidebarMixed,
            'border-r border-border bg-sidebar': !isSidebarMixed,
          },
        ]"
        :style="{ width: `${width}px` }"
      >
        <SidebarFixedButton
          v-if="!collapse && !isSidebarMixed && showFixedButton"
          v-model:expand-on-hover="expandOnHover"
        />
        <div v-if="slots.logo" :style="headerStyle">
          <slot name="logo"></slot>
        </div>
        <!--
          메뉴(스크롤 영역)와 하단 고정 영역(footer 슬롯)을 세로로 나눈다.
          footer 는 스크롤 밖에 있어 메뉴가 길어도 항상 사이드바 맨 아래에 남는다
          (지시, 2026-09-04 — 사이드바 하단 로그아웃 버튼).
        -->
        <div :style="contentStyle" class="flex flex-col">
          <VbenScrollbar class="min-h-0 flex-1 pt-2" shadow shadow-border>
            <slot></slot>
          </VbenScrollbar>
          <div v-if="slots.footer">
            <slot name="footer"></slot>
          </div>
        </div>

        <div :style="collapseStyle"></div>
        <SidebarCollapseButton
          v-if="showCollapseButton && !isSidebarMixed"
          v-model:collapsed="collapse"
        />
      </div>
      <div
        v-if="isSidebarMixed"
        :class="[
          themeSub,
          {
            'border-l': extraVisible,
          },
        ]"
        :style="extraStyle"
        class="fixed top-0 h-full overflow-hidden border-r border-border bg-sidebar transition-[left,width] duration-300 ease-out"
      >
        <SidebarCollapseButton
          v-if="isSidebarMixed && expandOnHover"
          v-model:collapsed="extraCollapse"
        />

        <SidebarFixedButton
          v-if="!extraCollapse"
          v-model:expand-on-hover="expandOnHover"
        />
        <div v-if="!extraCollapse" :style="extraTitleStyle" class="pl-2">
          <slot name="extra-title"></slot>
        </div>
        <VbenScrollbar
          :style="extraContentStyle"
          class="border-border py-2"
          shadow
          shadow-border
        >
          <slot name="extra"></slot>
        </VbenScrollbar>
      </div>
      <div
        v-if="draggable"
        ref="dragBarRef"
        :style="dragBarStyle"
        class="absolute inset-y-0 -right-px z-1000 w-0.5 cursor-col-resize hover:bg-primary"
        @mousedown="handleDragSidebar"
      ></div>
    </aside>
  </Transition>
</template>

<style scoped>
.mobile-sidebar-enter-active,
.mobile-sidebar-leave-active {
  transition: transform 300ms cubic-bezier(0.22, 1, 0.36, 1);
  will-change: transform;
}

.mobile-sidebar-enter-from,
.mobile-sidebar-leave-to {
  transform: translate3d(-100%, 0, 0);
}

@media (prefers-reduced-motion: reduce) {
  .mobile-sidebar-enter-active,
  .mobile-sidebar-leave-active {
    transition-duration: 0ms;
  }
}
</style>
