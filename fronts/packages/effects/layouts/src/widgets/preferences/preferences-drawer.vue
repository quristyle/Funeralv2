<script setup lang="ts">
/**
 * [환경설정 드로어]
 *
 * 헤더 톱니를 눌렀을 때 오른쪽에서 나오는 껍데기다.
 *
 * **설정 항목은 여기 없다.** 전부 `preferences-panel.vue` 가 갖고 있고,
 * 같은 패널을 `/setting/environment` 페이지(`preferences-view.vue`)도 쓴다.
 * 예전에는 이 파일 안에 탭과 블록이 모두 들어 있었다. 그 상태로 페이지를 만들면
 * 통째로 복사해야 하고, 그러면 설정이 하나 늘 때마다 두 곳을 고쳐야 한다.
 *
 * 설정 값은 선언하지 않는다 — `usePreferencesBinding()` 이 만든 props·listener 가
 * `$attrs` 로 들어와 그대로 패널에 흘러간다(`v-bind="$attrs"`).
 * 그래서 설정 항목이 늘어도 이 파일은 고치지 않는다.
 */
import { Copy, Pin, PinOff, RotateCw } from '@vben/icons';
import { $t } from '@vben/locales';
import { preferences, updatePreferences } from '@vben/preferences';

import { useVbenDrawer } from '@vben-core/popup-ui';
import { VbenButton, VbenIconButton } from '@vben-core/shadcn-ui';

import PreferencesPanel from './preferences-panel.vue';
import { usePreferencesActions } from './use-preferences-actions';

const emit = defineEmits<{ clearPreferencesAndLogout: [] }>();

const [Drawer] = useVbenDrawer();

const { handleClearCache, handleCopy, handleReset, mergedDiffPreference } =
  usePreferencesActions(() => emit('clearPreferencesAndLogout'));

function toggleStickyNavigationBar() {
  updatePreferences({
    app: {
      enableStickyPreferencesNavigationBar:
        !preferences.app.enableStickyPreferencesNavigationBar,
    },
  });
}
</script>

<template>
  <div>
    <Drawer
      :description="$t('preferences.subtitle')"
      :title="$t('preferences.title')"
      class="border-0! sm:max-w-sm"
    >
      <template #extra>
        <div class="flex items-center">
          <VbenIconButton
            :disabled="!mergedDiffPreference"
            :tooltip="$t('preferences.resetTip')"
            class="relative"
            @click="handleReset"
          >
            <span
              v-if="mergedDiffPreference"
              class="absolute top-0.5 right-0.5 size-2 rounded-sm bg-primary"
            ></span>
            <RotateCw class="size-4" />
          </VbenIconButton>
          <VbenIconButton
            :tooltip="
              preferences.app.enableStickyPreferencesNavigationBar
                ? $t('preferences.disableStickyPreferencesNavigationBar')
                : $t('preferences.enableStickyPreferencesNavigationBar')
            "
            class="relative"
            @click="toggleStickyNavigationBar"
          >
            <PinOff
              v-if="preferences.app.enableStickyPreferencesNavigationBar"
              class="size-4"
            />
            <Pin v-else class="size-4" />
          </VbenIconButton>
        </div>
      </template>

      <div>
        <PreferencesPanel
          v-bind="$attrs"
          :sticky-tabs="preferences.app.enableStickyPreferencesNavigationBar"
        />
      </div>

      <template #footer>
        <VbenButton
          v-if="preferences.app.enableCopyPreferences"
          :disabled="!mergedDiffPreference"
          class="mx-4 w-full"
          size="sm"
          variant="default"
          @click="handleCopy"
        >
          <Copy class="mr-2 size-3" />
          {{ $t('preferences.copyPreferences') }}
        </VbenButton>
        <VbenButton
          :disabled="!mergedDiffPreference"
          class="mr-4 w-full"
          size="sm"
          variant="ghost"
          @click="handleClearCache"
        >
          {{ $t('preferences.clearAndLogout') }}
        </VbenButton>
      </template>
    </Drawer>
  </div>
</template>
