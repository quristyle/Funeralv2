<script lang="ts" setup>
import { onMounted, reactive, ref } from 'vue';
import { useRoute } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Empty,
  RangePicker,
  Select,
  Space,
  Table,
  Tag,
} from 'ant-design-vue';

import { getDistinctFailureReasons, getPushLogs } from '#/api/helpdesk';

import { formatDateTime } from '../shared/constants';

/**
 * [푸시 발송 이력]
 *
 * 푸시 현황 화면에서 실패 사유를 클릭해 들어오면 쿼리스트링으로 필터가 전달된다.
 */

const route = useRoute();

const loading = ref(false);
const rows = ref<any[]>([]);
const reasonOptions = ref<{ label: string; value: string }[]>([]);

const filters = reactive<{
  range: [string, string] | undefined;
  reasons: string[];
  status: string;
}>({
  range: undefined,
  reasons: route.query.reason
    ? Array.isArray(route.query.reason)
      ? (route.query.reason as string[])
      : [route.query.reason as string]
    : [],
  status: (route.query.status as string) ?? 'all',
});

const STATUS_OPTIONS = [
  { label: '전체', value: 'all' },
  { label: '성공', value: 'success' },
  { label: '실패', value: 'failure' },
];

const pagination = reactive({
  current: 1,
  pageSize: 20,
  showSizeChanger: true,
  showTotal: (total: number) => `총 ${total}건`,
  total: 0,
});

const columns = [
  { dataIndex: 'createdAt', key: 'createdAt', title: '발송시각', width: 160 },
  { dataIndex: 'title', key: 'title', title: '제목', ellipsis: true },
  { dataIndex: 'userName', key: 'userName', title: '수신자', width: 120 },
  { dataIndex: 'isSuccess', key: 'isSuccess', title: '결과', width: 80 },
  { dataIndex: 'failureReason', key: 'failureReason', title: '실패 사유', ellipsis: true },
];

async function loadData() {
  loading.value = true;
  try {
    const params: Record<string, any> = {
      orderBy: 'createdAt desc',
      page: pagination.current,
      pageSize: pagination.pageSize,
    };

    if (filters.status === 'success') params.isSuccess = true;
    if (filters.status === 'failure') params.isSuccess = false;
    if (filters.range?.[0]) params.startDate = filters.range[0];
    if (filters.range?.[1]) params.endDate = filters.range[1];
    if (filters.reasons.length > 0) params.reasons = filters.reasons.join(',');

    const page = await getPushLogs(params);
    rows.value = page.items;
    pagination.total = page.totalCount;
  } finally {
    loading.value = false;
  }
}

function search() {
  pagination.current = 1;
  loadData();
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  pagination.current = pag.current ?? 1;
  pagination.pageSize = pag.pageSize ?? 20;
  loadData();
}

onMounted(async () => {
  const reasons = await getDistinctFailureReasons().catch(() => []);
  reasonOptions.value = (reasons ?? []).map((r: string) => ({
    label: r,
    value: r,
  }));
  await loadData();
});
</script>

<template>
  <Page auto-content-height>
    <Card class="mb-3" size="small">
      <Space wrap>
        <Select
          v-model:value="filters.status"
          :options="STATUS_OPTIONS"
          style="width: 110px"
          @change="search"
        />
        <RangePicker
          v-model:value="filters.range"
          value-format="YYYY-MM-DD"
          @change="search"
        />
        <Select
          v-model:value="filters.reasons"
          :options="reasonOptions"
          allow-clear
          mode="multiple"
          placeholder="실패 사유"
          style="min-width: 220px"
          @change="search"
        />
        <Button :loading="loading" type="primary" @click="search">조회</Button>
      </Space>
    </Card>

    <Card :body-style="{ padding: 0 }" size="small">
      <Table
        :columns="columns"
        :data-source="rows"
        :loading="loading"
        :pagination="pagination"
        :scroll="{ x: 800 }"
        row-key="id"
        size="small"
        @change="onTableChange"
      >
        <template #emptyText>
          <Empty description="발송 이력이 없습니다." />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'createdAt'">
            {{ formatDateTime(record.createdAt) }}
          </template>
          <template v-else-if="column.key === 'isSuccess'">
            <Tag :color="record.isSuccess ? 'success' : 'error'">
              {{ record.isSuccess ? '성공' : '실패' }}
            </Tag>
          </template>
        </template>
      </Table>
    </Card>
  </Page>
</template>
