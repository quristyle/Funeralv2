<script lang="ts" setup>
/**
 * 밀도 1 「운영」 — 시설 한 곳을 세로 컬럼 보드로 그린다.
 *
 * 층 머리글은 **층이 둘 이상일 때만** 그린다. 건물 하나 · 층 하나가 대부분인데
 * 예전에는 조건 없이 그려서 세로 28px 을 늘 먹었다 (전광판 화면은 이미 같은
 * 판정을 하고 있었다 — `status/simple`).
 */
import type { Dayjs } from 'dayjs';

import type { FacilityGroup, RoomState, RoomStatusRow } from '../composables/use-status-data';

import { computed, ref } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { useIsMobile } from '@vben/hooks';

import { useBalancedColumns } from '../composables/use-balanced-columns';
import DeviceColumn from './device-column.vue';
import FacilityHeader from './facility-header.vue';
import RoomColumn from './room-column.vue';

const props = defineProps<{
  facility: FacilityGroup;
  now: Dayjs;
  videos: any[];
  musics: any[];
  showCompany?: boolean;
  collapsed?: boolean;
  collapsible?: boolean;
  roomState: (room: RoomStatusRow) => RoomState;
}>();

const emit = defineEmits<{
  (e: 'toggle'): void;
  (e: 'update-media', payload: { deviceId: string; type: 'music' | 'video'; mediaId: string }): void;
  (e: 'show-detail', deviceId: string): void;
  (e: 'refresh'): void;
  (e: 'edit-deceased', deceasedId: string): void;
  (e: 'create-deceased', roomId: string): void;
  (e: 'move-room', payload: { deceasedId: string; deceasedName: string; roomId: string; buildingId?: string }): void;
  (e: 'cancel-departure', payload: { deceasedId: string; deceasedName: string }): void;
  (e: 'bulk-media', payload: { roomId: string; type: 'music' | 'video'; mediaId: string }): void;
}>();

// 휴대폰에서는 칸 최소폭을 줄여 한 줄에 둘은 들어가게 한다. 205px 를 그대로 두면
// 375px 화면에서 한 줄에 하나가 되어 일곱 호실이 세로로 끝없이 늘어선다.
const { isMobile } = useIsMobile();

const gridRef = ref<HTMLElement | null>(null);
// 공용 장비도 같은 격자의 칸이므로 수를 함께 센다 — 안 그러면 칸 나누기가 어긋난다.
const cardCount = computed(
  () => props.facility.rooms.length + props.facility.commonDevices.length,
);
const { gridStyle } = useBalancedColumns(gridRef, cardCount, {
  min: computed(() => (isMobile.value ? 150 : 205)),
  gap: 8,
});

/** 층이 하나뿐이면 층 머리글을 만들지 않는다. */
const showFloorHeaders = computed(() => props.facility.floors.length > 1);

/**
 * 공용 장비를 어느 자리에 그릴지.
 *
 * `commonDevices` 는 '호실이 없는' 장비 전부라 **층에 붙은 것과 건물에 붙은 것이
 * 섞여 있다.** 층을 나눠 그리는 시설에서는 층에 붙은 장비를 그 층 격자에 넣고,
 * 건물에 붙은 것만 맨 아래로 보낸다.
 */
function devicesOfFloor(floorId: string) {
  return props.facility.commonDevices.filter((d) => d.floorId === floorId);
}

const buildingScopedDevices = computed(() =>
  props.facility.commonDevices.filter((d) => !d.floorId),
);

/** 카드 오른쪽 위에 붙는 꼬리표 — 어디에 매인 장비인지. */
function scopeLabelOf(device: any) {
  if (!device.floorId) return '건물 공용';
  const floor = props.facility.floors.find((f) => f.floorId === device.floorId);
  return floor ? `${floor.floorName} 공용` : '층 공용';
}
</script>

<template>
  <section class="rounded-lg border border-border bg-card/40 p-2">
    <FacilityHeader
      :facility="facility"
      :show-company="showCompany"
      :collapsible="collapsible"
      :collapsed="collapsed"
      @toggle="emit('toggle')"
    />

    <div v-if="!collapsed" class="mt-1 space-y-2">
      <!-- 층이 여럿일 때만 층으로 나눈다 -->
      <template v-if="showFloorHeaders">
        <div v-for="floor in facility.floors" :key="floor.floorId">
          <div class="flex select-none items-center gap-1.5 pb-1">
            <IconifyIcon icon="lucide:layers" class="size-3.5 text-primary/80" />
            <span class="text-sm font-bold text-foreground">{{ floor.floorName }}</span>
            <span class="text-sm text-muted-foreground">
              {{ floor.rooms.length }}개 호실
              <template v-if="devicesOfFloor(floor.floorId).length > 0">
                · 공용 장비 {{ devicesOfFloor(floor.floorId).length }}대
              </template>
            </span>
          </div>
          <div
            class="grid grid-cols-[repeat(auto-fit,minmax(150px,1fr))] gap-2 sm:grid-cols-[repeat(auto-fit,minmax(205px,1fr))]"
          >
            <RoomColumn
              v-for="room in floor.rooms"
              :key="room.id"
              :room="room"
              :state="roomState(room)"
              :now="now"
              :videos="videos"
              :musics="musics"
              @update-media="(p) => emit('update-media', p)"
              @show-detail="(id) => emit('show-detail', id)"
              @refresh="emit('refresh')"
              @edit-deceased="(id) => emit('edit-deceased', id)"
              @create-deceased="(id) => emit('create-deceased', id)"
              @move-room="(p) => emit('move-room', p)"
              @cancel-departure="(p) => emit('cancel-departure', p)"
              @bulk-media="(p) => emit('bulk-media', p)"
            />
            <!-- 그 층에 붙은 공용 장비는 그 층 격자에 함께 둔다 -->
            <DeviceColumn
              v-for="device in devicesOfFloor(floor.floorId)"
              :key="device.id"
              :device="device"
              :videos="videos"
              :musics="musics"
              :scope-label="`${floor.floorName} 공용`"
              @update-media="(p) => emit('update-media', p)"
              @show-detail="(id) => emit('show-detail', id)"
            />
          </div>
        </div>

        <!-- 건물에 붙은 공용 장비 — 층에 매이지 않아 맨 아래에 둔다 -->
        <div v-if="buildingScopedDevices.length > 0">
          <div class="flex select-none items-center gap-1.5 pb-1">
            <IconifyIcon icon="mdi:office-building-cog" class="size-4 text-primary/80" />
            <span class="text-sm font-bold text-foreground">건물 공용 장비</span>
            <span class="text-sm text-muted-foreground">
              {{ buildingScopedDevices.length }}대
            </span>
          </div>
          <div
            class="grid grid-cols-[repeat(auto-fit,minmax(150px,1fr))] gap-2 sm:grid-cols-[repeat(auto-fit,minmax(205px,1fr))]"
          >
            <DeviceColumn
              v-for="device in buildingScopedDevices"
              :key="device.id"
              :device="device"
              :videos="videos"
              :musics="musics"
              scope-label="건물 공용"
              @update-media="(p) => emit('update-media', p)"
              @show-detail="(id) => emit('show-detail', id)"
            />
          </div>
        </div>
      </template>

      <!-- 층이 하나면 호실을 바로 늘어놓는다. 칸 수는 호실 수에 맞춰 고른다.
           호실에 매이지 않은 장비도 같은 격자에 카드로 이어 붙인다. -->
      <div v-else ref="gridRef" :style="gridStyle">
        <RoomColumn
          v-for="room in facility.rooms"
          :key="room.id"
          :room="room"
          :state="roomState(room)"
          :now="now"
          :videos="videos"
          :musics="musics"
          @update-media="(p) => emit('update-media', p)"
          @show-detail="(id) => emit('show-detail', id)"
          @refresh="emit('refresh')"
          @edit-deceased="(id) => emit('edit-deceased', id)"
          @create-deceased="(id) => emit('create-deceased', id)"
          @move-room="(p) => emit('move-room', p)"
          @cancel-departure="(p) => emit('cancel-departure', p)"
          @bulk-media="(p) => emit('bulk-media', p)"
        />
        <DeviceColumn
          v-for="device in facility.commonDevices"
          :key="device.id"
          :device="device"
          :videos="videos"
          :musics="musics"
          :scope-label="scopeLabelOf(device)"
          @update-media="(p) => emit('update-media', p)"
          @show-detail="(id) => emit('show-detail', id)"
        />
      </div>

    </div>
  </section>
</template>
