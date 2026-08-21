<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import {
  Button,
  Card,
  Col,
  Row,
  Select,
  Space,
  Spin,
  Statistic,
  Table,
} from 'ant-design-vue';

import {
  getPushEngagementStats,
  getPushFailureReasons,
  getPushStats,
  getPushSuccessTrend,
  getTopPerformingMessages,
  getUserEngagementStats,
} from '#/api/helpdesk';

/**
 * [푸시 현황]
 *
 * 원본(PushDashboard.vue + StatsCard/PushEngagementStats/TopPerformingMessages/UserEngagementStats)을
 * 한 화면으로 합쳤다. 실패 사유를 클릭하면 그 사유로 걸러진 발송 이력으로 넘어간다.
 */

const router = useRouter();

const loading = ref(false);
const days = ref(7);
const stats = ref<any>({
  failureCount: 0,
  successCount: 0,
  successRate: 0,
  totalAttempts: 0,
});
const engagement = ref<any>(null);
const failureReasons = ref<any[]>([]);
const topMessages = ref<any[]>([]);
const userEngagement = ref<any[]>([]);

const trendChartRef = ref<EchartsUIType>();
const { renderEcharts: renderTrend } = useEcharts(trendChartRef);

/** 내보내기용으로 원본 추이 데이터를 들고 있는다. */
const trendRows = ref<Record<string, any>[]>([]);
const exporting = ref(false);

const DAY_OPTIONS = [
  { label: '최근 7일', value: 7 },
  { label: '최근 14일', value: 14 },
  { label: '최근 30일', value: 30 },
];

const failureColumns = [
  { dataIndex: 'reason', key: 'reason', title: '실패 사유' },
  { dataIndex: 'count', key: 'count', title: '건수', width: 90 },
];

const messageColumns = [
  { dataIndex: 'title', key: 'title', title: '메시지', ellipsis: true },
  { dataIndex: 'sentCount', key: 'sentCount', title: '발송', width: 80 },
  { dataIndex: 'openCount', key: 'openCount', title: '열람', width: 80 },
  { dataIndex: 'openRate', key: 'openRate', title: '열람률', width: 90 },
];

const userColumns = [
  { dataIndex: 'userName', key: 'userName', title: '사용자' },
  { dataIndex: 'receivedCount', key: 'receivedCount', title: '수신', width: 80 },
  { dataIndex: 'openCount', key: 'openCount', title: '열람', width: 80 },
];

function drawTrend(trend: any[]) {
  renderTrend({
    grid: { bottom: 30, left: 45, right: 16, top: 20 },
    series: [
      {
        areaStyle: { opacity: 0.2 },
        data: trend.map((d) => d.successRate ?? 0),
        name: '성공률(%)',
        smooth: true,
        type: 'line',
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: { data: trend.map((d) => d.date), type: 'category' },
    yAxis: { max: 100, min: 0, type: 'value' },
  });
}

async function loadAll() {
  loading.value = true;
  try {
    const [s, trend, reasons, eng, top, users] = await Promise.all([
      getPushStats(days.value),
      getPushSuccessTrend('daily', days.value),
      getPushFailureReasons(days.value, 5),
      getPushEngagementStats(days.value),
      getTopPerformingMessages(10),
      getUserEngagementStats(20),
    ]);

    stats.value = s ?? stats.value;
    trendRows.value = trend ?? [];
    engagement.value = eng;
    failureReasons.value = reasons ?? [];
    topMessages.value = top ?? [];
    userEngagement.value = users ?? [];
    drawTrend(trend ?? []);
  } finally {
    loading.value = false;
  }
}

/** 값에 콤마·따옴표가 들어가도 깨지지 않도록 CSV 셀을 감싼다. */
function csvCell(value: unknown) {
  const text = String(value ?? '');
  return /[\n",]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
}

/** 표 하나를 CSV 조각으로 만든다. */
function csvBlock(title: string, rows: Record<string, any>[]) {
  if (rows.length === 0) return `${title}\n(데이터 없음)\n`;

  const headers = Object.keys(rows[0]!);
  const body = rows
    .map((row) => headers.map((h) => csvCell(row[h])).join(','))
    .join('\n');
  return `${title}\n${headers.join(',')}\n${body}\n`;
}

/**
 * 화면의 모든 표를 CSV 한 파일로 내려받는다.
 * 원본(PushDashboard.vue)의 exportDatasetsToCsv 와 같은 목적이다.
 */
function exportCsv() {
  exporting.value = true;
  try {
    const blocks = [
      csvBlock('발송 요약', [stats.value]),
      csvBlock('성공률 추이', trendRows.value),
      csvBlock('실패 사유 상위', failureReasons.value),
      csvBlock('성과 상위 메시지', topMessages.value),
      csvBlock('사용자별 반응', userEngagement.value),
      engagement.value ? csvBlock('참여 지표', [engagement.value]) : '',
    ].filter(Boolean);

    // 엑셀이 UTF-8 로 읽도록 BOM 을 붙인다.
    const blob = new Blob([`\uFEFF${blocks.join('\n')}`], {
      type: 'text/csv;charset=utf-8;',
    });

    const today = new Date().toISOString().split('T')[0];
    const link = document.createElement('a');
    link.download = `push_dashboard_${today}.csv`;
    link.href = URL.createObjectURL(blob);
    link.click();
    URL.revokeObjectURL(link.href);
  } finally {
    exporting.value = false;
  }
}

/** 실패 사유를 클릭하면 그 사유로 필터링된 발송 이력을 연다. */
function openLogsByReason(reason: string) {
  router.push({
    path: '/helpdesk/push/logs',
    query: { reason, status: 'failure' },
  });
}

onMounted(loadAll);
</script>

<template>
  <Page auto-content-height>
    <Spin :spinning="loading">
      <Card class="mb-3" size="small">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <Space>
            <span class="text-sm">기간</span>
            <Select
              v-model:value="days"
              :options="DAY_OPTIONS"
              style="width: 140px"
              @change="loadAll"
            />
          </Space>
          <Space>
            <Button :loading="exporting" @click="exportCsv">CSV 내려받기</Button>
            <Button :loading="loading" @click="loadAll">새로고침</Button>
          </Space>
        </div>
      </Card>

      <Row :gutter="[12, 12]">
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic :value="stats.totalAttempts ?? 0" title="발송 시도" />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :value="stats.successCount ?? 0"
              :value-style="{ color: '#22C55E' }"
              title="성공"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :value="stats.failureCount ?? 0"
              :value-style="{ color: '#EF4444' }"
              title="실패"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :precision="1"
              :value="Number(stats.successRate ?? 0)"
              suffix="%"
              title="성공률"
            />
          </Card>
        </Col>
      </Row>

      <Card class="mt-3" size="small" title="성공률 추이">
        <EchartsUI ref="trendChartRef" height="240px" />
      </Card>

      <Row :gutter="[12, 12]" class="mt-3">
        <Col :lg="8" :xs="24">
          <Card :body-style="{ padding: 0 }" size="small" title="실패 사유 상위">
            <Table
              :columns="failureColumns"
              :custom-row="
                (record: any) => ({
                  onClick: () => openLogsByReason(record.reason),
                  style: 'cursor: pointer',
                })
              "
              :data-source="failureReasons"
              :pagination="false"
              row-key="reason"
              size="small"
            />
          </Card>
        </Col>

        <Col :lg="8" :xs="24">
          <Card :body-style="{ padding: 0 }" size="small" title="성과 상위 메시지">
            <Table
              :columns="messageColumns"
              :data-source="topMessages"
              :pagination="false"
              row-key="id"
              size="small"
            />
          </Card>
        </Col>

        <Col :lg="8" :xs="24">
          <Card :body-style="{ padding: 0 }" size="small" title="사용자별 반응">
            <Table
              :columns="userColumns"
              :data-source="userEngagement"
              :pagination="false"
              row-key="userId"
              size="small"
            />
          </Card>
        </Col>
      </Row>

      <Card v-if="engagement" class="mt-3" size="small" title="참여 지표">
        <Row :gutter="[12, 12]">
          <Col :lg="6" :xs="12">
            <Statistic :value="engagement.deliveredCount ?? 0" title="도달" />
          </Col>
          <Col :lg="6" :xs="12">
            <Statistic :value="engagement.readCount ?? 0" title="읽음" />
          </Col>
          <Col :lg="6" :xs="12">
            <Statistic
              :precision="1"
              :value="Number(engagement.readRate ?? 0)"
              suffix="%"
              title="읽음률"
            />
          </Col>
          <Col :lg="6" :xs="12">
            <Statistic
              :value="engagement.subscriberCount ?? 0"
              title="구독자"
            />
          </Col>
        </Row>
      </Card>
    </Spin>
  </Page>
</template>
