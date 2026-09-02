<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import { onMounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import { Card, Col, Row, Select, Space, Spin, Tag } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import GridIconButton from '#/components/GridIconButton.vue';
import { getMonthlyReport } from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import {
  formatDate,
  REQUEST_STATUSES,
  REQUEST_TYPES,
} from '../shared/constants';

/**
 * [유지보수 보고서]
 *
 * 원본(JinReception maintenance/MaintenanceReport.vue, `/maintenance-report`).
 * 월 단위 처리 실적을 회사별로 본다.
 *
 * 원본과 같은 구성:
 *  - 기본 조회 기간은 '지난달'
 *  - KPI 1행 5칸(총 접수·완료·종료·처리율·평균 처리 시간)
 *  - KPI 2행 4칸(대기·진행·협의·논의)
 *  - 차트 4종(상태별 / 유형별 / 처리 소요 시간 분포 / 일별 접수·완료 추세)
 *  - 최근 완료 내역 Top 10
 *
 * 고객으로 연결된 계정은 자기 회사로 고정된다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] 아래쪽 '최근 완료 내역' 표를 ant-design-vue `<Table>` 에서
 * `useVbenVxeGrid` 로 옮겼다. 정렬·필터는 공통 레이어
 * (`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * **가져오기 방식은 그대로다** — 월간 보고 응답에 실려 오는 Top 10 을
 * 그대로 그린다(페이저 없음). 행을 누르면 요청 상세로 가는 것도 그대로다.
 * ------------------------------------------------------------
 */

const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);

// 원본과 동일하게 지난달을 기본값으로 잡는다.
const lastMonth = new Date();
lastMonth.setMonth(lastMonth.getMonth() - 1);

const selectedYear = ref(lastMonth.getFullYear());
const selectedMonth = ref(lastMonth.getMonth() + 1);
const selectedCompany = ref<number | undefined>();

/** 최근 5년 */
const YEAR_OPTIONS = Array.from({ length: 5 }, (_, i) => {
  const y = new Date().getFullYear() - i;
  return { label: `${y}년`, value: y };
});
const MONTH_OPTIONS = Array.from({ length: 12 }, (_, i) => ({
  label: `${i + 1}월`,
  value: i + 1,
}));

const stats = ref({
  averageResolutionTimeHours: 0,
  completedRequests: 0,
  consultationRequests: 0,
  inProgressRequests: 0,
  negotiationRequests: 0,
  pendingRequests: 0,
  recentCompletedItems: [] as Record<string, any>[],
  resolutionRate: 0,
  totalRequests: 0,
  userCompletedRequests: 0,
});

const statusChartRef = ref<EchartsUIType>();
const typeChartRef = ref<EchartsUIType>();
const durationChartRef = ref<EchartsUIType>();
const trendChartRef = ref<EchartsUIType>();

const { renderEcharts: renderStatus } = useEcharts(statusChartRef);
const { renderEcharts: renderType } = useEcharts(typeChartRef);
const { renderEcharts: renderDuration } = useEcharts(durationChartRef);
const { renderEcharts: renderTrend } = useEcharts(trendChartRef);

const PIE_COLORS = [
  '#42A5F5',
  '#66BB6A',
  '#FFA726',
  '#FF7043',
  '#AB47BC',
  '#26A69A',
];
const DURATION_COLORS = ['#4DB6AC', '#81C784', '#FFD54F', '#FF8A65'];

/** KPI 1행. 원본의 5칸 구성과 색을 맞췄다. */
const primaryKpis = ref<
  { key: string; label: string; suffix: string; tone: string }[]
>([
  { key: 'totalRequests', label: '총 접수', suffix: '건', tone: 'text-blue-500' },
  { key: 'completedRequests', label: '완료', suffix: '건', tone: 'text-green-600' },
  {
    key: 'userCompletedRequests',
    label: '종료',
    suffix: '건',
    tone: 'text-emerald-500',
  },
]);

/** KPI 2행. 진행 중인 상태 4칸. */
const activeKpis = [
  { key: 'pendingRequests', label: '대기', tone: 'text-orange-500' },
  { key: 'inProgressRequests', label: '진행', tone: 'text-yellow-500' },
  { key: 'consultationRequests', label: '협의', tone: 'text-indigo-500' },
  { key: 'negotiationRequests', label: '논의', tone: 'text-pink-500' },
];

const [RecentGrid] = useVbenVxeGrid({
  gridEvents: {
    // 원본의 `custom-row` 클릭과 같은 자리 — 행을 누르면 요청 상세로 간다.
    cellClick: ({ row }: any) => {
      if (row?.id) router.push(`/helpdesk/request/detail/${row.id}`);
    },
  },
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      { field: 'title', minWidth: 240, title: '제목' },
      {
        field: 'status',
        // 값이 정해진 칸이다. 화면에는 서버가 준 이름(`Completed`)이 그대로
        // 찍히지만, 고르는 칸은 한글 이름표로 보여 준다.
        params: {
          filterOptions: REQUEST_STATUSES.map((s) => ({
            label: s.label,
            value: s.value,
          })),
        },
        slots: { default: 'status' },
        title: '상태',
        width: 130,
      },
      {
        field: 'type',
        params: {
          filterOptions: REQUEST_TYPES.map((t) => ({
            label: t.label,
            value: t.value,
          })),
        },
        title: '유형',
        width: 110,
      },
      {
        field: 'requestedAt',
        params: { filterText: (row: any) => formatDate(row.requestedAt) },
        slots: { default: 'requestedAt' },
        title: '요청일',
        width: 120,
      },
      {
        field: 'completedAt',
        params: { filterText: (row: any) => formatDate(row.completedAt) },
        slots: { default: 'completedAt' },
        title: '완료일',
        width: 120,
      },
    ],
    // 행 배열은 `:table-data` 로 간다. 여기는 빈 배열이 바탕값이다.
    data: [],
    emptyText: '완료된 요청이 없습니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // Top 10 은 월간 보고 응답에 실려 오므로 '조회' 가 부르는 함수를 그대로 준다.
    gridFeatures: { onRefresh: () => loadData() },
    height: 340,
    // 전량(Top 10) 조회다. 페이저를 끄지 않으면 한 줄도 안 그려진다.
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'id' },
  } as any,
});

/** 시간을 '3일 4시간' 처럼 읽기 쉽게 바꾼다. 원본 formatDuration 과 같다. */
function formatDuration(hours?: number) {
  const h = Number(hours ?? 0);
  if (!h) return '0시간';
  if (h < 24) return `${h.toFixed(1)}시간`;
  const days = Math.floor(h / 24);
  const remain = (h % 24).toFixed(0);
  return `${days}일 ${remain}시간`;
}

/** 배열/객체 두 형태로 오는 집계를 {name, value} 로 통일한다. */
function pairsOf(obj: any): { name: string; value: number }[] {
  if (Array.isArray(obj)) {
    return obj.map((o) => ({ name: String(o.key), value: Number(o.value) }));
  }
  return Object.entries(obj ?? {}).map(([name, value]) => ({
    name,
    value: Number(value),
  }));
}

function drawCharts(payload: any) {
  // 상태별 — 원형
  const statusPairs = pairsOf(payload?.requestsByStatus);
  renderStatus({
    legend: { bottom: 0, type: 'scroll' },
    series: [
      {
        color: PIE_COLORS,
        data: statusPairs,
        // 원본은 차트 위에 값을 직접 찍었다(customDataLabels 플러그인).
        label: { formatter: '{b}\n{c}', fontSize: 11 },
        name: '상태별',
        radius: '65%',
        type: 'pie',
      },
    ],
    tooltip: { trigger: 'item' },
  });

  // 유형별 — 막대
  const typePairs = pairsOf(payload?.requestsByType);
  renderType({
    grid: { bottom: 24, containLabel: true, left: 10, right: 12, top: 24 },
    series: [
      {
        barMaxWidth: 42,
        data: typePairs.map((p) => p.value),
        itemStyle: { color: '#5C6BC0' },
        label: { position: 'top', show: true },
        name: '요청 유형',
        type: 'bar',
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: {
      axisLabel: { fontSize: 10, interval: 0 },
      data: typePairs.map((p) => p.name),
      type: 'category',
    },
    yAxis: { minInterval: 1, type: 'value' },
  });

  // 처리 소요 시간 분포 — 막대
  const durationPairs = pairsOf(payload?.resolutionTimeDistribution);
  renderDuration({
    grid: { bottom: 24, containLabel: true, left: 10, right: 12, top: 24 },
    series: [
      {
        barMaxWidth: 42,
        data: durationPairs.map((p, index) => ({
          itemStyle: { color: DURATION_COLORS[index % DURATION_COLORS.length] },
          value: p.value,
        })),
        label: { position: 'top', show: true },
        name: '처리 소요 시간',
        type: 'bar',
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: {
      axisLabel: { fontSize: 10, interval: 0 },
      data: durationPairs.map((p) => p.name),
      type: 'category',
    },
    yAxis: { minInterval: 1, type: 'value' },
  });

  // 일별 접수·완료 추세 — 선
  const daily: any[] = payload?.dailyStats ?? [];
  renderTrend({
    grid: { bottom: 26, containLabel: true, left: 10, right: 12, top: 30 },
    legend: { top: 0 },
    series: [
      {
        data: daily.map((d) => Number(d.requestCount ?? 0)),
        itemStyle: { color: '#42A5F5' },
        name: '접수',
        smooth: true,
        type: 'line',
      },
      {
        data: daily.map((d) => Number(d.completedCount ?? 0)),
        itemStyle: { color: '#66BB6A' },
        name: '완료',
        smooth: true,
        type: 'line',
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: {
      data: daily.map((d) => `${d.day}일`),
      type: 'category',
    },
    yAxis: { minInterval: 1, type: 'value' },
  });
}

async function loadData() {
  loading.value = true;
  try {
    // 고객으로 연결된 계정은 자기 회사만 볼 수 있다.
    const companyId = helpdesk.isAdmin
      ? selectedCompany.value
      : helpdesk.companyId;

    const payload = await getMonthlyReport(
      selectedYear.value,
      selectedMonth.value,
      companyId,
    );
    if (!payload) return;

    stats.value = {
      averageResolutionTimeHours: payload.averageResolutionTimeHours ?? 0,
      completedRequests: payload.completedRequests ?? 0,
      consultationRequests: payload.consultationRequests ?? 0,
      inProgressRequests: payload.inProgressRequests ?? 0,
      negotiationRequests: payload.negotiationRequests ?? 0,
      pendingRequests: payload.pendingRequests ?? 0,
      recentCompletedItems: payload.recentCompletedItems ?? [],
      resolutionRate: payload.resolutionRate ?? 0,
      totalRequests: payload.totalRequests ?? 0,
      userCompletedRequests: payload.userCompletedRequests ?? 0,
    };

    drawCharts(payload);
  } finally {
    loading.value = false;
  }
}

/** 완료 계열이면 초록, 나머지는 파랑. 원본과 같은 기준. */
function statusColor(status?: string) {
  return status === 'Completed' || status === 'UserCompleted'
    ? 'success'
    : 'processing';
}

// 조회 조건이 바뀌면 바로 다시 읽는다(원본의 watch 와 동일).
watch([selectedYear, selectedMonth, selectedCompany], loadData);

onMounted(async () => {
  await helpdesk.loadIdentity();
  if (helpdesk.isAdmin) await helpdesk.loadOrganizations();
  await loadData();
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <!-- 조회 조건 -->
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space wrap>
          <Select
            v-if="helpdesk.isAdmin"
            v-model:value="selectedCompany"
            :options="helpdesk.companyOptions.filter((o) => o.value !== null)"
            allow-clear
            option-filter-prop="label"
            placeholder="전체 회사"
            show-search
            style="width: 190px"
          />
          <Select
            v-model:value="selectedYear"
            :options="YEAR_OPTIONS"
            style="width: 110px"
          />
          <Select
            v-model:value="selectedMonth"
            :options="MONTH_OPTIONS"
            style="width: 90px"
          />
        </Space>
        <GridIconButton
          :loading="loading"
          icon="vxe-icon-search"
          title="조회"
          @click="loadData"
        />
      </div>
    </Card>

    <Spin :spinning="loading">
      <!-- KPI 1행 -->
      <Row :gutter="[12, 12]">
        <Col v-for="kpi in primaryKpis" :key="kpi.key" :lg="5" :xs="12">
          <Card size="small">
            <div class="mb-1 text-sm font-semibold" :class="kpi.tone">
              {{ kpi.label }}
            </div>
            <div class="text-lg font-bold">
              {{ (stats as any)[kpi.key] }}{{ kpi.suffix }}
            </div>
          </Card>
        </Col>

        <Col :lg="4" :xs="12">
          <Card size="small">
            <div class="mb-1 text-sm font-semibold text-purple-500">처리율</div>
            <div class="text-lg font-bold">
              {{ Number(stats.resolutionRate).toFixed(1) }}%
            </div>
          </Card>
        </Col>

        <Col :lg="5" :xs="24">
          <Card size="small">
            <div class="mb-1 text-sm font-semibold text-teal-500">
              평균 처리 시간
            </div>
            <div class="text-lg font-bold">
              {{ formatDuration(stats.averageResolutionTimeHours) }}
            </div>
          </Card>
        </Col>
      </Row>

      <!-- KPI 2행 -->
      <Row :gutter="[12, 12]" class="mt-3">
        <Col v-for="kpi in activeKpis" :key="kpi.key" :lg="6" :xs="12">
          <Card size="small">
            <div class="mb-1 text-sm font-semibold" :class="kpi.tone">
              {{ kpi.label }}
            </div>
            <div class="text-lg font-bold">{{ (stats as any)[kpi.key] }}건</div>
          </Card>
        </Col>
      </Row>

      <!-- 차트 1행 -->
      <Row :gutter="[12, 12]" class="mt-3">
        <Col :lg="8" :xs="24">
          <Card size="small" title="상태별 분포">
            <EchartsUI ref="statusChartRef" height="280px" />
          </Card>
        </Col>
        <Col :lg="8" :xs="24">
          <Card size="small" title="유형별 분포">
            <EchartsUI ref="typeChartRef" height="280px" />
          </Card>
        </Col>
        <Col :lg="8" :xs="24">
          <Card size="small" title="처리 소요 시간 분포">
            <EchartsUI ref="durationChartRef" height="280px" />
          </Card>
        </Col>
      </Row>

      <!-- 차트 2행 -->
      <Card class="mt-3" size="small" title="일별 접수 및 처리 추세">
        <EchartsUI ref="trendChartRef" height="280px" />
      </Card>

      <!-- 최근 완료 내역 -->
      <Card
        :body-style="{ padding: 0 }"
        class="mt-3"
        size="small"
        title="최근 완료 내역 (Top 10)"
      >
        <RecentGrid
          class="cursor-pointer"
          :table-data="stats.recentCompletedItems"
        >
          <template #status="{ row }">
            <Tag :color="statusColor(row.status)">{{ row.status }}</Tag>
          </template>
          <template #requestedAt="{ row }">
            {{ formatDate(row.requestedAt) }}
          </template>
          <template #completedAt="{ row }">
            {{ formatDate(row.completedAt) || '-' }}
          </template>
        </RecentGrid>
      </Card>
    </Spin>
  </Page>
</template>
