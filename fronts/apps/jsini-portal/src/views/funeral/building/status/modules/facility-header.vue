<script lang="ts" setup>
/**
 * 시설 머리글 — 밀도 셋이 함께 쓴다.
 *
 * 예전 건물 머리글은 8타일 배너였는데, 장비 유형 다섯을 **0 이어도 늘 그렸다.**
 * 호실당 영정 DID 하나만 두는 흔한 배치에서는 넷이 "0대" 였고, 시설이 다섯이면
 * 그런 타일이 스무 개가 됐다. 여기서는 **행동할 수 있는 것만** 한 줄로 적는다 —
 * 사용/공실, 오늘 발인·입관, 오프라인.
 */
import type { FacilityGroup } from '../composables/use-status-data';

import { IconifyIcon } from '@vben/icons';

defineProps<{
  facility: FacilityGroup;
  /** 회사가 둘 이상 걸렸을 때만 회사명을 앞에 붙인다. */
  showCompany?: boolean;
  /** 접기 화살표를 보일지 (운영·감시 밀도) */
  collapsible?: boolean;
  collapsed?: boolean;
  /** 시설 하나로 좁혀 들어가는 진입점을 보일지 */
  drillable?: boolean;
}>();

const emit = defineEmits<{
  (e: 'toggle'): void;
  (e: 'drill'): void;
}>();
</script>

<template>
  <div class="flex flex-wrap items-center gap-x-3 gap-y-1 py-1">
    <div
      class="flex min-w-0 items-center gap-1.5"
      :class="collapsible ? 'cursor-pointer select-none hover:opacity-80' : ''"
      @click="collapsible && emit('toggle')"
    >
      <IconifyIcon
        v-if="collapsible"
        :icon="collapsed ? 'lucide:chevron-right' : 'lucide:chevron-down'"
        class="size-4 shrink-0 text-muted-foreground"
      />
      <IconifyIcon icon="mdi:office-building" class="size-4 shrink-0 text-primary" />
      <span v-if="showCompany" class="shrink-0 text-sm text-muted-foreground">
        {{ facility.companyName || '회사 미지정' }} ·
      </span>
      <span class="truncate text-base font-bold text-foreground">{{ facility.name }}</span>
    </div>

    <!-- 숫자 줄. 0 인 항목은 그리지 않는다. -->
    <div class="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
      <span class="text-foreground">
        <span class="font-bold text-emerald-600">{{ facility.summary.using }}</span>
        <span class="text-muted-foreground">/{{ facility.summary.total }} 사용</span>
      </span>
      <span v-if="facility.summary.empty > 0" class="text-muted-foreground">
        공실 {{ facility.summary.empty }}
      </span>
      <span v-if="facility.summary.dischargeToday > 0" class="text-amber-600">
        오늘 발인 {{ facility.summary.dischargeToday }}
      </span>
      <span v-if="facility.summary.coffinToday > 0" class="text-muted-foreground">
        오늘 입관 {{ facility.summary.coffinToday }}
      </span>
      <span
        v-if="facility.summary.deviceOffline > 0"
        class="flex items-center gap-1 font-bold text-red-500"
      >
        <IconifyIcon icon="lucide:wifi-off" class="size-3.5" />
        {{ facility.summary.deviceOffline }}대 오프라인
      </span>
    </div>

    <button
      v-if="drillable"
      type="button"
      class="ml-auto flex shrink-0 items-center gap-0.5 text-sm text-primary hover:underline"
      @click="emit('drill')"
    >
      이 시설만 보기
      <IconifyIcon icon="lucide:chevron-right" class="size-3" />
    </button>
  </div>
</template>
