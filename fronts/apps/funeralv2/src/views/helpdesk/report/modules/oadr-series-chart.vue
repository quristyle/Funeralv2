<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import { ref, watch } from 'vue';

import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

/**
 * OADR 리포트용 시계열 차트.
 *
 * 리포트 화면들이 모두 "행 목록 + x축 컬럼 + 여러 y 컬럼" 형태라 하나로 묶었다.
 */
const props = withDefaults(
  defineProps<{
    /** 그릴 값 컬럼들 */
    fields: { color?: string; key: string; label: string; yAxis?: number }[];
    height?: string;
    rows: Record<string, any>[];
    /** 두 번째 y축 이름(있을 때만 축을 만든다) */
    secondAxisName?: string;
    type?: 'bar' | 'line';
    /** x축으로 쓸 컬럼 */
    xField: string;
  }>(),
  { height: '260px', type: 'line' },
);

const chartRef = ref<EchartsUIType>();
const { renderEcharts } = useEcharts(chartRef);

/** 시각만 남겨 x축 라벨을 짧게 만든다. */
function shortLabel(value: any) {
  const raw = String(value ?? '');
  const asDate = new Date(raw);
  if (!Number.isNaN(asDate.getTime()) && raw.includes('T')) {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${pad(asDate.getMonth() + 1)}/${pad(asDate.getDate())} ${pad(asDate.getHours())}:${pad(asDate.getMinutes())}`;
  }
  return raw;
}

function draw() {
  const yAxis: any[] = [{ type: 'value' }];
  if (props.secondAxisName) {
    yAxis.push({ name: props.secondAxisName, type: 'value' });
  }

  renderEcharts({
    grid: { bottom: 40, left: 50, right: props.secondAxisName ? 55 : 16, top: 30 },
    legend: { top: 0, type: 'scroll' },
    series: props.fields.map((f) => ({
      data: props.rows.map((r) => Number(r[f.key] ?? 0)),
      itemStyle: f.color ? { color: f.color } : undefined,
      name: f.label,
      smooth: props.type === 'line',
      type: props.type,
      yAxisIndex: f.yAxis ?? 0,
    })),
    tooltip: { trigger: 'axis' },
    xAxis: {
      axisLabel: { rotate: props.rows.length > 20 ? 45 : 0 },
      data: props.rows.map((r) => shortLabel(r[props.xField])),
      type: 'category',
    },
    yAxis,
  });
}

watch(() => props.rows, draw, { deep: true, immediate: true });
</script>

<template>
  <EchartsUI ref="chartRef" :height="height" />
</template>
