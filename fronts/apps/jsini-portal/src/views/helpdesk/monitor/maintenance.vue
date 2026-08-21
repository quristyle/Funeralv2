<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import {
  Card,
  Col,
  Row,
  Select,
  Space,
  Spin,
  Statistic,
  Table,
  Tag,
} from 'ant-design-vue';

import { getMonthlyReport } from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import { formatDateTime, statusMeta } from '../shared/constants';

/**
 * [유지보수 보고서]
 *
 * 원본(MaintenanceReport.vue). 월 단위 처리 실적을 회사별로 본다.
 * 고객으로 연결된 계정은 자기 회사로 고정된다.
 */

const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);
const now = new Date();
const selectedYear = ref(now.getFullYear());
const selectedMonth = ref(now.getMonth() + 1);
const selectedCompany = ref<number | undefined>();

const stats = ref<any>({
  averageResolutionTimeHours: 0,
  completedRequests: 0,
  consultationRequests: 0,
  inProgressRequests: 0,
  negotiationRequests: 0,
  pendingRequests: 0,
  recentCompletedItems: [],
  resolutionRate: 0,
  totalRequests: 0,
  userCompletedRequests: 0,
});

const statusChartRef = ref<EchartsUIType>();
const typeChartRef = ref<EchartsUIType>();
const { renderEcharts: renderStatus } = useEcharts(statusChartRef);
const { renderEcharts: renderType } = useEcharts(typeChartRef);

const YEAR_OPTIONS = Array.from({ length: 5 }, (_, i) => {
  const y = now.getFullYear() - i;
  return { label: `${y}년`, value: y };
});
const MONTH_OPTIONS = Array.from({ length: 12 }, (_, i) => ({
  label: `${i + 1}월`,
  value: i + 1,
}));

const COLORS = ['#42A5F5', '#66BB6A', '#FFA726', '#FF7043', '#AB47BC', '#26A69A'];

const recentColumns = [
  { dataIndex: 'title', key: 'title', title: '제목', ellipsis: true },
  { dataIndex: 'adminName', key: 'adminName', title: '담당자', width: 110 },
  { dataIndex: 'completededAt', key: 'completededAt', title: '완료일', width: 160 },
  { dataIndex: 'status', key: 'status', title: '상태', width: 90 },
];

/** 배열/객체 두 형태로 오는 집계 결과를 모두 받아준다. */
function pairsOf(obj: any) {
  if (Array.isArray(obj)) {
    return obj.map((o) => ({ name: o.key, value: o.value }));
  }
  return Object.entries(obj ?? {}).map(([name, value]) => ({
    name,
    value: Number(value),
  }));
}

function drawCharts(payload: any) {
  const pie = (data: any[], title: string) => ({
    legend: { bottom: 0, type: 'scroll' as const },
    series: [
      {
        color: COLORS,
        data,
        name: title,
        radius: ['45%', '70%'],
        type: 'pie' as const,
      },
    ],
    tooltip: { trigger: 'item' as const },
  });

  renderStatus(pie(pairsOf(payload?.requestsByStatus), '상태별'));
  renderType(pie(pairsOf(payload?.requestsByType), '유형별'));
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

onMounted(async () => {
  await helpdesk.loadIdentity();
  if (helpdesk.isAdmin) await helpdesk.loadOrganizations();
  await loadData();
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Spin :spinning="loading">
      <Card class="mb-3" size="small">
        <Space wrap>
          <Select
            v-model:value="selectedYear"
            :options="YEAR_OPTIONS"
            style="width: 110px"
            @change="loadData"
          />
          <Select
            v-model:value="selectedMonth"
            :options="MONTH_OPTIONS"
            style="width: 90px"
            @change="loadData"
          />
          <Select
            v-if="helpdesk.isAdmin"
            v-model:value="selectedCompany"
            :options="helpdesk.companyOptions"
            option-filter-prop="label"
            placeholder="회사"
            show-search
            style="width: 180px"
            @change="loadData"
          />
        </Space>
      </Card>

      <Row :gutter="[12, 12]">
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic :value="stats.totalRequests" title="전체 요청" />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :value="stats.completedRequests + stats.userCompletedRequests"
              :value-style="{ color: '#22C55E' }"
              title="완료"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :precision="1"
              :value="Number(stats.resolutionRate)"
              suffix="%"
              title="해결률"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :precision="1"
              :value="Number(stats.averageResolutionTimeHours)"
              suffix="h"
              title="평균 해결 시간"
            />
          </Card>
        </Col>
      </Row>

      <Row :gutter="[12, 12]" class="mt-3">
        <Col :lg="12" :xs="24">
          <Card size="small" title="상태별 분포">
            <EchartsUI ref="statusChartRef" height="240px" />
          </Card>
        </Col>
        <Col :lg="12" :xs="24">
          <Card size="small" title="유형별 분포">
            <EchartsUI ref="typeChartRef" height="240px" />
          </Card>
        </Col>
      </Row>

      <Card
        :body-style="{ padding: 0 }"
        class="mt-3"
        size="small"
        title="최근 완료 항목"
      >
        <Table
          :columns="recentColumns"
          :custom-row="
            (record: any) => ({
              onClick: () =>
                router.push(`/helpdesk/request/detail/${record.id}`),
              style: 'cursor: pointer',
            })
          "
          :data-source="stats.recentCompletedItems"
          :pagination="false"
          row-key="id"
          size="small"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'completededAt'">
              {{ formatDateTime(record.completededAt) }}
            </template>
            <template v-else-if="column.key === 'status'">
              <Tag :color="statusMeta(record.status).color">
                {{ record.statusName || statusMeta(record.status).label }}
              </Tag>
            </template>
          </template>
        </Table>
      </Card>
    </Spin>
  </Page>
</template>
