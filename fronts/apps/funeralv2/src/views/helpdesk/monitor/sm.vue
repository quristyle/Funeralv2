<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import {
  Alert,
  Card,
  Col,
  Progress,
  Row,
  Select,
  Space,
  Spin,
  Statistic,
  Table,
  Tag,
} from 'ant-design-vue';

import {
  getAdminContributionStats,
  getAdminContributionTrend,
  getCollaborationReport,
  getEmergencyIncidents,
  getMonthlyReport,
  getQualityReport,
} from '#/api/helpdesk';

import { formatDateTime } from '../shared/constants';

/**
 * [SM 모니터링]
 *
 * 원본(SMMonitoring.vue)의 네 블록을 옮겼다.
 *  1. 작업 현황 — 담당자별 기여도, 상태·유형 분포
 *  2. 효율성 — MTTR, 기여도 12주 추이
 *  3. 품질 — 재오픈율, SR/버그 비율, 사용자 확인율
 *  4. 긴급/장애 발생 목록
 */

const loading = ref(false);
const now = new Date();
const selectedYear = ref(now.getFullYear());
const selectedMonth = ref(now.getMonth() + 1);

const emergencyIncidents = ref<any[]>([]);
const adminWorkload = ref<any[]>([]);
const mttr = ref(0);
const statusCounts = ref<{ label: string; value: number }[]>([]);
const typeCounts = ref<{ label: string; value: number }[]>([]);
const collaboration = ref<any>({ avgUserFeedbackHours: 0, topEngagedUsers: [] });
const quality = ref<any>({
  changeSuccessRate: 0,
  reopenRate: 0,
  rollbackCount: 0,
  srBugRatio: { bug: 0, sr: 0 },
  userConfirmationRate: 0,
});

const trendChartRef = ref<EchartsUIType>();
const distChartRef = ref<EchartsUIType>();
const { renderEcharts: renderTrend } = useEcharts(trendChartRef);
const { renderEcharts: renderDist } = useEcharts(distChartRef);

const YEAR_OPTIONS = Array.from({ length: 5 }, (_, i) => {
  const y = now.getFullYear() - i;
  return { label: `${y}년`, value: y };
});
const MONTH_OPTIONS = Array.from({ length: 12 }, (_, i) => ({
  label: `${i + 1}월`,
  value: i + 1,
}));

const TREND_COLORS = [
  '#42A5F5',
  '#66BB6A',
  '#FFA726',
  '#26C6DA',
  '#7E57C2',
  '#EC407A',
  '#AB47BC',
  '#78909C',
];

const workloadColumns = [
  { dataIndex: 'adminName', key: 'adminName', title: '담당자', width: 100 },
  { dataIndex: 'monthlyShare', key: 'monthlyShare', title: '월간 비중', width: 130 },
  { dataIndex: 'totalShare', key: 'totalShare', title: '누적 비중', width: 130 },
  { dataIndex: 'inProgressCount', key: 'inProgressCount', title: '진행', width: 70 },
];

const engagedColumns = [
  { dataIndex: 'name', key: 'name', title: '사용자' },
  { dataIndex: 'company', key: 'company', title: '회사' },
  { dataIndex: 'interactions', key: 'interactions', title: '상호작용', width: 90 },
  { dataIndex: 'confirms', key: 'confirms', title: '확인', width: 70 },
];

const emergencyColumns = [
  { dataIndex: 'title', key: 'title', title: '제목', ellipsis: true },
  { dataIndex: 'createdAt', key: 'createdAt', title: '발생', width: 160 },
  { dataIndex: 'statusName', key: 'statusName', title: '상태', width: 90 },
];

/** 배열 또는 객체로 오는 집계 결과를 {label, value} 형태로 통일한다. */
function toPairs(data: any): { label: string; value: number }[] {
  if (Array.isArray(data)) {
    return data.map((item) => ({ label: item.key, value: item.value }));
  }
  return Object.entries(data ?? {}).map(([label, value]) => ({
    label,
    value: Number(value),
  }));
}

function drawDistribution() {
  renderDist({
    legend: { bottom: 0, type: 'scroll' },
    series: [
      {
        center: ['25%', '45%'],
        data: statusCounts.value.map((d) => ({ name: d.label, value: d.value })),
        name: '상태별',
        radius: ['40%', '65%'],
        type: 'pie',
      },
      {
        center: ['75%', '45%'],
        data: typeCounts.value.map((d) => ({ name: d.label, value: d.value })),
        name: '유형별',
        radius: ['40%', '65%'],
        type: 'pie',
      },
    ],
    tooltip: { trigger: 'item' },
  });
}

function drawTrend(trendData: any[]) {
  if (!trendData?.length) return;

  const labels = trendData.map((d) => d.week);
  const adminNames: string[] =
    trendData[0]?.admins?.map((a: any) => a.adminName) ?? [];

  renderTrend({
    grid: { bottom: 30, left: 45, right: 16, top: 30 },
    legend: { top: 0, type: 'scroll' },
    series: adminNames.map((adminName, index) => ({
      data: trendData.map(
        (d) => d.admins?.find((a: any) => a.adminName === adminName)?.share ?? 0,
      ),
      itemStyle: { color: TREND_COLORS[index % TREND_COLORS.length] },
      name: adminName,
      smooth: true,
      type: 'line',
    })),
    tooltip: { trigger: 'axis' },
    xAxis: { data: labels, type: 'category' },
    yAxis: { name: '기여도(%)', type: 'value' },
  });
}

async function loadData() {
  loading.value = true;
  try {
    const [incidents, workload, trend, report, collab, qualityData] =
      await Promise.all([
        getEmergencyIncidents().catch(() => []),
        getAdminContributionStats(selectedYear.value, selectedMonth.value),
        getAdminContributionTrend(),
        getMonthlyReport(selectedYear.value, selectedMonth.value),
        getCollaborationReport(selectedYear.value, selectedMonth.value),
        getQualityReport(selectedYear.value, selectedMonth.value),
      ]);

    emergencyIncidents.value = incidents ?? [];
    adminWorkload.value = workload ?? [];

    if (report) {
      mttr.value = report.averageResolutionTimeHours ?? 0;
      statusCounts.value = toPairs(report.requestsByStatus);
      typeCounts.value = toPairs(report.requestsByType);
    }

    if (collab) {
      collaboration.value = {
        avgUserFeedbackHours: collab.avgUserFeedbackHours ?? 0,
        topEngagedUsers: collab.topEngagedUsers ?? [],
      };
      quality.value.userConfirmationRate = Math.round(
        collab.confirmationRate ?? 0,
      );
    }

    if (qualityData) {
      quality.value = {
        ...quality.value,
        changeSuccessRate: qualityData.changeSuccessRate ?? 0,
        reopenRate: qualityData.reopenRate ?? 0,
        rollbackCount: qualityData.rollbackCount ?? 0,
        srBugRatio: {
          bug: qualityData.bugRatio ?? 0,
          sr: qualityData.srRatio ?? 0,
        },
      };
    }

    drawDistribution();
    drawTrend(trend ?? []);
  } finally {
    loading.value = false;
  }
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height>
    <Spin :spinning="loading">
      <Card class="mb-3" size="small">
        <Space>
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
        </Space>
      </Card>

      <Alert
        v-if="emergencyIncidents.length > 0"
        class="mb-3"
        :message="`긴급/장애 ${emergencyIncidents.length}건이 등록되어 있습니다.`"
        show-icon
        type="error"
      />

      <Row :gutter="[12, 12]">
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :precision="1"
              :value="mttr"
              suffix="h"
              title="평균 해결 시간(MTTR)"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :precision="1"
              :value="collaboration.avgUserFeedbackHours"
              suffix="h"
              title="평균 사용자 응답"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :precision="1"
              :value="quality.reopenRate"
              suffix="%"
              title="재오픈율"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :value="quality.userConfirmationRate"
              suffix="%"
              title="사용자 확인율"
            />
          </Card>
        </Col>
      </Row>

      <Row :gutter="[12, 12]" class="mt-3">
        <Col :lg="12" :xs="24">
          <Card
            :body-style="{ padding: 0 }"
            size="small"
            title="1. 담당자별 해결 기여도"
          >
            <Table
              :columns="workloadColumns"
              :data-source="adminWorkload"
              :pagination="false"
              :scroll="{ y: 200 }"
              row-key="adminName"
              size="small"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.key === 'monthlyShare'">
                  <Progress
                    :percent="Math.round(record.monthlyShare ?? 0)"
                    size="small"
                  />
                </template>
                <template v-else-if="column.key === 'totalShare'">
                  <Progress
                    :percent="Math.round(record.totalShare ?? 0)"
                    size="small"
                    status="normal"
                  />
                </template>
              </template>
            </Table>
          </Card>
        </Col>

        <Col :lg="12" :xs="24">
          <Card size="small" title="상태별 · 유형별 분포">
            <EchartsUI ref="distChartRef" height="240px" />
          </Card>
        </Col>
      </Row>

      <Card class="mt-3" size="small" title="2. 담당자 기여도 12주 추이">
        <EchartsUI ref="trendChartRef" height="260px" />
      </Card>

      <Row :gutter="[12, 12]" class="mt-3">
        <Col :lg="12" :xs="24">
          <Card size="small" title="3. 품질 및 안정성">
            <Row :gutter="[12, 12]">
              <Col :span="12">
                <Statistic
                  :precision="1"
                  :value="quality.srBugRatio.sr"
                  suffix="%"
                  title="SR 비율"
                />
              </Col>
              <Col :span="12">
                <Statistic
                  :precision="1"
                  :value="quality.srBugRatio.bug"
                  suffix="%"
                  title="버그 비율"
                />
              </Col>
              <Col :span="12">
                <Statistic
                  :precision="1"
                  :value="quality.changeSuccessRate"
                  suffix="%"
                  title="변경 성공률"
                />
              </Col>
              <Col :span="12">
                <Statistic :value="quality.rollbackCount" title="롤백 건수" />
              </Col>
            </Row>
          </Card>
        </Col>

        <Col :lg="12" :xs="24">
          <Card
            :body-style="{ padding: 0 }"
            size="small"
            title="사용자 협업 상위"
          >
            <Table
              :columns="engagedColumns"
              :data-source="collaboration.topEngagedUsers"
              :pagination="false"
              :scroll="{ y: 200 }"
              row-key="name"
              size="small"
            />
          </Card>
        </Col>
      </Row>

      <Card
        :body-style="{ padding: 0 }"
        class="mt-3"
        size="small"
        title="4. 긴급 · 장애 발생"
      >
        <Table
          :columns="emergencyColumns"
          :data-source="emergencyIncidents"
          :pagination="false"
          row-key="id"
          size="small"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'createdAt'">
              {{ formatDateTime(record.createdAt) }}
            </template>
            <template v-else-if="column.key === 'statusName'">
              <Tag color="error">{{ record.statusName }}</Tag>
            </template>
          </template>
        </Table>
      </Card>
    </Spin>
  </Page>
</template>
