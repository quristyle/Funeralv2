<script lang="ts" setup>
/**
 * 밀도 2 「감시」 — 시설 하나가 가로 레인 하나다.
 *
 * 예전 구조는 건물마다 아코디언 + 8타일 배너 + 층 머리글 + 카드 격자를 세로로
 * 쌓았다. 시설 넷이면 그것만으로 2,000px 이 넘어 한 화면에 못 담겼다.
 * 여기서는 시설당 머리글 한 줄 + 타일 격자만 남긴다.
 */
import type { Dayjs } from 'dayjs';

import type { FacilityGroup, RoomState, RoomStatusRow } from '../composables/use-status-data';

import { computed, ref } from 'vue';

import { useBalancedColumns } from '../composables/use-balanced-columns';
import DeviceTile from './device-tile.vue';
import FacilityHeader from './facility-header.vue';
import RoomTile from './room-tile.vue';

const props = defineProps<{
  facility: FacilityGroup;
  now: Dayjs;
  showCompany?: boolean;
  collapsed?: boolean;
  selectedRoomId?: string;
  roomState: (room: RoomStatusRow) => RoomState;
}>();

const emit = defineEmits<{
  (e: 'toggle'): void;
  (e: 'drill'): void;
  (e: 'select-room', roomId: string): void;
  (e: 'show-device', deviceId: string): void;
}>();

const gridRef = ref<HTMLElement | null>(null);
// 공용 장비 타일도 같은 격자의 칸이라 함께 센다.
const tileCount = computed(
  () => props.facility.rooms.length + props.facility.commonDevices.length,
);
// 타일에 장비 수(3/3)까지 들어가면서 최소폭을 늘렸다 — 좁으면 호실명이 잘린다.
const { gridStyle } = useBalancedColumns(gridRef, tileCount, { min: 150, gap: 4 });
</script>

<template>
  <section class="rounded-lg border border-border bg-card/40 px-2 py-1">
    <FacilityHeader
      :facility="facility"
      :show-company="showCompany"
      collapsible
      :collapsed="collapsed"
      drillable
      @toggle="emit('toggle')"
      @drill="emit('drill')"
    />

    <div v-if="!collapsed" ref="gridRef" :style="gridStyle" class="pb-1">
      <RoomTile
        v-for="room in facility.rooms"
        :key="room.id"
        :room="room"
        :state="roomState(room)"
        :now="now"
        :selected="room.id === selectedRoomId"
        @select="emit('select-room', room.id)"
      />
      <!-- 호실에 매이지 않은 장비 — 누르면 장비 상세가 열린다 -->
      <DeviceTile
        v-for="device in facility.commonDevices"
        :key="device.id"
        :device="device"
        :scope-label="device.floorId ? '층 공용' : '건물 공용'"
        @select="emit('show-device', device.id)"
      />
    </div>
  </section>
</template>
