<script lang="ts" setup>
import { computed } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { Tooltip } from 'ant-design-vue';

/**
 * 그림으로 고르는 항목 — 레이아웃 · 본문 폭 · 테마 · 전환 효과.
 *
 * 넷을 한 부품으로 그리는 이유: 고르는 방식이 같다(그림 + 이름, 하나만 선택).
 * 그림만 `kind` 에 따라 달라진다.
 *
 * **프레임워크의 SVG 아이콘을 쓰지 않고 직접 그린다.** 그 파일들은
 * `layouts/src/widgets/preferences/icons` 안에 있고 패키지 밖으로 내보내지 않는다.
 * 내보내려면 상위와 갈라지는 포크 수정이 하나 늘어난다(CLAUDE.md).
 * 네모 몇 개로 충분히 알아볼 수 있는 그림이라 여기서 그린다.
 */

interface Item {
  label: string;
  tip?: string;
  value: string;
}

interface Props {
  disabled?: boolean;
  items: Item[];
  kind: 'contentWidth' | 'layout' | 'themeMode' | 'transition';
  modelValue?: string;
}

const props = withDefaults(defineProps<Props>(), {
  disabled: false,
  modelValue: '',
});

const emit = defineEmits<{ 'update:modelValue': [value: string] }>();

/**
 * 레이아웃 그림의 짜임.
 *
 * `rails` 는 왼쪽 메뉴 칸들이다 — `rail` 은 아이콘만 있는 좁은 칸,
 * `panel` 은 이름이 보이는 칸. `headerFull` 은 머리줄이 메뉴 위까지
 * 가로로 뻗는가이고, `headerMenus` 는 그 머리줄에 메뉴가 있는가다.
 * (혼합 계열과 '헤더 세로형' 을 구분하는 것이 이 값이다.)
 */
const LAYOUT_SHAPE: Record<
  string,
  { headerFull: boolean; headerMenus: boolean; rails: string[] }
> = {
  'full-content': { headerFull: false, headerMenus: false, rails: [] },
  'header-mixed-nav': {
    headerFull: true,
    headerMenus: true,
    rails: ['rail', 'panel'],
  },
  'header-nav': { headerFull: true, headerMenus: true, rails: [] },
  'header-sidebar-nav': {
    headerFull: true,
    headerMenus: false,
    rails: ['panel'],
  },
  'mixed-nav': { headerFull: true, headerMenus: true, rails: ['panel'] },
  'sidebar-mixed-nav': {
    headerFull: false,
    headerMenus: false,
    rails: ['rail', 'panel'],
  },
  'sidebar-nav': { headerFull: false, headerMenus: false, rails: ['panel'] },
};

const THEME_ICON: Record<string, string> = {
  auto: 'lucide:sun-moon',
  dark: 'lucide:moon-star',
  light: 'lucide:sun',
};

function shape(value: string) {
  return (
    LAYOUT_SHAPE[value] ?? {
      headerFull: false,
      headerMenus: false,
      rails: [],
    }
  );
}

/** 본문만 남는 레이아웃은 머리줄도 없다. */
const hasHeader = (value: string) => value !== 'full-content';

const tileClass = computed(() => (props.kind === 'layout' ? 'w-28' : 'w-24'));

function pick(value: string) {
  if (props.disabled) return;
  emit('update:modelValue', value);
}
</script>

<template>
  <div class="flex flex-wrap gap-3" :class="{ 'opacity-45': disabled }">
    <button
      v-for="item in items"
      :key="item.value"
      type="button"
      class="group flex flex-col items-center gap-1.5"
      :class="[tileClass, disabled ? 'cursor-not-allowed' : 'cursor-pointer']"
      :disabled="disabled"
      :aria-pressed="modelValue === item.value"
      @click="pick(item.value)"
    >
      <span
        class="bg-background flex w-full items-center justify-center overflow-hidden rounded-md border-2 p-1.5 transition-colors"
        :class="
          modelValue === item.value
            ? 'border-primary'
            : 'border-border group-hover:border-primary/50'
        "
      >
        <!-- 레이아웃 · 본문 폭 — 네모로 그린 도식 -->
        <span
          v-if="kind === 'layout'"
          class="bg-muted/40 flex h-14 w-full overflow-hidden rounded-sm"
          :class="{ 'flex-col': shape(item.value).headerFull }"
        >
          <!-- 가로로 뻗는 머리줄 -->
          <span
            v-if="shape(item.value).headerFull"
            class="bg-primary flex h-2.5 w-full shrink-0 items-center gap-0.5 px-1"
          >
            <template v-if="shape(item.value).headerMenus">
              <span
                v-for="n in 3"
                :key="n"
                class="bg-primary-foreground/70 h-1 w-2.5 rounded-full"
              ></span>
            </template>
          </span>

          <span class="flex min-h-0 flex-1">
            <!-- 왼쪽 메뉴 칸 -->
            <span
              v-for="(rail, index) in shape(item.value).rails"
              :key="index"
              class="bg-primary/85 flex shrink-0 flex-col gap-1 py-1"
              :class="rail === 'rail' ? 'w-2.5 items-center' : 'w-6 px-1'"
            >
              <span
                v-for="n in 3"
                :key="n"
                class="bg-primary-foreground/70 h-1 rounded-full"
                :class="rail === 'rail' ? 'w-1.5' : 'w-full'"
              ></span>
            </span>

            <span class="flex min-w-0 flex-1 flex-col">
              <!-- 본문 위 머리줄 (세로형 계열) -->
              <span
                v-if="!shape(item.value).headerFull && hasHeader(item.value)"
                class="bg-primary/30 h-2.5 w-full shrink-0"
              ></span>
              <span class="flex min-h-0 flex-1 flex-col gap-1 p-1">
                <span class="bg-foreground/15 h-1 w-3/4 rounded-full"></span>
                <span class="bg-foreground/10 h-1 w-full rounded-full"></span>
                <span class="bg-foreground/10 h-1 w-2/3 rounded-full"></span>
              </span>
            </span>
          </span>
        </span>

        <span
          v-else-if="kind === 'contentWidth'"
          class="bg-muted/40 flex h-14 w-full flex-col overflow-hidden rounded-sm"
        >
          <span class="bg-primary/30 h-2.5 w-full shrink-0"></span>
          <span class="flex min-h-0 flex-1 justify-center p-1">
            <span
              class="bg-foreground/10 flex flex-col gap-1 rounded-sm p-1"
              :class="item.value === 'compact' ? 'w-1/2' : 'w-full'"
            >
              <span class="bg-foreground/20 h-1 w-3/4 rounded-full"></span>
              <span class="bg-foreground/15 h-1 w-full rounded-full"></span>
            </span>
          </span>
        </span>

        <!-- 테마 — 아이콘 -->
        <span
          v-else-if="kind === 'themeMode'"
          class="flex h-12 w-full items-center justify-center"
        >
          <IconifyIcon
            :icon="THEME_ICON[item.value] ?? 'lucide:circle'"
            class="size-6"
          />
        </span>

        <!-- 전환 효과 — 실제로 움직이는 미리보기 -->
        <span
          v-else
          class="flex h-12 w-full items-center justify-center overflow-hidden"
        >
          <span
            class="bg-primary size-8 rounded-md"
            :class="`${item.value}-slow`"
          ></span>
        </span>
      </span>

      <span
        class="flex items-center gap-1 text-center text-xs"
        :class="
          modelValue === item.value
            ? 'text-foreground font-medium'
            : 'text-muted-foreground'
        "
      >
        {{ item.label }}
        <Tooltip v-if="item.tip" :title="item.tip">
          <IconifyIcon icon="lucide:circle-help" class="size-3 cursor-help" />
        </Tooltip>
      </span>
    </button>
  </div>
</template>
