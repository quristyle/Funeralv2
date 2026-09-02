<script lang="ts" setup>
import type { MenuItemProps, MenuItemRegistered } from '../types';

import { computed, onBeforeUnmount, onMounted, reactive, useSlots } from 'vue';

import { useNamespace } from '@vben-core/composables';
import { VbenIcon, VbenTooltip } from '@vben-core/shadcn-ui';
import { isHttpUrl } from '@vben-core/shared/utils';

import qs from 'qs';

import { MenuBadge } from '../components';
import { useMenu, useMenuContext, useSubMenuContext } from '../hooks';

interface Props extends MenuItemProps {}

defineOptions({ name: 'MenuItem' });

const props = withDefaults(defineProps<Props>(), {
  disabled: false,
});

const emit = defineEmits<{ click: [MenuItemRegistered] }>();

const slots = useSlots();
const { b, e, is } = useNamespace('menu-item');
const nsMenu = useNamespace('menu');
const rootMenu = useMenuContext();
const subMenu = useSubMenuContext();
const { parentMenu, parentPaths } = useMenu();

const active = computed(() => props.path === rootMenu?.activePath);
const menuIcon = computed(() =>
  active.value ? props.activeIcon || props.icon : props.icon,
);

/**
 * 이동할 곳.
 *
 * 지금까지는 신원(`path`)이 곧 이동할 곳이었다. 같은 메뉴를 트리 밖에 한 번 더
 * 얹는 경우(즐겨찾기)에는 둘을 나눠야 한다 — 아래 `active` 판정이 `path` 하나로
 * 이뤄지므로 두 항목이 `path` 를 공유하면 양쪽이 함께 활성이 된다.
 * `link` 가 없으면 예전과 같이 `path` 로 이동한다.
 */
const linkTo = computed(() => props.link ?? item.parentPaths.at(-1) ?? '');

const isHttp = computed(() => isHttpUrl(linkTo.value));

const isTopLevelMenuItem = computed(
  () => parentMenu.value?.type.name === 'MenuUI',
);

const collapseShowTitle = computed(
  () =>
    rootMenu.props?.collapseShowTitle &&
    isTopLevelMenuItem.value &&
    rootMenu.props.collapse,
);

const showTooltip = computed(
  () =>
    rootMenu.props.mode === 'vertical' &&
    isTopLevelMenuItem.value &&
    rootMenu.props?.collapse &&
    slots.title,
);

const item: MenuItemRegistered = reactive({
  active,
  link: props.link,
  parentPaths: parentPaths.value,
  path: props.path || '',
  query: props.query,
});

/**
 * 메뉴 항목 클릭 이벤트
 */
function handleClick() {
  if (props.disabled) {
    return;
  }
  rootMenu?.handleMenuItemClick?.({
    parentPaths: parentPaths.value,
    path: props.path,
  });
  emit('click', item);
}

onMounted(() => {
  subMenu?.addSubMenu?.(item);
  rootMenu?.addMenuItem?.(item);
});

onBeforeUnmount(() => {
  subMenu?.removeSubMenu?.(item);
  rootMenu?.removeMenuItem?.(item);
});
</script>
<template>
  <router-link
    v-slot="{ href }"
    custom
    :to="linkTo + (item?.query ? `?${qs.stringify(item?.query)}` : '')"
  >
    <a
      :href="isHttp ? linkTo : href"
      :class="[
        rootMenu.theme,
        b(),
        is('active', active),
        is('disabled', disabled),
        is('collapse-show-title', collapseShowTitle),
      ]"
      role="menuitem"
      @click.prevent.stop="handleClick"
    >
      <VbenTooltip
        v-if="showTooltip"
        :content-class="[rootMenu.theme]"
        side="right"
      >
        <template #trigger>
          <div :class="[nsMenu.be('tooltip', 'trigger')]">
            <VbenIcon :class="nsMenu.e('icon')" :icon="menuIcon" fallback />
            <slot></slot>
            <span v-if="collapseShowTitle" :class="nsMenu.e('name')">
              <slot name="title"></slot>
            </span>
          </div>
        </template>
        <slot name="title"></slot>
      </VbenTooltip>
      <div v-show="!showTooltip" :class="[e('content')]">
        <MenuBadge
          v-if="rootMenu.props.mode !== 'horizontal'"
          class="right-2"
          v-bind="props"
        />
        <VbenIcon :class="nsMenu.e('icon')" :icon="menuIcon" />
        <slot></slot>
        <span v-if="$slots.title" :class="nsMenu.e('name')">
          <slot name="title"></slot>
        </span>
      </div>
    </a>
  </router-link>
</template>
