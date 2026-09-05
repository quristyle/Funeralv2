<script lang="ts" setup>
/**
 * 압축 호실 타일 — 감시 밀도(2~4 시설)의 칸.
 *
 * 영정 사진도 미디어 드롭다운도 없다. 시설 넷을 한 화면에 놓으려면 호실 하나가
 * 64px 를 넘으면 안 되고, 그 안에 들어가는 것은 **한눈에 훑을 것뿐**이다 —
 * 호실번호 · 고인명 · 발인까지 남은 시간 · 장비 점 하나.
 *
 * 조작은 여기서 하지 않는다. 누르면 옆의 상세 패널에 `RoomColumn` 이 열리고
 * 거기서 한다 — 액션 진입점을 한 벌로 두기 위해서다 (47번 문서 4.3).
 */
import type { Dayjs } from 'dayjs';

import type { RoomState, RoomStatusRow } from '../composables/use-status-data';

import { computed } from 'vue';

import { IconifyIcon } from '@vben/icons';

import dayjs from 'dayjs';

const props = defineProps<{
  room: RoomStatusRow;
  state: RoomState;
  now: Dayjs;
  selected?: boolean;
}>();

defineEmits<{ (e: 'select'): void }>();

const accentClass = computed(
  () =>
    ({
      offline: 'border-l-red-500',
      soon: 'border-l-amber-500',
      using: 'border-l-primary',
      empty: 'border-l-border',
    })[props.state],
);

/** 발인까지 — 하루 넘게 남으면 D-n 으로 줄인다. */
const remain = computed(() => {
  const t = props.room.deceased?.dischargeTime;
  if (!t) return null;
  const target = dayjs(t);
  const minutes = target.diff(props.now, 'minute');
  if (minutes < 0) return '시각 지남';
  if (minutes < 60) return `${minutes}분`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}시간`;
  return `D-${target.startOf('day').diff(props.now.startOf('day'), 'day')}`;
});

/**
 * 장비 상태 한 줄.
 *
 * 타일에 점 하나만 찍었더니 "몇 대 중 몇 대가 살아 있나" 를 알 수 없었다.
 * 감시 밀도는 장비를 보러 오는 화면이므로 수와 상태를 같이 적는다.
 */
const deviceLabel = computed(() => {
  const { deviceCount, onlineDeviceCount } = props.room;
  if (deviceCount === 0) return null;
  return {
    text: `${onlineDeviceCount}/${deviceCount}`,
    bad: onlineDeviceCount < deviceCount,
  };
});
</script>

<template>
  <button
    type="button"
    class="flex w-full flex-col gap-0.5 border border-l-[3px] border-border/70 bg-card px-1.5 py-1 text-left transition-colors hover:border-primary/60"
    :class="[accentClass, selected ? 'ring-1 ring-primary' : '']"
    @click="$emit('select')"
  >
    <div class="flex items-center gap-1">
      <span class="min-w-0 flex-1 truncate text-base font-bold text-foreground">
        {{ room.shortName ?? room.name }}
      </span>
      <!-- 장비: 살아 있는 수 / 전체. 하나라도 죽어 있으면 빨갛게. -->
      <span
        v-if="deviceLabel"
        class="flex shrink-0 items-center gap-0.5 text-sm font-medium tabular-nums"
        :class="deviceLabel.bad ? 'text-red-500' : 'text-emerald-600'"
      >
        <IconifyIcon
          :icon="deviceLabel.bad ? 'lucide:wifi-off' : 'lucide:monitor'"
          class="size-3.5"
        />
        {{ deviceLabel.text }}
      </span>
      <span v-else class="shrink-0 text-sm text-muted-foreground/50">장비 없음</span>
    </div>

    <template v-if="room.deceased">
      <span class="truncate text-sm text-foreground/90">故 {{ room.deceased.name }}</span>
      <span
        v-if="remain"
        class="text-sm"
        :class="state === 'soon' ? 'font-bold text-amber-600' : 'text-muted-foreground'"
      >
        발인 {{ remain }}
      </span>
    </template>
    <span v-else class="text-sm text-muted-foreground/70">공실</span>
  </button>
</template>
