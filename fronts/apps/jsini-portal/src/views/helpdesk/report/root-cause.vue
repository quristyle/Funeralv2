<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import { Button, Card, Col, Row, Space, Spin, Tag } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getServerReport } from '#/api/helpdesk';

/**
 * [원인 분석]
 *
 * 원본(JinReception reports/RootCauseAnalysis.vue, `/reports/root-cause`).
 * PLE 급락의 원인을 메모리 배분(MEM_DETAIL)과 부하 쿼리(LOAD_ANALYSIS) 양쪽에서 본다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * 가져오기 방식은 그대로다 — LOAD_ANALYSIS 를 한 번에 전량 받아 차트와 표가
 * 같은 배열을 본다. 그래서 표는 `:table-data` 로 받는다.
 * ------------------------------------------------------------
 */

const loading = ref(false);
const memInfo = ref<null | Record<string, any>>(null);
const topQueries = ref<Record<string, any>[]>([]);

const chartRef = ref<EchartsUIType>();
const { renderEcharts } = useEcharts(chartRef);

/** 메모리 상태 판정과 소견. 원본의 stateMap. */
const STATE_MAP: Record<
  string,
  { advice: string; color: string; label: string }
> = {
  'Buffer Shortage': {
    advice:
      '물리적 메모리가 부족하여 디스크 접근이 빈번합니다. 인덱스 튜닝 및 RAM 증설을 검토하십시오.',
    color: 'error',
    label: '버퍼 부족 (I/O 유발)',
  },
  'Cache Churn': {
    advice:
      '대량의 일회성 쿼리가 유입되어 캐시 효율이 급감했습니다. Ad-hoc 쿼리 최적화가 필요합니다.',
    color: 'warning',
    label: '캐시 요동 (잦은 교체)',
  },
  'Stable': {
    advice: '현재 메모리 운영이 매우 안정적입니다. 정기 모니터링 체제를 유지하십시오.',
    color: 'success',
    label: '정상 안정',
  },
};

const memoryState = computed(() => {
  const state = memInfo.value?.Memory_State;
  return (
    STATE_MAP[state ?? ''] ?? { advice: '', color: 'default', label: state ?? '-' }
  );
});

/** 4대 정밀 지표 카드. 원본과 같은 항목·가이드 문구. */
const metricCards = computed(() => [
  {
    accent: 'border-l-blue-500',
    hint: '정상: 1000s 이상',
    label: '메모리 수명 (PLE)',
    tone: 'text-blue-700 dark:text-blue-400',
    unit: 's',
    value: memInfo.value?.PLE ?? '-',
  },
  {
    accent: 'border-l-red-500',
    hint: '100ms 초과 시 병목',
    label: '페이지 읽기 대기',
    tone: 'text-red-700 dark:text-red-400',
    unit: 'ms',
    value: memInfo.value?.PAGEIOLATCH_WAIT_MS ?? '-',
  },
  {
    accent: 'border-l-orange-500',
    hint: '높을수록 메모리 부족',
    label: '초당 페이지 방출',
    tone: 'text-orange-700 dark:text-orange-400',
    unit: 'p/s',
    value: memInfo.value?.LazyWrites_Sec ?? '-',
  },
  {
    accent: 'border-l-purple-500',
    hint: '인덱스 부족의 증거',
    label: '물리적 읽기량',
    tone: 'text-purple-700 dark:text-purple-400',
    unit: 'p/s',
    value: memInfo.value?.PageReads_Sec ?? '-',
  },
]);

/** MB 를 GB 로 환산해 표시한다. */
function toGb(mb: any) {
  const n = Number(mb);
  return Number.isNaN(n) ? '-' : (n / 1024).toFixed(1);
}

function formatDateTime(value?: string) {
  if (!value) return '-';
  return String(value).replace('T', ' ').split('.')[0] ?? '-';
}

/** 읽기량으로 가른 진단. 원본이 태그에 직접 적어 두었던 기준과 같다. */
function queryVerdict(row: Record<string, any>) {
  return Number(row.AvgLogicalReads) > 50_000
    ? { color: 'error', label: 'Tuning' }
    : { color: 'warning', label: 'Check' };
}

const [Grid] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      {
        align: 'left',
        field: 'QueryText',
        minWidth: 320,
        // 쿼리 원문은 여러 줄로 접어서 보여 준다. 전역 `showOverflow` 를 끄지 않으면
        // 한 줄로 잘려 원본과 다르게 보인다.
        showOverflow: false,
        slots: { default: 'QueryText' },
        title: '부하 쿼리 (SQL)',
      },
      {
        field: 'AvgLogicalReads',
        slots: { default: 'AvgLogicalReads' },
        title: '평균 읽기(P)',
        width: 130,
      },
      {
        field: 'last_execution_time',
        // 화면에 보이는 글자와 저장된 값이 다른 칸이다.
        params: {
          filterText: (row: any) => formatDateTime(row.last_execution_time),
        },
        slots: { default: 'last_execution_time' },
        title: '마지막 실행',
        width: 170,
      },
      {
        // 값이 아니라 읽기량에서 뽑아낸 판정이라 행에 없는 칸이다.
        // 훑을 글자를 직접 준다(정렬은 의미가 없어 끈다).
        field: 'verdict',
        params: { filterText: (row: any) => queryVerdict(row).label, sort: false },
        slots: { default: 'verdict' },
        title: '진단',
        width: 90,
      },
    ],
    // 행 배열은 `:table-data` 로 간다.
    data: [],
    emptyText: '부하 쿼리가 없습니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 위쪽 '정밀 진단 재실행' 이 부르는 것과 같은 함수를 준다.
    gridFeatures: { onRefresh: () => loadData() },
    height: 560,
    // 전량을 한 번에 받는 표다. 켜 두면 응답을 `{ result, page }` 로 읽어 한 행도 안 나온다.
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'QueryText' },
  } as any,
});

function drawChart() {
  if (!memInfo.value) return;

  renderEcharts({
    legend: { bottom: 0, textStyle: { fontSize: 10 } },
    series: [
      {
        data: [
          {
            itemStyle: { color: '#3B82F6' },
            name: '데이터 캐시',
            value: Number(memInfo.value.BufferPool_MB ?? 0),
          },
          {
            itemStyle: { color: '#6366F1' },
            name: '실행 계획',
            value: Number(memInfo.value.PlanCache_MB ?? 0),
          },
          {
            itemStyle: { color: '#94A3B8' },
            name: '기타',
            value: Number(memInfo.value.Other_Memory_MB ?? 0),
          },
        ],
        name: '메모리 배분(MB)',
        radius: ['60%', '82%'],
        type: 'pie',
      },
    ],
    tooltip: { trigger: 'item' },
  });
}

async function loadData() {
  loading.value = true;
  try {
    const [mem, load] = await Promise.all([
      getServerReport<Record<string, any>[]>('MEM_DETAIL'),
      getServerReport<Record<string, any>[]>('LOAD_ANALYSIS'),
    ]);
    memInfo.value = mem?.[0] ?? null;
    topQueries.value = load ?? [];
    drawChart();
  } finally {
    loading.value = false;
  }
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space>
          <span class="text-base font-semibold">
            성능 저하(PLE 급락) 근본 원인 정밀 분석
          </span>
          <Tag v-if="memInfo" :color="memoryState.color">
            {{ memoryState.label }}
          </Tag>
        </Space>
        <Button :loading="loading" type="primary" @click="loadData">
          정밀 진단 재실행
        </Button>
      </div>
    </Card>

    <Spin :spinning="loading">
      <Row :gutter="[12, 12]">
        <!-- 왼쪽: 메모리 정밀 진단 -->
        <Col :lg="10" :xs="24">
          <Card size="small">
            <template #title>
              <span class="text-xs font-semibold uppercase text-muted-foreground">
                Memory Allocation Distribution
              </span>
            </template>

            <div class="flex items-center gap-4">
              <div class="min-w-0 flex-1">
                <EchartsUI ref="chartRef" height="200px" />
              </div>
              <div class="flex w-28 flex-col gap-3">
                <div>
                  <div class="text-[10px] font-semibold text-muted-foreground">
                    데이터 캐시
                  </div>
                  <div class="text-sm font-bold text-blue-600">
                    {{ toGb(memInfo?.BufferPool_MB) }} GB
                  </div>
                </div>
                <div>
                  <div class="text-[10px] font-semibold text-muted-foreground">
                    실행 계획
                  </div>
                  <div class="text-sm font-bold text-indigo-600">
                    {{ toGb(memInfo?.PlanCache_MB) }} GB
                  </div>
                </div>
                <div>
                  <div class="text-[10px] font-semibold text-muted-foreground">
                    가용 RAM
                  </div>
                  <div class="text-sm font-bold text-green-600">
                    {{ toGb(memInfo?.Available_RAM_MB) }} GB
                  </div>
                </div>
              </div>
            </div>
          </Card>

          <!-- 4대 정밀 지표 -->
          <Row :gutter="[12, 12]" class="mt-3">
            <Col v-for="card in metricCards" :key="card.label" :span="12">
              <div class="rounded border border-l-4 border-border p-3" :class="card.accent">
                <div class="mb-1 text-[10px] font-semibold text-muted-foreground">
                  {{ card.label }}
                </div>
                <div class="text-lg font-bold" :class="card.tone">
                  {{ card.value }}<span class="ml-1 text-xs">{{ card.unit }}</span>
                </div>
                <div class="mt-1 text-[10px] text-muted-foreground">
                  {{ card.hint }}
                </div>
              </div>
            </Col>
          </Row>

          <!-- 전문가 소견 -->
          <Card class="mt-3" size="small">
            <div class="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
              Diagnostic Opinion
            </div>
            <p class="m-0 mt-2 text-sm font-medium italic leading-relaxed">
              "{{ memoryState.advice || '진단 소견이 없습니다.' }}"
            </p>
          </Card>
        </Col>

        <!-- 오른쪽: 부하 주범 쿼리 -->
        <Col :lg="14" :xs="24">
          <Card :body-style="{ padding: 0 }" size="small">
            <template #title>
              <span class="text-xs font-semibold uppercase text-red-600">
                Top 10 High-Read Queries (PLE Killers)
              </span>
            </template>

            <Grid :table-data="topQueries">
              <template #QueryText="{ row }">
                <div
                  class="max-h-24 overflow-auto rounded border border-border bg-accent/40 p-2 font-mono text-[10px] leading-snug"
                >
                  {{ row.QueryText }}
                </div>
              </template>

              <template #AvgLogicalReads="{ row }">
                <span class="font-bold text-red-600">
                  {{ Number(row.AvgLogicalReads ?? 0).toLocaleString() }}
                </span>
              </template>

              <template #last_execution_time="{ row }">
                <span class="text-muted-foreground">
                  {{ formatDateTime(row.last_execution_time) }}
                </span>
              </template>

              <template #verdict="{ row }">
                <Tag :color="queryVerdict(row).color">
                  {{ queryVerdict(row).label }}
                </Tag>
              </template>
            </Grid>
          </Card>
        </Col>
      </Row>
    </Spin>
  </Page>
</template>
