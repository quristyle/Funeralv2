<script lang="ts" setup>
import type { SelectOption } from '@vben/types';

import { computed } from 'vue';

import { IconifyIcon } from '@vben/icons';
import { $t } from '@vben/locales';

import { Select, Tooltip } from 'ant-design-vue';

/**
 * 헤더 위젯 — 순서와 위치.
 *
 * **드래그 대신 위·아래 화살표를 쓴다.** 드로어 쪽(`draggable-list.vue`)은
 * `sortablejs` 로 끌어 옮기는데, 그 꾸러미는 프레임워크 패키지의 의존성이라
 * 앱에는 없다. 의존성을 하나 더 늘리기보다 화살표로 둔다 —
 * 데스크톱에서는 한 칸씩 옮기는 쪽이 오히려 정확하고, 자리를 잡으려고
 * 끌다 놓치는 일도 없다.
 *
 * 위치가 '표시하지 않음' 인 위젯은 아래 따로 모은다. 순서는 그 안에서 뜻이 없다.
 */

interface WidgetItem {
  /** 이 위젯의 위치를 담는 스토어 경로. */
  path: string;
  icon: string;
  key: string;
  label: string;
  /** 위젯마다 고를 수 있는 값이 다르다(환경설정 버튼만 다섯). */
  options: SelectOption[];
  position: string;
  tip?: string;
}

interface Props {
  items: WidgetItem[];
  order: string[];
}

const props = defineProps<Props>();

const emit = defineEmits<{
  'update:order': [value: string[]];
  'update:position': [path: string, value: string];
}>();

const byKey = computed(
  () => new Map(props.items.map((item) => [item.key, item])),
);

/** 헤더·사용자 메뉴에 나오는 것 — 순서가 뜻을 가진다. */
const visible = computed(() =>
  props.order
    .map((key) => byKey.value.get(key))
    .filter((item): item is WidgetItem => Boolean(item))
    .filter((item) => item.position !== 'none'),
);

/** 감춘 것 — 순서와 무관하다. */
const hidden = computed(() =>
  props.items.filter((item) => item.position === 'none'),
);

/**
 * 한 칸 옮긴다.
 *
 * `order` 에는 감춘 위젯도 함께 들어 있다. 보이는 것만 추려 자리를 바꾼 뒤
 * 감춘 것을 뒤에 붙인다 — 감춘 것이 사이에 끼어 있으면 화살표를 한 번 눌러도
 * 자리가 안 바뀐 것처럼 보인다.
 */
function move(key: string, delta: number) {
  const keys = visible.value.map((item) => item.key);
  const from = keys.indexOf(key);
  const to = from + delta;
  if (from === -1 || to < 0 || to >= keys.length) return;

  const next = [...keys];
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved as string);

  emit('update:order', [...next, ...hidden.value.map((item) => item.key)]);
}
</script>

<template>
  <div class="flex flex-col gap-4">
    <ul class="divide-y rounded-md border">
      <li
        v-for="(item, index) in visible"
        :key="item.key"
        class="flex items-center gap-3 px-3 py-2"
      >
        <span class="text-muted-foreground w-5 text-center text-xs">
          {{ index + 1 }}
        </span>
        <IconifyIcon :icon="item.icon" class="text-muted-foreground size-4" />
        <span class="flex min-w-0 flex-1 items-center gap-1 text-sm">
          {{ item.label }}
          <Tooltip v-if="item.tip" :title="item.tip">
            <IconifyIcon icon="lucide:circle-help" class="size-3 cursor-help" />
          </Tooltip>
        </span>

        <span class="flex items-center gap-0.5">
          <button
            type="button"
            class="hover:bg-accent text-muted-foreground hover:text-foreground rounded p-1 disabled:opacity-30 disabled:hover:bg-transparent"
            :disabled="index === 0"
            title="위로"
            @click="move(item.key, -1)"
          >
            <IconifyIcon icon="lucide:chevron-up" class="size-4" />
          </button>
          <button
            type="button"
            class="hover:bg-accent text-muted-foreground hover:text-foreground rounded p-1 disabled:opacity-30 disabled:hover:bg-transparent"
            :disabled="index === visible.length - 1"
            title="아래로"
            @click="move(item.key, 1)"
          >
            <IconifyIcon icon="lucide:chevron-down" class="size-4" />
          </button>
        </span>

        <Select
          class="w-40"
          :options="item.options"
          :value="item.position"
          @change="(v) => emit('update:position', item.path, String(v))"
        />
      </li>

      <li
        v-if="visible.length === 0"
        class="text-muted-foreground px-3 py-3 text-sm"
      >
        헤더에 표시할 위젯이 없습니다.
      </li>
    </ul>

    <div v-if="hidden.length > 0">
      <p class="text-muted-foreground mb-1.5 text-xs">
        {{ $t('common.notShow') }} — 다시 쓰려면 위치를 바꾼다.
      </p>
      <ul class="divide-y rounded-md border border-dashed">
        <li
          v-for="item in hidden"
          :key="item.key"
          class="flex items-center gap-3 px-3 py-2"
        >
          <IconifyIcon
            :icon="item.icon"
            class="text-muted-foreground/60 size-4"
          />
          <span class="text-muted-foreground flex-1 text-sm">
            {{ item.label }}
          </span>
          <Select
            class="w-40"
            :options="item.options"
            :value="item.position"
            @change="(v) => emit('update:position', item.path, String(v))"
          />
        </li>
      </ul>
    </div>
  </div>
</template>
