<script lang="ts" setup>
import { Tag, Badge } from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import RoomCard from './room-card.vue';

defineProps<{
  building: {
    id: string;
    name: string;
  };
  rooms: any[];
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

      <!-- 건물 하위 호실 카드 그리드 -->
      <div 
        v-if="rooms.length > 0"
        class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6"
      >
        <RoomCard 
          v-for="room in rooms" 
          :key="room.id" 
          :room="room" 
        />
      </div>

      <div v-else class="py-12 text-center text-muted-foreground text-sm">
        등록된 호실이 없습니다.
      </div>
    </div>
  </div>
</template>
