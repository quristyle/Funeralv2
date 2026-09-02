<script lang="ts" setup>
import { onMounted, reactive, ref } from 'vue';
import { useRoute } from 'vue-router';

import { Page } from '@vben/common-ui';

import { Card, RangePicker, Select, Space, Tag } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import GridIconButton from '#/components/GridIconButton.vue';
import { getDistinctFailureReasons, getPushLogs } from '#/api/helpdesk';

import { formatDateTime } from '#/views/helpdesk/shared/constants';

/**
 * [푸시 발송 이력]
 *
 * 푸시 현황 화면에서 실패 사유를 클릭해 들어오면 쿼리스트링으로 필터가 전달된다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 *
 * 시스템의 모든 표를 한 부품으로 모으기 위해서다. 그리드 공통 기능(이름줄 정렬 +
 * 필터 전용 행)은 `adapter/vxe-grid-features.ts` 가 자동으로 붙이므로
 * 이 화면에는 그에 관한 코드가 없다.
 *
 * **가져오기 방식은 그대로다** — 페이지도 정렬도 서버가 한다(`orderBy` · `page`).
 * 그래서 머리글 필터줄은 **지금 화면에 올라온 페이지 안에서만** 걸린다.
 * 전체에서 걸러야 하는 조건(상태 · 기간 · 실패 사유)은 위의 조회 줄이 맡는다.
 * ------------------------------------------------------------
 */

const route = useRoute();

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

/** endpoint URL 에서 호스트만 뽑아 '발송 서버'로 보여준다(원본과 동일). */
function endpointHost(endpoint?: string) {
  if (!endpoint) return '-';
  try {
    return new URL(endpoint).hostname;
  } catch {
    return endpoint.slice(0, 30);
  }
}

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      // 정렬은 서버가 맡는데 `isSuccess` · `createdAt` 두 칸만 받는다(원본과 같다).
      // 나머지 칸은 정렬을 끈다 — 켜 두면 서버가 모르는 이름으로 `orderBy` 를 보낸다.
      {
        field: 'endpoint',
        minWidth: 150,
        // 같은 값을 두 가지로 보여 주는 칸이다. 필터가 훑을 글자를
        // 화면에 보이는 것(호스트)으로 맞춰 준다.
        params: {
          filterText: (row: any) => endpointHost(row.endpoint),
          sort: false,
        },
        slots: { default: 'server' },
        title: '발송 서버',
      },
      {
        field: 'endpointFull',
        minWidth: 260,
        params: { filterText: (row: any) => row.endpoint ?? '', sort: false },
        slots: { default: 'endpoint' },
        title: 'Endpoint',
      },
      {
        field: 'isSuccess',
        params: {
          filterOptions: [
            { label: '성공', value: true },
            { label: '실패', value: false },
          ],
        },
        slots: { default: 'isSuccess' },
        title: '상태',
        width: 100,
      },
      {
        field: 'failureReason',
        minWidth: 200,
        params: { sort: false },
        title: '실패 원인',
      },
      {
        field: 'createdAt',
        params: { filterText: (row: any) => formatDateTime(row.createdAt) },
        slots: { default: 'createdAt' },
        title: '발송 시간',
        width: 170,
      },
    ],
    emptyText: '발송 이력이 없습니다.',
    height: 'auto',
    pagerConfig: { enabled: true, pageSize: 20 },
    // 정렬은 서버가 한다. 한 페이지만 올라와 있어 화면에서 세우면 그 페이지만 선다.
    sortConfig: { multiple: false, remote: true },
    proxyConfig: {
      ajax: {
        query: async ({ page, sorts }: any) => {
          const sort = sorts?.[0];
          const params: Record<string, any> = {
            orderBy: sort?.field
              ? `${sort.field} ${sort.order === 'asc' ? 'asc' : 'desc'}`
              : 'createdAt desc',
            page: page?.currentPage ?? 1,
            pageSize: page?.pageSize ?? 20,
          };

          if (filters.status === 'success') params.isSuccess = true;
          if (filters.status === 'failure') params.isSuccess = false;
          if (filters.range?.[0]) params.startDate = filters.range[0];
          if (filters.range?.[1]) params.endDate = filters.range[1];
          if (filters.reasons.length > 0)
            params.reasons = filters.reasons.join(',');

          const result = await getPushLogs(params);
          return { page: { total: result.totalCount }, result: result.items };
        },
      },
    },
    rowConfig: { keyField: 'id' },
  },
});

function search() {
  gridApi.query();
}

onMounted(async () => {
  const reasons = await getDistinctFailureReasons().catch(() => []);
  reasonOptions.value = (reasons ?? []).map((r: string) => ({
    label: r,
    value: r,
  }));
});
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
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
        </Space>
        <!-- 동작 단추는 오른쪽에 모은다 — 조회도 동작이다. -->
        <GridIconButton icon="vxe-icon-search" title="조회" @click="search" />
      </div>
    </Card>

    <Grid>
      <template #server="{ row }">{{ endpointHost(row.endpoint) }}</template>
      <template #endpoint="{ row }">
        <span class="font-mono text-[11px]">{{ row.endpoint }}</span>
      </template>
      <template #createdAt="{ row }">
        {{ formatDateTime(row.createdAt) }}
      </template>
      <template #isSuccess="{ row }">
        <Tag :color="row.isSuccess ? 'success' : 'error'">
          {{ row.isSuccess ? '성공' : '실패' }}
        </Tag>
      </template>
    </Grid>
  </Page>
</template>
