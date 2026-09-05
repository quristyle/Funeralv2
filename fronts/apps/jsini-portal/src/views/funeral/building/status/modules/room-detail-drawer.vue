<script lang="ts" setup>
/**
 * 호실 상세 서랍 — **모바일에서만** 쓴다.
 *
 * 감시 밀도의 상세는 데스크탑에서 오른쪽 260px 패널이다. 그 폭을 모바일에
 * 그대로 두면 타일 격자가 절반으로 눌려 관제가 안 된다. 그래서 좁은 화면에서는
 * 같은 `RoomColumn` 을 서랍에 담아 아래에서 올린다.
 *
 * `useVbenDrawer` 를 쓰는 이유는 **뒤로가기로 닫히기 때문**이다
 * (`use-back-close.ts`, 2026-09-04 지시). 안드로이드에서 뒤로가기가 페이지를
 * 물러 버리면 관제 화면이 통째로 날아간다.
 */
import type { Dayjs } from 'dayjs';

import type { RoomState, RoomStatusRow } from '../composables/use-status-data';

import { computed, ref } from 'vue';

import { useVbenDrawer } from '@vben/common-ui';

import RoomColumn from './room-column.vue';

const props = defineProps<{
  now: Dayjs;
  videos: any[];
  musics: any[];
  /** 목록이 갱신돼도 열려 있는 서랍이 최신 값을 보도록 id 로 되짚는다. */
  rooms: RoomStatusRow[];
  roomState: (room: RoomStatusRow) => RoomState;
}>();

const emit = defineEmits<{
  (e: 'update-media', payload: { deviceId: string; type: 'music' | 'video'; mediaId: string }): void;
  (e: 'show-detail', deviceId: string): void;
  (e: 'refresh'): void;
  (e: 'edit-deceased', deceasedId: string): void;
  (e: 'create-deceased', roomId: string): void;
  (e: 'move-room', payload: { deceasedId: string; deceasedName: string; roomId: string; buildingId?: string }): void;
  (e: 'cancel-departure', payload: { deceasedId: string; deceasedName: string }): void;
  (e: 'bulk-media', payload: { roomId: string; type: 'music' | 'video'; mediaId: string }): void;
}>();

const roomId = ref<string>('');
const room = computed(() => props.rooms.find((r) => r.id === roomId.value));

const [Drawer, drawerApi] = useVbenDrawer({
  placement: 'bottom',
  footer: false,
  // **반드시 켜 둔다.** 모바일에서 서랍은 `<Sheet :modal="isMobile">` 이라
  // reka 가 `body { pointer-events: none }` 을 건다. 이 옵션이 꺼져 있으면
  // 내용이 `force-mount` 로 남아 닫혀도 reka 의 해제 코드가 돌지 않고,
  // **화면 전체가 눌리지 않는 상태로 굳는다** (2026-09-05 실측).
  // 저장소의 다른 서랍이 모두 이 옵션을 켜 둔 덕에 여태 드러나지 않았다.
  destroyOnClose: true,
  onClosed: () => {
    roomId.value = '';
  },
});

function open(id: string) {
  roomId.value = id;
  const target = props.rooms.find((r) => r.id === id);
  // 제목은 **시설명**이다. 호실명은 아래 컬럼 머리에 이미 있고, 타일 격자에서
  // 올라온 터라 "어느 시설의 호실인지" 가 오히려 사라지는 정보다.
  drawerApi.setState({ title: target?.buildingName ?? '호실 상세' });
  drawerApi.open();
}

function close() {
  drawerApi.close();
}

defineExpose({ open, close });
</script>

<template>
  <Drawer class="h-[85%]" content-class="p-2">
    <RoomColumn
      v-if="room"
      :room="room"
      :state="roomState(room)"
      :now="now"
      :videos="videos"
      :musics="musics"
      variant="panel"
      @update-media="(p) => emit('update-media', p)"
      @show-detail="(id) => emit('show-detail', id)"
      @refresh="emit('refresh')"
      @edit-deceased="(id) => emit('edit-deceased', id)"
      @create-deceased="(id) => emit('create-deceased', id)"
      @move-room="(p) => emit('move-room', p)"
      @cancel-departure="(p) => emit('cancel-departure', p)"
      @bulk-media="(p) => emit('bulk-media', p)"
    />
  </Drawer>
</template>
