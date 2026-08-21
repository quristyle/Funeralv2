<script lang="ts" setup>
import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button, Card, Col, Row, Spin, Statistic } from 'ant-design-vue';

import { getServerReport } from '#/api/helpdesk';

import OadrSeriesChart from './modules/oadr-series-chart.vue';
import OadrTable from './modules/oadr-table.vue';

/**
 * [월간 리포트]
 *
 * 원본(reports/MonthlyReport.vue). 한주 OADR 시스템의 P_QURI_SERVER_REPORT 를
 * QueryType=MONTHLY 로 호출한다.
 */

const loading = ref(false);
const rows = ref<Record<string, any>[]>([]);
const executive = ref<Record<string, any>>({});

const FIELDS = [
  { color: '#EF4444', key: 'Max_CPU', label: '최대 CPU(%)' },
  { color: '#FFA726', key: 'Avg_IO_ms', label: '평균 IO(ms)' },
  { color: '#66BB6A', key: 'Avg_PLE', label: '평균 PLE', yAxis: 1 },
];

async function loadData() {
  loading.value = true;
  try {
    const [monthly, exec] = await Promise.all([
      getServerReport<Record<string, any>[]>('MONTHLY'),
      getServerReport<Record<string, any>[]>('EXECUTIVE'),
    ]);
    rows.value = monthly ?? [];
    executive.value = exec?.[0] ?? {};
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
        <span class="text-sm text-muted-foreground">최근 월간 서버 지표</span>
        <Button :loading="loading" @click="loadData">새로고침</Button>
      </div>
    </Card>

    <Spin :spinning="loading">
      <Row :gutter="[12, 12]" class="mb-3">
        <Col :lg="8" :xs="24">
          <Card size="small">
            <Statistic
              :precision="1"
              :value="Number(executive.Server_Health_Score ?? 0)"
              title="서버 건강 점수"
            />
          </Card>
        </Col>
      </Row>

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
