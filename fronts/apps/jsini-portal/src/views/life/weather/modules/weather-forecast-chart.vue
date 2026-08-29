<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import type { LifeWeatherApi } from '#/api/life/weather';

import { nextTick, onMounted, ref, watch } from 'vue';

import { IconifyIcon } from '@vben/icons';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import { Button, Radio } from 'ant-design-vue';

import { getForecast } from '#/api/life/weather';

/**
 * [예보 추이 차트] — 원본 WeatherForecastChart.vue 이식.
 *
 * -10h~+10h 타임라인(getForecast)을 기온/강수/바람/습도 카테고리로 갈아 끼우며
 * 보여 준다. 원본은 ApexCharts 였으나 EchartsUI 로 재구현했다.
 * 현재 시점(첫 예보 칸)에 주황 점선(NOW)을 긋는다.
 */

const props = defineProps<{
  locationId: null | number;
}>();

type Category = 'HUMID' | 'RAIN' | 'TEMP' | 'WIND';

const loading = ref(false);
const category = ref<Category>('TEMP');
const rows = ref<LifeWeatherApi.TimelinePoint[]>([]);

const chartRef = ref<EchartsUIType>();
const { renderEcharts } = useEcharts(chartRef);

/** X축 라벨: 날짜가 바뀌는 칸은 M/D, 나머지는 HH시 */
function buildLabels() {
  return rows.value.map((item, idx) => {
    const prev = rows.value[idx - 1];
    const isNewDate = idx === 0 || item.date !== prev?.date;
    return isNewDate
      ? `${Number(item.date.slice(4, 6))}/${Number(item.date.slice(6, 8))}`
      : `${item.time.slice(0, 2)}시`;
  });
}

function nowMarkLine() {
  const nowIdx = rows.value.findIndex((d) => !d.isPast);
  if (nowIdx < 0) return undefined;
  return {
    data: [{ xAxis: nowIdx }],
    label: {
      color: '#f97316',
      formatter: 'NOW',
      position: 'insideEndTop' as const,
    },
    lineStyle: { color: '#f97316', type: 'dashed' as const },
    silent: true,
    symbol: 'none',
  };
}

function draw() {
  if (rows.value.length === 0) return;
  const labels = buildLabels();
  const markLine = nowMarkLine();
  const base: any = {
    grid: { bottom: 28, left: 48, right: 48, top: 32 },
    tooltip: { trigger: 'axis' },
    xAxis: {
      axisLabel: { fontSize: 10 },
      data: labels,
      type: 'category',
    },
  };

  switch (category.value) {
    case 'HUMID': {
      base.color = ['#06B6D4'];
      base.yAxis = { max: 100, min: 0, name: '습도(%)', type: 'value' };
      base.series = [
        {
          areaStyle: { opacity: 0.25 },
          data: rows.value.map((d) => d.reh ?? 0),
          markLine,
          name: '습도',
          smooth: true,
          type: 'line',
        },
      ];
      break;
    }
    case 'RAIN': {
      base.color = ['#0EA5E9', '#3B82F6'];
      base.yAxis = [
        { name: '강수량(mm)', type: 'value' },
        { max: 100, min: 0, name: '강수확률(%)', type: 'value' },
      ];
      base.series = [
        {
          data: rows.value.map((d) => d.rain ?? 0),
          markLine,
          name: '강수량',
          type: 'bar',
        },
        {
          data: rows.value.map((d) => d.pop ?? 0),
          name: '강수확률',
          smooth: true,
          type: 'line',
          yAxisIndex: 1,
        },
      ];
      base.legend = { top: 0 };
      break;
    }
    case 'WIND': {
      base.color = ['#10B981'];
      base.yAxis = { name: '풍속(m/s)', type: 'value' };
      base.series = [
        {
          data: rows.value.map((d) => d.windSpeed ?? 0),
          markLine,
          name: '풍속',
          smooth: true,
          type: 'line',
        },
      ];
      break;
    }
    default: {
      base.color = ['#F59E0B'];
      base.yAxis = { name: '기온(°C)', type: 'value' };
      base.series = [
        {
          data: rows.value.map((d) => d.temp ?? 0),
          markLine,
          name: '기온',
          smooth: true,
          type: 'line',
        },
      ];
    }
  }
  renderEcharts(base);
}

async function fetchData() {
  if (!props.locationId) return;
  loading.value = true;
  try {
    rows.value = (await getForecast(props.locationId)) ?? [];
    await nextTick();
    draw();
  } catch {
    rows.value = [];
  } finally {
    loading.value = false;
  }
}

watch(() => props.locationId, fetchData);
watch(category, draw);
onMounted(fetchData);
defineExpose({ reload: fetchData });
</script>

<template>
  <div>
    <div class="mb-2 flex items-center justify-between gap-2">
      <Radio.Group v-model:value="category" button-style="solid" size="small">
        <Radio.Button value="TEMP">기온</Radio.Button>
        <Radio.Button value="RAIN">강수</Radio.Button>
        <Radio.Button value="WIND">바람</Radio.Button>
        <Radio.Button value="HUMID">습도</Radio.Button>
      </Radio.Group>
      <Button :loading="loading" size="small" type="text" @click="fetchData">
        <IconifyIcon class="size-4" icon="lucide:rotate-cw" />
      </Button>
    </div>

    <EchartsUI v-show="rows.length > 0" ref="chartRef" height="280px" />
    <div
      v-if="rows.length === 0"
      class="text-muted-foreground flex h-[280px] items-center justify-center text-sm"
    >
      데이터가 없습니다.
    </div>
  </div>
</template>
