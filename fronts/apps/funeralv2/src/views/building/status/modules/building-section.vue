<script lang="ts" setup>
import { computed } from 'vue';
import { Tag, Badge, Dropdown, Menu, Tooltip } from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import RoomCard from './room-card.vue';

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

// 건물 공용 장비 필터링
function getBuildingCommonDevices() {
  if (!Array.isArray(props.devices)) return [];
  return props.devices.filter(
    (d) => d.buildingId === props.building.id && !d.floorId && !d.roomId
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
  <div class="bg-card/40 border border-border p-6 rounded-xl shadow-sm">
    <!-- 건물 헤더 타이틀 (아코디언 토글 클릭 영역) -->
    <div 
      class="flex items-center justify-between mb-4 border-b border-border pb-3 cursor-pointer select-none hover:opacity-85 transition-opacity"
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
      <div class="flex gap-2 text-xs">
        <Tag color="blue" class="m-0 select-none">
          호실 {{ summary.total }}개
        </Tag>
        <Tag color="green" class="m-0 select-none">
          사용중 {{ summary.active }}개
        </Tag>
        <Tag color="orange" class="m-0 select-none">
          공실 {{ summary.empty }}개
        </Tag>
      </div>
    </div>

    <!-- 아코디언 본문 영역 (펼침 상태일 때만 노출) -->
    <div v-if="!collapsed" class="space-y-5">
      <!-- 건물 요약 통계 배너 -->
      <div class="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-8 gap-4 bg-muted/30 p-4 rounded-xl text-xs border border-border/40 select-none">
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
          <span class="text-base font-bold text-amber-600">{{ summary.empty }}개</span>
        </div>
        <div 
          v-for="(count, type) in summary.deviceSummary" 
          :key="type" 
          class="flex flex-col gap-1 border-l border-border pl-4"
        >
          <span class="text-muted-foreground font-medium flex items-center gap-1">
            <Badge :color="deviceTypeMap[type]?.color || 'default'" class="scale-75" />
            {{ deviceTypeMap[type]?.label || type }}
          </span>
          <span class="text-base font-bold text-foreground">{{ count }}대</span>
        </div>
      </div>

      <!-- 건물 공용 장비 목록 (배정 장비가 존재할 때만 표시) -->
      <div 
        v-if="getBuildingCommonDevices().length > 0"
        class="border border-primary/20 bg-primary/5 rounded-xl p-4 flex flex-col gap-3"
      >
        <div class="flex items-center gap-2 select-none">
          <IconifyIcon icon="mdi:office-building-cog" class="size-5 text-primary" />
          <span class="text-sm font-bold text-foreground">건물 공용 서비스 장비</span>
          <span class="text-xs text-muted-foreground">({{ getBuildingCommonDevices().length }}대)</span>
        </div>
        <div class="flex flex-wrap gap-4">
          <div 
            v-for="device in getBuildingCommonDevices()"
            :key="device.id"
            class="flex flex-col gap-3 bg-card p-4 rounded-xl border border-border shadow-sm text-xs min-w-[260px] max-w-[320px]"
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
              <Tag :color="device.status === 'ONLINE' ? 'green' : 'red'" class="m-0 select-none">
                {{ device.status === 'ONLINE' ? '온라인' : '오프라인' }}
              </Tag>
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

      <!-- 건물 하위 층별 섹션 및 호실 카드 배치 -->
      <div v-if="groupedFloors.length > 0" class="space-y-6">
        <div 
          v-for="floorGroup in groupedFloors" 
          :key="floorGroup.floorId"
          class="border border-border/40 bg-muted/5 rounded-xl p-4 shadow-sm"
        >
          <!-- 층 헤더 -->
          <div class="flex items-center gap-2 mb-3 border-b border-border/30 pb-2 select-none">
            <IconifyIcon icon="lucide:layers" class="size-4.5 text-primary/80" />
            <h3 class="text-sm font-bold text-foreground">{{ floorGroup.floorName }}</h3>
            <span class="text-xs text-muted-foreground font-medium">({{ floorGroup.rooms.length }}개 호실)</span>
          </div>
          
          <!-- 해당 층의 호실 카드 그리드 -->
          <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
            <!-- 층 공용 장비 카드 -->
            <div 
              v-if="getFloorCommonDevices(floorGroup.floorId).length > 0"
              class="bg-muted/15 rounded-xl border border-dashed border-primary/40 p-5 shadow-sm flex flex-col justify-between min-h-[180px]"
            >
              <div>
                <div class="flex items-center justify-between mb-3 select-none">
                  <span class="font-bold text-base text-foreground leading-none">층 공용 장비</span>
                  <Tag color="purple" class="m-0 select-none text-xs font-semibold">공용</Tag>
                </div>
                
                <div class="space-y-3 py-1">
                  <div 
                    v-for="device in getFloorCommonDevices(floorGroup.floorId)"
                    :key="device.id"
                    class="flex flex-col gap-2 bg-card p-2.5 rounded-lg border border-border/80 text-xs"
                  >
                    <div class="flex items-center justify-between">
                      <div class="flex flex-col min-w-0">
                        <span class="font-semibold text-foreground leading-tight truncate flex items-center gap-1">
                          {{ device.name }}
                          <Tooltip :title="device.code">
                            <IconifyIcon 
                              icon="lucide:info" 
                              class="size-3.5 text-muted-foreground/60 hover:text-primary cursor-pointer shrink-0 transition-colors"
                              @click="emit('show-detail', device.id)"
                            />
                          </Tooltip>
                        </span>
                        <span class="text-[9px] text-muted-foreground/50 mt-0.5">{{ device.code }}</span>
                      </div>
                      <Badge :status="device.status === 'ONLINE' ? 'success' : 'error'" class="scale-75" />
                    </div>
                    <!-- 드롭다운 -->
                    <div class="flex gap-1.5 text-[9px] text-muted-foreground select-none border-t border-border/40 pt-1.5">
                      <Dropdown :trigger="['click']">
                        <span class="hover:text-primary cursor-pointer border border-border/80 bg-muted/15 px-1.5 py-0.5 rounded flex items-center gap-1 max-w-[100px] truncate">
                          🎬 {{ device.videoName || '영상 없음' }}
                          <IconifyIcon icon="lucide:chevron-down" class="size-2.5" />
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
                        <span class="hover:text-primary cursor-pointer border border-border/80 bg-muted/15 px-1.5 py-0.5 rounded flex items-center gap-1 max-w-[100px] truncate">
                          🎵 {{ device.musicName || '음원 없음' }}
                          <IconifyIcon icon="lucide:chevron-down" class="size-2.5" />
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
              <div class="border-t border-border/60 pt-2.5 mt-auto flex items-center gap-1 select-none text-[10px] text-muted-foreground/75 font-semibold">
                <IconifyIcon icon="mdi:monitor-cellphone" class="size-3.5" />
                <span>공용 장비 ({{ getFloorCommonDevices(floorGroup.floorId).length }}대)</span>
              </div>
            </div>

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
