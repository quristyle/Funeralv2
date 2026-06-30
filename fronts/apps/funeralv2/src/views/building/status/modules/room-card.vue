<script lang="ts" setup>
import { Tag, Badge, Dropdown, Menu } from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';

const props = defineProps<{
  room: {
    id: string;
    name: string;
    shortName?: string;
    deceased?: any;
    devices: any[];
  };
  videos?: any[];
  musics?: any[];
}>();

const emit = defineEmits<{
  (e: 'update-media', payload: { deviceId: string; type: 'video' | 'music'; mediaId: string }): void;
}>();

const deviceTypeMap: Record<string, { label: string; color: string }> = {
  FUNERAL_PORTRAIT: { label: '영정', color: 'purple' },
  MULTIMEDIA: { label: '미디어', color: 'orange' },
  ROOM_GUIDE: { label: '호실안내', color: 'blue' },
  ENTRANCE_GUIDE: { label: '입구안내', color: 'green' },
  KIOSK: { label: '키오스크', color: 'cyan' },
};
</script>

<template>
  <div 
    class="bg-card rounded-xl border border-border/80 p-5 shadow-sm hover:shadow-md hover:border-primary/50 transition-all duration-300 flex flex-col justify-between"
  >
    <div>
      <!-- 호실 카드 헤더 -->
      <div class="flex items-center justify-between mb-4">
        <span class="font-bold text-base text-foreground leading-none">{{ room.name }}</span>
        <Tag v-if="room.shortName" color="blue" class="m-0 select-none text-xs font-semibold">
          {{ room.shortName }}
        </Tag>
      </div>

      <!-- 고인 정보 표출부 (입실중) -->
      <div v-if="room.deceased" class="flex gap-3 mb-4 p-3 bg-primary/5 rounded-lg border border-primary/10">
        <!-- 영정 썸네일 -->
        <div class="w-[60px] h-[75px] bg-muted rounded border border-border flex items-center justify-center overflow-hidden shrink-0">
          <img 
            v-if="room.deceased.memorialEditedPhotoFileId || room.deceased.memorialEditedPhotoUrl || room.deceased.memorialPhotoFileId || room.deceased.memorialPhotoUrl"
            :src="room.deceased.memorialEditedPhotoFileId 
              ? `/api/file/thumbnail/${room.deceased.memorialEditedPhotoFileId}` 
              : (room.deceased.memorialEditedPhotoUrl 
                ? room.deceased.memorialEditedPhotoUrl 
                : (room.deceased.memorialPhotoFileId 
                  ? `/api/file/thumbnail/${room.deceased.memorialPhotoFileId}` 
                  : room.deceased.memorialPhotoUrl))"
            class="w-full h-full object-cover"
            alt="영정"
          />
          <IconifyIcon v-else icon="mdi:account" class="size-8 text-muted-foreground/40" />
        </div>

        <!-- 고인 인적 사항 -->
        <div class="flex flex-col justify-between py-0.5">
          <div>
            <div class="font-bold text-base text-foreground">고 {{ room.deceased.name }}</div>
            <div class="text-xs text-muted-foreground mt-0.5">
              {{ room.deceased.gender === 'M' ? '남성' : '여성' }} / {{ room.deceased.age }}세
            </div>
          </div>
          <div>
            <Tag v-if="room.deceased.status === 'IN_HOSPITAL'" color="processing" class="m-0 text-[10px] py-0 px-1.5">장례 진행중</Tag>
            <Tag v-else-if="room.deceased.status === 'DISCHARGED'" color="warning" class="m-0 text-[10px] py-0 px-1.5">발인 완료</Tag>
            <Tag v-else color="success" class="m-0 text-[10px] py-0 px-1.5">정산 완료</Tag>
          </div>
        </div>
      </div>

      <!-- 공실 상태 표출부 -->
      <div v-else class="flex flex-col items-center justify-center py-6 mb-4 bg-muted/20 border border-dashed border-border rounded-lg text-muted-foreground text-xs gap-1.5">
        <IconifyIcon icon="mdi:door-closed" class="size-6 text-muted-foreground/35" />
        <span>배정 고인 없음 (공실)</span>
      </div>
    </div>

    <!-- 할당 서비스 장비 칩 나열 구역 -->
    <div class="border-t border-border/60 pt-3 mt-auto">
      <div class="text-[11px] font-semibold text-muted-foreground mb-2 flex items-center gap-1">
        <IconifyIcon icon="mdi:monitor-cellphone" class="size-3.5 text-muted-foreground/75" />
        <span>서비스 제공 장비 ({{ room.devices.length }})</span>
      </div>
      <div v-if="room.devices.length > 0" class="space-y-2 mt-1">
        <div 
          v-for="device in room.devices" 
          :key="device.id"
          class="flex flex-col gap-1.5 bg-muted/30 p-2 rounded-lg border border-border/60 text-xs"
        >
          <!-- 장비명 & 타입 뱃지 & 온라인 상태 -->
          <div class="flex items-center justify-between">
            <span class="font-bold text-foreground truncate max-w-[120px]">{{ device.name }}</span>
            <div class="flex items-center gap-1">
              <Tag 
                :color="deviceTypeMap[device.deviceType]?.color || 'default'"
                class="m-0 text-[9px] py-0 px-1 font-semibold select-none scale-90"
              >
                {{ deviceTypeMap[device.deviceType]?.label || device.deviceType }}
              </Tag>
              <Badge :status="device.status === 'ONLINE' ? 'success' : 'error'" class="scale-75" />
            </div>
          </div>
          
          <!-- 미디어 드롭다운 선택 영역 -->
          <div class="flex gap-1.5 text-[9px] text-muted-foreground select-none border-t border-border/40 pt-1.5">
            <Dropdown :trigger="['click']">
              <span class="hover:text-primary cursor-pointer border border-border/80 bg-card px-1.5 py-0.5 rounded flex items-center gap-1 max-w-[100px] truncate">
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
              <span class="hover:text-primary cursor-pointer border border-border/80 bg-card px-1.5 py-0.5 rounded flex items-center gap-1 max-w-[100px] truncate">
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
      <span v-else class="text-[10px] text-muted-foreground/60 italic">연결된 IoT 장비가 없습니다.</span>
    </div>
  </div>
</template>

<style scoped>
/* 카드 마우스 호버 효과 미세 조율 */
.bg-card {
  transition: transform 0.2s ease, border-color 0.2s ease, box-shadow 0.2s ease;
}
.bg-card:hover {
  transform: translateY(-2px);
}
</style>
