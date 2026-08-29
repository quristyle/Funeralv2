<script lang="ts" setup>
import { onMounted, ref, watch } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { Button } from 'ant-design-vue';
import dayjs from 'dayjs';

import { getMidTermForecast } from '#/api/life/weather';

import { iconFromSkyText } from './weather-shared';

/**
 * [주간 예보 띠] — 원본 WeatherWeeklyForecast.vue 이식.
 *
 * 오늘~10일 예보(getMidTermForecast)를 가로 스크롤 카드 띠로 보여 준다.
 * 원본의 1시간 자동 갱신 타이머는 이식하지 않았다 (새로고침 버튼으로 대체).
 */

const props = defineProps<{
  locationId: null | number;
}>();

const loading = ref(false);
const weeklyData = ref<any[]>([]);

async function fetchData() {
  if (!props.locationId) return;
  loading.value = true;
  try {
    weeklyData.value = (await getMidTermForecast(props.locationId)) ?? [];
  } catch {
    weeklyData.value = [];
  } finally {
    loading.value = false;
  }
}

/** 요일 표시와 색 (일요일 빨강 · 토요일 파랑) */
function dayInfo(dateStr: string) {
  const d = dayjs(dateStr);
  const names = ['일', '월', '화', '수', '목', '금', '토'];
  const dow = d.day();
  let cls = '';
  if (dow === 0) cls = '!text-red-500';
  else if (dow === 6) cls = '!text-blue-500';
  return { text: names[dow], class: cls };
}

watch(() => props.locationId, fetchData);
onMounted(fetchData);
defineExpose({ reload: fetchData });
</script>

<template>
  <div class="relative">
    <div class="absolute right-1 top-0 z-10">
      <Button :loading="loading" size="small" type="text" @click="fetchData">
        <IconifyIcon class="size-4" icon="lucide:rotate-cw" />
      </Button>
    </div>

    <div
      v-if="weeklyData.length === 0"
      class="text-muted-foreground flex h-24 items-center justify-center text-sm"
    >
      주간 예보 데이터가 없습니다.
    </div>

    <div v-else class="flex gap-2 overflow-x-auto p-2">
      <div
        v-for="day in weeklyData"
        :key="day.date"
        class="bg-card border-border flex w-32 flex-shrink-0 flex-col items-center rounded-lg border px-3 py-3"
      >
        <span class="text-muted-foreground mb-1 text-xs font-semibold">
          {{ dayjs(day.date).format('MM-DD') }}
        </span>
        <span :class="['mb-3 text-sm font-bold', dayInfo(day.date).class]">
          {{ day.dayDisplay }} ({{ dayInfo(day.date).text }})
        </span>

        <div class="mb-2 flex w-full justify-center gap-3">
          <!-- 오전 -->
          <div v-if="day.amSky" class="flex flex-col items-center">
            <span class="text-muted-foreground mb-0.5 text-[10px]">오전</span>
            <IconifyIcon :icon="iconFromSkyText(day.amSky)" class="text-foreground/70 size-6" />
            <span class="mt-0.5 text-[10px] font-medium text-blue-500">{{ day.amPop }}%</span>
          </div>
          <!-- 오후 (하루 단위 예보면 이 칸만) -->
          <div class="flex flex-col items-center">
            <span class="text-muted-foreground mb-0.5 text-[10px]">
              {{ day.amSky ? '오후' : '하루종일' }}
            </span>
            <IconifyIcon :icon="iconFromSkyText(day.pmSky)" class="text-foreground/70 size-6" />
            <span class="mt-0.5 text-[10px] font-medium text-blue-500">{{ day.pmPop }}%</span>
          </div>
        </div>

        <div class="mt-auto flex items-center gap-2 text-sm">
          <span class="font-bold text-blue-600">{{ day.minTemp }}°</span>
          <span class="text-muted-foreground/50">/</span>
          <span class="font-bold text-red-600">{{ day.maxTemp }}°</span>
        </div>
      </div>
    </div>
  </div>
</template>
