<script lang="ts" setup>
import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Col,
  Descriptions,
  DescriptionsItem,
  Empty,
  Row,
  Spin,
  Statistic,
  Table,
  Tooltip,
} from 'ant-design-vue';

import { getServerReport } from '#/api/helpdesk';

/**
 * [원인 분석]
 *
 * 원본(reports/RootCauseAnalysis.vue).
 * MEM_DETAIL(메모리 상세) + LOAD_ANALYSIS(부하 유발 쿼리)를 함께 본다.
 */

const loading = ref(false);
const memory = ref<Record<string, any>>({});
const heavyQueries = ref<Record<string, any>[]>([]);

const queryColumns = [
  { dataIndex: 'QueryText', key: 'QueryText', title: '쿼리', ellipsis: true },
  {
    dataIndex: 'AvgLogicalReads',
    key: 'AvgLogicalReads',
    title: '평균 논리 읽기',
    width: 140,
  },
  {
    dataIndex: 'execution_count',
    key: 'execution_count',
    title: '실행 횟수',
    width: 110,
  },
  {
    dataIndex: 'last_execution_time',
    key: 'last_execution_time',
    title: '마지막 실행',
    width: 180,
  },
];

async function loadData() {
  loading.value = true;
  try {
    const [mem, load] = await Promise.all([
      getServerReport<Record<string, any>[]>('MEM_DETAIL'),
      getServerReport<Record<string, any>[]>('LOAD_ANALYSIS'),
    ]);
    memory.value = mem?.[0] ?? {};
    heavyQueries.value = load ?? [];
  } finally {
    loading.value = false;
  }
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height>
    <Card class="mb-3" size="small">
      <div class="flex items-center justify-between">
        <span class="text-sm text-muted-foreground">
          메모리 상태와 부하를 유발하는 쿼리
        </span>
        <Button :loading="loading" @click="loadData">새로고침</Button>
      </div>
    </Card>

    <Spin :spinning="loading">
      <Row :gutter="[12, 12]">
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic :value="Number(memory.PLE ?? 0)" title="PLE" />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :value="Number(memory.PAGEIOLATCH_WAIT_MS ?? 0)"
              suffix="ms"
              title="PAGEIOLATCH 대기"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :value="Number(memory.LazyWrites_Sec ?? 0)"
              title="LazyWrites/sec"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic :value="memory.Memory_State ?? '-'" title="메모리 상태" />
          </Card>
        </Col>
      </Row>

      <Card class="mt-3" size="small" title="메모리 상세">
        <Descriptions :column="{ md: 3, xs: 1 }" bordered size="small">
          <DescriptionsItem label="버퍼 풀">
            {{ memory.BufferPool_MB ?? '-' }} MB
          </DescriptionsItem>
          <DescriptionsItem label="플랜 캐시">
            {{ memory.PlanCache_MB ?? '-' }} MB
          </DescriptionsItem>
          <DescriptionsItem label="기타 메모리">
            {{ memory.Other_Memory_MB ?? '-' }} MB
          </DescriptionsItem>
          <DescriptionsItem label="가용 RAM">
            {{ memory.Available_RAM_MB ?? '-' }} MB
          </DescriptionsItem>
          <DescriptionsItem label="PageReads/sec">
            {{ memory.PageReads_Sec ?? '-' }}
          </DescriptionsItem>
          <DescriptionsItem label="점검 시각">
            {{ memory.CHECK_TIME ?? '-' }}
          </DescriptionsItem>
        </Descriptions>
      </Card>

      <Card
        :body-style="{ padding: 0 }"
        class="mt-3"
        size="small"
        title="부하 상위 쿼리"
      >
        <Table
          :columns="queryColumns"
          :data-source="heavyQueries"
          :pagination="false"
          :scroll="{ x: 900 }"
          row-key="QueryText"
          size="small"
        >
          <template #emptyText>
            <Empty description="데이터가 없습니다." />
          </template>
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'QueryText'">
              <Tooltip :title="record.QueryText">
                <span class="font-mono text-xs">{{ record.QueryText }}</span>
              </Tooltip>
            </template>
          </template>
        </Table>
      </Card>
    </Spin>
  </Page>
</template>
