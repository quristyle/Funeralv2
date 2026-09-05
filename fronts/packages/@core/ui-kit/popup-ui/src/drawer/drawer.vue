<script lang="ts" setup>
import type { DrawerProps, ExtendedDrawerApi } from './drawer';

import { useBackClose } from '../use-back-close';

import {
  computed,
  onDeactivated,
  provide,
  ref,
  unref,
  useId,
  watch,
} from 'vue';

import {
  useIsMobile,
  usePriorityValues,
  useSimpleLocale,
} from '@vben-core/composables';
import { X } from '@vben-core/icons';
import {
  Separator,
  Sheet,
  SheetClose,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
  VbenButton,
  VbenHelpTooltip,
  VbenIconButton,
  VbenLoading,
  VisuallyHidden,
} from '@vben-core/shadcn-ui';
import { ELEMENT_ID_MAIN_CONTENT } from '@vben-core/shared/constants';
import { globalShareState } from '@vben-core/shared/global-state';
import { cn } from '@vben-core/shared/utils';

interface Props extends DrawerProps {
  drawerApi?: ExtendedDrawerApi;
}

const props = withDefaults(defineProps<Props>(), {
  appendToMain: false,
  closeIconPlacement: 'right',
  destroyOnClose: false,
  drawerApi: undefined,
  submitting: false,
  zIndex: 1000,
});

const components = globalShareState.getComponents();

const id = useId();
provide('DISMISSABLE_DRAWER_ID', id);

// @ts-expect-error unused
const wrapperRef = ref<HTMLElement>();
const { $t } = useSimpleLocale();
const { isMobile } = useIsMobile();

const state = props.drawerApi?.useStore?.();

// 모바일에서는 뒤로가기가 이 드로어를 닫는다 (use-back-close.ts 참고).
useBackClose(
  // state 는 ref 다 — 템플릿과 달리 스크립트에서는 .value 를 거쳐야 한다.
  () => !!state?.value?.isOpen,
  () => props.drawerApi?.close(),
);

const {
  appendToMain,
  cancelText,
  class: drawerClass,
  closable,
  closeIconPlacement,
  closeOnClickModal,
  closeOnPressEscape,
  confirmLoading,
  confirmText,
  contentClass,
  description,
  destroyOnClose,
  footer: showFooter,
  footerClass,
  header: showHeader,
  headerClass,
  loading: showLoading,
  modal,
  openAutoFocus,
  overlayBlur,
  placement,
  showCancelButton,
  showConfirmButton,
  submitting,
  title,
  titleTooltip,
  zIndex,
} = usePriorityValues(props, state);

// watch(
//   () => showLoading.value,
//   (v) => {
//     if (v && wrapperRef.value) {
//       wrapperRef.value.scrollTo({
//         // behavior: 'smooth',
//         top: 0,
//       });
//     }
//   },
// );

/**
 * keepAlive가 활성화된 상태에서 브라우저 버튼/제스처 등을 통해 뒤로 가기를 하면 팝업이 닫히지 않습니다.
 */
onDeactivated(() => {
  // 팝업이 콘텐츠 영역에 마운트되지 않은 경우, 팝업을 닫습니다.
  if (!appendToMain.value) {
    props.drawerApi?.close();
  }
});

function interactOutside(e: Event) {
  if (!closeOnClickModal.value || submitting.value) {
    e.preventDefault();
  }
}
function escapeKeyDown(e: KeyboardEvent) {
  if (!closeOnPressEscape.value || submitting.value) {
    e.preventDefault();
  }
}
// pointer-down-outside
function pointerDownOutside(e: Event) {
  const target = e.target as HTMLElement;
  const dismissableDrawer = target?.dataset.dismissableDrawer;
  if (
    submitting.value ||
    !closeOnClickModal.value ||
    dismissableDrawer !== id
  ) {
    e.preventDefault();
  }
}

function handerOpenAutoFocus(e: Event) {
  if (!openAutoFocus.value) {
    e?.preventDefault();
  }
}

function handleFocusOutside(e: Event) {
  e.preventDefault();
  e.stopPropagation();
}

const getAppendTo = computed(() => {
  return appendToMain.value ? `#${ELEMENT_ID_MAIN_CONTENT}` : undefined;
});

/**
 * destroyOnClose 기능 보완
 */
// 열린 적이 있는지 여부
const hasOpened = ref(false);
const isClosed = ref(true);
watch(
  () => state?.value?.isOpen,
  (value) => {
    isClosed.value = false;
    if (value && !unref(hasOpened)) {
      hasOpened.value = true;
    }
    // 닫는 애니메이션이 끝나며 오는 `@closed` 를 못 받는 경로가 있어
    // (뒤로가기로 닫을 때 등) 상태 변화에서도 한 번 더 걷는다. 아래 참고.
    if (value === false) {
      releaseStuckPointerEvents();
    }
  },
  { immediate: true },
);
/**
 * 닫힌 뒤에도 남는 `body { pointer-events: none }` 을 걷는다.
 *
 * 서랍은 모바일에서만 `<Sheet :modal="isMobile">` 이라 reka 가 body 에
 * 이 스타일을 건다. 그런데 닫을 때 그 해제가 돌지 않는 경로가 있어
 * **화면 전체가 눌리지 않는 상태로 굳는다** (2026-09-05 빈소현황 모바일에서 실측 —
 * 서랍을 닫은 뒤 아무 곳도 클릭되지 않았다. `destroyOnClose` 를 켜도 남았다).
 *
 * 겹쳐 있는 팝업이 아직 있으면 건드리지 않는다 — 그때는 잠금이 유효하다.
 * reka 가 스스로 해제한 경우에는 지울 것이 없어 아무 일도 일어나지 않는다.
 *
 * 오버레이와 스크롤 잠금은 `SheetOverlay` 가 따로 하고 있으므로(자체 `useScrollLock`)
 * 여기서 스타일 하나를 걷어도 잠금 동작은 그대로다.
 */
function releaseStuckPointerEvents() {
  if (typeof document === 'undefined') {
    return;
  }
  // 닫기 직후에는 `data-state` 가 아직 `open` 인 프레임이 있고, `destroyOnClose` 면
  // 내용이 곧바로 사라져 애니메이션 끝(`@closed`)이 오지 않기도 한다. 그래서
  // 한 번만 보지 않고 닫힘 애니메이션이 끝날 만한 시점까지 몇 번 더 확인한다.
  const attempt = () => {
    const stillOpen = document.querySelector(
      '[role="dialog"][data-state="open"], [role="alertdialog"][data-state="open"]',
    );
    if (!stillOpen && document.body.style.pointerEvents === 'none') {
      document.body.style.removeProperty('pointer-events');
    }
  };
  requestAnimationFrame(attempt);
  setTimeout(attempt, 200);
  setTimeout(attempt, 500);
}

function handleClosed() {
  isClosed.value = true;
  props.drawerApi?.onClosed();
  releaseStuckPointerEvents();
}
const getForceMount = computed(() => {
  return !unref(destroyOnClose) && unref(hasOpened);
});
</script>
<template>
  <Sheet
    :modal="isMobile"
    :open="state?.isOpen"
    @update:open="() => drawerApi?.close()"
  >
    <SheetContent
      :append-to="getAppendTo"
      :class="
        cn(
          'flex w-130 flex-col',
          {
            'w-full!':
              isMobile || placement === 'bottom' || placement === 'top',
            'max-h-screen': placement === 'bottom' || placement === 'top',
            hidden: isClosed,
          },
          drawerClass,
        )
      "
      :modal="modal"
      :open="state?.isOpen"
      :side="placement"
      :z-index="zIndex"
      :force-mount="getForceMount"
      :overlay-blur="overlayBlur"
      @close-auto-focus="handleFocusOutside"
      @closed="handleClosed"
      @escape-key-down="escapeKeyDown"
      @focus-outside="handleFocusOutside"
      @interact-outside="interactOutside"
      @open-auto-focus="handerOpenAutoFocus"
      @opened="() => drawerApi?.onOpened()"
      @pointer-down-outside="pointerDownOutside"
    >
      <SheetHeader
        v-if="showHeader"
        :class="
          cn(
            'flex! flex-row items-center justify-between border-b px-6 py-5',
            headerClass,
            {
              'px-4 py-3': closable,
              'pl-2': closable && closeIconPlacement === 'left',
            },
          )
        "
      >
        <div class="flex items-center">
          <SheetClose
            v-if="closable && closeIconPlacement === 'left'"
            as-child
            :disabled="submitting"
            class="ml-0.5 cursor-pointer rounded-full opacity-80 transition-opacity hover:opacity-100 focus:outline-hidden disabled:pointer-events-none data-[state=open]:bg-secondary"
          >
            <slot name="close-icon">
              <VbenIconButton>
                <X class="size-4" />
              </VbenIconButton>
            </slot>
          </SheetClose>
          <Separator
            v-if="closable && closeIconPlacement === 'left'"
            class="mr-2 ml-1 h-8"
            decorative
            orientation="vertical"
          />
          <SheetTitle v-if="title" class="text-left">
            <slot name="title">
              {{ title }}

              <VbenHelpTooltip v-if="titleTooltip" trigger-class="pb-1">
                {{ titleTooltip }}
              </VbenHelpTooltip>
            </slot>
          </SheetTitle>
          <SheetDescription v-if="description" class="mt-1 text-xs">
            <slot name="description">
              {{ description }}
            </slot>
          </SheetDescription>
        </div>

        <VisuallyHidden v-if="!title || !description">
          <SheetTitle v-if="!title" />
          <SheetDescription v-if="!description" />
        </VisuallyHidden>

        <div class="flex-center">
          <slot name="extra"></slot>
          <SheetClose
            v-if="closable && closeIconPlacement === 'right'"
            as-child
            :disabled="submitting"
            class="ml-0.5 cursor-pointer rounded-full opacity-80 transition-opacity hover:opacity-100 focus:outline-hidden disabled:pointer-events-none data-[state=open]:bg-secondary"
          >
            <slot name="close-icon">
              <VbenIconButton>
                <X class="size-4" />
              </VbenIconButton>
            </slot>
          </SheetClose>
        </div>
      </SheetHeader>
      <template v-else>
        <VisuallyHidden>
          <SheetTitle />
          <SheetDescription />
        </VisuallyHidden>
      </template>
      <div
        ref="wrapperRef"
        :class="
          cn('relative flex-1 overflow-y-auto p-3', contentClass, {
            'pointer-events-none': showLoading || submitting,
          })
        "
      >
        <slot></slot>
      </div>
      <VbenLoading v-if="showLoading || submitting" spinning />
      <SheetFooter
        v-if="showFooter"
        :class="
          cn(
            'w-full flex-row items-center justify-end border-t p-2 px-3',
            footerClass,
          )
        "
      >
        <slot name="prepend-footer"></slot>
        <slot name="footer">
          <component
            :is="components.DefaultButton || VbenButton"
            v-if="showCancelButton"
            variant="ghost"
            :disabled="submitting"
            @click="() => drawerApi?.onCancel()"
          >
            <slot name="cancelText">
              {{ cancelText || $t('cancel') }}
            </slot>
          </component>
          <slot name="center-footer"></slot>
          <component
            :is="components.PrimaryButton || VbenButton"
            v-if="showConfirmButton"
            :loading="confirmLoading || submitting"
            @click="() => drawerApi?.onConfirm()"
          >
            <slot name="confirmText">
              {{ confirmText || $t('confirm') }}
            </slot>
          </component>
        </slot>
        <slot name="append-footer"></slot>
      </SheetFooter>
    </SheetContent>
  </Sheet>
</template>
