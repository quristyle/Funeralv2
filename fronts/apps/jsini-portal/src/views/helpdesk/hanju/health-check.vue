<script lang="ts" setup>
import { onBeforeUnmount, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Card,
  Descriptions,
  DescriptionsItem,
  Empty,
  Space,
  Spin,
  Switch,
  Table,
  Tag,
} from 'ant-design-vue';

import { oadrGet } from '#/api/helpdesk';

/**
 * [한주 설비 헬스체크]
 *
 * 원본(hanju/HealthCheck.vue). OADR 의 `/health` 와 `/health/All_un_MC` 두 곳을 읽는다.
 */

const loading = ref(false);
const autoRefresh = ref(true);
const serverStatus = ref('Unknown');
const mainData = ref<Record<string, any>>({});
const checks = ref<Record<string, any>[]>([]);
const mcStatus = ref('Unknown');
const mcChecks = ref<Record<string, any>[]>([]);
const errorMessage = ref('');

let timer: null | ReturnType<typeof setInterval> = null;

const columns = [
  { dataIndex: 'name', key: 'name', title: '항목', width: 220 },
  { dataIndex: 'status', key: 'status', title: '상태', width: 110 },
  { dataIndex: 'description', key: 'description', title: '설명', ellipsis: true },
  { dataIndex: 'totalCount', key: 'totalCount', title: '전체', width: 80 },
  { dataIndex: 'healthyCount', key: 'healthyCount', title: '정상', width: 80 },
  { dataIndex: 'unhealthyCount', key: 'unhealthyCount', title: '이상', width: 80 },
  { dataIndex: 'duration', key: 'duration', title: '소요', width: 110 },
];

/**
 * 점검 항목의 data 안에는 설비 상세가 들어 있다.
 * 서버가 `{ PET1: [ {mcId, mcName, gaudt, status}, ... ] }` 처럼 그룹 키로 감싸 주므로
 * 그룹을 풀어 한 줄씩 펼친다(원본 HealthCheck.vue 가 표로 보여주던 부분).
 */
function detailRows(check: Record<string, any>) {
  const data = check?.data;
  if (!data) return [];
  if (Array.isArray(data)) return data;

  return Object.entries(data).flatMap(([group, list]) =>
    Array.isArray(list)
      ? list.map((item: any) => ({ ...item, __group: group }))
      : [],
  );
}

const detailColumns = [
  { dataIndex: '__group', key: '__group', title: '그룹', width: 90 },
  { dataIndex: 'mcId', key: 'mcId', title: 'ID', width: 100 },
  { dataIndex: 'mcName', key: 'mcName', title: '설비명' },
  { dataIndex: 'gaudt', key: 'gaudt', title: 'Last', width: 170 },
  { dataIndex: 'status', key: 'status', title: '상태', width: 100 },
];

/** 상태 문자열에 맞는 색 */
function statusColor(status?: string) {
  const s = (status ?? '').toLowerCase();
  if (s === 'healthy') return 'success';
  if (s === 'degraded') return 'warning';
  if (s === 'unhealthy' || s === 'error' || s === 'timeout') return 'error';
  return 'default';
}

async function loadData() {
  loading.value = true;
  errorMessage.value = '';

  const [health, mc] = await Promise.allSettled([
    oadrGet<any>('/health'),
    oadrGet<any>('/health/All_un_MC'),
  ]);

  if (health.status === 'fulfilled') {
    mainData.value = health.value ?? {};
    serverStatus.value = health.value?.healthChecks?.status ?? 'Unknown';
    checks.value = health.value?.healthChecks?.checks ?? [];
  } else {
    serverStatus.value = 'Error';
    checks.value = [];
    errorMessage.value = '서버 헬스체크를 불러오지 못했습니다.';
  }

  if (mc.status === 'fulfilled') {
    mcStatus.value = mc.value?.status ?? 'Unknown';
    mcChecks.value = mc.value?.checks ?? [];
  } else {
    mcStatus.value = 'Error';
    mcChecks.value = [];
  }

  loading.value = false;
}

function startTimer() {
  stopTimer();
  timer = setInterval(loadData, 60_000);
}

function stopTimer() {
  if (timer) clearInterval(timer);
  timer = null;
}

function onAutoRefreshChange(value: boolean) {
  autoRefresh.value = value;
  if (value) {
    startTimer();
  } else {
    stopTimer();
  }
}

onMounted(async () => {
  await loadData();
  if (autoRefresh.value) startTimer();
});

onBeforeUnmount(stopTimer);
</script>

<template>
  <Page auto-content-height>
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space>
          <span class="text-sm">서버</span>
          <Tag :color="statusColor(serverStatus)">{{ serverStatus }}</Tag>
          <span class="text-sm">설비(MC)</span>
          <Tag :color="statusColor(mcStatus)">{{ mcStatus }}</Tag>
        </Space>
        <Space>
          <span class="text-sm">자동 새로고침</span>
          <Switch :checked="autoRefresh" @change="onAutoRefreshChange as any" />
          <Button :loading="loading" @click="loadData">새로고침</Button>
        </Space>
      </div>
    </Card>

    <Alert
      v-if="errorMessage"
      class="mb-3"
      :message="errorMessage"
      show-icon
      type="error"
    />

    <Spin :spinning="loading">
      <Card class="mb-3" size="small" title="서버 정보">
        <Descriptions :column="{ md: 2, xs: 1 }" size="small">
          <DescriptionsItem label="기동 시각">
            {{ mainData.startTime ?? '-' }}
          </DescriptionsItem>
          <DescriptionsItem label="가동 시간">
            {{ mainData.uptime ?? '-' }}
          </DescriptionsItem>
        </Descriptions>
      </Card>

      <Card :body-style="{ padding: 0 }" size="small" title="서버 점검 항목">
        <Table
          :columns="columns"
          :data-source="checks"
          :pagination="false"
          :scroll="{ x: 900 }"
          row-key="name"
          size="small"
        >
          <template #emptyText>
            <Empty description="점검 결과가 없습니다." />
          </template>

          <template #expandedRowRender="{ record }">
            <Table
              :columns="detailColumns"
              :data-source="detailRows(record)"
              :pagination="false"
              :scroll="{ y: 260 }"
              row-key="mcId"
              size="small"
            >
              <template #emptyText>
                <Empty description="상세 항목이 없습니다." />
              </template>
              <template #bodyCell="{ column: c, record: r }">
                <template v-if="c.key === 'status'">
                  <Tag :color="statusColor(r.status)">{{ r.status }}</Tag>
                </template>
              </template>
            </Table>
          </template>

          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'status'">
              <Tag :color="statusColor(record.status)">{{ record.status }}</Tag>
            </template>
          </template>
        </Table>
      </Card>

      <Card
        :body-style="{ padding: 0 }"
        class="mt-3"
        size="small"
        title="미수집 설비 점검"
      >
        <Table
          :columns="columns"
          :data-source="mcChecks"
          :pagination="false"
          :scroll="{ x: 900 }"
          row-key="name"
          size="small"
        >
          <template #emptyText>
            <Empty description="점검 결과가 없습니다." />
          </template>

          <!-- 점검 항목을 펼치면 설비 상세가 나온다 -->
          <template #expandedRowRender="{ record }">
            <Table
              :columns="detailColumns"
              :data-source="detailRows(record)"
              :pagination="false"
              :scroll="{ y: 260 }"
              row-key="mcId"
              size="small"
            >
              <template #emptyText>
                <Empty description="상세 항목이 없습니다." />
              </template>
              <template #bodyCell="{ column: c, record: r }">
                <template v-if="c.key === 'status'">
                  <Tag :color="statusColor(r.status)">{{ r.status }}</Tag>
                </template>
              </template>
            </Table>
          </template>

          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'status'">
              <Tag :color="statusColor(record.status)">{{ record.status }}</Tag>
            </template>
          </template>
        </Table>
      </Card>
    </Spin>
  </Page>
</template>
