<script lang="ts" setup>
import type { BuiltinThemeType } from '@vben/types';

import { computed, ref } from 'vue';

import { IconifyIcon } from '@vben/icons';
import { $t } from '@vben/locales';
import { BUILT_IN_THEME_PRESETS } from '@vben/preferences';
import { convertToHsl, TinyColor } from '@vben/utils';

/**
 * 기본 테마 색 고르기.
 *
 * 색 동그라미를 누르면 그 테마가 되고, 맨 끝 [직접 고르기] 는 색 선택기를 띄운다.
 *
 * **주 색상(`theme.colorPrimary`)을 여기서 직접 쓰지 않는다.** 기본 테마를 고르면
 * 프레임워크가 라이트/다크에 맞는 값을 알아서 넣어 준다
 * (`update-css-variables.ts`). 직접 고른 색만 이 화면이 넣는다 —
 * 그러지 않으면 어두운 테마로 바꿀 때 색이 따라오지 않는다.
 */

interface Props {
  builtinType?: string;
  colorPrimary?: string;
}

const props = defineProps<Props>();

const emit = defineEmits<{
  'update:builtinType': [value: string];
  'update:colorPrimary': [value: string];
}>();

const colorInputRef = ref<HTMLInputElement>();

/** 테마 이름 (locale 키가 카멜케이스라 그대로 못 쓴다). */
const LABEL_KEY: Record<string, string> = {
  custom: 'custom',
  'deep-blue': 'deepBlue',
  'deep-green': 'deepGreen',
  default: 'default',
  gray: 'gray',
  green: 'green',
  neutral: 'neutral',
  orange: 'orange',
  pink: 'pink',
  rose: 'rose',
  'sky-blue': 'skyBlue',
  slate: 'slate',
  violet: 'violet',
  yellow: 'yellow',
  zinc: 'zinc',
};

const presets = computed(() =>
  [...BUILT_IN_THEME_PRESETS].map((preset) => ({
    color: preset.color,
    label: $t(
      `preferences.theme.builtin.${LABEL_KEY[preset.type] ?? preset.type}`,
    ),
    type: preset.type as BuiltinThemeType,
  })),
);

/** 색 선택기(`<input type="color">`)가 읽을 수 있는 `#rrggbb` 로 바꾼다. */
const hexValue = computed(() =>
  new TinyColor(props.colorPrimary || '').toHexString(),
);

/**
 * 색 선택기가 주는 `#rrggbb` 를 스토어가 쓰는 `hsl(h s% l%)` 로 바꿔 넣는다.
 * 그러지 않으면 CSS 변수 계산이 어긋난다(프레임워크도 같은 변환을 한다).
 */
function handleCustomInput(event: Event) {
  const target = event.target as HTMLInputElement;
  emit('update:builtinType', 'custom');
  emit('update:colorPrimary', convertToHsl(target.value));
}
</script>

<template>
  <div class="flex flex-wrap gap-2">
    <template v-for="preset in presets" :key="preset.type">
      <!-- 사용자 정의는 색 선택기를 품고 있어 따로 그린다. -->
      <button
        v-if="preset.type === 'custom'"
        type="button"
        class="group relative flex flex-col items-center gap-1"
        :title="preset.label"
        @click="colorInputRef?.click()"
      >
        <span
          class="flex size-9 items-center justify-center rounded-md border-2 transition-colors"
          :class="
            builtinType === 'custom'
              ? 'border-primary'
              : 'border-border group-hover:border-primary/50'
          "
          :style="
            builtinType === 'custom'
              ? { backgroundColor: hexValue }
              : undefined
          "
        >
          <IconifyIcon
            icon="lucide:pipette"
            class="size-4"
            :class="builtinType === 'custom' ? 'text-white/90' : ''"
          />
        </span>
        <span class="text-muted-foreground text-[11px]">{{ preset.label }}</span>
        <input
          ref="colorInputRef"
          type="color"
          class="pointer-events-none absolute inset-0 size-0 opacity-0"
          :value="hexValue"
          @input="handleCustomInput"
        />
      </button>

      <button
        v-else
        type="button"
        class="group flex flex-col items-center gap-1"
        :title="preset.label"
        @click="emit('update:builtinType', preset.type)"
      >
        <span
          class="flex size-9 items-center justify-center rounded-md border-2 transition-colors"
          :class="
            builtinType === preset.type
              ? 'border-primary'
              : 'border-border group-hover:border-primary/50'
          "
        >
          <span
            class="size-5 rounded-sm"
            :style="{ backgroundColor: preset.color }"
          ></span>
        </span>
        <span class="text-muted-foreground text-[11px]">{{ preset.label }}</span>
      </button>
    </template>
  </div>
</template>
