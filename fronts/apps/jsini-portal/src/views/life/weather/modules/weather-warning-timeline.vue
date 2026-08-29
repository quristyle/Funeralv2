<script lang="ts" setup>
import { onMounted, ref } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { Button } from 'ant-design-vue';

import { getWarnings4LocationRange } from '#/api/life/weather';

import { parseTmFc } from './weather-shared';

/**
 * [오늘의 기상 특보 띠] — 원본 WeatherWarningTimeline.vue 이식.
 *
 * 오늘 통보문 중 관리지역이 걸린 문장(제목별 최초·최종)을
 * 가로 타임라인으로 보여 준다. '해제' 가 아닌 항목은 붉은 점으로 강조한다.
 */

interface TimelineItem {
  id: string;
  time: string; // 표시용 HH:mm
  sortKey: number;
  title: string;
  command?: string;
  isActive: boolean;
}

const timelineItems = ref<TimelineItem[]>([]);
const loading = ref(false);

async function fetchData() {
  loading.value = true;
  try {
    const res = (await getWarnings4LocationRange()) ?? [];
    timelineItems.value = res
      .map((s: any) => {
        const announce = parseTmFc(s.weatherWarningMsg?.tmFc);
        return {
          command: s.command,
          id: `W-${s.id}`,
          isActive: !s.command?.includes('해제'),
          sortKey: announce?.valueOf() ?? 0,
          time: announce ? announce.format('HH:mm') : '',
          title: s.title || '기상 특보',
        };
      })
      .sort((a, b) => a.sortKey - b.sortKey);
  } catch {
    timelineItems.value = [];
  } finally {
    loading.value = false;
  }
}

/** 특보 종류별 색 (해제는 회색) */
function itemColor(item: TimelineItem): string {
  if (!item.isActive || item.command?.includes('해제')) {
    return 'text-gray-400 bg-gray-500/10 border-gray-300/50';
  }
  const t = item.title;
  if (t.includes('호우')) return 'text-blue-500 bg-blue-500/10 border-blue-300/60';
  if (t.includes('폭염')) return 'text-red-500 bg-red-500/10 border-red-300/60';
  if (t.includes('태풍')) return 'text-purple-500 bg-purple-500/10 border-purple-300/60';
  if (t.includes('강풍') || t.includes('풍랑')) {
    return 'text-teal-500 bg-teal-500/10 border-teal-300/60';
  }
  if (t.includes('한파') || t.includes('대설')) {
    return 'text-cyan-500 bg-cyan-500/10 border-cyan-300/60';
  }
  return 'text-orange-500 bg-orange-500/10 border-orange-300/60';
}

onMounted(fetchData);
defineExpose({ reload: fetchData });
</script>

<template>
  <div class="bg-card border-border rounded-lg border p-4">
    <div class="mb-3 flex items-center justify-between">
      <div class="flex items-center gap-2">
        <IconifyIcon class="size-5 text-red-500" icon="lucide:alert-triangle" />
        <h3 class="text-foreground text-base font-bold">오늘의 기상 특보</h3>
      </div>
      <Button :loading="loading" size="small" type="text" @click="fetchData">
        <IconifyIcon class="size-4" icon="lucide:rotate-cw" />
      </Button>
    </div>

    <div
      v-if="loading && timelineItems.length === 0"
      class="text-muted-foreground flex items-center justify-center py-6 text-sm"
    >
      <span class="animate-pulse">데이터 불러오는 중...</span>
    </div>

    <div
      v-else-if="timelineItems.length === 0"
      class="text-muted-foreground flex flex-col items-center gap-2 py-4"
    >
      <IconifyIcon class="size-8 opacity-50" icon="lucide:cloud-off" />
      <span class="text-sm">오늘 발생한 기상 특보가 없습니다.</span>
    </div>

    <div v-else class="relative overflow-x-auto pb-1">
      <div class="bg-border absolute left-0 right-0 top-2 mx-4 h-0.5 min-w-[max-content]"></div>
      <div class="flex min-w-max gap-6 px-4">
        <div
          v-for="item in timelineItems"
          :key="item.id"
          class="relative flex flex-col items-center text-center"
        >
          <div
            :class="item.isActive ? 'bg-red-500' : 'bg-gray-300'"
            class="border-card z-10 size-4 rounded-full border-4 shadow-sm"
          ></div>
          <div class="text-muted-foreground mb-1 flex items-center gap-1 text-[11px] font-medium">
            <IconifyIcon class="size-3" icon="lucide:clock" />
            {{ item.time }}
          </div>
          <div
            :class="itemColor(item)"
            class="w-full break-keep rounded-md border px-2 py-2 text-xs font-semibold leading-tight shadow-sm"
          >
            <div class="flex flex-col gap-1">
              <span>{{ item.title }}</span>
              <span v-if="item.command" class="text-[10px] font-normal opacity-80">
                {{ item.command }}
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
