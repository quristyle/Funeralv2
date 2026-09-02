<script lang="ts" setup>
import type { AnyCustomPreferencesField } from '@vben/preferences';
import type { SelectOption } from '@vben/types';

import type { EnvContext, EnvField, EnvSection } from './catalog';

import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import { usePreferencesActions } from '@vben/layouts';
import { $t, loadLocaleMessages } from '@vben/locales';
import {
  preferences,
  updateCustomPreferences,
  updatePreferences,
  usePreferences,
} from '@vben/preferences';
import { useTimezoneStore } from '@vben/stores';

import {
  Button,
  Empty,
  Input,
  message,
  Popconfirm,
  Tooltip,
} from 'ant-design-vue';

import { useAuthStore } from '#/store';

import { buildSections } from './catalog';
import SettingField from './modules/setting-field.vue';
import ThemeColorChoice from './modules/theme-color-choice.vue';
import TileChoice from './modules/tile-choice.vue';
import WidgetOrder from './modules/widget-order.vue';

/**
 * [환경설정] — `/setting/environment`
 *
 * ── 왜 새로 만들었나 ────────────────────────────────────────
 *
 * 예전에는 `<PreferencesView>`(프레임워크)를 그대로 렌더했고, 그것은 헤더 톱니의
 * **드로어와 같은 부품**(`PreferencesPanel`)을 품고 있었다. 그 부품은 폭 350px
 * 짜리 서랍을 위해 만든 것이다 —
 *
 *   · 고르는 칸·입력칸 폭이 165px 로 못 박혀 있어 넓은 화면에서 라벨과 컨트롤 사이가 텅 빈다
 *   · 설명이 전부 물음표 툴팁에 숨어 있다 (좁아서 그렇게 한 것이다)
 *   · 항목 70여 개가 탭 넷 안에서 한 줄로 세로로 쌓인다
 *
 * 그래서 이 화면은 **자기 UI 를 갖는다.** 왼쪽에 갈래 목록, 위에 찾기 칸,
 * 오른쪽에 그 갈래의 항목만 — 설명은 펼쳐 두고 컨트롤은 넓게 둔다.
 *
 * ── 드로어는 손대지 않았다 ──────────────────────────────────
 *
 * 헤더 톱니의 서랍은 그대로다. 이 화면은 메뉴라서 열람 권한(`scom.role_menus`)이
 * 걸리는데, 권한 없는 역할이 생기면 그 사람은 자기 테마조차 못 바꾸게 된다.
 * 톱니가 있으면 그 경우에도 길이 남는다(23번 문서).
 *
 * ── 항목이 갈라지지 않게 하는 장치 ──────────────────────────
 *
 * 화면이 둘이 되면 한쪽에만 설정이 붙는 사고가 난다. 그래서
 * 항목을 [catalog.ts](./catalog.ts) 한 곳에 적고,
 * [coverage.test.ts](./coverage.test.ts) 가 드로어 패널의 `defineModel` 이름과
 * 대조한다 — **하나라도 빠지면 테스트가 깨진다.**
 *
 * 값을 저장하는 길은 예전과 같다. `updatePreferences()` 가 스토어를 고치고
 * `store/preferences-sync.ts` 가 계정에 붙여 서버로 보낸다(23번 문서).
 */

const authStore = useAuthStore();
const timezoneStore = useTimezoneStore();

const {
  customPreferences,
  isDark,
  isFullContent,
  isHeaderNav,
  isHeaderSidebarNav,
  isMixedNav,
  isSideMixedNav,
  isSideMode,
  isSideNav,
  layout,
  preferencesExtension,
} = usePreferences();

const { handleClearCache, handleCopy, handleReset, mergedDiffPreference } =
  usePreferencesActions(() => authStore.logout(false));

const timezones = ref<SelectOption[]>([]);
const keyword = ref('');
const activeKey = ref('general');

onMounted(async () => {
  timezones.value = (await timezoneStore.getTimezoneOptions()) ?? [];
});

/** 비활성·숨김 판정에 넘길 지금 상태. */
const ctx = computed<EnvContext>(() => ({
  isDark: isDark.value,
  isFullContent: isFullContent.value,
  isHeaderNav: isHeaderNav.value,
  isHeaderSidebarNav: isHeaderSidebarNav.value,
  isMixedNav: isMixedNav.value,
  isSideMixedNav: isSideMixedNav.value,
  isSideMode: isSideMode.value,
  isSideNav: isSideNav.value,
  layout: layout.value,
  p: preferences,
  timezones: timezones.value,
}));

/**
 * 우리 항목 갈래 (지금은 AI 하나).
 *
 * 프레임워크가 주는 확장 자리라 항목이 코드가 아니라 **설정으로** 온다
 * (`src/preferences.ts` 의 `jsiniPreferencesExtension`). 그래서 카탈로그에
 * 적지 않고 여기서 만든다 — 항목이 늘거나 줄어도 이 화면은 그대로다.
 */
const customSection = computed<EnvSection | null>(() => {
  const extension = preferencesExtension.value;
  if (!extension || extension.fields.length === 0) return null;

  return {
    key: 'custom',
    title: extension.title ? $t(extension.title) : $t(extension.tabLabel),
    desc: '이 포털이 더한 설정이다. 프레임워크 기본 항목이 아니다.',
    icon: 'lucide:bot',
    fields: (extension.fields as AnyCustomPreferencesField[]).map((field) => ({
      path: `custom.${field.key}`,
      model: `custom.${field.key}`,
      label: $t(field.label),
      desc: field.tip ? $t(field.tip) : undefined,
      control:
        field.component === 'number'
          ? 'number'
          : field.component === 'select'
            ? 'select'
            : field.component === 'switch'
              ? 'switch'
              : 'text',
      options:
        field.component === 'select'
          ? field.options.map((option) => ({
              label: $t(option.label),
              value: option.value,
            }))
          : undefined,
    })) as EnvField[],
  };
});

const sections = computed<EnvSection[]>(() => {
  const list = buildSections();
  const custom = customSection.value;
  return custom ? [...list, custom] : list;
});

// ── 값 읽기 · 쓰기 ─────────────────────────────────────────
//
// 경로는 `그룹.키` 두 마디다. `custom.*` 만 다른 저장소를 쓴다
// (프레임워크가 우리 항목을 `payload.custom` 에 따로 담는다).

function readPath(path: string): unknown {
  const [group, key] = path.split('.');
  if (!group || !key) return undefined;
  if (group === 'custom') {
    // 우리 항목은 키가 설정으로 오므로(`jsiniPreferencesExtension`) 타입이
    // 그 키만 아는 좁은 레코드다. 여기서는 문자열 경로로 찾아야 한다.
    return (customPreferences.value as unknown as Record<string, unknown>)?.[
      key
    ];
  }
  return (preferences as unknown as Record<string, Record<string, unknown>>)[
    group
  ]?.[key];
}

function writePath(path: string, value: unknown) {
  const [group, key] = path.split('.');
  if (!group || !key) return;

  if (group === 'custom') {
    updateCustomPreferences({ [key]: value as boolean | number | string });
    return;
  }

  updatePreferences({ [group]: { [key]: value } } as any);

  // 언어는 값만 바꿔서는 부족하다 — 그 언어의 번역을 받아 와야 화면이 바뀐다.
  // (드로어의 바인딩도 같은 일을 한다. 한쪽만 하면 언어가 반만 바뀐다.)
  if (path === 'app.locale') {
    loadLocaleMessages(value as any);
  }
}

// ── 그림으로 고르는 항목의 선택지 ──────────────────────────

const LAYOUT_ITEMS = computed(() => [
  {
    label: $t('preferences.vertical'),
    tip: $t('preferences.verticalTip'),
    value: 'sidebar-nav',
  },
  {
    label: $t('preferences.twoColumn'),
    tip: $t('preferences.twoColumnTip'),
    value: 'sidebar-mixed-nav',
  },
  {
    label: $t('preferences.horizontal'),
    tip: $t('preferences.horizontalTip'),
    value: 'header-nav',
  },
  {
    label: $t('preferences.headerSidebarNav'),
    tip: $t('preferences.headerSidebarNavTip'),
    value: 'header-sidebar-nav',
  },
  {
    label: $t('preferences.mixedMenu'),
    tip: $t('preferences.mixedMenuTip'),
    value: 'mixed-nav',
  },
  {
    label: $t('preferences.headerTwoColumn'),
    tip: $t('preferences.headerTwoColumnTip'),
    value: 'header-mixed-nav',
  },
  {
    label: $t('preferences.fullContent'),
    tip: $t('preferences.fullContentTip'),
    value: 'full-content',
  },
]);

const CONTENT_ITEMS = computed(() => [
  { label: $t('preferences.wide'), value: 'wide' },
  { label: $t('preferences.compact'), value: 'compact' },
]);

const THEME_MODE_ITEMS = computed(() => [
  { label: $t('preferences.theme.light'), value: 'light' },
  { label: $t('preferences.theme.dark'), value: 'dark' },
  { label: $t('preferences.followSystem'), value: 'auto' },
]);

const TRANSITION_ITEMS = [
  { label: 'fade', value: 'fade' },
  { label: 'fade-slide', value: 'fade-slide' },
  { label: 'fade-up', value: 'fade-up' },
  { label: 'fade-down', value: 'fade-down' },
];

// ── 헤더 위젯 ──────────────────────────────────────────────
//
// 위젯 한 개 = 순서 목록의 열쇠 하나 + 위치를 담는 설정 하나.
// 열쇠 이름은 `widget.order` 에 저장된 값이라 바꿀 수 없다.

const WIDGET_META: Record<
  string,
  { icon: string; label: string; path: string }
> = {
  fullscreen: {
    icon: 'lucide:maximize',
    label: 'preferences.widget.fullscreen',
    path: 'widget.fullscreenButtonPosition',
  },
  globalSearch: {
    icon: 'lucide:search',
    label: 'preferences.widget.globalSearch',
    path: 'widget.globalSearchButtonPosition',
  },
  languageToggle: {
    icon: 'lucide:languages',
    label: 'preferences.widget.languageToggle',
    path: 'widget.languageToggleButtonPosition',
  },
  lockScreenBtn: {
    icon: 'lucide:lock',
    label: 'ui.widgets.lockScreen.title',
    path: 'widget.lockScreenButtonPosition',
  },
  logoutBtn: {
    icon: 'lucide:log-out',
    label: 'common.logout',
    path: 'widget.logoutButtonPosition',
  },
  notification: {
    icon: 'lucide:bell',
    label: 'preferences.widget.notification',
    path: 'widget.notificationButtonPosition',
  },
  preferences: {
    icon: 'lucide:settings-2',
    label: 'preferences.title',
    path: 'app.preferencesButtonPosition',
  },
  refresh: {
    icon: 'lucide:refresh-cw',
    label: 'preferences.widget.refresh',
    path: 'widget.refreshButtonPosition',
  },
  themeToggle: {
    icon: 'lucide:sun-moon',
    label: 'preferences.widget.themeToggle',
    path: 'widget.themeToggleButtonPosition',
  },
  timezone: {
    icon: 'lucide:clock',
    label: 'preferences.widget.timezone',
    path: 'widget.timezoneButtonPosition',
  },
};

const POSITION_ITEMS = computed<SelectOption[]>(() => [
  { label: $t('preferences.widget.header'), value: 'header' },
  { label: $t('preferences.widget.userDropdown'), value: 'user-dropdown' },
  { label: $t('common.notShow'), value: 'none' },
]);

/** 환경설정 단추만 `auto`·`fixed` 를 더 갖는다 (catalog.ts 주석 참고). */
const PREFERENCES_POSITION_ITEMS = computed<SelectOption[]>(() => [
  { label: $t('preferences.position.auto'), value: 'auto' },
  { label: $t('preferences.position.header'), value: 'header' },
  { label: $t('preferences.position.fixed'), value: 'fixed' },
  { label: $t('preferences.position.userDropdown'), value: 'user-dropdown' },
  { label: $t('common.notShow'), value: 'none' },
]);

const widgetItems = computed(() =>
  Object.entries(WIDGET_META).map(([key, meta]) => ({
    icon: meta.icon,
    key,
    label: $t(meta.label),
    options:
      key === 'preferences'
        ? PREFERENCES_POSITION_ITEMS.value
        : POSITION_ITEMS.value,
    path: meta.path,
    position: String(readPath(meta.path) ?? 'none'),
    tip:
      key === 'preferences'
        ? '자동은 화면이 좁거나 전체 콘텐츠일 때 프레임워크가 알아서 자리를 옮긴다. 설정으로 돌아올 길이 막히지 않게 두는 값이다.'
        : undefined,
  })),
);

const widgetOrder = computed(() => [...(preferences.widget.order ?? [])]);

/**
 * 순서 목록에 빠진 위젯을 뒤에 붙여 준다.
 *
 * 저장된 `widget.order` 는 옛 버전에서 온 값일 수 있어 나중에 생긴 위젯이
 * 빠져 있을 수 있다. 그러면 그 위젯은 화면에서 아예 사라진다.
 */
const normalizedOrder = computed(() => {
  const known = Object.keys(WIDGET_META);
  const saved = widgetOrder.value.filter((key) => known.includes(key));
  return [...saved, ...known.filter((key) => !saved.includes(key))];
});

// ── 찾기 ───────────────────────────────────────────────────

const trimmedKeyword = computed(() => keyword.value.trim().toLowerCase());

function matches(field: EnvField): boolean {
  const q = trimmedKeyword.value;
  if (!q) return true;
  return `${field.label} ${field.desc ?? ''} ${field.search ?? ''} ${field.path}`
    .toLowerCase()
    .includes(q);
}

/** 화면에 그릴 갈래. 찾는 중이면 걸린 것만 남긴 갈래 전부다. */
const shownSections = computed<EnvSection[]>(() => {
  if (trimmedKeyword.value) {
    return sections.value
      .map((section) => ({
        ...section,
        fields: section.fields.filter(
          (field) => matches(field) && !field.hidden?.(ctx.value),
        ),
      }))
      .filter((section) => section.fields.length > 0);
  }

  const section = sections.value.find((s) => s.key === activeKey.value);
  if (!section) return [];
  return [
    {
      ...section,
      fields: section.fields.filter((field) => !field.hidden?.(ctx.value)),
    },
  ];
});

/** 갈래마다 몇 개가 기본값과 다른지 — 왼쪽 목록의 점. */
const changedPaths = computed(() => {
  const diff = mergedDiffPreference.value as
    | Record<string, Record<string, unknown>>
    | undefined;
  const set = new Set<string>();
  if (!diff) return set;
  for (const [group, values] of Object.entries(diff)) {
    if (values && typeof values === 'object') {
      for (const key of Object.keys(values)) set.add(`${group}.${key}`);
    }
  }
  return set;
});

const changedCount = computed(() => changedPaths.value.size);

/** 이 갈래에서 기본값과 다른 항목 수. 컨트롤 하나가 값 여럿을 다루면 그것도 센다. */
function sectionChangedCount(section: EnvSection): number {
  return section.fields.filter((field) =>
    [field.path, ...(field.extraPaths ?? [])].some((path) =>
      changedPaths.value.has(path),
    ),
  ).length;
}

async function onReset() {
  await handleReset();
  message.success($t('preferences.resetSuccess'));
}
</script>

<template>
  <Page auto-content-height>
    <!--
      좁은 화면에서는 위아래로 쌓고 갈래 목록을 가로로 굴린다.
      데스크톱용 화면이지만(지시), 폭 375px 에서 왼쪽 목록이 196px 을 차지하면
      설정 쪽이 140px 밖에 남지 않아 아무것도 못 만진다 — 실제로 재 보았다.
    -->
    <div class="flex h-full flex-col gap-3 lg:flex-row">
      <!-- 왼쪽(좁으면 위) — 갈래 목록 -->
      <aside class="flex w-full shrink-0 flex-col gap-2 lg:w-56">
        <Input
          v-model:value="keyword"
          allow-clear
          placeholder="설정 찾기 (예: 워터마크)"
        >
          <template #prefix>
            <IconifyIcon icon="lucide:search" class="size-3.5 opacity-50" />
          </template>
        </Input>

        <nav
          class="bg-card flex gap-1 overflow-x-auto rounded-lg border p-1.5 lg:block lg:min-h-0 lg:flex-1 lg:gap-0 lg:overflow-x-hidden lg:overflow-y-auto"
        >
          <button
            v-for="section in sections"
            :key="section.key"
            type="button"
            class="flex shrink-0 items-center gap-2 rounded-md px-2.5 py-2 text-left text-sm whitespace-nowrap transition-colors lg:w-full lg:whitespace-normal"
            :class="
              !trimmedKeyword && activeKey === section.key
                ? 'bg-primary/10 text-primary font-medium'
                : 'hover:bg-accent text-foreground/80'
            "
            @click="
              () => {
                keyword = '';
                activeKey = section.key;
              }
            "
          >
            <IconifyIcon :icon="section.icon" class="size-4 shrink-0" />
            <span class="min-w-0 lg:flex-1 lg:truncate">{{ section.title }}</span>
            <span
              v-if="sectionChangedCount(section) > 0"
              class="bg-primary/15 text-primary rounded px-1 text-[10px]"
            >
              {{ sectionChangedCount(section) }}
            </span>
          </button>
        </nav>
      </aside>

      <!-- 오른쪽 — 도구줄 + 항목 -->
      <section class="flex min-h-0 min-w-0 flex-1 flex-col gap-3">
        <div
          class="bg-card flex flex-wrap items-center justify-between gap-2 rounded-lg border px-4 py-2.5"
        >
          <div class="flex items-baseline gap-2">
            <h1 class="text-base font-semibold">{{ $t('preferences.title') }}</h1>
            <span class="text-muted-foreground text-xs">
              {{ $t('preferences.subtitle') }} · 이 계정에만 적용된다
            </span>
            <span
              v-if="changedCount > 0"
              class="bg-primary/10 text-primary rounded px-1.5 py-0.5 text-xs"
            >
              기본값과 다른 항목 {{ changedCount }}개
            </span>
          </div>

          <div class="flex items-center gap-1.5">
            <Tooltip
              v-if="preferences.app.enableCopyPreferences"
              title="기본값과 다른 항목만 JSON 으로 복사한다. 개발자가 기본값으로 옮길 때 쓴다."
            >
              <Button
                :disabled="!mergedDiffPreference"
                size="small"
                @click="handleCopy"
              >
                <IconifyIcon icon="lucide:copy" class="mr-1 size-3.5" />
                {{ $t('preferences.copyPreferences') }}
              </Button>
            </Tooltip>

            <Popconfirm
              title="모든 설정을 기본값으로 되돌립니다."
              ok-text="초기화"
              cancel-text="취소"
              :disabled="!mergedDiffPreference"
              @confirm="onReset"
            >
              <Button :disabled="!mergedDiffPreference" size="small">
                <IconifyIcon icon="lucide:rotate-cw" class="mr-1 size-3.5" />
                {{ $t('preferences.resetTitle') }}
              </Button>
            </Popconfirm>

            <Popconfirm
              title="설정과 캐시를 지우고 로그아웃합니다. 다시 로그인해야 합니다."
              ok-text="지우고 로그아웃"
              cancel-text="취소"
              @confirm="handleClearCache"
            >
              <Button danger size="small" type="text">
                {{ $t('preferences.clearAndLogout') }}
              </Button>
            </Popconfirm>
          </div>
        </div>

        <div class="min-h-0 flex-1 overflow-y-auto">
          <div class="flex flex-col gap-3 pb-1">
            <div
              v-for="section in shownSections"
              :key="section.key"
              class="bg-card rounded-lg border"
            >
              <div class="border-b px-4 py-2.5">
                <h2 class="flex items-center gap-2 text-sm font-semibold">
                  <IconifyIcon :icon="section.icon" class="size-4" />
                  {{ section.title }}
                </h2>
                <p v-if="section.desc" class="text-muted-foreground mt-0.5 text-xs">
                  {{ section.desc }}
                </p>
              </div>

              <div
                class="px-4 py-1"
                :class="
                  section.twoColumn && !trimmedKeyword
                    ? 'grid gap-x-8 divide-y xl:grid-cols-2 xl:divide-y-0'
                    : 'divide-y'
                "
              >
                <template v-for="field in section.fields" :key="field.path">
                  <!-- 그림·목록으로 고르는 것은 한 줄을 통째로 쓴다. -->
                  <div
                    v-if="field.wide"
                    class="py-3"
                    :class="
                      section.twoColumn && !trimmedKeyword
                        ? 'xl:col-span-2'
                        : ''
                    "
                  >
                    <div class="text-sm font-medium">{{ field.label }}</div>
                    <p
                      v-if="field.desc"
                      class="text-muted-foreground mt-0.5 mb-2.5 text-xs"
                    >
                      {{ field.desc }}
                    </p>

                    <TileChoice
                      v-if="field.control === 'layout'"
                      kind="layout"
                      :items="LAYOUT_ITEMS"
                      :model-value="readPath(field.path) as string"
                      @update:model-value="(v) => writePath(field.path, v)"
                    />
                    <TileChoice
                      v-else-if="field.control === 'contentWidth'"
                      kind="contentWidth"
                      :items="CONTENT_ITEMS"
                      :model-value="readPath(field.path) as string"
                      @update:model-value="(v) => writePath(field.path, v)"
                    />
                    <TileChoice
                      v-else-if="field.control === 'themeMode'"
                      kind="themeMode"
                      :items="THEME_MODE_ITEMS"
                      :model-value="readPath(field.path) as string"
                      @update:model-value="(v) => writePath(field.path, v)"
                    />
                    <TileChoice
                      v-else-if="field.control === 'transition'"
                      kind="transition"
                      :disabled="field.disabled?.(ctx) ?? false"
                      :items="TRANSITION_ITEMS"
                      :model-value="readPath(field.path) as string"
                      @update:model-value="(v) => writePath(field.path, v)"
                    />
                    <ThemeColorChoice
                      v-else-if="field.control === 'builtinTheme'"
                      :builtin-type="readPath('theme.builtinType') as string"
                      :color-primary="readPath('theme.colorPrimary') as string"
                      @update:builtin-type="
                        (v) => writePath('theme.builtinType', v)
                      "
                      @update:color-primary="
                        (v) => writePath('theme.colorPrimary', v)
                      "
                    />
                    <WidgetOrder
                      v-else-if="field.control === 'widgets'"
                      :items="widgetItems"
                      :order="normalizedOrder"
                      @update:order="(v) => writePath('widget.order', v)"
                      @update:position="(path, v) => writePath(path, v)"
                    />
                  </div>

                  <SettingField
                    v-else
                    :ctx="ctx"
                    :field="field"
                    :value="readPath(field.path)"
                    @change="(v) => writePath(field.path, v)"
                  />
                </template>
              </div>
            </div>

            <Empty
              v-if="shownSections.length === 0"
              :description="`'${keyword}' 에 해당하는 설정이 없습니다.`"
            />
          </div>
        </div>
      </section>
    </div>
  </Page>
</template>
