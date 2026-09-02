<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import { Card, Col, Row, Spin, Tag } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import GridIconButton from '#/components/GridIconButton.vue';
import { getServerReport } from '#/api/helpdesk';

/**
 * [주간 리포트]
 *
 * 원본(JinReception reports/WeeklyReport.vue, `/reports/weekly`).
 * OADR 의 P_QURI_SERVER_REPORT(WEEKLY) 로 최근 일주일 일자별 지표를 본다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * 가져오기 방식은 그대로다 — WEEKLY 를 한 번에 전량 받아 차트와 표가 같은
 * 배열을 본다. 그래서 표는 `:table-data` 로 받는다.
 * ------------------------------------------------------------
 */

const loading = ref(false);
const rows = ref<Record<string, any>[]>([]);

const chartRef = ref<EchartsUIType>();
const { renderEcharts } = useEcharts(chartRef);

/** 지표 해설. 원본의 metricGlossary 를 그대로 옮겼다. */
const GLOSSARY = [
  {
    desc: '일일 전체 업무 시간 동안의 평균적인 프로세서 부하입니다.',
    guide: '70% 이하 유지 권장',
    title: '평균 CPU 사용률',
  },
  {
    desc: '스토리지의 물리적 반응 속도로, 50ms 초과 시 체감 성능이 급락합니다.',
    guide: '20ms 이내 정상',
    title: '평균 디스크 응답',
  },
  {
    desc: 'TempDB와 로그 파일에서 발생한 초당 평균 지연 시간(ms/s)입니다.',
    guide: '50ms/s 미만 권장',
    title: '서브시스템 부하',
  },
  {
    desc: '하루 중 메모리 압박이 가장 심했던 순간의 지표입니다.',
    guide: '300s 이상 필수',
    title: '일일 최저 PLE',
  },
];

/** 하루치 지표를 종합해 상태를 판정한다. 원본의 getDayStatus 와 같은 기준. */
function dayStatus(row: Record<string, any>) {
  const ple = Number(row.Min_PLE ?? 0);
  const cpu = Number(row.Avg_CPU ?? 0);
  const io = Number(row.Avg_IO_ms ?? 0);
  const tempdb = Number(row.Avg_TempDB_Stall ?? 0);

  if (ple < 300 || cpu > 80 || io > 50) {
    return { color: 'error', label: '위험' };
  }
  if (ple < 1000 || cpu > 60 || tempdb > 50) {
    return { color: 'warning', label: '주의' };
  }
  return { color: 'success', label: '안정' };
}

function toFixed(value: any, digits = 2) {
  const n = Number(value);
  return Number.isNaN(n) ? '-' : n.toFixed(digits);
}

function dateOnly(value?: string) {
  return String(value ?? '').split('T')[0] ?? '';
}

const [Grid] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      {
        field: 'LogDate',
        // 저장된 값은 ISO 라 화면에 보이는 날짜로 훑게 한다.
        params: { filterText: (row: any) => dateOnly(row.LogDate) },
        slots: { default: 'LogDate' },
        title: '날짜',
        width: 130,
      },
      {
        field: 'Avg_CPU',
        slots: { default: 'Avg_CPU' },
        title: '평균 CPU (%)',
        width: 130,
      },
      {
        field: 'Avg_IO_ms',
        slots: { default: 'Avg_IO_ms' },
        title: '디스크 응답 (ms)',
        width: 150,
      },
      {
        field: 'Avg_TempDB_Stall',
        slots: { default: 'Avg_TempDB_Stall' },
        title: 'TempDB (ms/s)',
        width: 140,
      },
      {
        field: 'Avg_Log_Stall',
        slots: { default: 'Avg_Log_Stall' },
        title: '로그 지연 (ms/s)',
        width: 150,
      },
      {
        field: 'Min_PLE',
        slots: { default: 'Min_PLE' },
        title: '최소 PLE (s)',
        width: 130,
      },
      {
        // 여러 지표를 종합한 판정이라 행에 없는 칸이다. 훑을 글자를 직접 준다.
        field: 'state',
        params: { filterText: (row: any) => dayStatus(row).label, sort: false },
        slots: { default: 'state' },
        title: '일일 상태',
        width: 110,
      },
    ],
    // 행 배열은 `:table-data` 로 간다.
    data: [],
    emptyText: '데이터가 없습니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 위쪽 '데이터 갱신' 이 부르는 것과 같은 함수를 준다.
    gridFeatures: { onRefresh: () => loadData() },
    height: 360,
    // 전량을 한 번에 받는 표다. 켜 두면 응답을 `{ result, page }` 로 읽어 한 행도 안 나온다.
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'LogDate' },
  } as any,
});

/**
 * 원본과 같은 혼합 차트를 그린다.
 * 막대는 TempDB/로그 지연, 선은 CPU·디스크 응답, PLE 는 오른쪽 축을 따로 쓴다.
 */
function drawChart() {
  const labels = rows.value.map((d) => dateOnly(d.LogDate));
  const pick = (key: string) => rows.value.map((d) => Number(d[key] ?? 0));

  renderEcharts({
    grid: { bottom: 30, containLabel: true, left: 10, right: 10, top: 40 },
    legend: { textStyle: { fontSize: 10 }, top: 0, type: 'scroll' },
    series: [
      {
        data: pick('Avg_TempDB_Stall'),
        itemStyle: { color: 'rgba(102,187,106,0.65)' },
        name: 'TempDB 지연',
        type: 'bar',
      },
      {
        data: pick('Avg_Log_Stall'),
        itemStyle: { color: 'rgba(171,71,188,0.65)' },
        name: '로그 지연',
        type: 'bar',
      },
      {
        data: pick('Avg_CPU'),
        itemStyle: { color: '#42A5F5' },
        name: '평균 CPU (%)',
        smooth: true,
        type: 'line',
      },
      {
        data: pick('Avg_IO_ms'),
        itemStyle: { color: '#EF5350' },
        lineStyle: { type: 'dashed' as const },
        name: '디스크 응답 (ms)',
        smooth: true,
        type: 'line',
      },
      {
        data: pick('Min_PLE'),
        itemStyle: { color: '#FFA726' },
        name: '최저 PLE (s)',
        smooth: true,
        type: 'line',
        yAxisIndex: 1,
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: { data: labels, type: 'category' },
    yAxis: [
      { axisLabel: { fontSize: 10 }, min: 0, name: '성능 지표', type: 'value' },
      {
        axisLabel: { fontSize: 10 },
        min: 0,
        name: 'PLE (s)',
        splitLine: { show: false },
        type: 'value',
      },
    ],
  });
}

async function loadData() {
  loading.value = true;
  try {
    rows.value = (await getServerReport<Record<string, any>[]>('WEEKLY')) ?? [];
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
          최근 일주일 서버 운영 추이 및 병목 분석
        </span>
        <GridIconButton
          :loading="loading"
          icon="vxe-icon-repeat"
          title="데이터 갱신"
          @click="loadData"
        />
      </div>
    </Card>

    <Spin :spinning="loading">
      <!-- 1. 주간 지표 가이드 -->
      <Card class="mb-3" size="small">
        <Row :gutter="[12, 12]">
          <Col v-for="item in GLOSSARY" :key="item.title" :lg="6" :xs="24">
            <div class="h-full">
              <div class="mb-1 flex items-center justify-between border-b border-border pb-1">
                <span class="text-[11px] font-semibold">● {{ item.title }}</span>
                <span class="rounded bg-accent px-1 text-[10px] font-semibold">
                  {{ item.guide }}
                </span>
              </div>
              <span class="text-[10px] leading-tight text-muted-foreground">
                {{ item.desc }}
              </span>
            </div>
          </Col>
        </Row>
      </Card>

      <!-- 2. 주간 통합 추이 차트 -->
      <Card
        class="mb-3"
        size="small"
        title="일자별 자원 사용량 및 I/O 병목 추이 (Stall 포함)"
      >
        <EchartsUI ref="chartRef" height="320px" />
      </Card>

      <!-- 3. 일자별 상세 로그 -->
      <Card
        :body-style="{ padding: 0 }"
        size="small"
        title="일주일간 일자별 정밀 성능 지표 로그"
      >
        <Grid :table-data="rows">
          <template #LogDate="{ row }">{{ dateOnly(row.LogDate) }}</template>

          <template #Avg_CPU="{ row }">{{ toFixed(row.Avg_CPU) }}%</template>

          <template #Avg_IO_ms="{ row }">
            <span :class="Number(row.Avg_IO_ms) > 20 ? 'font-bold text-red-600' : ''">
              {{ toFixed(row.Avg_IO_ms) }}ms
            </span>
          </template>

          <template #Avg_TempDB_Stall="{ row }">
            {{ toFixed(row.Avg_TempDB_Stall) }}
          </template>

          <template #Avg_Log_Stall="{ row }">
            {{ toFixed(row.Avg_Log_Stall) }}
          </template>

          <template #Min_PLE="{ row }">
            <span
              :class="
                Number(row.Min_PLE) < 300
                  ? 'font-bold text-red-600'
                  : 'text-green-700 dark:text-green-400'
              "
            >
              {{ row.Min_PLE }}s
            </span>
          </template>

          <template #state="{ row }">
            <Tag :color="dayStatus(row).color">{{ dayStatus(row).label }}</Tag>
          </template>
        </Grid>
      </Card>
    </Spin>
  </Page>
</template>
