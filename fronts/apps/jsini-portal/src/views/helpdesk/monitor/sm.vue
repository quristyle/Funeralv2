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
  Tag,
} from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
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
 *
 * ------------------------------------------------------------
 * [2026-08-30] 표 셋을 ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * **가져오기 방식은 그대로다** — 셋 다 한 번에 전량을 받아 그린다(페이저 없음).
 * ------------------------------------------------------------
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

/** 1. 담당자별 해결 기여도 — 비중 두 칸은 막대로 그린다(필터 대상이 아니다). */
const [WorkloadGrid] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      { field: 'adminName', title: '담당자', width: 100 },
      {
        field: 'monthlyShare',
        params: { filter: false },
        slots: { default: 'monthlyShare' },
        title: '월간 비중',
        width: 130,
      },
      {
        field: 'totalShare',
        params: { filter: false },
        slots: { default: 'totalShare' },
        title: '누적 비중',
        width: 130,
      },
      { field: 'inProgressCount', title: '진행', width: 70 },
    ],
    // 행 배열은 `:table-data` 로 간다. 여기는 빈 배열이 바탕값이다.
    data: [],
    emptyText: '집계된 실적이 없습니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 표 셋이 한 번의 조회로 함께 채워지므로 셋 다 같은 함수를 준다.
    gridFeatures: { onRefresh: () => loadData() },
    height: 240,
    // 전량 조회다. 페이저를 끄지 않으면 한 줄도 안 그려진다.
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'adminName' },
  } as any,
});

/** 3. 사용자 협업 상위 */
const [EngagedGrid] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', minWidth: 120, title: '사용자' },
      { field: 'company', minWidth: 140, title: '회사' },
      { field: 'interactions', title: '상호작용', width: 90 },
      { field: 'confirms', title: '확인', width: 70 },
    ],
    data: [],
    emptyText: '협업 이력이 없습니다.',
    // 위 표와 같은 조회로 채워진다.
    gridFeatures: { onRefresh: () => loadData() },
    height: 240,
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'name' },
  } as any,
});

/**
 * 4. 긴급 · 장애 발생
 *
 * 상태 칸은 고르는 칸(`filterOptions`)으로 두지 않았다 — `statusName` 은 서버가
 * 그때그때 붙여 주는 표시용 이름이라 목록을 못 박으면 새 값이 걸러지지 않는다.
 */
const [EmergencyGrid] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'title', minWidth: 240, title: '제목' },
      {
        field: 'createdAt',
        params: { filterText: (row: any) => formatDateTime(row.createdAt) },
        slots: { default: 'createdAt' },
        title: '발생',
        width: 160,
      },
      {
        field: 'statusName',
        slots: { default: 'statusName' },
        title: '상태',
        width: 90,
      },
    ],
    data: [],
    emptyText: '등록된 긴급 · 장애가 없습니다.',
    // 위 표들과 같은 조회로 채워진다.
    gridFeatures: { onRefresh: () => loadData() },
    height: 300,
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'id' },
  } as any,
});

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
            <WorkloadGrid :table-data="adminWorkload">
              <template #monthlyShare="{ row }">
                <Progress
                  :percent="Math.round(row.monthlyShare ?? 0)"
                  size="small"
                />
              </template>
              <template #totalShare="{ row }">
                <Progress
                  :percent="Math.round(row.totalShare ?? 0)"
                  size="small"
                  status="normal"
                />
              </template>
            </WorkloadGrid>
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
            <EngagedGrid :table-data="collaboration.topEngagedUsers" />
          </Card>
        </Col>
      </Row>

      <!--
        원본의 '4. 시스템 반영 정보(기술)' 블록은 커밋·DB작업·배치결과를 보여주지만
        API 가 없고 화면에 값이 하드코딩돼 있었다. 가짜 데이터를 그대로 옮기면
        운영 화면에서 사실과 다른 정보를 보여주게 되므로 자리만 남기고 출처를 밝힌다.
      -->
      <Alert
        class="mt-3"
        description="원본 화면의 '시스템 반영 정보(커밋·DB 작업·배치 결과)' 영역은 API 없이 예시 값이 박혀 있던 부분이라 옮기지 않았습니다. 연동할 데이터 소스가 정해지면 이 자리에 붙이면 됩니다."
        message="미이식 영역 안내"
        show-icon
        type="info"
      />

      <Card
        :body-style="{ padding: 0 }"
        class="mt-3"
        size="small"
        title="긴급 · 장애 발생"
      >
        <EmergencyGrid :table-data="emergencyIncidents">
          <template #createdAt="{ row }">
            {{ formatDateTime(row.createdAt) }}
          </template>
          <template #statusName="{ row }">
            <Tag color="error">{{ row.statusName }}</Tag>
          </template>
        </EmergencyGrid>
      </Card>
    </Spin>
  </Page>
</template>
