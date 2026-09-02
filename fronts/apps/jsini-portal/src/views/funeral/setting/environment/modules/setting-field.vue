<script lang="ts" setup>
import type { SelectOption } from '@vben/types';

import type { EnvContext, EnvField } from '../catalog';

import { computed } from 'vue';

import { isWindowsOs } from '@vben/utils';

import {
  Input,
  InputNumber,
  Segmented,
  Select,
  Switch,
} from 'ant-design-vue';

/**
 * 설정 한 항목 = 한 줄.
 *
 * 드로어의 `switch-item.vue` 계열과 다른 점 셋.
 *
 * 1. **설명을 펼친다.** 드로어는 폭이 없어 물음표 툴팁에 숨겼다. 넓은 화면에서
 *    설명을 숨기면 사람이 물음표를 하나하나 눌러야 한다.
 * 2. **컨트롤 폭이 고정이 아니다.** 드로어는 `w-41.25`(165px)로 못 박아 두었고,
 *    그래서 넓은 화면에서 라벨과 컨트롤 사이가 텅 비었다.
 * 3. **부품이 ant-design-vue 다.** 이 화면 말고 앱의 다른 화면 전부가 antd 이므로
 *    같은 스위치·고르는 칸 모양이 된다(드로어는 프레임워크 부품을 계속 쓴다).
 */

interface Props {
  ctx: EnvContext;
  field: EnvField;
  /** 지금 값 */
  value: unknown;
}

const props = defineProps<Props>();

const emit = defineEmits<{ change: [value: unknown] }>();

const disabled = computed(() => props.field.disabled?.(props.ctx) ?? false);

const options = computed<SelectOption[]>(() => {
  const opts = props.field.options;
  if (!opts) return [];
  return typeof opts === 'function' ? opts(props.ctx) : opts;
});

/**
 * 단축키 표시. `mod` 는 윈도우에서 Ctrl, 맥에서 ⌘ 다.
 * 카탈로그에 OS 별 글자를 적어 두면 두 곳이 어긋난다.
 */
const shortcutKeys = computed(() => {
  const raw = props.field.shortcut;
  if (!raw) return [];
  return raw.split(' ').map((key) => {
    if (key === 'mod') return isWindowsOs() ? 'Ctrl' : '⌘';
    if (key === 'alt') return isWindowsOs() ? 'Alt' : '⌥';
    return key;
  });
});
</script>

<template>
  <div
    class="flex items-start justify-between gap-6 py-2.5"
    :class="{ 'opacity-45': disabled }"
  >
    <div class="min-w-0">
      <div class="flex items-center gap-2 text-sm font-medium">
        <span>{{ field.label }}</span>
        <span v-if="shortcutKeys.length > 0" class="flex items-center gap-1">
          <kbd
            v-for="key in shortcutKeys"
            :key="key"
            class="bg-muted text-muted-foreground rounded border px-1 py-px font-mono text-[10px] leading-4"
          >
            {{ key }}
          </kbd>
        </span>
      </div>
      <p v-if="field.desc" class="text-muted-foreground mt-0.5 text-xs">
        {{ field.desc }}
      </p>
    </div>

    <div class="shrink-0 pt-0.5">
      <Switch
        v-if="field.control === 'switch'"
        :checked="Boolean(value)"
        :disabled="disabled"
        @change="(v) => emit('change', Boolean(v))"
      />

      <Select
        v-else-if="field.control === 'select'"
        class="w-56"
        :disabled="disabled"
        :options="options"
        :value="value as string"
        show-search
        option-filter-prop="label"
        @change="(v) => emit('change', v)"
      />

      <Segmented
        v-else-if="field.control === 'segmented'"
        :disabled="disabled"
        :options="options as any"
        :value="value as string"
        @change="(v) => emit('change', v)"
      />

      <InputNumber
        v-else-if="field.control === 'number'"
        class="w-32"
        :disabled="disabled"
        :max="field.max"
        :min="field.min"
        :step="field.step"
        :value="value as number"
        @change="(v) => emit('change', v)"
      />

      <Input
        v-else-if="field.control === 'text'"
        class="w-64"
        allow-clear
        :disabled="disabled"
        :value="value as string"
        @update:value="(v) => emit('change', v ?? '')"
      />
    </div>
  </div>
</template>
