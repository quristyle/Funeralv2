<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import type { ImprovementRequest } from '#/api/helpdesk';

import { computed, onMounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import {
  Button,
  Card,
  Col,
  List,
  ListItem,
  Row,
  Space,
  Spin,
  Statistic,
  Tag,
} from 'ant-design-vue';

import {
  getMyCompanyStats,
  getMyMonthlyStats,
  searchRequests,
} from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import { formatDateTime, statusMeta } from '../shared/constants';

/**
 * [고객사 현황]
 *
 * 원본(CustomerDashboard.vue)의 구성을 옮겼다.
 *  - 내 요청 상태 분포(도넛)
 *  - 월별 접수/완료 추이(막대) — 막대를 클릭하면 그 달의 일별 추이로 내려간다
 *  - 선택한 달의 일별 상태 추이(누적 막대)
 *  - 최근 처리된 요청 목록
 */

const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);
const myRequests = ref<ImprovementRequest[]>([]);
const recentRequests = ref<ImprovementRequest[]>([]);
const companyStats = ref<any>(null);
const monthlyStats = ref<any[]>([]);

const statusChartRef = ref<EchartsUIType>();
const monthlyChartRef = ref<EchartsUIType>();
const dailyChartRef = ref<EchartsUIType>();

const { renderEcharts: renderStatusChart } = useEcharts(statusChartRef);
const { renderEcharts: renderMonthlyChart } = useEcharts(monthlyChartRef);
const { renderEcharts: renderDailyChart } = useEcharts(dailyChartRef);

const today = new Date();
const selectedYear = ref(today.getFullYear());
/** 0-based 월. 일별 차트가 이 값을 따라간다. */
const selectedMonth = ref(today.getMonth());

/** 원본과 동일한 상태별 색상 */
const COLORS = {
  completed: '#22C55E',
  consultation: '#EAB308',
  inProgress: '#F97316',
  negotiation: '#A855F7',
  pending: '#3B82F6',
  rejected: '#EF4444',
};

/** 집계에 쓰는 상태 키 — 서버 열거형 이름과 같다. */
type StatusKey =
  | 'Completed'
  | 'Consultation'
  | 'InProgress'
  | 'Negotiation'
  | 'Pending'
  | 'Rejected'
  | 'UserCompleted';

const STATUS_KEYS: StatusKey[] = [
  'Pending',
  'InProgress',
  'Consultation',
  'Negotiation',
  'Completed',
  'UserCompleted',
  'Rejected',
];

/** 내 요청을 상태별로 센다. */
const statusCounts = computed(() => {
  const counts = Object.fromEntries(
    STATUS_KEYS.map((k) => [k, 0]),
  ) as Record<StatusKey, number>;

  myRequests.value.forEach((r) => {
    const key = r.status as StatusKey;
    if (key in counts) counts[key] += 1;
  });
  return counts;
});

/** 선택한 달의 일별 상태 건수 */
const dailyCounts = computed(() => {
  const year = selectedYear.value;
  const month = selectedMonth.value;
  const days = new Date(year, month + 1, 0).getDate();

  const empty = () => Array.from({ length: days }, () => 0);
  const buckets = Object.fromEntries(
    STATUS_KEYS.map((k) => [k, empty()]),
  ) as Record<StatusKey, number[]>;

  myRequests.value.forEach((r) => {
    if (!r.createdAt) return;
    const d = new Date(r.createdAt);
    if (d.getFullYear() !== year || d.getMonth() !== month) return;
    const bucket = buckets[r.status as StatusKey];
    const index = d.getDate() - 1;
    if (bucket?.[index] !== undefined) bucket[index] += 1;
  });

  return { buckets, days };
});

const selectedMonthLabel = computed(
  () => `${selectedYear.value}년 ${selectedMonth.value + 1}월`,
);

function drawStatusChart() {
  const c = statusCounts.value;
  const total = Object.values(c).reduce((a, b) => a + b, 0);

  renderStatusChart({
    legend: { bottom: 0, type: 'scroll' },
    series: [
      {
        avoidLabelOverlap: true,
        data: [
          { itemStyle: { color: COLORS.pending }, name: '대기', value: c.Pending },
          {
            itemStyle: { color: COLORS.inProgress },
            name: '진행',
            value: c.InProgress,
          },
          {
            itemStyle: { color: COLORS.consultation },
            name: '협의',
            value: c.Consultation,
          },
          {
            itemStyle: { color: COLORS.negotiation },
            name: '논의',
            value: c.Negotiation,
          },
          {
            itemStyle: { color: COLORS.completed },
            name: '완료',
            value: c.Completed + c.UserCompleted,
          },
          {
            itemStyle: { color: COLORS.rejected },
            name: '반려',
            value: c.Rejected,
          },
        ],
        // 도넛 가운데에 전체 건수를 띄운다(원본의 DoughnutCenterText 플러그인과 같은 역할).
        label: {
          formatter: () => `${total}건`,
          fontSize: 18,
          fontWeight: 'bold',
          position: 'center',
          show: true,
        },
        labelLine: { show: false },
        name: '요청 상태',
        radius: ['55%', '80%'],
        type: 'pie',
      },
    ],
    tooltip: { trigger: 'item' },
  });
}

function drawMonthlyChart() {
  renderMonthlyChart({
    grid: { bottom: 30, left: 40, right: 16, top: 30 },
    legend: { top: 0 },
    series: [
      {
        data: monthlyStats.value.map((s) => s.totalCount ?? 0),
        itemStyle: { color: '#42A5F5' },
        name: '접수',
        type: 'bar',
      },
      {
        data: monthlyStats.value.map((s) => s.completedCount ?? 0),
        itemStyle: { color: '#66BB6A' },
        name: '완료',
        type: 'bar',
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: {
      data: monthlyStats.value.map((s) => {
        const [, month] = String(s.month ?? '').split('-');
        return `${Number.parseInt(month ?? '0', 10)}월`;
      }),
      type: 'category',
    },
    yAxis: { minInterval: 1, type: 'value' },
  });
}

function drawDailyChart() {
  const { buckets, days } = dailyCounts.value;

  renderDailyChart({
    grid: { bottom: 30, left: 40, right: 16, top: 30 },
    legend: { top: 0 },
    series: [
      {
        data: buckets.Pending,
        itemStyle: { color: COLORS.pending },
        name: '대기',
        stack: 'daily',
        type: 'bar',
      },
      {
        data: buckets.InProgress,
        itemStyle: { color: COLORS.inProgress },
        name: '진행',
        stack: 'daily',
        type: 'bar',
      },
      {
        data: buckets.Completed.map(
          (value, index) => value + (buckets.UserCompleted[index] ?? 0),
        ),
        itemStyle: { color: COLORS.completed },
        name: '완료',
        stack: 'daily',
        type: 'bar',
      },
      {
        data: buckets.Rejected,
        itemStyle: { color: COLORS.rejected },
        name: '반려',
        stack: 'daily',
        type: 'bar',
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: {
      data: Array.from({ length: days }, (_, i) => String(i + 1)),
      type: 'category',
    },
    yAxis: { minInterval: 1, type: 'value' },
  });
}

/** 월별 차트의 막대를 클릭하면 그 달의 일별 추이로 내려간다. */
function onMonthlyChartClick(params: any) {
  const stat = monthlyStats.value[params.dataIndex];
  if (!stat?.month) return;

  const [year, month] = String(stat.month).split('-').map(Number);
  if (year && month) {
    selectedYear.value = year;
    selectedMonth.value = month - 1;
  }
}

function goToCurrentMonth() {
  const now = new Date();
  selectedYear.value = now.getFullYear();
  selectedMonth.value = now.getMonth();
}

async function loadAll() {
  if (!helpdesk.helpdeskUserId) return;

  loading.value = true;
  try {
    const [stats, monthly, mine, recent] = await Promise.all([
      getMyCompanyStats(),
      getMyMonthlyStats(),
      // 상태·일별 집계용. 본문은 빼고 가볍게 받는다.
      searchRequests({
        select: 'id,createdAt,status',
        remove: 'customerId,description',
        customerId: helpdesk.helpdeskUserId,
        pageSize: 2000,
        page: 1,
      }),
      // 최근 처리된 요청 (완료·삭제·협의)
      searchRequests({
        select:
          'id,title,createdAt,customer.userName,admin,status,customer.company.name,completededAt',
        remove: 'customerId,description',
        sorts: [
          { dir: 'desc', field: 'completededAt' },
          { dir: 'desc', field: 'status' },
        ],
        page: 1,
        pageSize: 10,
        status_in: '3|4|5',
        'customer.companyId': helpdesk.companyId ?? null,
      }),
    ]);

    companyStats.value = stats;
    monthlyStats.value = monthly ?? [];
    myRequests.value = mine.items;
    recentRequests.value = recent.items;

    drawStatusChart();
    drawMonthlyChart();
    drawDailyChart();
  } finally {
    loading.value = false;
  }
}

watch([selectedYear, selectedMonth], drawDailyChart);

onMounted(async () => {
  await helpdesk.loadIdentity();
  await loadAll();
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Spin :spinning="loading">
      <Row :gutter="[12, 12]">
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :value="companyStats?.totalCount ?? myRequests.length"
              title="전체 요청"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :value="statusCounts.Pending"
              title="대기"
              :value-style="{ color: COLORS.pending }"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :value="statusCounts.InProgress"
              title="진행"
              :value-style="{ color: COLORS.inProgress }"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :value="statusCounts.Completed + statusCounts.UserCompleted"
              title="완료"
              :value-style="{ color: COLORS.completed }"
            />
          </Card>
        </Col>
      </Row>

      <Row :gutter="[12, 12]" class="mt-3">
        <Col :lg="8" :xs="24">
          <Card size="small" title="상태 분포">
            <EchartsUI ref="statusChartRef" height="260px" />
          </Card>
        </Col>

        <Col :lg="16" :xs="24">
          <Card size="small" title="월별 접수 · 완료">
            <EchartsUI
              ref="monthlyChartRef"
              height="260px"
              @click="onMonthlyChartClick"
            />
          </Card>
        </Col>
      </Row>

      <Card class="mt-3" size="small" :title="`${selectedMonthLabel} 일별 추이`">
        <template #extra>
          <Space>
            <span class="text-xs text-muted-foreground">
              위 월별 차트의 막대를 클릭하면 해당 월로 이동합니다.
            </span>
            <Button size="small" @click="goToCurrentMonth">이번 달</Button>
          </Space>
        </template>
        <EchartsUI ref="dailyChartRef" height="260px" />
      </Card>

      <Card class="mt-3" size="small" title="최근 처리 요청">
        <List
          :data-source="recentRequests"
          :locale="{ emptyText: '최근 처리된 요청이 없습니다.' }"
        >
          <template #renderItem="{ item }">
            <ListItem
              class="cursor-pointer"
              @click="router.push(`/helpdesk/request/detail/${item.id}`)"
            >
              <div class="flex w-full items-center gap-3">
                <Tag :color="statusMeta(item.status).color">
                  {{ item.statusName || statusMeta(item.status).label }}
                </Tag>
                <span class="min-w-0 flex-1 truncate">{{ item.title }}</span>
                <span class="text-xs text-muted-foreground">
                  {{ formatDateTime(item.completededAt || item.createdAt) }}
                </span>
              </div>
            </ListItem>
          </template>
        </List>
      </Card>
    </Spin>
  </Page>
</template>
