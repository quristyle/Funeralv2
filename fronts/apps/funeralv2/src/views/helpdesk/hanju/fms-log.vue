<script lang="ts" setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Empty,
  Input,
  Space,
  Switch,
  Table,
  Tag,
} from 'ant-design-vue';

import { oadrGet } from '#/api/helpdesk';

import { formatDateTime } from '../shared/constants';

/**
 * [FMS 상태 로그]
 *
 * 원본(hanju/FmsStatusLog.vue). OADR 의 `/fms_chk` 를 읽어
 * 태그별 A/B 값 차이(gap)와 판정(chk)을 보여준다.
 */

const loading = ref(false);
const autoRefresh = ref(false);
const rows = ref<Record<string, any>[]>([]);
const keyword = ref('');
const lastFetchTime = ref('');

let timer: null | ReturnType<typeof setInterval> = null;

const columns = [
  { dataIndex: 'mcId', key: 'mcId', title: '설비 ID', width: 110 },
  { dataIndex: 'tagId', key: 'tagId', title: '태그', width: 140 },
  { dataIndex: 'tagValueA', key: 'tagValueA', title: 'A 값', width: 110 },
  { dataIndex: 'saveDtimeA', key: 'saveDtimeA', title: 'A 시각', width: 160 },
  { dataIndex: 'tagValueB', key: 'tagValueB', title: 'B 값', width: 110 },
  { dataIndex: 'saveDtimeB', key: 'saveDtimeB', title: 'B 시각', width: 160 },
  { dataIndex: 'gap', key: 'gap', title: '차이', width: 100 },
  { dataIndex: 'chk', key: 'chk', title: '판정', width: 90 },
];

const filteredRows = computed(() => {
  const kw = keyword.value.trim().toLowerCase();
  if (!kw) return rows.value;
  return rows.value.filter((r) =>
    `${r.mcId} ${r.tagId} ${r.chk}`.toLowerCase().includes(kw),
  );
});

/** 판정값이 정상인지에 따라 색을 준다. */
function chkColor(chk: any) {
  const v = String(chk ?? '').toUpperCase();
  if (v === 'OK' || v === 'Y') return 'success';
  if (v === 'NG' || v === 'N') return 'error';
  return 'default';
}

async function loadData() {
  loading.value = true;
  try {
    rows.value = (await oadrGet<Record<string, any>[]>('/fms_chk')) ?? [];
    lastFetchTime.value = new Date().toLocaleTimeString('ko-KR');
  } catch {
    rows.value = [];
  } finally {
    loading.value = false;
  }
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

onMounted(loadData);
onBeforeUnmount(stopTimer);
</script>

<template>
  <Page auto-content-height>
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space wrap>
          <Input
            v-model:value="keyword"
            allow-clear
            placeholder="설비 ID · 태그 · 판정"
            style="width: 240px"
          />
          <span class="text-xs text-muted-foreground">
            {{ filteredRows.length }}건
            <template v-if="lastFetchTime"> · {{ lastFetchTime }} 기준</template>
          </span>
        </Space>
        <Space>
          <span class="text-sm">자동 새로고침</span>
          <Switch :checked="autoRefresh" @change="onAutoRefreshChange as any" />
          <Button :loading="loading" @click="loadData">새로고침</Button>
        </Space>
      </div>
    </Card>

    <Card :body-style="{ padding: 0 }" size="small">
      <Table
        :columns="columns"
        :data-source="filteredRows"
        :loading="loading"
        :pagination="{ pageSize: 50, showSizeChanger: true }"
        :scroll="{ x: 1000 }"
        row-key="(record) => `${record.mcId}-${record.tagId}`"
        size="small"
      >
        <template #emptyText>
          <Empty description="수집된 로그가 없습니다." />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'chk'">
            <Tag :color="chkColor(record.chk)">{{ record.chk }}</Tag>
          </template>
          <template v-else-if="column.key === 'saveDtimeA'">
            {{ formatDateTime(record.saveDtimeA) }}
          </template>
          <template v-else-if="column.key === 'saveDtimeB'">
            {{ formatDateTime(record.saveDtimeB) }}
          </template>
        </template>
      </Table>
    </Card>
  </Page>
</template>
