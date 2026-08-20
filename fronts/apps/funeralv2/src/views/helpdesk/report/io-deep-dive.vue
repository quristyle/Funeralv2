<script lang="ts" setup>
import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button, Card, Spin } from 'ant-design-vue';

import { getServerReport } from '#/api/helpdesk';

import OadrSeriesChart from './modules/oadr-series-chart.vue';
import OadrTable from './modules/oadr-table.vue';

/**
 * [IO 정밀 분석]
 *
 * 원본(reports/IODeepDive.vue). 한주 OADR 시스템의 P_QURI_SERVER_REPORT 를
 * QueryType=IO_DETAIL 로 호출한다.
 */

const loading = ref(false);
const rows = ref<Record<string, any>[]>([]);

const FIELDS = [
  { color: '#EF4444', key: 'Disk_Latency_ms', label: '디스크 지연(ms)' },
  { color: '#42A5F5', key: 'DataFile_Read_Stall_sec', label: '데이터파일 지연(s)', yAxis: 1 },
  { color: '#FFA726', key: 'TempDB_Stall_sec', label: 'TempDB 지연(s)', yAxis: 1 },
  { color: '#66BB6A', key: 'Log_Stall_sec', label: '로그 지연(s)', yAxis: 1 },
];

async function loadData() {
  loading.value = true;
  try {
    rows.value = (await getServerReport<Record<string, any>[]>('IO_DETAIL')) ?? [];
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
        <span class="text-sm text-muted-foreground">디스크 IO 지연 상세</span>
        <Button :loading="loading" @click="loadData">새로고침</Button>
      </div>
    </Card>

    <Spin :spinning="loading">

      <Card size="small" title="IO 지연 추이">
        <OadrSeriesChart
          :fields="FIELDS"
          :rows="rows"
          type="line"
          x-field="CHECK_TIME"
        />
      </Card>

      <Card class="mt-3" size="small" title="원본 데이터">
        <OadrTable :rows="rows" />
      </Card>
    </Spin>
  </Page>
</template>
