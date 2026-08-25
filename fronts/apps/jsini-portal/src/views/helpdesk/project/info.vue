<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import { onMounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';

import { Page } from '@vben/common-ui';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import {
  Card,
  Col,
  Descriptions,
  DescriptionsItem,
  Empty,
  Progress,
  Row,
  Spin,
  Statistic,
} from 'ant-design-vue';

import { getProjectStats } from '#/api/helpdesk';
import BizSelect from '#/components/BizSelect.vue';

import { formatDate } from '../shared/constants';

/**
 * [프로젝트 정보]
 *
 * 원본(ProjectInfo.vue)은 프로젝트 ID 를 prop 으로 받아 대시보드를 그렸다.
 * 여기서는 단독 화면으로도 열 수 있게 프로젝트 선택을 붙였다.
 * 쿼리스트링 `?projectId=` 로 들어오면 그 프로젝트를 바로 연다.
 */

const props = defineProps<{ projectId?: number | string }>();

const route = useRoute();

const loading = ref(false);
const selectedProjectId = ref<number | undefined>();
const stats = ref<any>(null);

const chartRef = ref<EchartsUIType>();
const { renderEcharts } = useEcharts(chartRef);

function drawChart(s: any) {
  renderEcharts({
    legend: { bottom: 0 },
    series: [
      {
        data: [
          {
            itemStyle: { color: '#4CAF50' },
            name: '완료',
            value: s.completedWbsCount ?? 0,
          },
          {
            itemStyle: { color: '#FFC107' },
            name: '진행중',
            value: s.inProgressWbsCount ?? 0,
          },
          {
            itemStyle: { color: '#607D8B' },
            name: '대기',
            value: s.pendingWbsCount ?? 0,
          },
        ],
        name: 'WBS 진행',
        radius: ['60%', '80%'],
        type: 'pie',
      },
    ],
    tooltip: { trigger: 'item' },
  });
}

async function loadStats(projectId?: number) {
  if (!projectId) {
    stats.value = null;
    return;
  }

  loading.value = true;
  try {
    stats.value = await getProjectStats(projectId);
    if (stats.value) drawChart(stats.value);
  } catch {
    stats.value = null;
  } finally {
    loading.value = false;
  }
}

watch(selectedProjectId, (id) => loadStats(id));

onMounted(() => {
  const fromProps = props.projectId ? Number(props.projectId) : undefined;
  const fromQuery = route.query.projectId
    ? Number(route.query.projectId)
    : undefined;

  selectedProjectId.value = fromProps ?? fromQuery;
});

/**
 * prop 도 쿼리스트링도 없이 들어온 경우에만 첫 프로젝트를 연다.
 * BizSelect 의 auto-select-first 는 목록이 오기 전에 정해진 값을 덮어쓰지 않지만,
 * 여기서는 값이 정해지는 시점이 목록보다 뒤일 수 있어 직접 판단한다.
 */
function onProjectsLoaded(items: any[]) {
  if (selectedProjectId.value === undefined) {
    selectedProjectId.value = items[0]?.id;
  }
}
</script>

<template>
  <Page auto-content-height>
    <Card class="mb-3" size="small">
      <!-- BizSelect 는 너비 100% 라 바깥에서 폭을 정한다 -->
      <div style="width: 260px">
        <BizSelect
          v-model:value="selectedProjectId"
          option-filter-prop="label"
          placeholder="프로젝트 선택"
          show-search
          type="helpdesk_project"
          @loaded="onProjectsLoaded"
        />
      </div>
    </Card>

    <Spin :spinning="loading">
      <template v-if="stats">
        <Row :gutter="[12, 12]">
          <Col :lg="6" :xs="12">
            <Card size="small">
              <Statistic :value="stats.totalWbsCount ?? 0" title="전체 작업" />
            </Card>
          </Col>
          <Col :lg="6" :xs="12">
            <Card size="small">
              <Statistic
                :value="stats.completedWbsCount ?? 0"
                :value-style="{ color: '#4CAF50' }"
                title="완료"
              />
            </Card>
          </Col>
          <Col :lg="6" :xs="12">
            <Card size="small">
              <Statistic
                :value="stats.inProgressWbsCount ?? 0"
                :value-style="{ color: '#FFC107' }"
                title="진행중"
              />
            </Card>
          </Col>
          <Col :lg="6" :xs="12">
            <Card size="small">
              <Statistic :value="stats.pendingWbsCount ?? 0" title="대기" />
            </Card>
          </Col>
        </Row>

        <Row :gutter="[12, 12]" class="mt-3">
          <Col :lg="10" :xs="24">
            <Card size="small" title="WBS 진행 분포">
              <EchartsUI ref="chartRef" height="240px" />
            </Card>
          </Col>

          <Col :lg="14" :xs="24">
            <Card size="small" title="프로젝트 개요">
              <Descriptions :column="{ md: 2, xs: 1 }" bordered size="small">
                <DescriptionsItem label="프로젝트">
                  {{ stats.projectName }}
                </DescriptionsItem>
                <DescriptionsItem label="담당 팀">
                  {{ stats.teamName ?? '-' }}
                </DescriptionsItem>
                <DescriptionsItem label="시작일">
                  {{ formatDate(stats.startDate) || '미지정' }}
                </DescriptionsItem>
                <DescriptionsItem label="종료일">
                  {{ formatDate(stats.endDate) || '미지정' }}
                </DescriptionsItem>
              </Descriptions>

              <div class="mt-4">
                <div class="mb-1 text-xs text-muted-foreground">전체 진행률</div>
                <Progress :percent="Math.round(stats.progressRate ?? 0)" />
              </div>
            </Card>
          </Col>
        </Row>
      </template>

      <Empty v-else-if="!loading" description="프로젝트를 선택하세요." />
    </Spin>
  </Page>
</template>
