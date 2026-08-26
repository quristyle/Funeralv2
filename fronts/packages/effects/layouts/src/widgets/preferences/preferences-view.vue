<script setup lang="ts">
/**
 * [환경설정 — 페이지]
 *
 * 헤더 톱니의 드로어와 **같은 설정을 같은 구현으로** 다룬다.
 * 라우트가 이 컴포넌트 하나만 렌더하면 된다(`/setting/environment`).
 *
 *   preferences.vue       톱니 버튼 + 드로어 껍데기
 *   preferences-view.vue  이 파일 — 페이지 껍데기
 *   preferences-panel.vue 둘이 공유하는 내용 (탭 · 설정 블록)
 *
 * [드로어와 다르게 둔 것]
 * 드로어는 폭이 `sm:max-w-sm` 로 좁아 세로로 길게 스크롤된다. 페이지는 넓으므로
 * 탭 머리를 스크롤에 붙이지 않고(`sticky-tabs` 끔), 블록을 여러 단으로 흘린다.
 * 설정 항목이 40개가 넘어 한 단으로 세우면 화면을 한참 굴려야 한다.
 */
import { computed } from 'vue';

import { Copy, Pin, PinOff, RotateCw } from '@vben/icons';
import { $t } from '@vben/locales';
import { preferences, updatePreferences } from '@vben/preferences';

import { VbenButton, VbenIconButton } from '@vben-core/shadcn-ui';

import PreferencesPanel from './preferences-panel.vue';
import { usePreferencesActions } from './use-preferences-actions';
import { usePreferencesBinding } from './use-preferences-binding';

const emit = defineEmits<{ clearPreferencesAndLogout: [] }>();

const { attrs, listen } = usePreferencesBinding();
const { handleClearCache, handleCopy, handleReset, mergedDiffPreference } =
  usePreferencesActions(() => emit('clearPreferencesAndLogout'));

/** 기본값과 달라진 항목이 몇 개인지. 페이지에는 자리가 있으니 숫자로 보여 준다. */
const changedCount = computed(() => {
  const diff = mergedDiffPreference.value;
  if (!diff) return 0;
  // 최상위(app · theme · widget …) 아래 실제 값의 개수를 센다.
  return Object.values(diff).reduce(
    (sum, group) =>
      sum +
      (group && typeof group === 'object'
        ? Object.keys(group as object).length
        : 1),
    0,
  );
});

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
  <div class="preferences-view flex h-full flex-col gap-3">
    <!-- 툴바 — 드로어의 머리(초기화 · 고정)와 바닥(복사 · 캐시 삭제)을 한 줄로 모았다. -->
    <div
      class="bg-card flex flex-wrap items-center justify-between gap-2 rounded-lg border px-4 py-3"
    >
      <div class="flex items-center gap-2">
        <span class="text-sm font-medium">{{ $t('preferences.title') }}</span>
        <span class="text-muted-foreground text-xs">
          {{ $t('preferences.subtitle') }}
        </span>
        <span
          v-if="changedCount > 0"
          class="bg-primary/10 text-primary rounded px-1.5 py-0.5 text-xs"
        >
          기본값과 다른 항목 {{ changedCount }}개
        </span>
      </div>

      <div class="flex items-center gap-1">
        <VbenIconButton
          :disabled="!mergedDiffPreference"
          :tooltip="$t('preferences.resetTip')"
          class="relative"
          @click="handleReset"
        >
          <span
            v-if="mergedDiffPreference"
            class="bg-primary absolute top-0.5 right-0.5 size-2 rounded-sm"
          ></span>
          <RotateCw class="size-4" />
        </VbenIconButton>

        <VbenIconButton
          :tooltip="
            preferences.app.enableStickyPreferencesNavigationBar
              ? $t('preferences.disableStickyPreferencesNavigationBar')
              : $t('preferences.enableStickyPreferencesNavigationBar')
          "
          @click="toggleStickyNavigationBar"
        >
          <PinOff
            v-if="preferences.app.enableStickyPreferencesNavigationBar"
            class="size-4"
          />
          <Pin v-else class="size-4" />
        </VbenIconButton>

        <VbenButton
          v-if="preferences.app.enableCopyPreferences"
          :disabled="!mergedDiffPreference"
          size="sm"
          variant="outline"
          @click="handleCopy"
        >
          <Copy class="mr-1.5 size-3" />
          {{ $t('preferences.copyPreferences') }}
        </VbenButton>

        <VbenButton
          :disabled="!mergedDiffPreference"
          size="sm"
          variant="ghost"
          @click="handleClearCache"
        >
          {{ $t('preferences.clearAndLogout') }}
        </VbenButton>
      </div>
    </div>

    <!-- 내용 — 드로어와 같은 패널 -->
    <div class="bg-card min-h-0 flex-1 overflow-auto rounded-lg border p-4">
      <PreferencesPanel v-bind="{ ...attrs }" v-on="listen" />
    </div>
  </div>
</template>

<style scoped>
/*
  드로어는 좁아서 한 단으로 세우지만, 페이지는 넓으므로 블록을 여러 단으로 나눈다.
  설정 항목이 40개가 넘어 한 단으로 두면 화면을 한참 굴려야 한다
  (준수사항 4번 — 세로 스크롤 없이 한 화면에 담는 것을 지향한다).

  **`column-count`(다단 흘리기)를 쓰면 안 된다.** 처음 그렇게 만들었다가 되돌렸다.
  다단은 요소를 단 경계에서 조각내고, 조각난 요소의 위치와 실제 클릭 영역이 어긋난다.
  그래서 테두리 반경·글꼴 크기 같은 컨트롤을 눌러도 아무 일이 없었다
  (값은 정상으로 보이니 바인딩이 깨진 것처럼 보인다 — 찾기 어려운 종류의 고장이다).
  드래그로 순서를 바꾸는 위젯 목록은 더 확실히 망가진다.

  그리드는 요소를 조각내지 않으므로 클릭 영역이 그대로다.
*/
@media (min-width: 1280px) {
  .preferences-view :deep([role='tabpanel']) {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 0 1.5rem;
    align-items: start;
  }
}

@media (min-width: 1800px) {
  .preferences-view :deep([role='tabpanel']) {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}
</style>
