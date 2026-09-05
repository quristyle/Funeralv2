<script lang="ts" setup>
/**
 * 밀도 3 「상황판」 — 시설 하나가 한 줄이다.
 *
 * 호실은 색 있는 사각형으로만 남는다. 다섯 곳 × 일곱 호실이 세로 130px 안에
 * 들어가고, 스무 곳이어도 스크롤 없이 담긴다. 이름·고인·발인은 툴팁에 있다.
 *
 * 이 밀도에서 눈으로 훑을 것은 **색이 튀는 칸뿐**이다. 손댈 것이 보이면
 * 시설명을 눌러 그 시설로 내려간다.
 */
import type { Dayjs } from 'dayjs';

import type { FacilityGroup, RoomState, RoomStatusRow } from '../composables/use-status-data';

import { IconifyIcon } from '@vben/icons';

import { Tooltip } from 'ant-design-vue';
import dayjs from 'dayjs';

const props = defineProps<{
  facility: FacilityGroup;
  now: Dayjs;
  showCompany?: boolean;
  roomState: (room: RoomStatusRow) => RoomState;
}>();

const emit = defineEmits<{ (e: 'drill'): void }>();

const cellClass: Record<RoomState, string> = {
  offline: 'bg-red-500/70 border-red-500',
  soon: 'bg-amber-400/70 border-amber-500',
  using: 'bg-primary/60 border-primary/70',
  empty: 'border-dashed border-border bg-transparent',
};

function tip(room: RoomStatusRow) {
  const head = `${room.shortName ?? room.name}`;
  if (!room.deceased) {
    return `${head} · 공실`;
  }
  const parts = [`${head} · 故 ${room.deceased.name}`];
  if (room.deceased.dischargeTime) {
    parts.push(`발인 ${dayjs(room.deceased.dischargeTime).format('MM-DD HH:mm')}`);
  }
  const offline = room.deviceCount - room.onlineDeviceCount;
  if (offline > 0) parts.push(`장비 ${offline}대 오프라인`);
  return parts.join(' · ');
}
</script>

<template>
  <!-- 좁은 화면에서는 시설명 줄과 사각형 줄을 위아래로 나눈다 — 이름에 w-40 을
       고정한 채 두면 휴대폰에서 사각형이 들어갈 자리가 없다. -->
  <div
    class="flex flex-col gap-0.5 border-b border-border/40 py-1 last:border-b-0 sm:flex-row sm:items-center sm:gap-2"
  >
    <div class="flex items-center justify-between gap-2 sm:contents">
      <button
        type="button"
        class="flex min-w-0 shrink-0 flex-col items-start truncate text-left hover:underline sm:w-40"
        :title="`${facility.companyName} ${facility.name}`"
        @click="emit('drill')"
      >
        <span class="w-full truncate text-sm font-bold text-foreground">{{ facility.name }}</span>
        <span v-if="showCompany" class="w-full truncate text-sm text-muted-foreground">
          {{ facility.companyName || '회사 미지정' }}
        </span>
      </button>

      <!-- 숫자는 모바일에서 이름 줄 오른쪽에, 넓은 화면에서는 맨 끝에 붙는다 -->
      <div
        class="flex shrink-0 items-center justify-end gap-2 text-sm sm:order-last sm:w-32"
      >
        <span class="text-muted-foreground">
          {{ facility.summary.using }}/{{ facility.summary.total }}
        </span>
        <span
          v-if="facility.summary.deviceOffline > 0"
          class="flex items-center gap-0.5 font-bold text-red-500"
        >
          <IconifyIcon icon="lucide:wifi-off" class="size-3" />
          {{ facility.summary.deviceOffline }}
        </span>
        <span v-if="facility.summary.dischargeSoon > 0" class="font-bold text-amber-600">
          임박 {{ facility.summary.dischargeSoon }}
        </span>
      </div>
    </div>

    <div class="flex min-w-0 flex-1 flex-wrap gap-1">
      <Tooltip v-for="room in facility.rooms" :key="room.id" :title="tip(room)">
        <div
          class="h-5 w-7 shrink-0 rounded-sm border"
          :class="cellClass[props.roomState(room)]"
        />
      </Tooltip>
    </div>
  </div>
</template>
