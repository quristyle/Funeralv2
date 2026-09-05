<script lang="ts" setup>
/**
 * 공용 장비 타일 — 감시 밀도에서 호실 타일 옆에 선다.
 *
 * 시설 머리글의 '오프라인 N대' 는 호실 장비와 공용 장비를 함께 센다. 여기에
 * 공용 장비가 없으면 **그 숫자를 화면에서 설명할 수 없다** — 여덟이라는데
 * 타일에는 여섯 몫만 있는 식이 된다. 그래서 같은 격자에 함께 둔다.
 */
import { computed } from 'vue';

import { IconifyIcon } from '@vben/icons';

const props = defineProps<{
  device: any;
  scopeLabel?: string;
}>();

defineEmits<{ (e: 'select'): void }>();

const online = computed(() => props.device.status === 'ONLINE');
</script>

<template>
  <button
    type="button"
    class="flex w-full flex-col gap-0.5 border border-l-[3px] border-dashed border-border/70 bg-card px-1.5 py-1 text-left transition-colors hover:border-primary/60"
    :class="online ? 'border-l-emerald-500' : 'border-l-red-500'"
    @click="$emit('select')"
  >
    <div class="flex items-center gap-1">
      <span class="min-w-0 flex-1 truncate text-base font-bold text-foreground">
        {{ device.shortName || device.name }}
      </span>
      <IconifyIcon
        :icon="online ? 'lucide:monitor' : 'lucide:wifi-off'"
        class="size-3.5 shrink-0"
        :class="online ? 'text-emerald-600' : 'text-red-500'"
      />
    </div>
    <span class="truncate text-sm text-muted-foreground">{{ scopeLabel ?? '건물 공용' }}</span>
    <span class="text-sm" :class="online ? 'text-muted-foreground' : 'font-bold text-red-500'">
      {{ online ? '온라인' : '오프라인' }}
    </span>
  </button>
</template>
