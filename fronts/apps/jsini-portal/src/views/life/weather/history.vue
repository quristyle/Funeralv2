<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import type { VxeTableGridOptions } from '#/adapter/vxe-table';

import { computed, onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';

import { Page } from '@vben/common-ui';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import {
  Button,
  message,
  Popover,
  RadioButton,
  RadioGroup,
  Select,
} from 'ant-design-vue';
import dayjs from 'dayjs';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import {
  getHourlyHistory,
  getLocations,
  getWeatherHistory,
  type LifeWeatherApi,
} from '#/api/life/weather';

/**
 * [날씨 이력 조회]
 *
 * 원본: ghubfront WeatherHistory.vue + components/weather/WeatherHistoryChart.vue.
 * 지역·기간을 고르면 기온/습도/강수/풍속 추이 차트(EchartsUI)와
 * 특정 시각의 일자별 기온 차트, 실측 이력 그리드를 함께 보여 준다.
 * ApexCharts → EchartsUI, Element Plus → ant-design-vue 로 바꿨다.
 */

const route = useRoute();

// ── 상태 ─────────────────────────────────────────────────────
const locations = ref<LifeWeatherApi.Location[]>([]);
const selectedLocation = ref<string>('');
const selectedDays = ref<number>(1);
const selectedHour = ref<number>(13);
const historyData = ref<LifeWeatherApi.Info[]>([]);
const loading = ref(false);

const selectedLocationId = computed(
  () => locations.value.find((l) => l.name === selectedLocation.value)?.id,
);

const periodOptions = [
  { label: '24시간', value: 1 },
  { label: '3일', value: 3 },
  { label: '7일', value: 7 },
];

const hourOptions = Array.from({ length: 24 }, (_, i) => ({
  label: `${i}시`,
  value: i,
}));

/** 데이터 해석 가이드 (원본의 collapse 표를 Popover 로 옮겼다) */
const categoryDefinitions = [
  { category: 'PTY', meaning: '강수 형태', interpretation: '0: 없음(맑음)' },
  { category: 'REH', meaning: '습도 (%)', interpretation: '상대습도' },
  { category: 'RN1', meaning: '1시간 강수량 (mm)', interpretation: '0: 비 없음' },
  { category: 'T1H', meaning: '기온 (℃)', interpretation: '현재 기온' },
  { category: 'VEC', meaning: '풍향 (°)', interpretation: '예: 215 → 남서풍' },
  { category: 'WSD', meaning: '풍속 (m/s)', interpretation: '2 이하: 약한 바람' },
];

// ── 차트 ─────────────────────────────────────────────────────
const tempChartRef = ref<EchartsUIType>();
const humidityChartRef = ref<EchartsUIType>();
const rainChartRef = ref<EchartsUIType>();
const windChartRef = ref<EchartsUIType>();
const dailyChartRef = ref<EchartsUIType>();

const { renderEcharts: renderTemp } = useEcharts(tempChartRef);
const { renderEcharts: renderHumidity } = useEcharts(humidityChartRef);
const { renderEcharts: renderRain } = useEcharts(rainChartRef);
const { renderEcharts: renderWind } = useEcharts(windChartRef);
const { renderEcharts: renderDaily } = useEcharts(dailyChartRef);

/** 관측시각(UTC ISO)을 [로컬 타임스탬프, 값] 쌍으로 만든다 */
function timeSeries(pick: (d: LifeWeatherApi.Info) => null | number | undefined) {
  return historyData.value.map((d) => [
    dayjs(d.observationTime).valueOf(),
    pick(d) ?? 0,
  ]);
}

const CHART_GRID = { bottom: 25, left: 45, right: 12, top: 30 };

function drawCharts() {
  renderTemp({
    grid: CHART_GRID,
    legend: { right: 0, show: true, textStyle: { fontSize: 10 }, top: 0 },
    series: [
      {
        color: '#f59e0b',
        data: timeSeries((d) => d.temperatureC),
        name: '기온',
        showSymbol: false,
        smooth: true,
        type: 'line',
      },
      {
        color: '#ef4444',
        data: timeSeries((d) => d.sensibleTemp ?? d.temperatureC),
        name: '체감온도',
        showSymbol: false,
        smooth: true,
        type: 'line',
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: { type: 'time' },
    yAxis: { name: '°C', type: 'value' },
  });

  renderHumidity({
    grid: CHART_GRID,
    series: [
      {
        areaStyle: { opacity: 0.2 },
        color: '#0ea5e9',
        data: timeSeries((d) => d.humidity),
        name: '습도',
        showSymbol: false,
        smooth: true,
        type: 'line',
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: { type: 'time' },
    yAxis: { name: '%', type: 'value' },
  });

  renderRain({
    grid: CHART_GRID,
    series: [
      {
        color: '#3b82f6',
        data: timeSeries((d) => d.rainfall),
        name: '강수량',
        type: 'bar',
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: { type: 'time' },
    yAxis: { name: 'mm', type: 'value' },
  });

  renderWind({
    grid: CHART_GRID,
    series: [
      {
        color: '#059669',
        data: timeSeries((d) => d.windSpeed),
        name: '풍속',
        showSymbol: false,
        smooth: true,
        type: 'line',
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: { type: 'time' },
    yAxis: { name: 'm/s', type: 'value' },
  });
}

/** 특정 시각(KST) 기준 최근 7일 기온 추이 — 원본 WeatherHistoryChart.vue */
async function drawDailyChart() {
  const locationId = selectedLocationId.value;
  if (!locationId) return;
  try {
    const rows = await getHourlyHistory(locationId, selectedHour.value, 7);

    // 첫 항목·월이 바뀌는 항목만 MM/DD, 나머지는 DD 로 줄인다 (원본과 동일)
    const categories = rows.map((item, index) => {
      const mm = item.date.slice(5, 7);
      const dd = item.date.slice(8, 10);
      if (index === 0) return `${mm}/${dd}`;
      const prev = rows[index - 1];
      return prev && prev.date.slice(5, 7) !== mm ? `${mm}/${dd}` : dd;
    });

    const todayYMD = dayjs().format('YYYY-MM-DD');
    const todayIndex = rows.findIndex((r) => r.date === todayYMD);
    const todayLabel = todayIndex === -1 ? null : categories[todayIndex];

    renderDaily({
      grid: CHART_GRID,
      series: [
        {
          color: '#3b82f6',
          data: rows.map((r) => r.temp),
          label: { fontSize: 10, show: true },
          markLine: todayLabel
            ? {
                data: [{ xAxis: todayLabel }],
                label: { color: '#f97316', formatter: 'TODAY' },
                lineStyle: { color: '#f97316', type: 'dashed' },
                symbol: 'none',
              }
            : undefined,
          name: `${selectedHour.value}시 기온`,
          smooth: true,
          type: 'line',
        },
      ],
      tooltip: { trigger: 'axis' },
      xAxis: { data: categories, type: 'category' },
      yAxis: { name: '°C', type: 'value' },
    });
  } catch {
    // 일자별 기온은 보조 차트라 실패해도 화면을 막지 않는다
  }
}

// ── 그리드 ───────────────────────────────────────────────────
const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      {
        field: 'observationTime',
        formatter: ({ cellValue }) =>
          cellValue ? dayjs(cellValue).format('MM/DD HH:mm') : '',
        title: '일시',
        width: 120,
      },
      { field: 'condition', title: '날씨', width: 90 },
      {
        align: 'right',
        field: 'temperatureC',
        formatter: ({ cellValue }) =>
          cellValue === null || cellValue === undefined
            ? ''
            : `${Math.round(cellValue)}°`,
        title: '기온',
        width: 70,
      },
      {
        align: 'right',
        field: 'sensibleTemp',
        formatter: ({ row }) =>
          `${Math.round(row.sensibleTemp ?? row.temperatureC)}°`,
        title: '체감',
        width: 70,
      },
      { align: 'right', field: 'humidity', title: '습도(%)', width: 80 },
      { align: 'right', field: 'rainfall', title: '강수(mm)', width: 90 },
      { align: 'right', field: 'snowfall', title: '적설(cm)', width: 90 },
      { align: 'right', field: 'windSpeed', title: '풍속(m/s)', width: 90 },
      { align: 'right', field: 'windDirection', title: '풍향(°)', minWidth: 80 },
    ],
    exportConfig: { filename: '기상_실측_이력' },
    height: 'auto',
    pagerConfig: { enabled: false },
    proxyConfig: {
      ajax: {
        query: async () => {
          if (!selectedLocation.value) return [];
          loading.value = true;
          try {
            historyData.value = await getWeatherHistory(
              selectedLocation.value,
              selectedDays.value,
            );
            drawCharts();
            drawDailyChart();
            return historyData.value;
          } catch {
            message.error('이력 로드에 실패했습니다.');
            return [];
          } finally {
            loading.value = false;
          }
        },
      },
    },
    rowConfig: { keyField: 'id' },
    // 엑셀은 아래의 v-perm:excel 버튼으로만 연다
    toolbarConfig: { export: false },
  } as VxeTableGridOptions,
});

function handleSearch() {
  gridApi.query();
}

/** 그리드 내장 내보내기(vxe exportData)로 엑셀을 내려받는다 */
function handleExport() {
  gridApi.grid?.exportData({
    filename: `기상이력_${selectedLocation.value}_${dayjs().format('YYYYMMDD')}`,
    type: 'xlsx',
  });
}

async function fetchLocations() {
  try {
    const rows = await getLocations();
    locations.value = rows.filter((l) => l.isActive);

    // 라우트 쿼리에 지역명이 있으면 우선 선택한다 (원본과 동일)
    const queryLocation = route.query.location as string;
    selectedLocation.value =
      queryLocation && locations.value.some((l) => l.name === queryLocation)
        ? queryLocation
        : (locations.value[0]?.name ?? '');
    handleSearch();
  } catch {
    message.error('지역 목록 로드에 실패했습니다.');
  }
}

onMounted(fetchLocations);
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <!-- 검색 조건 -->
    <div
      class="bg-card mb-4 flex flex-wrap items-center justify-between gap-4 rounded border p-4"
    >
      <div class="flex flex-wrap items-center gap-4">
        <div class="flex items-center gap-2">
          <span class="text-sm font-semibold">지역:</span>
          <Select
            v-model:value="selectedLocation"
            :options="locations.map((l) => ({ label: l.name, value: l.name }))"
            class="w-48"
            option-filter-prop="label"
            placeholder="지역 선택"
            show-search
            @change="handleSearch"
          />
        </div>
        <div class="flex items-center gap-2">
          <span class="text-sm font-semibold">조회 기간:</span>
          <RadioGroup
            v-model:value="selectedDays"
            button-style="solid"
            @change="handleSearch"
          >
            <RadioButton
              v-for="opt in periodOptions"
              :key="opt.value"
              :value="opt.value"
            >
              {{ opt.label }}
            </RadioButton>
          </RadioGroup>
        </div>
        <Button :loading="loading" type="primary" @click="handleSearch">
          검색
        </Button>
      </div>
      <Popover placement="bottomRight" title="데이터 해석 가이드">
        <template #content>
          <table class="text-xs">
            <thead>
              <tr class="border-b text-left">
                <th class="px-2 py-1">항목</th>
                <th class="px-2 py-1">의미</th>
                <th class="px-2 py-1">해석</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="def in categoryDefinitions" :key="def.category">
                <td class="px-2 py-1 font-mono font-bold">{{ def.category }}</td>
                <td class="px-2 py-1">{{ def.meaning }}</td>
                <td class="px-2 py-1 text-gray-500">{{ def.interpretation }}</td>
              </tr>
            </tbody>
          </table>
        </template>
        <Button>해석 가이드</Button>
      </Popover>
    </div>

    <!-- 추이 차트 -->
    <div class="mb-4 grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
      <div class="bg-card rounded border p-3">
        <h4 class="mb-1 text-sm font-bold">기온 및 체감온도</h4>
        <EchartsUI ref="tempChartRef" height="180px" />
      </div>
      <div class="bg-card rounded border p-3">
        <h4 class="mb-1 text-sm font-bold">습도 (REH)</h4>
        <EchartsUI ref="humidityChartRef" height="180px" />
      </div>
      <div class="bg-card rounded border p-3">
        <h4 class="mb-1 text-sm font-bold">강수량 (RN1)</h4>
        <EchartsUI ref="rainChartRef" height="180px" />
      </div>
      <div class="bg-card rounded border p-3">
        <h4 class="mb-1 text-sm font-bold">풍속 (WSD)</h4>
        <EchartsUI ref="windChartRef" height="180px" />
      </div>
      <div class="bg-card rounded border p-3 md:col-span-2">
        <div class="mb-1 flex items-center justify-between">
          <h4 class="text-sm font-bold">일자별 기온 (최근 7일)</h4>
          <Select
            v-model:value="selectedHour"
            :options="hourOptions"
            class="w-24"
            size="small"
            @change="drawDailyChart"
          />
        </div>
        <EchartsUI ref="dailyChartRef" height="180px" />
      </div>
    </div>

    <!-- 실측 이력 -->
    <Grid table-title="실측 이력">
      <template #toolbar-tools>
        <Button v-perm:excel @click="handleExport">엑셀 다운로드</Button>
      </template>
    </Grid>
  </Page>
</template>
