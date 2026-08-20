<script lang="ts" setup>
import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button, Card, Col, Row, Spin, Statistic } from 'ant-design-vue';

import { getServerReport } from '#/api/helpdesk';

import OadrSeriesChart from './modules/oadr-series-chart.vue';
import OadrTable from './modules/oadr-table.vue';

/**
 * [가용성 분석]
 *
 * 원본(reports/SystemAvailability.vue). 한주 OADR 시스템의 P_QURI_SERVER_REPORT 를
 * QueryType=MONITORING 로 호출한다.
 */

const loading = ref(false);
const rows = ref<Record<string, any>[]>([]);
const realtime = ref<Record<string, any>>({});
const kpi = ref<Record<string, any>>({});

const FIELDS = [
  { color: '#42A5F5', key: 'CPU_SQL_USAGE', label: 'CPU(%)' },
  { color: '#FFA726', key: 'AVG_IO_LATENCY_MS', label: 'IO 지연(ms)' },
];

async function loadData() {
  loading.value = true;
  try {
    const [mon, rt, k] = await Promise.all([
      getServerReport<Record<string, any>[]>('MONITORING'),
      getServerReport<Record<string, any>[]>('REALTIME'),
      getServerReport<Record<string, any>[]>('KPI'),
    ]);
    rows.value = mon ?? [];
    realtime.value = rt?.[0] ?? {};
    kpi.value = k?.[0] ?? {};
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
        <span class="text-sm text-muted-foreground">서버 가용성 지표</span>
        <Button :loading="loading" @click="loadData">새로고침</Button>
      </div>
    </Card>

    <Spin :spinning="loading">
      <Row :gutter="[12, 12]" class="mb-3">
        <Col :lg="8" :xs="12">
          <Card size="small">
            <Statistic :value="Number(kpi.Total_Checks ?? 0)" title="점검 횟수" />
          </Card>
        </Col>
        <Col :lg="8" :xs="12">
          <Card size="small">
            <Statistic :value="Number(kpi.Min_PLE ?? 0)" title="최저 PLE" />
          </Card>
        </Col>
        <Col :lg="8" :xs="24">
          <Card size="small">
            <Statistic
              :value="realtime.Server_State ?? '-'"
              title="현재 상태"
            />
          </Card>
        </Col>
      </Row>

      <Card size="small" title="가용성 추이">
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
