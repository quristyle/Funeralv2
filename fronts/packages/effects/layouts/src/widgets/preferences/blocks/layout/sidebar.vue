<script setup lang="ts">
import type { LayoutType, SidebarMenuSelectBehavior } from '@vben/types';

import { computed, onMounted } from 'vue';

import { $t } from '@vben/locales';

import CheckboxItem from '../checkbox-item.vue';
import NumberFieldItem from '../number-field-item.vue';
import SelectItem from '../select-item.vue';
import SwitchItem from '../switch-item.vue';

defineProps<{ currentLayout?: LayoutType; disabled: boolean }>();

const sidebarEnable = defineModel<boolean>('sidebarEnable');
const sidebarWidth = defineModel<number>('sidebarWidth');
const sidebarCollapsedShowTitle = defineModel<boolean>(
  'sidebarCollapsedShowTitle',
);
const sidebarAutoActivateChild = defineModel<boolean>(
  'sidebarAutoActivateChild',
);
const sidebarDraggable = defineModel<boolean>('sidebarDraggable');
const sidebarCollapsed = defineModel<boolean>('sidebarCollapsed');
const sidebarOnMenuSelect =
  defineModel<SidebarMenuSelectBehavior>('sidebarOnMenuSelect');
const sidebarExpandOnHover = defineModel<boolean>('sidebarExpandOnHover');

const sidebarButtons = defineModel<string[]>('sidebarButtons', {
  default: () => [],
});
const sidebarCollapsedButton = defineModel<boolean>('sidebarCollapsedButton');
const sidebarFixedButton = defineModel<boolean>('sidebarFixedButton');

onMounted(() => {
  if (
    sidebarCollapsedButton.value &&
    !sidebarButtons.value.includes('collapsed')
  ) {
    sidebarButtons.value.push('collapsed');
  }
  if (sidebarFixedButton.value && !sidebarButtons.value.includes('fixed')) {
    sidebarButtons.value.push('fixed');
  }
});

/**
 * 메뉴를 고른 뒤 사이드바를 어떻게 할지.
 *
 * 사이드바 상태가 셋(보이기 · 축소 · 완전히 숨기기)이라 스위치로는 모자란다.
 * `hide` 는 헤더 왼쪽 햄버거가 만드는 상태와 **같은 것**이라, 다시 보이게 하는
 * 방법도 그 버튼으로 같다.
 */
const onMenuSelectItems = computed(() => [
  { label: $t('preferences.sidebar.onMenuSelectNone'), value: 'none' },
  { label: $t('preferences.sidebar.onMenuSelectCollapse'), value: 'collapse' },
  { label: $t('preferences.sidebar.onMenuSelectHide'), value: 'hide' },
]);

const handleCheckboxChange = () => {
  sidebarCollapsedButton.value = !!sidebarButtons.value.includes('collapsed');
  sidebarFixedButton.value = !!sidebarButtons.value.includes('fixed');
};
</script>

<template>
  <SwitchItem v-model="sidebarEnable" :disabled="disabled">
    {{ $t('preferences.sidebar.visible') }}
  </SwitchItem>
  <SwitchItem v-model="sidebarDraggable" :disabled="!sidebarEnable || disabled">
    {{ $t('preferences.sidebar.draggable') }}
  </SwitchItem>
  <SwitchItem v-model="sidebarCollapsed" :disabled="!sidebarEnable || disabled">
    {{ $t('preferences.sidebar.collapsed') }}
  </SwitchItem>
  <!--
    메뉴를 고른 뒤 사이드바를 어떻게 할지.

    사이드바 상태는 셋이다 — 보이기 · 축소 · 완전히 숨기기.
    그래서 켜고 끄는 스위치가 아니라 **고르는 칸**이다.

    되돌리는 방법은 각 상태가 원래 쓰던 것 그대로다.
      · 축소        → 접기 버튼, 또는 '마우스 올리면 펼치기'
      · 완전히 숨기기 → 헤더 왼쪽의 햄버거 (같은 상태를 만든다)
  -->
  <SelectItem
    v-model="sidebarOnMenuSelect"
    :disabled="!sidebarEnable || disabled"
    :items="onMenuSelectItems"
  >
    {{ $t('preferences.sidebar.onMenuSelect') }}
  </SelectItem>
  <SwitchItem
    v-model="sidebarExpandOnHover"
    :disabled="!sidebarEnable || disabled || !sidebarCollapsed"
    :tip="$t('preferences.sidebar.expandOnHoverTip')"
  >
    {{ $t('preferences.sidebar.expandOnHover') }}
  </SwitchItem>
  <SwitchItem
    v-model="sidebarCollapsedShowTitle"
    :disabled="!sidebarEnable || disabled || !sidebarCollapsed"
  >
    {{ $t('preferences.sidebar.collapsedShowTitle') }}
  </SwitchItem>
  <SwitchItem
    v-model="sidebarAutoActivateChild"
    :disabled="
      !sidebarEnable ||
      !['sidebar-mixed-nav', 'mixed-nav', 'header-mixed-nav'].includes(
        currentLayout as string,
      ) ||
      disabled
    "
    :tip="$t('preferences.sidebar.autoActivateChildTip')"
  >
    {{ $t('preferences.sidebar.autoActivateChild') }}
  </SwitchItem>
  <CheckboxItem
    :items="[
      { label: $t('preferences.sidebar.buttonCollapsed'), value: 'collapsed' },
      { label: $t('preferences.sidebar.buttonFixed'), value: 'fixed' },
    ]"
    multiple
    v-model="sidebarButtons"
    :on-btn-click="handleCheckboxChange"
  >
    {{ $t('preferences.sidebar.buttons') }}
  </CheckboxItem>
  <NumberFieldItem
    v-model="sidebarWidth"
    :disabled="!sidebarEnable || disabled"
    :max="320"
    :min="160"
    :step="10"
  >
    {{ $t('preferences.sidebar.width') }}
  </NumberFieldItem>
</template>
