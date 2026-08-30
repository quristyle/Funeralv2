<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import { computed, onBeforeUnmount, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import {
  Button,
  Card,
  Col,
  Divider,
  Empty,
  Row,
  Space,
  Spin,
  Switch,
  Tag,
} from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getServerReport } from '#/api/helpdesk';

/**
 * [운영 모니터링]
 *
 * 원본(JinReception reports/Monitoring.vue, `/reports/monitoring`).
 * 한주 OADR 의 P_QURI_SERVER_REPORT 를 네 종류로 불러 한 화면에 모은다.
 *
 *   REALTIME   → 실시간 건강 상태 스코어카드 (지표별 임계 판정 · 가이드)
 *   MONITORING → 최근 1시간 정밀 추이 (5분 간격) 4종
 *   DAILY      → 24시간 장기 추이 4종 + 시간대별 상세 로그
 *   KPI        → 누적 지표 요약
 *
 * ------------------------------------------------------------
 * [2026-08-30] 시간대별 상세 로그의 ant-design-vue `<Table>` 을
 * `useVbenVxeGrid` 로 옮겼다. 정렬·필터는 공통 레이어
 * (`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * 가져오기 방식은 그대로다 — DAILY 를 한 번에 전량 받아 차트와 표가 같은
 * 배열을 본다. 그래서 표는 `:table-data` 로 받는다.
 * ------------------------------------------------------------
 */

const loading = ref(false);
const autoRefresh = ref(false);
const lastUpdated = ref('-');

const realtime = ref<null | Record<string, any>>(null);
const monitoring = ref<Record<string, any>[]>([]);
const daily = ref<Record<string, any>[]>([]);
const kpi = ref<null | Record<string, any>>(null);

let refreshTimer: null | ReturnType<typeof setInterval> = null;

// ============================================================
// 지표 정의 — 원본의 metricDefinitions / getMetricStatus 를 그대로 옮겼다.
// ============================================================

/** 스코어카드에 쓰는 지표 키 */
type MetricKey = 'cpu' | 'io' | 'ple' | 'tps';

interface MetricDefinition {
  /** REALTIME 응답에서 값을 꺼낼 컬럼 */
  field: string;
  guide: string;
  desc: string;
  /** 값 표시 시 소수점을 버릴지 */
  round: boolean;
  title: string;
  unit: string;
}

const METRICS: Record<MetricKey, MetricDefinition> = {
  cpu: {
    desc: '프로세서의 작업 부하량을 의미합니다. 80% 이상 지속 시 쿼리 처리 지연 및 시스템 병목이 발생합니다.',
    field: 'CPU_SQL_USAGE',
    guide: '표준: 70% 이하 유지',
    round: false,
    title: 'CPU 사용률',
    unit: '%',
  },
  io: {
    desc: '데이터 읽기/쓰기 요청이 완료되는 시간(ms)입니다. 100ms 초과 시 심각한 I/O 병목으로 판단합니다.',
    field: 'AVG_IO_LATENCY_MS',
    guide: '표준: 20ms 이내 정상',
    round: true,
    title: '디스크 응답',
    unit: 'ms',
  },
  ple: {
    desc: 'Page Life Expectancy. 데이터가 메모리에 머무는 시간(초)입니다. 300초 미만은 메모리 압박이 심각함을 의미합니다.',
    field: 'PLE',
    guide: '권장: 1000s 이상 유지',
    round: false,
    title: '메모리 수명(PLE)',
    unit: 's',
  },
  tps: {
    desc: 'Batch Requests/sec. 서버가 초당 처리하는 SQL 명령 수입니다. 서비스의 실제 업무 부하를 나타내는 핵심 지표입니다.',
    field: 'BATCH_REQUESTS_SEC',
    guide: '통상: 100~500 수준',
    round: true,
    title: '초당 처리량(TPS)',
    unit: '',
  },
};

const METRIC_ORDER: MetricKey[] = ['cpu', 'io', 'ple', 'tps'];

/** 지표값의 임계 판정. 원본의 getMetricStatus 와 같은 기준이다. */
function metricStatus(metric: MetricKey, value: number) {
  switch (metric) {
    case 'cpu': {
      if (value >= 90) {
        return { advice: '임계치 초과! 점검 요망.', color: 'error', label: '위험' };
      }
      return value >= 70
        ? { advice: '부하 증가 추세.', color: 'warning', label: '주의' }
        : { advice: '안정 운영 중.', color: 'success', label: '정상' };
    }
    case 'io': {
      if (value >= 50) {
        return { advice: 'I/O 장애 수준!', color: 'error', label: '심각' };
      }
      return value >= 20
        ? { advice: '디스크 응답 저하.', color: 'warning', label: '지연' }
        : { advice: '응답 속도 양호.', color: 'success', label: '쾌적' };
    }
    case 'ple': {
      if (value < 300) {
        return { advice: '메모리 고갈 위험!', color: 'error', label: '위기' };
      }
      return value < 1000
        ? { advice: '가용 메모리 부족.', color: 'warning', label: '압박' }
        : { advice: '수명 안정적 유지.', color: 'success', label: '양호' };
    }
    case 'tps': {
      if (value >= 1000) {
        return { advice: '트래픽 폭주 중.', color: 'error', label: '고부하' };
      }
      return value >= 500
        ? { advice: '업무 처리 활발.', color: 'warning', label: '활발' }
        : { advice: '통상적인 업무량.', color: 'success', label: '원활' };
    }
  }
}

/** 서버 전체 상태 태그 색. 원본의 getOverallSeverity 와 같다. */
function overallColor(state?: string) {
  if (state === 'Healthy') return 'success';
  if (
    state?.includes('Warning') ||
    state?.includes('Disk') ||
    state?.includes('Memory')
  ) {
    return 'warning';
  }
  return 'error';
}

/** 스코어카드에 그릴 지표 목록 */
const scorecard = computed(() =>
  METRIC_ORDER.map((key) => {
    const def = METRICS[key];
    const raw = Number(realtime.value?.[def.field] ?? 0);
    return {
      def,
      key,
      status: metricStatus(key, raw),
      value: def.round ? Math.round(raw) : raw,
    };
  }),
);

// ============================================================
// 차트
// ============================================================

const cpu1hRef = ref<EchartsUIType>();
const io1hRef = ref<EchartsUIType>();
const mem1hRef = ref<EchartsUIType>();
const tps1hRef = ref<EchartsUIType>();
const cpu24hRef = ref<EchartsUIType>();
const io24hRef = ref<EchartsUIType>();
const mem24hRef = ref<EchartsUIType>();
const stall24hRef = ref<EchartsUIType>();

const { renderEcharts: renderCpu1h } = useEcharts(cpu1hRef);
const { renderEcharts: renderIo1h } = useEcharts(io1hRef);
const { renderEcharts: renderMem1h } = useEcharts(mem1hRef);
const { renderEcharts: renderTps1h } = useEcharts(tps1hRef);
const { renderEcharts: renderCpu24h } = useEcharts(cpu24hRef);
const { renderEcharts: renderIo24h } = useEcharts(io24hRef);
const { renderEcharts: renderMem24h } = useEcharts(mem24hRef);
const { renderEcharts: renderStall24h } = useEcharts(stall24hRef);

/**
 * 차트 옵션 타입. echarts 옵션은 리터럴 값이 넓어지면 타입이 어긋나므로
 * renderEcharts 가 실제로 받는 타입을 그대로 끌어 쓴다.
 */
type ChartOption = Parameters<
  ReturnType<typeof useEcharts>['renderEcharts']
>[0];

/** 기준선 정의 */
interface Baseline {
  color: string;
  label: string;
  value: number;
}

/** 라인 차트 공통 옵션. 기준선은 markLine 으로 그린다. */
function lineOption(params: {
  baselines?: Baseline[];
  labels: string[];
  max?: number;
  min?: number;
  series: {
    area?: boolean;
    color: string;
    dashed?: boolean;
    data: number[];
    name: string;
  }[];
}): ChartOption {
  const [first, ...rest] = params.series;

  return {
    grid: { bottom: 24, containLabel: true, left: 8, right: 12, top: 28 },
    legend: {
      itemHeight: 8,
      itemWidth: 12,
      textStyle: { fontSize: 10 },
      top: 0,
      type: 'scroll' as const,
    },
    series: [first, ...rest].filter(Boolean).map((s, index) => ({
      areaStyle: s!.area ? { opacity: 0.12 } : undefined,
      data: s!.data,
      itemStyle: { color: s!.color },
      lineStyle: s!.dashed
        ? { type: 'dashed' as const, width: 1 }
        : { width: 1.5 },
      // 기준선은 첫 시리즈에만 붙인다(범례가 중복되지 않게).
      markLine:
        index === 0 && params.baselines?.length
          ? {
              data: params.baselines.map((b) => ({
                label: {
                  fontSize: 9,
                  formatter: b.label,
                  position: 'insideEndTop' as const,
                },
                lineStyle: { color: b.color, type: 'dashed' as const, width: 1 },
                yAxis: b.value,
              })),
              silent: true,
              symbol: 'none' as const,
            }
          : undefined,
      name: s!.name,
      showSymbol: false,
      smooth: true,
      type: 'line' as const,
    })),
    tooltip: { trigger: 'axis' as const },
    xAxis: {
      axisLabel: { fontSize: 9, hideOverlap: true },
      boundaryGap: false,
      data: params.labels,
      type: 'category' as const,
    },
    yAxis: {
      axisLabel: { fontSize: 9 },
      max: params.max,
      min: params.min ?? 0,
      type: 'value' as const,
    },
  };
}

/** MONITORING 은 5분 간격이라 시:분만 남긴다. */
function hourMinute(value?: string) {
  return String(value ?? '')
    .split('T')[1]
    ?.slice(0, 5) ?? '';
}

/** DAILY 의 TimeSlot 에서 시(hour)만 남긴다. */
function hourOnly(value?: string) {
  const time = String(value ?? '').split(' ')[1];
  return time ? (time.split(':')[0] ?? '') : String(value ?? '');
}

function drawCharts() {
  // ── 최근 1시간 (5분 간격) ────────────────────────────────
  const monLabels = monitoring.value.map((d) => hourMinute(d.CHECK_TIME));
  const pick = (key: string) =>
    monitoring.value.map((d) => Number(d[key] ?? 0));

  renderCpu1h(
    lineOption({
      labels: monLabels,
      max: 90,
      series: [{ area: true, color: '#42A5F5', data: pick('CPU_SQL_USAGE'), name: 'CPU' }],
    }),
  );

  renderIo1h(
    lineOption({
      baselines: [{ color: 'rgba(239,83,80,0.6)', label: '기준 20ms', value: 20 }],
      labels: monLabels,
      series: [
        { area: true, color: '#EF5350', data: pick('AVG_IO_LATENCY_MS'), name: 'I/O' },
      ],
    }),
  );

  renderMem1h(
    lineOption({
      baselines: [
        { color: 'rgba(76,175,80,0.6)', label: '권장 1000s', value: 1000 },
        { color: 'rgba(244,67,54,0.6)', label: '위기 300s', value: 300 },
      ],
      labels: monLabels,
      series: [{ color: '#66BB6A', data: pick('PLE'), name: 'PLE' }],
    }),
  );

  renderTps1h(
    lineOption({
      baselines: [
        { color: 'rgba(255,152,0,0.5)', label: '상한 500', value: 500 },
        { color: 'rgba(33,150,243,0.5)', label: '하한 100', value: 100 },
      ],
      labels: monLabels,
      max: 1000,
      series: [
        { color: '#FFA726', data: pick('BATCH_REQUESTS_SEC'), name: 'TPS' },
      ],
    }),
  );

  // ── 24시간 장기 추이 ─────────────────────────────────────
  const dayLabels = daily.value.map((d) => hourOnly(d.TimeSlot));
  const pickDay = (key: string) => daily.value.map((d) => Number(d[key] ?? 0));

  renderCpu24h(
    lineOption({
      labels: dayLabels,
      max: 90,
      series: [
        { area: true, color: '#42A5F5', data: pickDay('Avg_CPU'), name: '평균 CPU(%)' },
      ],
    }),
  );

  renderIo24h(
    lineOption({
      baselines: [{ color: 'rgba(255,0,0,0.5)', label: '임계 20ms', value: 20 }],
      labels: dayLabels,
      series: [
        { area: true, color: '#EF5350', data: pickDay('Peak_IO_ms'), name: '피크 I/O(ms)' },
      ],
    }),
  );

  renderMem24h(
    lineOption({
      baselines: [
        { color: 'rgba(76,175,80,0.5)', label: '권장 1000s', value: 1000 },
        { color: 'rgba(244,67,54,0.5)', label: '위기 300s', value: 300 },
      ],
      labels: dayLabels,
      series: [
        { color: '#2196F3', data: pickDay('Avg_PLE'), name: '평균 PLE(s)' },
        { color: '#FFA726', dashed: true, data: pickDay('Min_PLE'), name: '최소 PLE(s)' },
      ],
    }),
  );

  renderStall24h(
    lineOption({
      labels: dayLabels,
      series: [
        { area: true, color: '#66BB6A', data: pickDay('Avg_TempDB_Stall'), name: 'TempDB 지연' },
        { area: true, color: '#AB47BC', data: pickDay('Avg_Log_Stall'), name: '로그 지연' },
      ],
    }),
  );
}

// ============================================================
// 상세 로그 표
// ============================================================

/** 최신 시간대가 위로 오도록 정렬한다. */
const sortedDaily = computed(() =>
  daily.value.toSorted((a, b) =>
    String(b.TimeSlot ?? '').localeCompare(String(a.TimeSlot ?? '')),
  ),
);

/** 임계를 넘긴 값은 붉게 강조한다(원본과 같은 기준). */
function overClass(over: boolean) {
  return over ? 'font-bold text-red-600' : '';
}

function toFixed(value: any, digits = 2) {
  const n = Number(value);
  return Number.isNaN(n) ? '-' : n.toFixed(digits);
}

/** 표의 시간대 칸은 '13시' 처럼 시만 보여 준다(원본과 같다). */
function timeSlotLabel(value?: string) {
  return `${String(value ?? '').split(':')[0]}시`;
}

/** 상태 칸에 붙는 태그 글자. 필터가 훑을 글자로도 쓴다. */
function stateTags(row: Record<string, any>) {
  const tags: string[] = [];
  if (Number(row.Avg_CPU) > 80) tags.push('CPU');
  if (Number(row.Avg_TempDB_Stall) > 50) tags.push('TDB');
  if (Number(row.Min_PLE) < 300) tags.push('MEM');
  return tags.length > 0 ? tags : ['정상'];
}

const [Grid] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      {
        field: 'TimeSlot',
        // 화면에 보이는 글자와 저장된 값이 다른 칸이다.
        params: { filterText: (row: any) => timeSlotLabel(row.TimeSlot) },
        slots: { default: 'TimeSlot' },
        title: '시간대',
        width: 130,
      },
      {
        field: 'Avg_CPU',
        slots: { default: 'Avg_CPU' },
        title: '평균 CPU(%)',
        width: 120,
      },
      {
        field: 'Peak_IO_ms',
        slots: { default: 'Peak_IO_ms' },
        title: '피크 I/O(ms)',
        width: 120,
      },
      {
        field: 'Avg_PLE',
        slots: { default: 'Avg_PLE' },
        title: '평균 PLE(s)',
        width: 110,
      },
      {
        field: 'Min_PLE',
        slots: { default: 'Min_PLE' },
        title: '최소 PLE(s)',
        width: 110,
      },
      {
        field: 'Avg_TempDB_Stall',
        slots: { default: 'Avg_TempDB_Stall' },
        title: 'TempDB(ms/s)',
        width: 130,
      },
      {
        field: 'Avg_Log_Stall',
        slots: { default: 'Avg_Log_Stall' },
        title: 'Log(ms/s)',
        width: 120,
      },
      {
        // 여러 지표를 종합한 판정이라 행에 없는 칸이다. 훑을 글자를 직접 준다.
        field: 'state',
        params: {
          filterText: (row: any) => stateTags(row).join(' '),
          sort: false,
        },
        slots: { default: 'state' },
        title: '상태',
        width: 150,
      },
    ],
    // 행 배열은 `:table-data` 로 간다.
    data: [],
    emptyText: '데이터가 없습니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 표는 DAILY 를 걸러 그린 것이라 네 쿼리를 함께 읽는 함수를 준다
    // (위쪽 '새로고침' · 1분 자동 갱신이 부르는 것과 같다).
    gridFeatures: { onRefresh: () => loadData() },
    height: 360,
    // 전량을 한 번에 받는 표다. 켜 두면 응답을 `{ result, page }` 로 읽어 한 행도 안 나온다.
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'TimeSlot' },
  } as any,
});

// ============================================================
// 데이터 로드
// ============================================================

async function loadData() {
  loading.value = true;
  try {
    const [rt, mon, day, k] = await Promise.all([
      getServerReport<Record<string, any>[]>('REALTIME'),
      getServerReport<Record<string, any>[]>('MONITORING'),
      getServerReport<Record<string, any>[]>('DAILY'),
      getServerReport<Record<string, any>[]>('KPI'),
    ]);

    realtime.value = rt?.[0] ?? null;
    monitoring.value = mon ?? [];
    daily.value = day ?? [];
    kpi.value = k?.[0] ?? null;

    drawCharts();
    lastUpdated.value = new Date().toLocaleTimeString('ko-KR');
  } finally {
    loading.value = false;
  }
}

function startTimer() {
  stopTimer();
  refreshTimer = setInterval(loadData, 60_000);
}

function stopTimer() {
  if (refreshTimer) clearInterval(refreshTimer);
  refreshTimer = null;
}

/** 원본과 동일하게 1분 주기 자동 갱신. 기본은 꺼짐. */
function onAutoRefreshChange(value: boolean) {
  autoRefresh.value = value;
  if (value) {
    startTimer();
  } else {
    stopTimer();
  }
}

onMounted(loadData);
onBeforeUnmount(stopTimer);
</script>

<template>
  <Page auto-content-height>
    <!-- 헤더 -->
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space>
          <span class="text-base font-semibold">실시간 모니터링</span>
          <span class="text-xs text-muted-foreground">
            업데이트 {{ lastUpdated }}
          </span>
          <Button :loading="loading" size="small" @click="loadData">
            새로고침
          </Button>
        </Space>
        <Space>
          <span class="text-xs" :class="autoRefresh ? 'font-medium' : 'text-muted-foreground'">
            1분 자동 갱신 {{ autoRefresh ? 'ON' : 'OFF' }}
          </span>
          <Switch :checked="autoRefresh" @change="onAutoRefreshChange as any" />
        </Space>
      </div>
    </Card>

    <Spin :spinning="loading">
      <!-- 1. 실시간 건강 상태 스코어카드 -->
      <Card class="mb-3" size="small">
        <template #title>
          <span class="text-xs font-semibold uppercase text-muted-foreground">
            Real-Time System Health Scorecard
          </span>
        </template>
        <template #extra>
          <Tag :color="overallColor(realtime?.Server_State)">
            {{ realtime?.Server_State ?? '상태 미상' }}
          </Tag>
        </template>

        <Row v-if="realtime" :gutter="[12, 12]">
          <Col v-for="item in scorecard" :key="item.key" :lg="6" :xs="24">
            <div class="h-full rounded border border-border p-3">
              <div class="mb-2 flex items-center justify-between">
                <span class="text-xs font-semibold uppercase text-muted-foreground">
                  {{ item.def.title }}
                </span>
                <Tag :color="item.status.color">{{ item.status.label }}</Tag>
              </div>

              <div class="mb-2 text-2xl font-bold">
                {{ item.value }}
                <span class="ml-1 text-sm font-normal">{{ item.def.unit }}</span>
              </div>

              <Divider class="my-2" />

              <div class="mb-1 w-fit rounded bg-accent px-1.5 py-0.5 text-[10px] font-semibold">
                {{ item.def.guide }}
              </div>
              <p class="m-0 text-[10px] leading-snug text-muted-foreground">
                {{ item.def.desc }}
              </p>
              <p class="m-0 mt-2 text-[11px] font-medium italic">
                "{{ item.status.advice }}"
              </p>
            </div>
          </Col>
        </Row>

        <Empty v-else description="실시간 지표를 불러오지 못했습니다." />
      </Card>

      <!-- 2. 최근 1시간 고해상도 모니터링 -->
      <div class="mb-1 ml-1 text-xs font-semibold uppercase text-muted-foreground">
        1-Hour Precision Monitoring (5m Interval)
      </div>
      <Row :gutter="[12, 12]">
        <Col :lg="6" :xs="24">
          <Card size="small" title="1H CPU">
            <EchartsUI ref="cpu1hRef" height="170px" />
          </Card>
        </Col>
        <Col :lg="6" :xs="24">
          <Card size="small" title="1H I/O">
            <EchartsUI ref="io1hRef" height="170px" />
          </Card>
        </Col>
        <Col :lg="6" :xs="24">
          <Card size="small" title="1H Memory">
            <EchartsUI ref="mem1hRef" height="170px" />
          </Card>
        </Col>
        <Col :lg="6" :xs="24">
          <Card size="small" title="1H Traffic">
            <EchartsUI ref="tps1hRef" height="170px" />
          </Card>
        </Col>
      </Row>

      <!-- 3. 24시간 장기 추이 -->
      <div class="mb-1 ml-1 mt-4 text-xs font-semibold uppercase text-muted-foreground">
        24-Hour Operational Trends Analysis
      </div>
      <Row :gutter="[12, 12]">
        <Col :lg="6" :xs="24">
          <Card size="small" title="24H CPU LOAD (%)">
            <EchartsUI ref="cpu24hRef" height="170px" />
          </Card>
        </Col>
        <Col :lg="6" :xs="24">
          <Card size="small" title="24H I/O LATENCY (ms)">
            <EchartsUI ref="io24hRef" height="170px" />
          </Card>
        </Col>
        <Col :lg="6" :xs="24">
          <Card size="small" title="24H MEMORY STABILITY">
            <EchartsUI ref="mem24hRef" height="170px" />
          </Card>
        </Col>
        <Col :lg="6" :xs="24">
          <Card size="small" title="24H SUBSYSTEM STALLS">
            <EchartsUI ref="stall24hRef" height="170px" />
          </Card>
        </Col>
      </Row>

      <!-- 4. KPI 요약 -->
      <Card v-if="kpi" class="mt-4" size="small" title="누적 KPI">
        <Row :gutter="[12, 12]">
          <Col :lg="6" :xs="12">
            <div class="text-xs text-muted-foreground">점검 횟수</div>
            <div class="text-lg font-semibold">{{ kpi.Total_Checks ?? 0 }}</div>
          </Col>
          <Col :lg="6" :xs="12">
            <div class="text-xs text-muted-foreground">최저 PLE</div>
            <div class="text-lg font-semibold">{{ kpi.Min_PLE ?? 0 }}</div>
          </Col>
          <Col :lg="6" :xs="12">
            <div class="text-xs text-muted-foreground">데이터파일 지연(s)</div>
            <div class="text-lg font-semibold">
              {{ toFixed(kpi.Avg_DataFile_Stall_sec, 3) }}
            </div>
          </Col>
          <Col :lg="6" :xs="12">
            <div class="text-xs text-muted-foreground">로그 지연(s)</div>
            <div class="text-lg font-semibold">
              {{ toFixed(kpi.Avg_Log_Stall_sec, 3) }}
            </div>
          </Col>
        </Row>
      </Card>

      <!-- 5. 시간대별 상세 로그 -->
      <Card
        :body-style="{ padding: 0 }"
        class="mt-4"
        size="small"
        title="24시간 시간대별 정밀 성능 지표 로그"
      >
        <Grid :table-data="sortedDaily">
          <template #TimeSlot="{ row }">{{ timeSlotLabel(row.TimeSlot) }}</template>

          <template #Avg_CPU="{ row }">
            <span :class="overClass(Number(row.Avg_CPU) > 80)">
              {{ toFixed(row.Avg_CPU) }}%
            </span>
          </template>

          <template #Peak_IO_ms="{ row }">
            <span :class="overClass(Number(row.Peak_IO_ms) > 20)">
              {{ toFixed(row.Peak_IO_ms) }}
            </span>
          </template>

          <template #Avg_PLE="{ row }">{{ toFixed(row.Avg_PLE, 0) }}</template>

          <template #Min_PLE="{ row }">
            <span
              :class="
                Number(row.Min_PLE) < 300
                  ? 'font-bold text-red-600'
                  : 'text-green-700 dark:text-green-400'
              "
            >
              {{ row.Min_PLE }}
            </span>
          </template>

          <template #Avg_TempDB_Stall="{ row }">
            <span :class="overClass(Number(row.Avg_TempDB_Stall) > 50)">
              {{ toFixed(row.Avg_TempDB_Stall) }}
            </span>
          </template>

          <template #Avg_Log_Stall="{ row }">
            <span :class="overClass(Number(row.Avg_Log_Stall) > 50)">
              {{ toFixed(row.Avg_Log_Stall) }}
            </span>
          </template>

          <template #state="{ row }">
            <Space :size="4">
              <Tag
                v-for="tag in stateTags(row)"
                :key="tag"
                :color="tag === '정상' ? 'success' : 'error'"
              >
                {{ tag }}
              </Tag>
            </Space>
          </template>
        </Grid>
      </Card>
    </Spin>
  </Page>
</template>
