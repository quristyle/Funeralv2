<script lang="ts" setup>
/**
 * 호실 세로 컬럼.
 *
 * **한 컴포넌트가 두 자리에 쓰인다** — 운영 밀도에서는 보드의 칸이고,
 * 감시·상황판 밀도에서는 타일을 눌렀을 때 열리는 상세 패널이다. 그래야
 * ⋮ 메뉴(모든 관리 액션의 단일 진입점, 47번 문서 4.3)가 한 벌로 남는다.
 *
 * 가로 카드에서 세로 컬럼으로 돌린 이유는 데스크탑의 남는 가로폭 때문이다.
 * 빈소 6~7개는 1920px 한 줄에 들어가고(칸당 230px 남짓), 그러면 세로가 넉넉해져
 * 예전에 10px 글씨로 눌려 있던 입관·발인·장지가 잘리지 않고 다 들어간다.
 */
import type { Dayjs } from 'dayjs';

import type { RoomState, RoomStatusRow } from '../composables/use-status-data';

import { computed } from 'vue';
import { useRouter } from 'vue-router';

import { IconifyIcon } from '@vben/icons';

import { Button, Dropdown, Menu, Modal, Tag, Tooltip, message } from 'ant-design-vue';
import dayjs from 'dayjs';

import { departDeceased } from '#/api/funeral/building';

import DeviceRow from './device-row.vue';

const props = defineProps<{
  room: RoomStatusRow;
  state: RoomState;
  now: Dayjs;
  videos?: any[];
  musics?: any[];
  /** 상세 패널로 쓸 때는 높이를 늘리지 않고 채운다. */
  variant?: 'column' | 'panel';
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

const router = useRouter();


/** 상태별 좌측 띠. 급한 것이 이긴다 — 판정은 데이터 레이어 한 곳에서 한다. */
const accentClass = computed(
  () =>
    ({
      offline: 'border-l-red-500',
      soon: 'border-l-amber-500',
      using: 'border-l-primary',
      empty: 'border-l-border',
    })[props.state],
);

const deceasedStatusMap: Record<string, { color: string; label: string }> = {
  FUNERAL_IN_PROGRESS: { color: 'processing', label: '장례 진행중' },
  FUNERAL_DEPARTURE_COMPLETED: { color: 'warning', label: '출상 완료' },
  COMPLETED: { color: 'default', label: '장례 종료' },
};


function fmtDateTime(value?: string) {
  return value ? dayjs(value).format('MM-DD HH:mm') : '';
}

/**
 * 발인까지 남은 것.
 *
 * 옛 화면에도 지금 화면에도 없던 칸이다. 운영자가 하루에 몇 번씩 세던 것을
 * 화면이 대신 센다.
 */
const dischargeCountdown = computed(() => {
  const t = props.room.deceased?.dischargeTime;
  if (!t) return null;
  const target = dayjs(t);
  const minutes = target.diff(props.now, 'minute');
  if (minutes < 0) return { text: '발인 시각 지남', urgent: false };
  if (minutes < 60) return { text: `${minutes}분 남음`, urgent: true };
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return { text: `${hours}시간 ${minutes % 60}분 남음`, urgent: hours < 3 };
  return { text: `D-${target.startOf('day').diff(props.now.startOf('day'), 'day')}`, urgent: false };
});

/** 장비가 한둘이면 한 줄로 접는다 — 호실당 1대가 일반적인 배치다. */
const compactDevices = computed(() => props.room.devices.length <= 2);

const offlineCount = computed(
  () => props.room.deviceCount - props.room.onlineDeviceCount,
);



function handleDepart() {
  const deceased = props.room.deceased;
  if (!deceased) return;

  Modal.confirm({
    title: '출상 처리 확인',
    content: `고인 [고 ${deceased.name}] 님의 출상(장례 종료 및 배정 해제) 처리를 진행하시겠습니까?`,
    okText: '진행',
    cancelText: '취소',
    okButtonProps: { danger: true },
    onOk: async () => {
      try {
        // 상태 전환과 배정 해제만 하는 전용 API 다 — 전체 PUT 재구성은
        // 목록에 없는 칸(비고 등)을 지우는 문제가 있었다 (47번 문서 0단계).
        await departDeceased(deceased.id);
        message.success('출상 처리가 정상적으로 완료되었습니다.');
        emit('refresh');
      } catch (err) {
        console.error('출상 처리 실패:', err);
        message.error('출상 처리 중 오류가 발생했습니다.');
      }
    },
  });
}
</script>

<template>
  <div
    class="flex flex-col gap-1.5 border border-l-[3px] border-border/80 bg-card p-2 transition-colors hover:border-primary/50"
    :class="[accentClass, variant === 'panel' ? 'h-full' : '']"
  >
    <!-- 머리 — 호실명과 ⋮ 메뉴 -->
    <div class="flex items-center justify-between gap-1">
      <span class="truncate text-base font-bold text-foreground">
        {{ room.shortName ?? room.name }}
      </span>
      <div class="flex shrink-0 items-center gap-0.5">
        <Tooltip v-if="offlineCount > 0" :title="`장비 ${offlineCount}대 오프라인`">
          <IconifyIcon icon="lucide:wifi-off" class="size-3.5 text-red-500" />
        </Tooltip>
        <Dropdown :trigger="['click']" placement="bottomRight">
          <Button type="text" size="small" class="flex size-5 items-center justify-center p-0">
            <IconifyIcon icon="lucide:more-vertical" class="size-4 text-muted-foreground" />
          </Button>
          <template #overlay>
            <Menu>
              <template v-if="room.deceased">
                <Menu.Item key="edit" @click="emit('edit-deceased', room.deceased.id)">
                  고인 정보 관리 (사진 · 상주)
                </Menu.Item>
                <Menu.Item
                  key="move"
                  @click="emit('move-room', {
                    deceasedId: room.deceased.id,
                    deceasedName: room.deceased.name ?? '',
                    roomId: room.id,
                    buildingId: room.buildingId,
                  })"
                >
                  호실 변경
                </Menu.Item>
                <Menu.SubMenu v-if="room.devices.length > 0" key="bulkVideo" title="호실 영상 일괄 변경">
                  <Menu.Item
                    key="bulk-video-clear"
                    @click="emit('bulk-media', { roomId: room.id, type: 'video', mediaId: '' })"
                  >
                    미사용 (해제)
                  </Menu.Item>
                  <Menu.Divider />
                  <Menu.Item
                    v-for="v in videos"
                    :key="`bulk-video-${v.value}`"
                    @click="emit('bulk-media', { roomId: room.id, type: 'video', mediaId: v.value })"
                  >
                    {{ v.label }}
                  </Menu.Item>
                </Menu.SubMenu>
                <Menu.SubMenu v-if="room.devices.length > 0" key="bulkMusic" title="호실 음원 일괄 변경">
                  <Menu.Item
                    key="bulk-music-clear"
                    @click="emit('bulk-media', { roomId: room.id, type: 'music', mediaId: '' })"
                  >
                    미사용 (해제)
                  </Menu.Item>
                  <Menu.Divider />
                  <Menu.Item
                    v-for="m in musics"
                    :key="`bulk-music-${m.value}`"
                    @click="emit('bulk-media', { roomId: room.id, type: 'music', mediaId: m.value })"
                  >
                    {{ m.label }}
                  </Menu.Item>
                </Menu.SubMenu>
                <Menu.Divider />
                <Menu.Item key="depart" danger @click="handleDepart">출상 처리</Menu.Item>
              </template>
              <template v-else>
                <Menu.Item key="create" @click="emit('create-deceased', room.id)">
                  고인 등록 (이 호실로)
                </Menu.Item>
                <Menu.Item
                  v-if="room.lastDepartedDeceasedId"
                  key="cancelDepart"
                  @click="emit('cancel-departure', {
                    deceasedId: room.lastDepartedDeceasedId,
                    deceasedName: room.lastDepartedDeceasedName ?? '',
                  })"
                >
                  출상 취소 (故 {{ room.lastDepartedDeceasedName }})
                </Menu.Item>
              </template>
              <Menu.Divider />
              <Menu.Item key="goDeceased" @click="router.push('/building/deceased')">
                고인 관리 화면으로
              </Menu.Item>
              <Menu.Item key="goRoom" @click="router.push('/building/room')">
                호실 관리 화면으로
              </Menu.Item>
            </Menu>
          </template>
        </Dropdown>
      </div>
    </div>

    <!-- 사용 중 -->
    <template v-if="room.deceased">
      <!-- 영정 — 칸 폭에 맞춰 큰다. 예전 60×75 는 데스크에서 알아보기 어려웠다. -->
      <div class="flex aspect-[4/5] w-full items-center justify-center overflow-hidden rounded border border-border bg-muted">
        <img
          v-if="room.deceased.photoFileId || room.deceased.photoUrl"
          :src="room.deceased.photoFileId
            ? `/api/file/thumbnail/${room.deceased.photoFileId}`
            : room.deceased.photoUrl"
          class="size-full object-cover"
          alt="영정"
        />
        <IconifyIcon v-else icon="mdi:account" class="size-10 text-muted-foreground/40" />
      </div>

      <div class="min-w-0">
        <div class="truncate text-base font-bold text-foreground">
          故 {{ room.deceased.name }}
        </div>
        <div class="text-sm text-muted-foreground">
          {{ room.deceased.gender === 'M' || room.deceased.gender === 'MALE' ? '남' : '여' }}
          · {{ room.deceased.age }}세
        </div>
      </div>

      <div class="space-y-0.5 text-sm leading-snug text-muted-foreground">
        <div v-if="room.deceased.coffinTime">입관 {{ fmtDateTime(room.deceased.coffinTime) }}</div>
        <div v-if="room.deceased.dischargeTime">발인 {{ fmtDateTime(room.deceased.dischargeTime) }}</div>
        <div
          v-if="dischargeCountdown"
          class="font-bold"
          :class="dischargeCountdown.urgent ? 'text-amber-600' : 'text-foreground/70'"
        >
          {{ dischargeCountdown.text }}
        </div>
        <div v-if="room.deceased.burialPlace" class="truncate">
          장지 {{ room.deceased.burialPlace }}
        </div>
        <div v-if="room.deceased.chiefMourner" class="truncate">
          상주 {{ room.deceased.chiefMourner }}
        </div>
      </div>

      <!-- 상태 태그와 출상 버튼은 분리해 둔다 — 예전에는 상태 코드 어긋남 탓에
           else 가지의 빨간 태그가 '우연히' 출상 진입점이었다 (47번 문서 0단계). -->
      <div class="flex items-center gap-1">
        <Tag
          :color="deceasedStatusMap[room.deceased.status ?? '']?.color ?? 'processing'"
          class="m-0 px-2 py-0.5 text-sm"
        >
          {{ deceasedStatusMap[room.deceased.status ?? '']?.label ?? '장례 진행중' }}
        </Tag>
        <Button danger size="small" class="h-[18px] px-1.5 text-sm leading-none" @click="handleDepart">
          출상
        </Button>
      </div>
    </template>

    <!-- 공실 -->
    <div
      v-else
      class="flex flex-1 flex-col items-center justify-center gap-1.5 rounded border border-dashed border-border bg-muted/20 py-4 text-sm text-muted-foreground"
    >
      <IconifyIcon icon="mdi:door-closed" class="size-6 text-muted-foreground/35" />
      <span>공실</span>
      <span v-if="room.lastVacatedAt" class="text-sm text-muted-foreground/70">
        퇴실 {{ fmtDateTime(room.lastVacatedAt) }}
      </span>
      <Button size="small" type="dashed" class="text-sm" @click="emit('create-deceased', room.id)">
        고인 등록
      </Button>
    </div>

    <!-- 장비 — 한둘이면 줄로, 셋 이상이면 칸으로 감싼다.
         줄 자체는 공용 장비 카드와 같은 부품을 쓴다 (`DeviceRow`). -->
    <div v-if="room.devices.length > 0" class="mt-auto space-y-1 border-t border-border/40 pt-1.5">
      <DeviceRow
        v-for="device in room.devices"
        :key="device.id"
        :device="device"
        :videos="videos"
        :musics="musics"
        :boxed="!compactDevices"
        @update-media="(p) => emit('update-media', p)"
        @show-detail="(id) => emit('show-detail', id)"
      />
    </div>
  </div>
</template>
