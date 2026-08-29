<script lang="ts" setup>
import type { LifeWeatherApi } from '#/api/life/weather';

import { computed, onUnmounted, ref, watch } from 'vue';

import { IconifyIcon } from '@vben/icons';

import dayjs from 'dayjs';

import { getCurrentEvents, getCurrentWeather } from '#/api/life/weather';

import { cardThemeOf, conditionIconOf, getPtyName } from './weather-shared';

/**
 * [지역 실황 위젯] — 원본 SingleWeatherWidget.vue 이식.
 *
 * 부모(dashboard)가 getLatestWeather 로 받은 실황 한 건을 data 로 내려 주고,
 * 위젯은 자기 지역의 기준 초과 이벤트(getCurrentEvents)만 스스로 가져온다.
 * 이벤트가 여러 건이면 3초 간격으로 돌려 가며 보여 준다 (원본 동작).
 * 원본의 지도 팝업(카카오 지도)과 5분 자동 갱신은 이식하지 않았다.
 */

const props = defineProps<{
  data: LifeWeatherApi.Info;
  selected?: boolean;
}>();

const emit = defineEmits<{ (e: 'select'): void }>();

const weather = ref<LifeWeatherApi.Info>(props.data);
const events = ref<LifeWeatherApi.EventRecord[]>([]);
const eventIndex = ref(0);
const loading = ref(false);
let cycleTimer: null | ReturnType<typeof setInterval> = null;

const currentEvent = computed(() =>
  events.value.length > 0 ? events.value[eventIndex.value] : null,
);

const tempDiff = computed(() => {
  const w = weather.value;
  if (w.yesterdayTemperature === undefined || w.yesterdayTemperature === null) {
    return null;
  }
  return Math.round((w.temperatureC - w.yesterdayTemperature) * 10) / 10;
});

/** 이벤트 분류(카테고리) → 아이콘 */
function eventIcon(category?: null | string): string {
  switch (category?.toUpperCase()) {
    case 'COLD':
    case 'SNOW': {
      return 'lucide:snowflake';
    }
    case 'HEAT': {
      return 'lucide:thermometer-sun';
    }
    case 'RAIN': {
      return 'lucide:cloud-rain';
    }
    case 'WIND': {
      return 'lucide:wind';
    }
    default: {
      return 'lucide:alert-triangle';
    }
  }
}

/** 이벤트 분류 → 알림 카드 색 */
function eventStyle(category?: null | string): string {
  switch (category?.toUpperCase()) {
    case 'COLD': {
      return 'from-indigo-500/10 to-purple-600/40 border-indigo-300/50';
    }
    case 'HEAT': {
      return 'from-orange-500/10 to-red-600/40 border-orange-300/50';
    }
    case 'RAIN': {
      return 'from-blue-600/10 to-indigo-700/40 border-blue-400/50';
    }
    case 'SNOW': {
      return 'from-cyan-500/10 to-blue-500/40 border-cyan-300/50';
    }
    case 'WIND': {
      return 'from-sky-500/10 to-blue-600/40 border-sky-300/50';
    }
    default: {
      return 'from-amber-500/10 to-orange-600/40 border-amber-300/50';
    }
  }
}

async function fetchEvents() {
  const locId = weather.value.weatherLocationId;
  if (!locId) {
    events.value = [];
    return;
  }
  try {
    events.value = (await getCurrentEvents(locId)) ?? [];
  } catch {
    events.value = [];
  }
}

/** 이 지역만 실황 + 이벤트 다시 조회 */
async function reload() {
  const locId = weather.value.weatherLocationId;
  if (!locId) return;
  loading.value = true;
  try {
    const [w] = await Promise.all([getCurrentWeather(locId), fetchEvents()]);
    if (w) weather.value = w;
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.data,
  (v) => {
    weather.value = v;
    fetchEvents();
  },
  { immediate: true },
);

// 이벤트가 여러 건이면 순환
watch(events, (list) => {
  if (cycleTimer) clearInterval(cycleTimer);
  cycleTimer = null;
  eventIndex.value = 0;
  if (list.length > 1) {
    cycleTimer = setInterval(() => {
      eventIndex.value = (eventIndex.value + 1) % list.length;
    }, 3000);
  }
});

onUnmounted(() => {
  if (cycleTimer) clearInterval(cycleTimer);
});

defineExpose({ reload });
</script>

<template>
  <div
    :class="[
      cardThemeOf(weather),
      selected ? 'ring-primary ring-2 ring-offset-2' : '',
    ]"
    class="group relative h-full cursor-pointer overflow-hidden rounded-lg transition-all duration-300 hover:-translate-y-1 hover:shadow-xl"
    @click="emit('select')"
  >
    <!-- 기준 초과 이벤트: 붉은 점멸 테두리 + 순환 알림 카드 -->
    <template v-if="events.length > 0">
      <div
        class="pointer-events-none absolute inset-0 z-30 animate-pulse rounded-lg border border-red-500 shadow-[inset_0_0_90px_rgba(220,38,38,0.8)]"
      ></div>
      <div
        v-if="currentEvent"
        class="pointer-events-none absolute right-2 top-14 z-40 flex justify-end px-2"
      >
        <div
          :class="eventStyle(currentEvent.weatherStandard?.category)"
          class="flex flex-col items-center gap-2 rounded border bg-gradient-to-r p-2 text-white backdrop-blur-xl"
        >
          <div class="shrink-0 rounded-full bg-white/20 p-2">
            <IconifyIcon
              :icon="eventIcon(currentEvent.weatherStandard?.category)"
              class="size-6 drop-shadow-md"
            />
          </div>
          <div class="flex min-w-0 flex-col text-right">
            <span class="truncate text-base font-black leading-tight drop-shadow-sm">
              {{ currentEvent.weatherStandard?.name }}
            </span>
            <span class="text-sm font-bold opacity-95">
              {{ currentEvent.measuredValue }}
              <span class="text-xs font-normal">
                {{ currentEvent.weatherStandard?.unit }}
              </span>
            </span>
          </div>
        </div>
      </div>
    </template>

    <!-- 로딩 오버레이 -->
    <div
      v-if="loading"
      class="absolute inset-0 z-50 flex items-center justify-center bg-black/20 backdrop-blur-sm"
    >
      <div class="size-8 animate-spin rounded-full border-b-2 border-white"></div>
    </div>

    <!-- 머리: 지역명 · 관측시각 · 새로고침 -->
    <div class="relative z-10 flex items-start justify-between p-4">
      <div>
        <div class="flex items-center gap-1 opacity-90">
          <IconifyIcon class="size-3" icon="lucide:map-pin" />
          <span class="text-[10px] font-semibold uppercase tracking-wider">Location</span>
        </div>
        <h3 class="mt-0.5 text-xl font-bold tracking-tight">{{ weather.location }}</h3>
      </div>
      <div class="flex items-center gap-1 text-right opacity-80">
        <span class="rounded px-2 py-1 text-[10px] font-medium backdrop-blur-sm">
          {{ dayjs(weather.observationTime).format('MM-DD HH:mm') }}
        </span>
        <button
          class="rounded-full p-1 transition-colors hover:bg-white/20"
          title="새로고침"
          type="button"
          @click.stop="reload"
        >
          <IconifyIcon :class="{ 'animate-spin': loading }" class="size-3" icon="lucide:rotate-cw" />
        </button>
      </div>
    </div>

    <!-- 몸통: 기온 · 어제와 차이 · 상태 · 아이콘 -->
    <div class="relative z-10 flex items-center justify-between px-5 pb-3">
      <div class="flex flex-col">
        <div class="flex items-center gap-2">
          <div class="flex items-baseline">
            <span class="text-5xl font-black tracking-tighter drop-shadow-sm">
              {{ weather.temperatureC }}
            </span>
            <span class="ml-1 text-2xl font-bold">°</span>
          </div>
          <div
            v-if="tempDiff !== null"
            class="flex flex-col self-center rounded bg-black/20 px-2 py-1 text-xs font-bold backdrop-blur-sm"
          >
            <div class="flex items-center gap-0.5">
              <span v-if="tempDiff > 0" class="text-red-300">▲</span>
              <span v-else-if="tempDiff < 0" class="text-blue-300">▼</span>
              <span
                :class="{
                  'text-red-300': tempDiff > 0,
                  'text-blue-300': tempDiff < 0,
                  'text-gray-300': tempDiff === 0,
                }"
              >
                {{ Math.abs(tempDiff) }}°
              </span>
            </div>
          </div>
        </div>
        <span class="mt-1 text-base font-medium opacity-90">{{ weather.condition }}</span>
      </div>
      <IconifyIcon :icon="conditionIconOf(weather)" class="size-16 opacity-90 drop-shadow-md" />
    </div>

    <!-- 발: 상세 격자 -->
    <div
      class="relative z-10 grid grid-cols-3 gap-x-1 gap-y-2 border-t border-white/10 bg-black/20 p-3 text-center backdrop-blur-md"
    >
      <div class="flex flex-col items-center gap-0.5">
        <IconifyIcon class="size-4 opacity-70" icon="lucide:droplets" />
        <span class="text-xs font-bold">{{ weather.humidity ?? '-' }}%</span>
        <span class="text-[9px] uppercase tracking-wide opacity-60">습도</span>
      </div>
      <div class="flex flex-col items-center gap-0.5">
        <IconifyIcon
          v-if="weather.windDirection !== undefined && weather.windDirection !== null"
          :style="{ transform: `rotate(${weather.windDirection}deg)` }"
          class="size-4"
          icon="lucide:navigation"
        />
        <IconifyIcon v-else class="size-4 opacity-70" icon="lucide:wind" />
        <span class="text-xs font-bold">
          {{ weather.windSpeed ?? '-' }}<span class="text-[9px] font-normal">m/s</span>
        </span>
        <span class="text-[9px] uppercase tracking-wide opacity-60">풍속</span>
      </div>
      <div class="flex flex-col items-center gap-0.5">
        <IconifyIcon class="size-4 opacity-70" icon="lucide:umbrella" />
        <span class="text-xs font-bold">
          {{ weather.rainfall ?? 0 }}<span class="text-[9px] font-normal">mm</span>
        </span>
        <span class="text-[9px] uppercase tracking-wide opacity-60">강수</span>
      </div>
      <div class="flex flex-col items-center gap-0.5">
        <IconifyIcon class="size-4 opacity-70" icon="lucide:info" />
        <span class="text-xs font-bold">{{ getPtyName(weather.pty) }}</span>
        <span class="text-[9px] uppercase tracking-wide opacity-60">형태</span>
      </div>
      <div class="flex flex-col items-center gap-0.5">
        <IconifyIcon class="size-4 opacity-70" icon="lucide:snowflake" />
        <span class="text-xs font-bold">
          {{ weather.snowfall ?? 0 }}<span class="text-[9px] font-normal">cm</span>
        </span>
        <span class="text-[9px] uppercase tracking-wide opacity-60">적설</span>
      </div>
      <div class="flex flex-col items-center gap-0.5">
        <IconifyIcon class="size-4 opacity-70" icon="lucide:thermometer" />
        <span class="text-xs font-bold">
          {{ weather.sensibleTemp ?? Math.round(weather.temperatureC) }}°C
        </span>
        <span class="text-[9px] uppercase tracking-wide opacity-60">체감</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
svg {
  shape-rendering: geometricprecision;
}
</style>
