<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import {
  Card,
  Col,
  Empty,
  Progress,
  Row,
  Spin,
  Tag,
} from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import GridIconButton from '#/components/GridIconButton.vue';
import { getServerReport } from '#/api/helpdesk';

/**
 * [월간 리포트]
 *
 * 원본(JinReception reports/MonthlyReport.vue, `/reports/monthly`).
 * MONTHLY(일자별 지표) + EXECUTIVE(종합 건강 점수) 두 쿼리를 함께 본다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * 가져오기 방식은 그대로다 — MONTHLY 를 한 번에 전량 받아 차트와 표가 같은
 * 배열을 본다. 그래서 표는 `:table-data` 로 받는다.
 * ------------------------------------------------------------
 */

const loading = ref(false);
const rows = ref<Record<string, any>[]>([]);
const executive = ref<null | Record<string, any>>(null);

const chartRef = ref<EchartsUIType>();
const { renderEcharts } = useEcharts(chartRef);

/** 리포트 핵심 지표 설명. 원본의 reportGlossary. */
const GLOSSARY = [
  {
    desc: 'I/O 지연, 메모리 압박 등 병목 이벤트 발생 빈도를 역산한 신뢰도 점수입니다.',
    title: '시스템 건강 점수',
  },
  {
    desc: '하루 중 가장 높았던 프로세서 사용률입니다. 피크 시간대 수용량을 판단합니다.',
    title: '일일 최대 CPU',
  },
  {
    desc: 'TempDB 및 로그 쓰기 지연의 평균값입니다. DB 내부의 처리 품질을 나타냅니다.',
    title: '서브시스템 지연',
  },
];

/** 건강 점수 구간 판정. 원본의 getHealthStatus 와 같다. */
const healthStatus = computed(() => {
  const score = Number(executive.value?.Server_Health_Score ?? 0);
  if (score >= 90) {
    return { color: 'success', hex: '#10B981', label: '최상 (Healthy)', score };
  }
  if (score >= 70) {
    return { color: 'processing', hex: '#3B82F6', label: '보통 (Stable)', score };
  }
  return {
    color: 'error',
    hex: '#EF4444',
    label: '위험 (Action Required)',
    score,
  };
});

/** 일자별 기술 판정. 원본과 같은 우선순위(메모리 → CPU → 안정). */
function rowVerdict(row: Record<string, any>) {
  if (Number(row.Min_PLE) < 300) {
    return { color: 'error', label: '메모리위험' };
  }
  if (Number(row.Max_CPU) > 85) {
    return { color: 'warning', label: '과부하' };
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
        field: 'Max_CPU',
        slots: { default: 'Max_CPU' },
        title: '최대 CPU (%)',
        width: 130,
      },
      {
        field: 'Avg_IO_ms',
        slots: { default: 'Avg_IO_ms' },
        title: '평균 I/O (ms)',
        width: 140,
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
        field: 'verdict',
        params: { filterText: (row: any) => rowVerdict(row).label, sort: false },
        slots: { default: 'verdict' },
        title: '기술 판정',
        width: 120,
      },
    ],
    // 행 배열은 `:table-data` 로 간다.
    data: [],
    emptyText: '데이터가 없습니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 위쪽 '리포트 갱신' 이 부르는 것과 같은 함수를 준다.
    gridFeatures: { onRefresh: () => loadData() },
    height: 400,
    // 전량을 한 번에 받는 표다. 켜 두면 응답을 `{ result, page }` 로 읽어 한 행도 안 나온다.
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'LogDate' },
  } as any,
});

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
        areaStyle: { opacity: 0.12 },
        data: pick('Max_CPU'),
        itemStyle: { color: '#EF5350' },
        name: '일일 최대 CPU (%)',
        smooth: true,
        type: 'line',
      },
      {
        data: pick('Avg_IO_ms'),
        itemStyle: { color: '#42A5F5' },
        name: '평균 I/O (ms)',
        smooth: true,
        type: 'line',
      },
      {
        data: pick('Min_PLE'),
        itemStyle: { color: '#FFA726' },
        lineStyle: { type: 'dashed' as const },
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
    const [monthly, exec] = await Promise.all([
      getServerReport<Record<string, any>[]>('MONTHLY'),
      getServerReport<Record<string, any>[]>('EXECUTIVE'),
    ]);
    rows.value = monthly ?? [];
    executive.value = exec?.[0] ?? null;
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
          월간 서버 운영 성과 분석 및 전략 리포트
        </span>
        <GridIconButton
          :loading="loading"
          icon="vxe-icon-repeat"
          title="리포트 갱신"
          @click="loadData"
        />
      </div>
    </Card>

    <Spin :spinning="loading">
      <!-- 1. 경영 요약 스코어보드 -->
      <Row :gutter="[12, 12]">
        <Col :lg="12" :xs="24">
          <Card size="small">
            <template #title>
              <span class="text-xs font-semibold uppercase text-muted-foreground">
                종합 시스템 건강 점수 (Reliability Score)
              </span>
            </template>

            <div v-if="executive" class="flex flex-col items-center py-2">
              <div class="flex items-baseline gap-2">
                <span class="text-6xl font-bold">
                  {{ Math.round(healthStatus.score) }}
                </span>
                <span class="text-xl text-muted-foreground">/ 100</span>
              </div>

              <div class="mt-4 w-full px-6">
                <Progress
                  :percent="Math.round(healthStatus.score)"
                  :show-info="false"
                  :stroke-color="healthStatus.hex"
                />
              </div>

              <Tag :color="healthStatus.color" class="mt-4">
                {{ healthStatus.label }}
              </Tag>
            </div>

            <Empty v-else description="건강 점수를 불러오지 못했습니다." />
          </Card>
        </Col>

        <Col :lg="12" :xs="24">
          <Card size="small">
            <template #title>
              <span class="text-xs font-semibold uppercase text-muted-foreground">
                리포트 핵심 지표 설명
              </span>
            </template>

            <div
              v-for="item in GLOSSARY"
              :key="item.title"
              class="border-b border-border pb-2 pt-2 first:pt-0 last:border-b-0"
            >
              <div class="text-xs font-semibold">● {{ item.title }}</div>
              <div class="text-[10px] leading-tight text-muted-foreground">
                {{ item.desc }}
              </div>
            </div>
          </Card>
        </Col>
      </Row>

      <!-- 2. 월간 장기 추이 -->
      <Card
        class="mt-3"
        size="small"
        title="월간 자원 Peak 및 서브시스템 병목 추이 (Min_PLE 포함)"
      >
        <EchartsUI ref="chartRef" height="320px" />
      </Card>

      <!-- 3. 상세 운영 로그 -->
      <Card
        :body-style="{ padding: 0 }"
        class="mt-3"
        size="small"
        title="월간 일자별 운영 정밀 데이터 로그"
      >
        <Grid :table-data="rows">
          <template #LogDate="{ row }">{{ dateOnly(row.LogDate) }}</template>

          <template #Max_CPU="{ row }">
            <span :class="Number(row.Max_CPU) > 90 ? 'font-bold text-red-600' : ''">
              {{ toFixed(row.Max_CPU, 1) }}%
            </span>
          </template>

          <template #Avg_IO_ms="{ row }">{{ toFixed(row.Avg_IO_ms) }}</template>

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

          <template #verdict="{ row }">
            <Tag :color="rowVerdict(row).color">{{ rowVerdict(row).label }}</Tag>
          </template>
        </Grid>
      </Card>
    </Spin>
  </Page>
</template>
