<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import type { LifeWeatherApi } from '#/api/life/weather';

import { computed, nextTick, onMounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import { Button, message, Select, Space, Spin } from 'ant-design-vue';
import dayjs from 'dayjs';

import { getForecast, getLocations } from '#/api/life/weather';

import WeatherWeeklyForecast from './modules/weather-weekly-forecast.vue';
import { forecastIconOf, getPtyName } from './modules/weather-shared';

/**
 * [시간대별 예보] — 원본 WeatherForecast.vue 이식.
 *
 * 지역을 고르면 과거 -10h ~ 미래 +10h 타임라인(getForecast)을 가로 카드 띠로,
 * 카테고리별 추이(기온·강수확률·강수량·적설·습도·풍속·동서·남북·강수형태)를
 * 차트 그리드로 보여 준다. 주간 예보(getMidTermForecast)는 부품을 재사용한다.
 *
 * 원본 대비: Swiper → 가로 스크롤 + 좌우 버튼, ApexCharts → EchartsUI.
 */

const loading = ref(false);
const locations = ref<LifeWeatherApi.Location[]>([]);
const selectedLocationId = ref<null | number>(null);
const forecastData = ref<LifeWeatherApi.TimelinePoint[]>([]);
const timelineWrap = ref<HTMLElement>();

const locationOptions = computed(() =>
  locations.value.map((l) => ({ label: l.name, value: l.id })),
);

// ── 시간대 카드 ──────────────────────────────────────────────

function formatDateTime(date: string, time: string) {
  return `${date.slice(4, 6)}/${date.slice(6, 8)} ${time.slice(0, 2)}:${time.slice(2, 4)}`;
}

/** 이 칸이 현재 시각(같은 날짜 · 같은 시)인지 */
function isCurrentHour(item: LifeWeatherApi.TimelinePoint) {
  const now = dayjs();
  return item.date === now.format('YYYYMMDD') && Number(item.time.slice(0, 2)) === now.hour();
}

/** 현재 시각 카드가 보이도록 가로 스크롤 */
function scrollToCurrent() {
  nextTick(() => {
    const el = timelineWrap.value?.querySelector('[data-now="1"]');
    el?.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
  });
}

function scrollTimeline(delta: number) {
  timelineWrap.value?.scrollBy({ behavior: 'smooth', left: delta });
}

// ── 카테고리별 차트 ──────────────────────────────────────────

interface ChartDef {
  id: string;
  title: string;
  color: string;
  kind: 'area' | 'bar' | 'line' | 'step';
  yName: string;
  max?: number;
  value: (d: LifeWeatherApi.TimelinePoint) => number;
}

const chartDefs: ChartDef[] = [
  {
    color: '#F59E0B',
    id: 't1h',
    kind: 'line',
    title: '기온 예보 (T1H)',
    value: (d) => d.temp ?? 0,
    yName: '°C',
  },
  {
    color: '#3B82F6',
    id: 'pop',
    kind: 'bar',
    max: 100,
    title: '강수확률 (POP)',
    value: (d) => d.pop ?? 0,
    yName: '%',
  },
  {
    color: '#0EA5E9',
    id: 'rn1',
    kind: 'bar',
    title: '강수량 예보 (RN1)',
    value: (d) => d.rain ?? 0,
    yName: 'mm',
  },
  {
    color: '#94A3B8',
    id: 'sno',
    kind: 'bar',
    title: '적설량 예보 (SNO)',
    value: (d) => d.sno ?? 0,
    yName: 'cm',
  },
  {
    color: '#06B6D4',
    id: 'reh',
    kind: 'area',
    max: 100,
    title: '습도 예보 (REH)',
    value: (d) => d.reh ?? 0,
    yName: '%',
  },
  {
    color: '#10B981',
    id: 'wsd',
    kind: 'line',
    title: '풍속 예보 (WSD)',
    value: (d) => d.windSpeed ?? 0,
    yName: 'm/s',
  },
  {
    color: '#8B5CF6',
    id: 'uuu',
    kind: 'line',
    title: '동서 바람 (UUU)',
    value: (d) => d.uuu ?? 0,
    yName: 'm/s',
  },
  {
    color: '#EC4899',
    id: 'vvv',
    kind: 'line',
    title: '남북 바람 (VVV)',
    value: (d) => d.vvv ?? 0,
    yName: 'm/s',
  },
  {
    color: '#64748B',
    id: 'pty',
    kind: 'step',
    title: '강수형태 (PTY)',
    value: (d) => Number.parseInt(String(d.pty ?? '0'), 10) || 0,
    yName: 'Code',
  },
];

const chartRefs = chartDefs.map(() => ref<EchartsUIType>());
const chartRenderers = chartDefs.map((_, i) => useEcharts(chartRefs[i] as any));

function setChartRef(idx: number, el: unknown) {
  const r = chartRefs[idx];
  if (r) r.value = el as EchartsUIType;
}

function chartOptions(def: ChartDef): any {
  const rows = forecastData.value;
  const labels = rows.map((d) => formatDateTime(d.date, d.time));
  const nowIdx = rows.findIndex((d) => !d.isPast);
  const series: any = {
    data: rows.map((d) => def.value(d)),
    name: def.title,
    type: def.kind === 'bar' ? 'bar' : 'line',
  };
  if (def.kind === 'line' || def.kind === 'area') series.smooth = true;
  if (def.kind === 'area') series.areaStyle = { opacity: 0.25 };
  if (def.kind === 'step') series.step = 'middle';
  if (nowIdx >= 0) {
    series.markLine = {
      data: [{ xAxis: nowIdx }],
      label: { color: '#f97316', formatter: 'NOW', position: 'insideEndTop' },
      lineStyle: { color: '#f97316', type: 'dashed' },
      silent: true,
      symbol: 'none',
    };
  }
  return {
    color: [def.color],
    grid: { bottom: 26, left: 42, right: 16, top: 30 },
    series: [series],
    tooltip: { trigger: 'axis' },
    xAxis: {
      axisLabel: { fontSize: 10, formatter: (v: string) => v.split(' ')[1] ?? v },
      data: labels,
      type: 'category',
    },
    yAxis: { max: def.max, name: def.yName, type: 'value' },
  };
}

function drawCharts() {
  if (forecastData.value.length === 0) return;
  chartDefs.forEach((def, i) => {
    chartRenderers[i]?.renderEcharts(chartOptions(def));
  });
}

// ── 조회 ─────────────────────────────────────────────────────

async function fetchLocations() {
  try {
    const res = (await getLocations()) ?? [];
    locations.value = res.filter((l) => l.isActive && l.id !== undefined);
    const first = locations.value[0];
    if (first?.id && !selectedLocationId.value) {
      selectedLocationId.value = first.id;
    }
  } catch {
    message.error('지역 목록 로딩 실패');
  }
}

async function fetchForecast() {
  if (!selectedLocationId.value) return;
  loading.value = true;
  try {
    forecastData.value = (await getForecast(selectedLocationId.value)) ?? [];
    scrollToCurrent();
    await nextTick();
    drawCharts();
  } catch {
    forecastData.value = [];
  } finally {
    loading.value = false;
  }
}

watch(selectedLocationId, fetchForecast);
onMounted(fetchLocations);
</script>

<template>
  <Page auto-content-height>
    <div class="flex h-full flex-col gap-3 overflow-hidden">
      <!-- 도구줄 (모바일에서는 줄바꿈으로 잘림을 막는다) -->
      <div
        class="bg-card border-border flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3"
      >
        <Space>
          <span class="text-sm font-semibold">지역 선택</span>
          <Select
            v-model:value="selectedLocationId"
            :options="locationOptions"
            class="max-w-full"
            placeholder="지역 선택"
            style="width: 200px"
          />
        </Space>
        <Button :loading="loading" @click="fetchForecast">
          <IconifyIcon class="mr-1 size-4" icon="lucide:rotate-cw" />
          새로고침
        </Button>
      </div>

      <!-- 본문 (내부 스크롤) -->
      <div class="min-h-0 flex-1 space-y-3 overflow-y-auto pr-1">
        <!-- 시간대 카드 띠 -->
        <div class="bg-card border-border relative rounded-lg border p-3">
          <h3 class="text-foreground mb-2 text-base font-bold">시간대별 예보</h3>
          <Spin :spinning="loading">
            <div class="relative px-8">
              <div ref="timelineWrap" class="flex gap-3 overflow-x-auto pb-3 pt-2">
                <div
                  v-for="(item, idx) in forecastData"
                  :key="idx"
                  :class="[
                    isCurrentHour(item)
                      ? 'z-10 scale-105 border-blue-500 bg-blue-500/10 ring-2 ring-blue-500'
                      : item.isPast
                        ? 'border-border bg-muted/40 opacity-75'
                        : 'border-border bg-card hover:scale-105',
                  ]"
                  :data-now="isCurrentHour(item) ? '1' : undefined"
                  class="relative flex w-[150px] flex-shrink-0 flex-col items-center gap-2 rounded-xl border p-3 shadow-sm transition-transform"
                >
                  <!-- 현재 시각 배지 -->
                  <span v-if="isCurrentHour(item)" class="absolute right-2 top-2 flex size-2">
                    <span
                      class="absolute inline-flex h-full w-full animate-ping rounded-full bg-blue-400 opacity-75"
                    ></span>
                    <span class="relative inline-flex size-2 rounded-full bg-blue-500"></span>
                  </span>
                  <!-- 과거(실측) 배지 -->
                  <span
                    v-else-if="item.isPast"
                    class="text-muted-foreground absolute right-2 top-2"
                  >
                    <IconifyIcon class="size-3" icon="lucide:history" />
                  </span>

                  <span
                    :class="{ 'font-bold text-blue-600': isCurrentHour(item) }"
                    class="text-muted-foreground text-xs font-medium"
                  >
                    {{ formatDateTime(item.date, item.time) }}
                  </span>
                  <IconifyIcon
                    :class="
                      isCurrentHour(item)
                        ? 'text-blue-600'
                        : item.isPast
                          ? 'text-muted-foreground'
                          : 'text-blue-500'
                    "
                    :icon="forecastIconOf(item)"
                    class="size-8"
                  />
                  <span class="text-foreground text-lg font-bold">{{ item.temp }}°</span>

                  <div class="flex min-h-[16px] items-center gap-1 text-xs text-blue-500">
                    <template v-if="(item.pop || 0) > 0">
                      <IconifyIcon class="size-3" icon="lucide:umbrella" />
                      <span>{{ item.pop }}%</span>
                    </template>
                    <span v-if="(item.rain || 0) > 0" class="ml-1">{{ item.rain }}mm</span>
                    <template v-if="(item.sno || 0) > 0">
                      <IconifyIcon class="ml-1 size-3" icon="lucide:cloud-snow" />
                      <span>{{ item.sno }}cm</span>
                    </template>
                  </div>

                  <div class="text-muted-foreground flex items-center gap-1 text-xs">
                    <IconifyIcon
                      :style="{ transform: `rotate(${item.windDir}deg)` }"
                      class="size-3"
                      icon="lucide:navigation"
                    />
                    <span>{{ item.windSpeed }}m/s</span>
                  </div>

                  <!-- 상세 격자 -->
                  <div
                    class="border-border text-muted-foreground mt-1 grid w-full grid-cols-2 gap-x-2 gap-y-1 border-t pt-2 text-[10px]"
                  >
                    <div class="flex items-center justify-between">
                      <span>습도</span>
                      <span class="text-foreground font-bold">{{ item.reh }}%</span>
                    </div>
                    <div class="flex items-center justify-between">
                      <span>강수</span>
                      <span class="text-foreground font-bold">{{ getPtyName(item.pty) }}</span>
                    </div>
                    <div
                      v-if="(item.sno || 0) > 0"
                      class="col-span-2 flex items-center justify-between rounded bg-blue-500/10 px-1"
                    >
                      <span class="text-blue-600 dark:text-blue-300">적설</span>
                      <span class="font-bold text-blue-600 dark:text-blue-300">
                        {{ item.sno }}cm
                      </span>
                    </div>
                    <div class="col-span-2 mt-1 grid grid-cols-2 gap-x-2">
                      <div class="flex justify-between"><span>동서</span><span>{{ item.uuu }}</span></div>
                      <div class="flex justify-between"><span>남북</span><span>{{ item.vvv }}</span></div>
                    </div>
                  </div>
                </div>

                <div
                  v-if="forecastData.length === 0 && !loading"
                  class="text-muted-foreground flex h-24 w-full items-center justify-center text-sm"
                >
                  데이터가 없습니다.
                </div>
              </div>

              <!-- 좌우 스크롤 버튼 -->
              <button
                class="bg-card border-border text-muted-foreground absolute left-0 top-1/2 z-20 flex size-8 -translate-y-1/2 items-center justify-center rounded-full border shadow-md hover:text-blue-500"
                type="button"
                @click="scrollTimeline(-600)"
              >
                <IconifyIcon class="size-5" icon="lucide:chevron-left" />
              </button>
              <button
                class="bg-card border-border text-muted-foreground absolute right-0 top-1/2 z-20 flex size-8 -translate-y-1/2 items-center justify-center rounded-full border shadow-md hover:text-blue-500"
                type="button"
                @click="scrollTimeline(600)"
              >
                <IconifyIcon class="size-5" icon="lucide:chevron-right" />
              </button>
            </div>
          </Spin>
        </div>

        <!-- 주간 예보 -->
        <div class="bg-card border-border rounded-lg border p-3">
          <h3 class="text-foreground mb-2 text-base font-bold">주간 예보</h3>
          <WeatherWeeklyForecast :location-id="selectedLocationId" />
        </div>

        <!-- 카테고리별 예보 추이 -->
        <div class="bg-card border-border rounded-lg border p-3">
          <h3 class="text-foreground mb-3 text-base font-bold">기상 카테고리별 예보 추이</h3>
          <div v-show="forecastData.length > 0" class="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div
              v-for="(def, idx) in chartDefs"
              :key="def.id"
              class="border-border bg-muted/20 rounded-xl border p-3"
            >
              <h4 class="text-foreground/80 mb-2 flex items-center gap-2 text-sm font-semibold">
                <span class="h-4 w-1 rounded-full bg-blue-500"></span>
                {{ def.title }}
              </h4>
              <EchartsUI :ref="(el: any) => setChartRef(idx, el)" height="240px" />
            </div>
          </div>
          <div
            v-if="forecastData.length === 0"
            class="text-muted-foreground flex h-[200px] items-center justify-center text-sm"
          >
            데이터가 없습니다.
          </div>
        </div>
      </div>
    </div>
  </Page>
</template>
