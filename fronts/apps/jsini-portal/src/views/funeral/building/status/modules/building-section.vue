<script lang="ts" setup>
import { computed } from 'vue';
import { Tag, Badge, Dropdown, Menu, Modal, Tooltip, message } from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import RoomCard from './room-card.vue';
import { restartDeviceApp, setDeviceScreenPower } from '#/api/funeral/building';
import { useJsiniUser } from '#/composables/use-jsini-user';

// 장비 제어(전원·재시작)는 관리자 역할만 (47번 문서 D-RS4). 서버도 같은 검사를 한다.
const { roles } = useJsiniUser();
const canControlDevices = computed(() =>
  roles.value.some((r) =>
    ['ADMINISTRATOR', 'SYSTEM_ADMINISTRATOR', 'PARTNER_ADMINISTRATOR'].includes(r),
  ),
);

/** 원격 화면 전원 — 즉시 실행 명령. 장비가 온라인일 때만 전달된다. */
async function handleScreenPower(device: any, state: 'OFF' | 'ON') {
  try {
    await setDeviceScreenPower(device.code, state);
    message.success(`[${device.name}] 화면 ${state === 'ON' ? '켜기' : '끄기'} 명령을 전송했습니다.`);
  } catch (err: any) {
    message.error(err?.response?.data?.message || err?.message || '명령 전송에 실패했습니다. 장비가 오프라인일 수 있습니다.');
  }
}

/** 플레이어 앱 재시작 — 확인 후 전송. */
function handleAppRestart(device: any) {
  Modal.confirm({
    title: '앱 재시작 확인',
    content: `[${device.name}] 장비의 플레이어 앱을 재시작하시겠습니까? 재기동까지 수 초간 화면이 꺼집니다.`,
    okText: '재시작',
    cancelText: '취소',
    okButtonProps: { danger: true },
    onOk: async () => {
      try {
        await restartDeviceApp(device.code);
        message.success(`[${device.name}] 앱 재시작 명령을 전송했습니다.`);
      } catch (err: any) {
        message.error(err?.response?.data?.message || err?.message || '명령 전송에 실패했습니다. 장비가 오프라인일 수 있습니다.');
      }
    },
  });
}

const props = defineProps<{
  building: {
    id: string;
    name: string;
  };
  rooms: any[];
  devices: any[];
  videos: any[];
  musics: any[];
  collapsed: boolean;
  summary: {
    total: number;
    active: number;
    empty: number;
    deviceSummary: Record<string, number>;
  };
}>();

const emit = defineEmits<{
  (e: 'toggle'): void;
  (e: 'update-media', payload: { deviceId: string; type: 'video' | 'music'; mediaId: string }): void;
  (e: 'show-detail', deviceId: string): void;
  (e: 'refresh'): void;
  // ── 카드 관리 메뉴 패스스루 (47번 문서 2단계) ──
  (e: 'edit-deceased', deceasedId: string): void;
  (e: 'create-deceased', roomId: string): void;
  (e: 'move-room', payload: { deceasedId: string; deceasedName: string; roomId: string; buildingId?: string }): void;
  (e: 'cancel-departure', payload: { deceasedId: string; deceasedName: string }): void;
  (e: 'bulk-media', payload: { roomId: string; type: 'video' | 'music'; mediaId: string }): void;
}>();

interface FloorGroup {
  floorId: string;
  floorName: string;
  rooms: any[];
}

// 층별 호실 그룹화 및 순서 유지
const groupedFloors = computed(() => {
  const groupsMap = new Map<string, FloorGroup>();
  
  for (const room of props.rooms) {
    const floorId = room.floorId || 'unknown';
    const floorName = room.floorName || '기타';
    
    if (!groupsMap.has(floorId)) {
      groupsMap.set(floorId, {
        floorId,
        floorName,
        rooms: [],
      });
    }
    groupsMap.get(floorId)!.rooms.push(room);
  }
  
  return Array.from(groupsMap.values());
});

// 건물의 모든 층 공용 장비 필터링
function getBuildingFloorCommonDevices() {
  if (!Array.isArray(props.devices)) return [];
  return props.devices.filter(
    (d) => d.buildingId === props.building.id && !d.roomId
  );
}


// 층 공용 장비 필터링
function getFloorCommonDevices(floorId: string) {
  if (!Array.isArray(props.devices)) return [];
  return props.devices.filter(
    (d) => d.buildingId === props.building.id && d.floorId === floorId && !d.roomId
  );
}

const deviceTypeMap: Record<string, { label: string; color: string }> = {
  FUNERAL_PORTRAIT: { label: '영정', color: 'purple' },
  MULTIMEDIA: { label: '미디어', color: 'orange' },
  ROOM_GUIDE: { label: '호실안내', color: 'blue' },
  ENTRANCE_GUIDE: { label: '입구안내', color: 'green' },
  KIOSK: { label: '키오스크', color: 'cyan' },
};
</script>

<template>
  <div class="bg-card/40 border border-border p-4 rounded-xl shadow-sm">
    <!-- 건물 헤더 타이틀 (아코디언 토글 클릭 영역) — 좁은 화면에서는 배너가 아래로 내려온다 -->
    <div
      class="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between border-border pb-3 cursor-pointer select-none hover:opacity-85 transition-opacity"
      @click="emit('toggle')"
    >
      <div class="flex items-center gap-2">
        <IconifyIcon
          :icon="collapsed ? 'lucide:chevron-right' : 'lucide:chevron-down'"
          class="size-5 text-muted-foreground transition-transform"
        />
        <IconifyIcon icon="mdi:office-building" class="size-6 text-primary" />
        <h2 class="text-xl font-bold text-foreground">{{ building.name }}</h2>
      </div>

      <!-- 건물 요약 통계 배너 -->
      <div class="grid grid-cols-4 lg:grid-cols-8 gap-2 lg:gap-4 bg-muted/30 p-3 lg:p-4 rounded-xl text-xs border border-border/40 select-none text-center">
        <div class="flex flex-col gap-1">
          <span class="text-muted-foreground font-medium">전체 호실</span>
          <span class="text-base font-bold text-foreground">{{ summary.total }}개</span>
        </div>
        <div class="flex flex-col gap-1">
          <span class="text-emerald-600 font-medium">사용 중 (배정)</span>
          <span class="text-base font-bold text-emerald-600">{{ summary.active }}개</span>
        </div>
        <div class="flex flex-col gap-1">
          <span class="text-amber-600 font-medium">공실 (미배정)</span>
          <span class="text-base font-bold text-amber-600 ">{{ summary.empty }}개</span>
        </div>
        <div
          v-for="(count, type) in summary.deviceSummary"
          :key="type"
          class="flex flex-col gap-1 lg:border-l lg:border-border lg:pl-4"
        >
          <span class="text-muted-foreground font-medium flex items-center gap-1">
            <Badge :color="deviceTypeMap[type]?.color || 'default'" class="scale-75" />
            {{ deviceTypeMap[type]?.label || type }}
          </span>
          <span class="text-base font-bold text-foreground">{{ count }}대</span>
        </div>
      </div>


    </div>

    <!-- 아코디언 본문 영역 (펼침 상태일 때만 노출) -->
    <div v-if="!collapsed" class="space-y-5">



      <!-- 건물 하위 층별 섹션 및 호실 카드 배치 -->
      <div v-if="groupedFloors.length > 0" class="space-y-4">

















        <!-- 건물 공용 장비 목록 (배정 장비가 존재할 때만 표시) -->
      <div 
        v-if="getBuildingFloorCommonDevices().length > 0"
        class="flex flex-col gap-3"
      >
        <div class="flex items-center gap-2 select-none">
          <IconifyIcon icon="mdi:office-building-cog" class="size-5 text-primary" />
          <span class="text-sm font-bold text-foreground">건물 공용 서비스 장비</span>
          <span class="text-xs text-muted-foreground">({{ getBuildingFloorCommonDevices().length }}대)</span>
        </div>

        <div class="border border-primary/20 bg-primary/5 rounded-xl p-4 flex flex-wrap gap-4">



          <div             v-for="device in getBuildingFloorCommonDevices()"
            :key="device.id"
            class="flex flex-col gap-3 bg-card p-4 rounded-xl border border-border shadow-sm text-xs max-w-[320px]"
          >
            <!-- 상단 장비명 & 상태 -->
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2 min-w-0">
                <IconifyIcon icon="mdi:monitor-dashboard" class="size-5 text-primary/70 shrink-0" />
                <div class="flex flex-col min-w-0">
                  <span class="font-bold text-foreground leading-tight truncate flex items-center gap-1">
                    {{ device.name }}
                    <Tooltip :title="device.code">
                      <IconifyIcon 
                        icon="lucide:info" 
                        class="size-3.5 text-muted-foreground/60 hover:text-primary cursor-pointer shrink-0 transition-colors"
                        @click="emit('show-detail', device.id)"
                      />
                    </Tooltip>
                  </span>
                  <span class="text-[10px] text-muted-foreground/50">{{ device.code }}</span>
                </div>
              </div>
              <div class="flex items-center gap-1">
                <Badge :status="device.status === 'ONLINE' ? 'success' : 'error'" class="scale-75" />
                <!-- 장비 제어 — 관리자 역할만 노출 (D-RS4) -->
                <Dropdown v-if="canControlDevices" :trigger="['click']">
                  <IconifyIcon
                    icon="lucide:power"
                    class="size-4 text-muted-foreground/60 hover:text-primary cursor-pointer transition-colors"
                  />
                  <template #overlay>
                    <Menu>
                      <Menu.Item key="on" @click="handleScreenPower(device, 'ON')">화면 켜기</Menu.Item>
                      <Menu.Item key="off" @click="handleScreenPower(device, 'OFF')">화면 끄기</Menu.Item>
                      <Menu.Divider />
                      <Menu.Item key="restart" danger @click="handleAppRestart(device)">앱 재시작</Menu.Item>
                    </Menu>
                  </template>
                </Dropdown>
              </div>
            </div>

            <!-- 드롭다운 영역 -->
            <div class="flex gap-2 text-[10px] text-muted-foreground select-none border-t border-border/40 pt-2.5">
              <Dropdown :trigger="['click']">
                <span class="hover:text-primary cursor-pointer border border-border/80 bg-muted/15 px-2 py-1 rounded flex items-center gap-1">
                  🎬 {{ device.videoName || '영상 없음' }}
                  <IconifyIcon icon="lucide:chevron-down" class="size-3" />
                </span>
                <template #overlay>
                  <Menu @click="({ key }) => emit('update-media', { deviceId: device.id, type: 'video', mediaId: key as string })">
                    <Menu.Item key="">🚫 미사용 (해제)</Menu.Item>
                    <Menu.Divider />
                    <Menu.Item v-for="v in videos" :key="v.value">
                      {{ v.label }}
                    </Menu.Item>
                  </Menu>
                </template>
              </Dropdown>
              <Dropdown :trigger="['click']">
                <span class="hover:text-primary cursor-pointer border border-border/80 bg-muted/15 px-2 py-1 rounded flex items-center gap-1">
                  🎵 {{ device.musicName || '음원 없음' }}
                  <IconifyIcon icon="lucide:chevron-down" class="size-3" />
                </span>
                <template #overlay>
                  <Menu @click="({ key }) => emit('update-media', { deviceId: device.id, type: 'music', mediaId: key as string })">
                    <Menu.Item key="">🚫 미사용 (해제)</Menu.Item>
                    <Menu.Divider />
                    <Menu.Item v-for="m in musics" :key="m.value">
                      {{ m.label }}
                    </Menu.Item>
                  </Menu>
                </template>
              </Dropdown>
            </div>

          </div>




        </div>
      </div>























        

          <div
            v-for="floorGroup in groupedFloors"
            :key="floorGroup.floorId"
            class="w-full"
          >


            <!-- 층 헤더 -->
            <div class="flex items-center gap-2 border-border/30 pb-2 select-none">
              <IconifyIcon icon="lucide:layers" class="size-4.5 text-primary/80" />
              <h3 class="text-sm font-bold text-foreground">{{ floorGroup.floorName }}</h3>
              <span class="text-xs text-muted-foreground font-medium">({{ floorGroup.rooms.length }}개 호실)</span>
            </div>

            <!-- 해당 층의 호실 카드 그리드 — 폭에 따라 1~5열. 고정 flex 행이
                 좁은 화면에서 477px 넘치던 것(16번 문서 R2)을 없앤다. -->
            <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4 min-[2200px]:grid-cols-5 gap-2">

              <!-- 호실 카드 리스트 -->
              <RoomCard
                v-for="room in floorGroup.rooms"
                :key="room.id"
                :room="room"
                :videos="videos"
                :musics="musics"
                @update-media="(payload) => emit('update-media', payload)"
                @show-detail="(id) => emit('show-detail', id)"
                @refresh="() => emit('refresh')"
                @edit-deceased="(id) => emit('edit-deceased', id)"
                @create-deceased="(roomId) => emit('create-deceased', roomId)"
                @move-room="(payload) => emit('move-room', payload)"
                @cancel-departure="(payload) => emit('cancel-departure', payload)"
                @bulk-media="(payload) => emit('bulk-media', payload)"
              />
            </div>

          </div>

        
      </div>

      <div v-else class="py-12 text-center text-muted-foreground text-sm">
        등록된 호실이 없습니다.
      </div>








    </div>
  </div>
</template>
