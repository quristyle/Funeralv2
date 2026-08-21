<script lang="ts" setup>
import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button, Card, Spin } from 'ant-design-vue';

import { getServerReport } from '#/api/helpdesk';

import OadrSeriesChart from './modules/oadr-series-chart.vue';
import OadrTable from './modules/oadr-table.vue';

/**
 * [주간 리포트]
 *
 * 원본(reports/WeeklyReport.vue). 한주 OADR 시스템의 P_QURI_SERVER_REPORT 를
 * QueryType=WEEKLY 로 호출한다.
 */

const loading = ref(false);
const rows = ref<Record<string, any>[]>([]);

const FIELDS = [
  { color: '#42A5F5', key: 'Avg_CPU', label: '평균 CPU(%)' },
  { color: '#FFA726', key: 'Avg_IO_ms', label: '평균 IO(ms)' },
  { color: '#66BB6A', key: 'Min_PLE', label: '최저 PLE', yAxis: 1 },
];

async function loadData() {
  loading.value = true;
  try {
    rows.value = (await getServerReport<Record<string, any>[]>('WEEKLY')) ?? [];
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
        <span class="text-sm text-muted-foreground">최근 주간 서버 지표</span>
        <Button :loading="loading" @click="loadData">새로고침</Button>
      </div>
    </Card>

    <Spin :spinning="loading">

      <Card size="small" title="일자별 지표">
        <OadrSeriesChart
          :fields="FIELDS"
          :rows="rows"
          type="bar"
          x-field="LogDate"
        />
      </Card>

      <Card class="mt-3" size="small" title="원본 데이터">
        <OadrTable :rows="rows" />
      </Card>
    </Spin>
  </Page>
</template>
