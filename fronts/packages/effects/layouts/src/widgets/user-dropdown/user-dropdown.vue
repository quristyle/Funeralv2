<script setup lang="ts">
import type { Component } from 'vue';

import type { AnyFunction } from '@vben/types';

import { computed, useTemplateRef, watch } from 'vue';

import { useHoverToggle } from '@vben/hooks';
import { LockKeyhole, LogOut } from '@vben/icons';
import { $t } from '@vben/locales';
import { preferences, usePreferences } from '@vben/preferences';
import { useAccessStore, useUserStore } from '@vben/stores';
import { isWindowsOs } from '@vben/utils';

import { useVbenModal } from '@vben-core/popup-ui';
import {
  Badge,
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuShortcut,
  DropdownMenuTrigger,
  VbenAvatar,
  VbenIcon,
} from '@vben-core/shadcn-ui';

import { useMagicKeys, whenever } from '@vueuse/core';

import { LockScreenModal } from '../lock-screen';

interface Props {
  /**
   * 프로필 사진
   */
  avatar?: string;
  /**
   * @ko_KR 설명
   */
  description?: string;
  /**
   * 단축키 활성화 여부
   */
  enableShortcutKey?: boolean;
  /**
   * 메뉴 배열
   */
  menus?: Array<{
    handler: AnyFunction;
    icon?: Component | Function | string;
    text: string;
  }>;

  /**
   * 태그 텍스트
   */
  tagText?: string;
  /**
   * 텍스트
   */
  text?: string;
  /** 트리거 방식 */
  trigger?: 'both' | 'click' | 'hover';
  /** hover 트리거 시, 응답 지연 시간 */
  hoverDelay?: number;
}

defineOptions({
  name: 'UserDropdown',
});

const props = withDefaults(defineProps<Props>(), {
  avatar: '',
  description: '',
  enableShortcutKey: true,
  menus: () => [],
  showShortcutKey: true,
  tagText: '',
  text: '',
  trigger: 'click',
  hoverDelay: 500,
});

const emit = defineEmits<{ logout: [] }>();

const { globalLockScreenShortcutKey, globalLogoutShortcutKey } =
  usePreferences();
const accessStore = useAccessStore();
const userStore = useUserStore();

const companyName = computed(() => {
  return userStore.userInfo?.companyName || '';
});

const deptName = computed(() => {
  return userStore.userInfo?.deptName || '';
});

const userEmail = computed(() => {
  return userStore.userInfo?.email || props.description;
});

/**
 * 로그인한 사용자에게 배정된 역할.
 *
 * 서버(`GET /auth/user/info`)는 식별자(`roles`: SYSTEM_ADMINISTRATOR …)와
 * 표시 이름(`roleNames`: 시스템관리자 …)을 함께 준다. 사람이 읽는 자리라 이름을 먼저 쓰고,
 * 이름이 없으면 식별자로 대신한다.
 */
const userRoles = computed<string[]>(() => {
  const info = userStore.userInfo;
  const names = info?.roleNames;
  if (Array.isArray(names) && names.length > 0) {
    return names.filter(Boolean);
  }
  return (info?.roles ?? []).filter(Boolean);
});

const computedAvatar = computed(() => {
  return props.avatar || userStore.userInfo?.avatar || preferences.app.defaultAvatar;
});

const [LockModal, lockModalApi] = useVbenModal({
  connectedComponent: LockScreenModal,
});
const [LogoutModal, logoutModalApi] = useVbenModal({
  onConfirm() {
    handleSubmitLogout();
  },
});

const refTrigger = useTemplateRef('refTrigger');
const refContent = useTemplateRef('refContent');
const [openPopover, hoverWatcher] = useHoverToggle(
  [refTrigger, refContent],
  () => props.hoverDelay,
);

watch(
  () => props.trigger === 'hover' || props.trigger === 'both',
  (val) => {
    if (val) {
      hoverWatcher.enable();
    } else {
      hoverWatcher.disable();
    }
  },
  {
    immediate: true,
  },
);

const altView = computed(() => (isWindowsOs() ? 'Alt' : '⌥'));

const enableLogoutShortcutKey = computed(() => {
  return props.enableShortcutKey && globalLogoutShortcutKey.value;
});

const enableLockScreenShortcutKey = computed(() => {
  return props.enableShortcutKey && globalLockScreenShortcutKey.value;
});

const enableShortcutKey = computed(() => {
  return props.enableShortcutKey && preferences.shortcutKeys.enable;
});

function handleOpenLock() {
  lockModalApi.open();
}

function handleSubmitLock(lockScreenPassword: string) {
  lockModalApi.close();
  accessStore.lockScreen(lockScreenPassword);
}

function handleLogout() {
  // 이벤트 발생
  logoutModalApi.open();
  openPopover.value = false;
}

function handleSubmitLogout() {
  emit('logout');
  logoutModalApi.close();
}

if (enableShortcutKey.value) {
  const keys = useMagicKeys();
  const logoutKey = keys['Alt+KeyQ'];
  const lockKey = keys['Alt+KeyL'];

  if (logoutKey) {
    whenever(logoutKey, () => {
      if (enableLogoutShortcutKey.value) {
        handleLogout();
      }
    });
  }

  if (lockKey) {
    whenever(lockKey, () => {
      if (enableLockScreenShortcutKey.value) {
        handleOpenLock();
      }
    });
  }
}
</script>

<template>
  <LockModal
    v-if="preferences.widget.lockScreen"
    :avatar="computedAvatar"
    :text="text"
    @submit="handleSubmitLock"
  />

  <LogoutModal
    :cancel-text="$t('common.cancel')"
    :confirm-text="$t('common.confirm')"
    :fullscreen-button="false"
    :title="$t('common.prompt')"
    centered
    content-class="px-8 min-h-10"
    footer-class="border-none mb-3 mr-3"
    header-class="border-none"
  >
    {{ $t('ui.widgets.logoutTip') }}
  </LogoutModal>

  <DropdownMenu v-model:open="openPopover">
    <DropdownMenuTrigger ref="refTrigger" :disabled="props.trigger === 'hover'">
      <div class="mr-2 ml-1 cursor-pointer rounded-full p-1.5 hover:bg-accent">
        <div class="flex-center hover:text-accent-foreground">
          <VbenAvatar :alt="text" :src="computedAvatar" class="size-8" dot />
        </div>
      </div>
    </DropdownMenuTrigger>
    <DropdownMenuContent class="mr-2 min-w-60 p-0 pb-1">
      <div ref="refContent">
        <DropdownMenuLabel class="flex items-center p-3">
          <VbenAvatar
            :alt="text"
            :src="computedAvatar"
            class="size-12"
            dot
            dot-class="bottom-0 right-1 border-2 size-4 bg-green-500"
          />
          <div class="ml-2 w-full flex flex-col gap-1">
            <div
              v-if="text"
              class="flex flex-wrap items-center gap-1.5 text-sm font-semibold text-foreground leading-none"
            >
              <span class="mr-0.5">{{ text }}</span>
              <Badge v-if="companyName" class="text-[9px] px-1.5 py-0.5 bg-primary/10 text-primary border-primary/20 max-w-[90px] truncate shrink-0">
                {{ companyName }}
              </Badge>
              <Badge v-if="deptName" class="text-[9px] px-1.5 py-0.5 bg-muted text-muted-foreground border max-w-[90px] truncate shrink-0">
                {{ deptName }}
              </Badge>
            </div>
            <div class="text-xs font-normal text-muted-foreground/80 truncate max-w-[180px]">
              {{ userEmail }}
            </div>
            <!-- 배정된 역할. 없는 계정도 있으므로 그 사실까지 보여 준다. -->
            <div class="flex flex-wrap items-center gap-1">
              <template v-if="userRoles.length > 0">
                <Badge
                  v-for="role in userRoles"
                  :key="role"
                  class="text-[9px] px-1.5 py-0.5 bg-transparent text-foreground/70 border-border font-normal"
                >
                  {{ role }}
                </Badge>
              </template>
              <span v-else class="text-[10px] text-muted-foreground/70">
                배정된 역할 없음
              </span>
            </div>
          </div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator v-if="menus?.length" />
        <DropdownMenuItem
          v-for="menu in menus"
          :key="menu.text"
          class="mx-1 flex cursor-pointer items-center rounded-sm py-1 leading-8"
          @click="menu.handler"
        >
          <VbenIcon :icon="menu.icon" class="mr-2 size-4" />
          {{ menu.text }}
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          v-if="preferences.widget.lockScreen"
          class="mx-1 flex cursor-pointer items-center rounded-sm py-1 leading-8"
          @click="handleOpenLock"
        >
          <LockKeyhole class="mr-2 size-4" />
          {{ $t('ui.widgets.lockScreen.title') }}
          <DropdownMenuShortcut v-if="enableLockScreenShortcutKey">
            {{ altView }} L
          </DropdownMenuShortcut>
        </DropdownMenuItem>
        <DropdownMenuSeparator v-if="preferences.widget.lockScreen" />
        <DropdownMenuItem
          class="mx-1 flex cursor-pointer items-center rounded-sm py-1 leading-8"
          @click="handleLogout"
        >
          <LogOut class="mr-2 size-4" />
          {{ $t('common.logout') }}
          <DropdownMenuShortcut v-if="enableLogoutShortcutKey">
            {{ altView }} Q
          </DropdownMenuShortcut>
        </DropdownMenuItem>
      </div>
    </DropdownMenuContent>
  </DropdownMenu>
</template>
