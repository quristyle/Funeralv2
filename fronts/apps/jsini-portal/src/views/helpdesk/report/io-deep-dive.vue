<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import {
  Alert,
  Button,
  Card,
  Col,
  Empty,
  Row,
  Spin,
  Table,
  Tag,
} from 'ant-design-vue';

import { getServerReport } from '#/api/helpdesk';

/**
 * [IO 정밀 분석]
 *
 * 원본(JinReception reports/IODeepDive.vue, `/reports/io-deep-dive`).
 * OADR 의 IO_DETAIL 쿼리로 최근 3시간 디스크 응답과 병목 원인을 본다.
 */

const loading = ref(false);
const rows = ref<Record<string, any>[]>([]);

const chartRef = ref<EchartsUIType>();
const { renderEcharts } = useEcharts(chartRef);

/** 지표 해설과 정상 수치. 원본의 ioGlossary. */
const GLOSSARY = [
  {
    desc: '하드웨어적 응답 시간입니다. 20ms 이상 시 스토리지 성능 한계로 판단합니다.',
    guide: '정상: 20ms 이내',
    title: '물리 디스크 응답 (Latency)',
  },
  {
    desc: '메모리에 데이터가 없어 디스크에서 강제로 읽어올 때 발생하는 대기 시간입니다.',
    guide: '정상: 0.1s 미만',
    title: '데이터 파일 읽기 압박',
  },
  {
    desc: '복잡한 쿼리나 정렬 작업 시 발생하는 지연입니다. 높을수록 쿼리 간 경합이 심합니다.',
    guide: '정상: 0.05s 미만',
    title: '임시 DB 지연 (TempDB)',
  },
  {
    desc: '데이터 변경(CUD) 기록 시 발생하는 대기입니다. 입력/수정 처리 속도를 결정합니다.',
    guide: '정상: 0.05s 미만',
    title: '로그 기록 지연 (Log)',
  },
];

/** 근본 원인 한글화. 원본의 getRootCauseInfo 와 같은 매핑. */
const ROOT_CAUSES: Record<string, { color: string; label: string }> = {
  'Healthy': { color: 'success', label: '정상 운영' },
  'Log Bottleneck': { color: 'error', label: '로그 쓰기 병목' },
  'Memory Pressure (causing Disk Reads)': {
    color: 'warning',
    label: '메모리 부족 (디스크 읽기 유발)',
  },
  'Physical Disk Slow': { color: 'error', label: '물리 디스크 저하' },
  'TempDB Bottleneck': { color: 'error', label: '임시 DB 병목' },
};

function rootCause(cause?: string) {
  return ROOT_CAUSES[cause ?? ''] ?? { color: 'default', label: cause ?? '' };
}

const columns = [
  { dataIndex: 'CHECK_TIME', key: 'CHECK_TIME', title: '측정 시각', width: 170 },
  {
    dataIndex: 'Disk_Latency_ms',
    key: 'Disk_Latency_ms',
    title: '디스크 응답',
    width: 120,
  },
  {
    dataIndex: 'DataFile_Read_Stall_sec',
    key: 'DataFile_Read_Stall_sec',
    title: '데이터 읽기(s)',
    width: 140,
  },
  {
    dataIndex: 'TempDB_Stall_sec',
    key: 'TempDB_Stall_sec',
    title: '임시DB 대기(s)',
    width: 140,
  },
  {
    dataIndex: 'Log_Stall_sec',
    key: 'Log_Stall_sec',
    title: '로그 쓰기(s)',
    width: 130,
  },
  { dataIndex: 'RootCause', key: 'RootCause', title: '근본 원인 분석', width: 220 },
];

function toFixed(value: any, digits: number) {
  const n = Number(value);
  return Number.isNaN(n) ? '-' : n.toFixed(digits);
}

/** ISO 시각을 'YYYY-MM-DD HH:mm:ss' 로 다듬는다. */
function timestamp(value?: string) {
  return String(value ?? '')
    .split('.')[0]
    ?.replace('T', ' ') ?? '';
}

/** 시:분만 남긴 축 라벨 */
function hourMinute(value?: string) {
  return String(value ?? '')
    .split('T')[1]
    ?.slice(0, 5) ?? '';
}

/**
 * 원본에는 없던 추이 차트를 추가했다.
 * 180건짜리 표만으로는 언제 튀었는지 보기 어려워, 같은 데이터를 시계열로 함께 보여준다.
 */
function drawChart() {
  const labels = rows.value.map((d) => hourMinute(d.CHECK_TIME));
  const pick = (key: string) => rows.value.map((d) => Number(d[key] ?? 0));

  renderEcharts({
    grid: { bottom: 28, containLabel: true, left: 10, right: 10, top: 32 },
    legend: { textStyle: { fontSize: 10 }, top: 0, type: 'scroll' },
    series: [
      {
        areaStyle: { opacity: 0.12 },
        data: pick('Disk_Latency_ms'),
        itemStyle: { color: '#EF5350' },
        markLine: {
          data: [
            {
              label: { fontSize: 9, formatter: '기준 20ms' },
              lineStyle: { color: 'rgba(239,83,80,0.6)', type: 'dashed' as const },
              yAxis: 20,
            },
          ],
          silent: true,
          symbol: 'none' as const,
        },
        name: '디스크 응답(ms)',
        showSymbol: false,
        smooth: true,
        type: 'line',
      },
      {
        data: pick('DataFile_Read_Stall_sec'),
        itemStyle: { color: '#FFA726' },
        name: '데이터 읽기(s)',
        showSymbol: false,
        smooth: true,
        type: 'line',
        yAxisIndex: 1,
      },
      {
        data: pick('TempDB_Stall_sec'),
        itemStyle: { color: '#66BB6A' },
        name: '임시DB 대기(s)',
        showSymbol: false,
        smooth: true,
        type: 'line',
        yAxisIndex: 1,
      },
      {
        data: pick('Log_Stall_sec'),
        itemStyle: { color: '#AB47BC' },
        name: '로그 쓰기(s)',
        showSymbol: false,
        smooth: true,
        type: 'line',
        yAxisIndex: 1,
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: {
      axisLabel: { fontSize: 9, hideOverlap: true },
      boundaryGap: false,
      data: labels,
      type: 'category',
    },
    yAxis: [
      { axisLabel: { fontSize: 9 }, min: 0, name: 'ms', type: 'value' },
      {
        axisLabel: { fontSize: 9 },
        min: 0,
        name: 'sec',
        splitLine: { show: false },
        type: 'value',
      },
    ],
  });
}

async function loadData() {
  loading.value = true;
  try {
    rows.value =
      (await getServerReport<Record<string, any>[]>('IO_DETAIL')) ?? [];
    drawChart();
  } finally {
    loading.value = false;
  }
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height>
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <span class="text-base font-semibold">
          I/O 서브시스템 병목 원인 정밀 분석 (속도 기반)
        </span>
        <Button :loading="loading" danger @click="loadData">데이터 갱신</Button>
      </div>
    </Card>

    <Spin :spinning="loading">
      <!-- 1. 지표 용어 해설 -->
      <Card class="mb-3" size="small">
        <Row :gutter="[12, 12]">
          <Col v-for="item in GLOSSARY" :key="item.title" :lg="6" :xs="24">
            <div class="mb-1 flex items-center justify-between border-b border-border pb-1">
              <span class="text-[11px] font-semibold">{{ item.title }}</span>
              <span class="rounded bg-accent px-1 text-[10px] font-semibold">
                {{ item.guide }}
              </span>
            </div>
            <span class="text-[10px] leading-tight text-muted-foreground">
              {{ item.desc }}
            </span>
          </Col>
        </Row>
      </Card>

      <!-- 2. 추이 차트 -->
      <Card class="mb-3" size="small" title="최근 3시간 I/O 지연 추이">
        <EchartsUI ref="chartRef" height="260px" />
      </Card>

      <!-- 3. 분석 로그 -->
      <Card :body-style="{ padding: 0 }" size="small">
        <template #title>
          <span class="text-xs font-semibold uppercase text-muted-foreground">
            최근 3시간 디스크 응답 및 부하 원인 분석
          </span>
        </template>
        <template #extra>
          <span class="text-[11px] text-muted-foreground">
            데이터 샘플 {{ rows.length }}개
          </span>
        </template>

        <Table
          :columns="columns"
          :data-source="rows"
          :pagination="{ pageSize: 15, showSizeChanger: true }"
          :scroll="{ x: 920 }"
          row-key="CHECK_TIME"
          size="small"
        >
          <template #emptyText>
            <Empty description="데이터가 없습니다." />
          </template>

          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'CHECK_TIME'">
              {{ timestamp(record.CHECK_TIME) }}
            </template>

            <template v-else-if="column.key === 'Disk_Latency_ms'">
              <span
                class="font-semibold"
                :class="Number(record.Disk_Latency_ms) > 20 ? 'text-red-600' : ''"
              >
                {{ toFixed(record.Disk_Latency_ms, 2) }}ms
              </span>
            </template>

            <template v-else-if="column.key === 'DataFile_Read_Stall_sec'">
              <span
                :class="
                  Number(record.DataFile_Read_Stall_sec) > 0.1
                    ? 'font-bold text-orange-600'
                    : ''
                "
              >
                {{ toFixed(record.DataFile_Read_Stall_sec, 3) }}
              </span>
            </template>

            <template v-else-if="column.key === 'TempDB_Stall_sec'">
              <span
                :class="
                  Number(record.TempDB_Stall_sec) > 0.05
                    ? 'font-bold text-red-600'
                    : ''
                "
              >
                {{ toFixed(record.TempDB_Stall_sec, 3) }}
              </span>
            </template>

            <template v-else-if="column.key === 'Log_Stall_sec'">
              <span
                :class="
                  Number(record.Log_Stall_sec) > 0.05
                    ? 'font-bold text-red-600'
                    : ''
                "
              >
                {{ toFixed(record.Log_Stall_sec, 3) }}
              </span>
            </template>

            <template v-else-if="column.key === 'RootCause'">
              <Tag :color="rootCause(record.RootCause).color">
                {{ rootCause(record.RootCause).label }}
              </Tag>
            </template>
          </template>
        </Table>
      </Card>

      <!-- 4. 진단 가이드 -->
      <Alert
        class="mt-3"
        show-icon
        type="error"
        message="분석 팁"
        description="[데이터 읽기] 지연이 높으면 인덱스 부족이며, [임시DB] 지연은 복잡한 쿼리, [로그] 지연은 과도한 변경 작업이 원인입니다. 판정 결과를 보고 튜닝 대상을 결정하십시오."
      />
    </Spin>
  </Page>
</template>
