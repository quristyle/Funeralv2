<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import {
  Alert,
  Button,
  Card,
  Col,
  Divider,
  Empty,
  Progress,
  Row,
  Space,
  Spin,
  Tag,
} from 'ant-design-vue';

import { getServerReport } from '#/api/helpdesk';

/**
 * [가용성 분석]
 *
 * 원본(JinReception reports/SystemAvailability.vue, `/reports/availability`).
 * KPI(24시간 집계) · REALTIME(현재) · MONITORING(최근 1시간)을 함께 읽어
 * SLA 목표 대비 가용성과 실시간 변동(drift), 예측 소견까지 만든다.
 */

const loading = ref(false);
const kpi = ref<null | Record<string, any>>(null);
const realtime = ref<null | Record<string, any>>(null);
const monitoring = ref<Record<string, any>[]>([]);

const pleChartRef = ref<EchartsUIType>();
const ioChartRef = ref<EchartsUIType>();
const { renderEcharts: renderPle } = useEcharts(pleChartRef);
const { renderEcharts: renderIo } = useEcharts(ioChartRef);

// ============================================================
// KPI 판정 — 원본의 getKpiStatus / kpiGlossary
// ============================================================

/** SLA 지표 정의 */
const GLOSSARY = {
  dataFile: {
    desc: '데이터 파일을 디스크에서 읽어올 때 발생하는 평균 지연 시간입니다.',
    goal: '목표: 0.100s 미만',
    title: '데이터 읽기 가용성',
  },
  log: {
    desc: '로그 파일 기록 시 발생하는 지연 시간으로, CUD 작업의 성능을 결정합니다.',
    goal: '목표: 0.050s 미만',
    title: '트랜잭션 처리 가용성',
  },
  minPle: {
    desc: '최근 24시간 중 메모리 압박이 가장 심했던 순간의 데이터 생존 시간입니다.',
    goal: '목표: 1000s 이상',
    title: '메모리 안전성 (Min PLE)',
  },
  tempdb: {
    desc: 'TempDB 작업(정렬, 조인 등) 시 발생하는 초당 평균 지연 시간입니다.',
    goal: '목표: 0.050s 미만',
    title: '임시 DB 서비스 품질',
  },
};

/** PLE 판정 */
function pleStatus(value: number) {
  if (value >= 1000) {
    return { color: 'success', advice: '메모리 자원이 매우 여유롭습니다.', label: '최상' };
  }
  if (value >= 300) {
    return {
      advice: '정상 범위이나 모니터링이 필요합니다.',
      color: 'processing',
      label: '보통',
    };
  }
  return {
    advice: '메모리 고갈! 즉각적인 조치가 필요합니다.',
    color: 'error',
    label: '위기',
  };
}

/** I/O 계열(데이터 읽기 / TempDB / 로그) 판정. 임계값만 다르다. */
function stallStatus(value: number, threshold: number) {
  if (value <= threshold * 0.5) {
    return { advice: 'I/O 응답 속도가 매우 빠릅니다.', color: 'success', label: '쾌적' };
  }
  if (value <= threshold) {
    return {
      advice: '운영 목표 수치 내에서 작동 중입니다.',
      color: 'processing',
      label: '정상',
    };
  }
  if (value <= threshold * 2) {
    return { advice: '디스크 경합이 감지됩니다.', color: 'warning', label: '지체' };
  }
  return {
    advice: '심각한 I/O 병목이 발생하고 있습니다.',
    color: 'error',
    label: '병목',
  };
}

/** 서브시스템 3종 카드 */
const subsystemCards = computed(() => {
  if (!kpi.value) return [];

  const dataFile = Number(kpi.value.Avg_DataFile_Stall_sec ?? 0);
  const tempdb = Number(kpi.value.Avg_TempDB_Stall_sec ?? 0);
  const log = Number(kpi.value.Avg_Log_Stall_sec ?? 0);

  return [
    { def: GLOSSARY.dataFile, key: 'dataFile', status: stallStatus(dataFile, 0.1), value: dataFile },
    { def: GLOSSARY.tempdb, key: 'tempdb', status: stallStatus(tempdb, 0.05), value: tempdb },
    { def: GLOSSARY.log, key: 'log', status: stallStatus(log, 0.05), value: log },
  ];
});

const minPleStatus = computed(() => pleStatus(Number(kpi.value?.Min_PLE ?? 0)));

// ============================================================
// 전문가 진단 — 원본의 diagnosticReport
// ============================================================

const diagnostic = computed(() => {
  if (!kpi.value || !realtime.value) return null;

  const currentPle = Number(realtime.value.PLE ?? 0);
  const minPle = Number(kpi.value.Min_PLE ?? 0);
  const pleDrift = currentPle - minPle;

  // 최근 1시간 I/O 평균. 표본이 없으면 실시간 값으로 대체한다.
  const recentIoAvg =
    monitoring.value.length > 0
      ? monitoring.value.reduce(
          (acc, cur) => acc + Number(cur.AVG_IO_LATENCY_MS ?? 0),
          0,
        ) / monitoring.value.length
      : Number(realtime.value.AVG_IO_LATENCY_MS ?? 0);

  // KPI 는 초 단위, 실시간은 ms 단위라 초로 맞춰 비교한다.
  const baseline = Number(kpi.value.Avg_DataFile_Stall_sec ?? 0);
  const ioDriftPct =
    baseline > 0 ? ((recentIoAvg / 1000 - baseline) / baseline) * 100 : 0;

  if (currentPle < 300) {
    return {
      actionPriority: 'URGENT',
      insight:
        '현재 PLE가 임계치(300s) 미만으로, 메모리 내부의 데이터 페이지 교체가 비정상적으로 빈번합니다. 이는 쿼리 성능의 급격한 저하를 초래합니다.',
      ioDriftPct,
      pleDrift,
      prediction:
        '1시간 이내에 특정 대용량 쿼리 실행 시 시스템 프리징 또는 타임아웃 발생 가능성이 80% 이상입니다.',
      recentIoAvg,
    };
  }

  if (ioDriftPct > 50) {
    return {
      actionPriority: 'HIGH',
      insight: `24시간 평균 대비 최근 I/O 지연이 ${ioDriftPct.toFixed(1)}% 급증했습니다. 물리적 디스크 경합 또는 특정 백업/인덱스 작업이 진행 중일 가능성이 높습니다.`,
      ioDriftPct,
      pleDrift,
      prediction:
        '현 상태 지속 시 로그 기록(LOG STALL) 지연으로 이어져 전체 트랜잭션 처리량이 30% 감소할 것으로 예측됩니다.',
      recentIoAvg,
    };
  }

  return {
    actionPriority: 'NORMAL',
    insight:
      '전반적인 지표가 SLA 범위 내에서 안정적으로 유지되고 있습니다. 실시간 부하 변동폭이 낮아 예측 가능성이 높습니다.',
    ioDriftPct,
    pleDrift,
    prediction: '향후 4시간 동안 현재의 안정적인 처리량(TPS)이 유지될 것으로 보입니다.',
    recentIoAvg,
  };
});

/** 위험도 태그 색 */
const priorityColor = computed(() => {
  const p = diagnostic.value?.actionPriority;
  if (p === 'URGENT') return 'error';
  if (p === 'HIGH') return 'warning';
  return 'success';
});

/** 처리 효율 지수. 원본과 동일한 계산식. */
const efficiencyIndex = computed(() =>
  diagnostic.value
    ? (100 - Math.max(0, diagnostic.value.ioDriftPct / 2)).toFixed(1)
    : '-',
);

const RECOMMENDED_ACTIONS = [
  '임계치 초과 쿼리 실시간 락(Lock) 모니터링 강화',
  '트랜잭션 로그 플러시 주기 및 체크포인트 점검',
];

// ============================================================
// 1시간 추이 차트
// ============================================================

function hourMinute(value?: string) {
  return String(value ?? '')
    .split('T')[1]
    ?.slice(0, 5) ?? '';
}

function drawCharts() {
  if (!kpi.value || monitoring.value.length === 0) return;

  const labels = monitoring.value.map((d) => hourMinute(d.CHECK_TIME));
  const minPle = Number(kpi.value.Min_PLE ?? 0);
  const ioBaselineMs = Number(kpi.value.Avg_DataFile_Stall_sec ?? 0) * 1000;

  renderPle({
    grid: { bottom: 26, containLabel: true, left: 10, right: 12, top: 30 },
    legend: { textStyle: { fontSize: 9 }, top: 0, type: 'scroll' },
    series: [
      {
        areaStyle: { opacity: 0.12 },
        data: monitoring.value.map((d) => Number(d.PLE ?? 0)),
        itemStyle: { color: '#66BB6A' },
        markLine: {
          data: [
            {
              label: { fontSize: 9, formatter: `24H 최소 ${minPle}s` },
              lineStyle: { color: '#FF7043', type: 'dashed' as const },
              yAxis: minPle,
            },
            {
              label: { fontSize: 9, formatter: '표준 1000s' },
              lineStyle: { color: 'rgba(76,175,80,0.6)', type: 'dashed' as const },
              yAxis: 1000,
            },
          ],
          silent: true,
          symbol: 'none' as const,
        },
        name: '실시간 PLE (s)',
        showSymbol: false,
        smooth: true,
        type: 'line',
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: {
      axisLabel: { fontSize: 9, hideOverlap: true },
      boundaryGap: false,
      data: labels,
      type: 'category',
    },
    yAxis: { axisLabel: { fontSize: 9 }, type: 'value' },
  });

  renderIo({
    grid: { bottom: 26, containLabel: true, left: 10, right: 12, top: 30 },
    legend: { textStyle: { fontSize: 9 }, top: 0, type: 'scroll' },
    series: [
      {
        areaStyle: { opacity: 0.12 },
        data: monitoring.value.map((d) => Number(d.AVG_IO_LATENCY_MS ?? 0)),
        itemStyle: { color: '#42A5F5' },
        markLine: {
          data: [
            {
              label: {
                fontSize: 9,
                formatter: `24H 평균 ${ioBaselineMs.toFixed(1)}ms`,
              },
              lineStyle: { color: 'rgba(66,165,245,0.6)', type: 'dashed' as const },
              yAxis: ioBaselineMs,
            },
            {
              label: { fontSize: 9, formatter: '관리 임계 20ms' },
              lineStyle: { color: 'rgba(239,83,80,0.6)', type: 'dashed' as const },
              yAxis: 20,
            },
          ],
          silent: true,
          symbol: 'none' as const,
        },
        name: '실시간 I/O (ms)',
        showSymbol: false,
        smooth: true,
        type: 'line',
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: {
      axisLabel: { fontSize: 9, hideOverlap: true },
      boundaryGap: false,
      data: labels,
      type: 'category',
    },
    yAxis: { axisLabel: { fontSize: 9 }, min: 0, type: 'value' },
  });
}

function toFixed(value: any, digits: number) {
  const n = Number(value);
  return Number.isNaN(n) ? '-' : n.toFixed(digits);
}

async function loadData() {
  loading.value = true;
  try {
    const [k, rt, mon] = await Promise.all([
      getServerReport<Record<string, any>[]>('KPI'),
      getServerReport<Record<string, any>[]>('REALTIME'),
      getServerReport<Record<string, any>[]>('MONITORING'),
    ]);

    kpi.value = k?.[0] ?? null;
    realtime.value = rt?.[0] ?? null;
    monitoring.value = mon ?? [];
    drawCharts();
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
          시스템 가용성 및 서비스 수준(SLA) 정밀 진단
        </span>
        <Button :loading="loading" type="primary" @click="loadData">
          지표 갱신
        </Button>
      </div>
    </Card>

    <Spin :spinning="loading">
      <Empty v-if="!kpi && !loading" description="지표를 불러오지 못했습니다." />

      <template v-else-if="kpi">
        <!-- 1. 분석 표본 -->
        <Card class="mb-3" size="small">
          <div class="text-center">
            <span class="text-sm font-medium">
              최근 24시간 서비스 가용성 분석 표본:
            </span>
            <span class="ml-1 text-lg font-bold text-blue-600">
              {{ Number(kpi.Total_Checks ?? 0).toLocaleString() }}
            </span>
            <span class="ml-1 text-xs text-muted-foreground">회 수집</span>
          </div>
        </Card>

        <!-- 2. 메모리 가용성 -->
        <Card class="mb-3" size="small">
          <template #title>
            <span class="text-xs font-semibold uppercase text-muted-foreground">
              Memory Stability (SLA)
            </span>
          </template>
          <template #extra>
            <Space>
              <Tag :color="minPleStatus.color">{{ minPleStatus.label }}</Tag>
              <Tag :color="Number(kpi.Min_PLE) >= 300 ? 'success' : 'error'">
                {{ Number(kpi.Min_PLE) >= 300 ? '가용성 확보' : '가용성 위기' }}
              </Tag>
            </Space>
          </template>

          <div class="flex flex-col items-center gap-4 py-4 md:flex-row md:justify-around">
            <div class="text-center">
              <div class="mb-1 text-[10px] font-semibold text-muted-foreground">
                최소 메모리 수명
              </div>
              <div
                class="text-5xl font-bold"
                :class="
                  minPleStatus.color === 'error'
                    ? 'text-red-600'
                    : 'text-orange-600'
                "
              >
                {{ kpi.Min_PLE }}s
              </div>
            </div>

            <div class="max-w-md text-center md:text-left">
              <Space class="mb-2" wrap>
                <span class="rounded bg-accent px-2 py-1 text-xs font-semibold">
                  {{ GLOSSARY.minPle.goal }}
                </span>
                <span class="text-[11px] font-semibold italic">
                  "{{ minPleStatus.advice }}"
                </span>
              </Space>
              <p class="m-0 text-[11px] leading-relaxed text-muted-foreground">
                {{ GLOSSARY.minPle.desc }}
              </p>
            </div>
          </div>
        </Card>

        <!-- 3. 서브시스템별 가용성 -->
        <Row :gutter="[12, 12]">
          <Col v-for="card in subsystemCards" :key="card.key" :lg="8" :xs="24">
            <Card size="small">
              <template #title>
                <span class="text-xs font-semibold uppercase text-muted-foreground">
                  {{ card.def.title }}
                </span>
              </template>
              <template #extra>
                <Tag :color="card.status.color">{{ card.status.label }}</Tag>
              </template>

              <div class="flex flex-col items-center py-2">
                <div
                  class="text-lg font-bold"
                  :class="card.status.color === 'error' ? 'text-red-600' : ''"
                >
                  {{ toFixed(card.value, 3) }} <span class="text-sm">s</span>
                </div>
                <span class="mt-2 text-[10px] font-semibold">
                  {{ card.def.goal }}
                </span>
                <p class="m-0 mt-1 text-[10px] font-semibold italic">
                  "{{ card.status.advice }}"
                </p>
                <Divider class="my-2" />
                <p class="m-0 text-center text-[10px] leading-tight text-muted-foreground">
                  {{ card.def.desc }}
                </p>
              </div>
            </Card>
          </Col>
        </Row>

        <!-- 4. 실시간 변동(Drift) -->
        <div class="mb-2 ml-1 mt-4 text-sm font-semibold uppercase">
          Real-time Operational Drift (Current vs 24H Avg)
        </div>
        <Row v-if="diagnostic && realtime" :gutter="[12, 12]">
          <Col :lg="12" :xs="24">
            <Card size="small">
              <div class="flex items-end justify-between">
                <span class="text-[11px] font-semibold text-muted-foreground">
                  메모리 가용성 변동 (Current PLE vs Min PLE)
                </span>
                <span
                  class="text-xs font-bold"
                  :class="diagnostic.pleDrift >= 0 ? 'text-green-600' : 'text-red-600'"
                >
                  {{ diagnostic.pleDrift >= 0 ? '+' : '' }}{{ diagnostic.pleDrift }}s
                  {{ diagnostic.pleDrift >= 0 ? '개선' : '악화' }}
                </span>
              </div>

              <Progress
                class="my-2"
                :percent="
                  Math.min(100, Math.max(0, (Number(realtime.PLE) / 2000) * 100))
                "
                :show-info="false"
                size="small"
              />

              <div class="flex justify-between text-[10px] font-semibold text-muted-foreground">
                <span>KPI Min: {{ kpi.Min_PLE }}s</span>
                <span>Current: {{ realtime.PLE }}s</span>
              </div>
            </Card>
          </Col>

          <Col :lg="12" :xs="24">
            <Card size="small">
              <div class="flex items-end justify-between">
                <span class="text-[11px] font-semibold text-muted-foreground">
                  I/O 응답성 변동 (1H Avg vs 24H Avg)
                </span>
                <span
                  class="text-xs font-bold"
                  :class="diagnostic.ioDriftPct <= 0 ? 'text-green-600' : 'text-red-600'"
                >
                  {{ diagnostic.ioDriftPct > 0 ? '+' : ''
                  }}{{ diagnostic.ioDriftPct.toFixed(1) }}%
                  {{ diagnostic.ioDriftPct <= 0 ? '안정' : '부하증가' }}
                </span>
              </div>

              <Progress
                class="my-2"
                :percent="Math.min(100, diagnostic.recentIoAvg)"
                :show-info="false"
                size="small"
                status="active"
              />

              <div class="flex justify-between text-[10px] font-semibold text-muted-foreground">
                <span>
                  KPI Avg:
                  {{ toFixed(Number(kpi.Avg_DataFile_Stall_sec) * 1000, 1) }}ms
                </span>
                <span>1H Avg: {{ diagnostic.recentIoAvg.toFixed(1) }}ms</span>
              </div>
            </Card>
          </Col>
        </Row>

        <!-- 5. 1시간 세부 추이 -->
        <div class="mb-2 ml-1 mt-4 text-sm font-semibold uppercase">
          1-Hour Detailed Operational Trend Analysis
        </div>
        <Row :gutter="[12, 12]">
          <Col :lg="12" :xs="24">
            <Card size="small" title="메모리 안정성 추이 (Memory Life Expectancy)">
              <EchartsUI ref="pleChartRef" height="200px" />
              <Alert
                class="mt-2"
                message="오렌지색 점선(24H 최소값) 대비 실시간 선이 상단에 위치할수록 현재 메모리 가용성이 개선되고 있음을 의미합니다."
                type="success"
              />
            </Card>
          </Col>

          <Col :lg="12" :xs="24">
            <Card size="small" title="I/O 응답성 추이 (Disk Response Latency)">
              <EchartsUI ref="ioChartRef" height="200px" />
              <Alert
                class="mt-2"
                message="파란색 점선(24H 평균) 대비 실시간 선이 하단에 위치할수록 현재 디스크 응답 속도가 표준보다 우수함을 의미합니다."
                type="info"
              />
            </Card>
          </Col>
        </Row>

        <!-- 6. 전문가 예측 및 권고 -->
        <Card v-if="diagnostic" class="mt-4" size="small">
          <template #title>
            <span class="text-sm font-semibold">
              Expert Predictive Health Report &amp; Insight
            </span>
          </template>
          <template #extra>
            <Tag :color="priorityColor">
              위험도: {{ diagnostic.actionPriority }}
            </Tag>
          </template>

          <Row :gutter="[12, 12]">
            <Col :lg="14" :xs="24">
              <div class="mb-2 text-xs font-semibold uppercase">
                현시점 정밀 진단 소견
              </div>
              <p class="m-0 text-[12px] font-medium leading-relaxed">
                {{ diagnostic.insight }}
              </p>

              <Divider class="my-3" />

              <Alert type="info">
                <template #message>
                  <span class="text-[11px] leading-snug">
                    최근 1시간 동안의 <b>{{ monitoring.length }}개</b> 데이터
                    세그먼트를 분석한 결과, 시스템의 처리 효율(Efficiency Index)은
                    약 <b>{{ efficiencyIndex }}%</b> 수준으로 평가됩니다.
                  </span>
                </template>
              </Alert>
            </Col>

            <Col :lg="10" :xs="24">
              <div class="rounded bg-primary p-3 text-primary-foreground">
                <div class="mb-2 text-[11px] font-semibold uppercase tracking-wider opacity-80">
                  향후 서비스 가용성 예측
                </div>
                <p class="m-0 text-[13px] font-semibold leading-relaxed">
                  "{{ diagnostic.prediction }}"
                </p>
              </div>

              <div class="mt-2 rounded border-2 border-dashed border-border p-3">
                <div class="mb-2 text-[10px] font-semibold uppercase text-muted-foreground">
                  Recommended Operational Action
                </div>
                <ul class="m-0 list-none space-y-2 p-0">
                  <li
                    v-for="action in RECOMMENDED_ACTIONS"
                    :key="action"
                    class="text-[11px] font-medium"
                  >
                    ✓ {{ action }}
                  </li>
                </ul>
              </div>
            </Col>
          </Row>
        </Card>
      </template>
    </Spin>
  </Page>
</template>
