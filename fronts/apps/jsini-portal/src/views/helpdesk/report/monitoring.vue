<script lang="ts" setup>
import { onBeforeUnmount, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Col,
  Row,
  Space,
  Spin,
  Statistic,
  Switch,
  Tag,
} from 'ant-design-vue';

import { getServerReport } from '#/api/helpdesk';

import OadrSeriesChart from './modules/oadr-series-chart.vue';
import OadrTable from './modules/oadr-table.vue';

/**
 * [운영 모니터링]
 *
 * 원본(reports/Monitoring.vue). 한주 OADR 시스템의 P_QURI_SERVER_REPORT 를
 * REALTIME / MONITORING / DAILY / KPI 네 종류로 불러 한 화면에 모은다.
 */

const loading = ref(false);
const autoRefresh = ref(true);
const realtime = ref<Record<string, any>>({});
const monitoring = ref<Record<string, any>[]>([]);
const daily = ref<Record<string, any>[]>([]);
const kpi = ref<Record<string, any>>({});

let timer: null | ReturnType<typeof setInterval> = null;

const MONITORING_FIELDS = [
  { color: '#42A5F5', key: 'CPU_SQL_USAGE', label: 'CPU(%)' },
  { color: '#FFA726', key: 'AVG_IO_LATENCY_MS', label: 'IO 지연(ms)' },
  { color: '#66BB6A', key: 'PLE', label: 'PLE', yAxis: 1 },
];

const DAILY_FIELDS = [
  { color: '#42A5F5', key: 'Avg_CPU', label: '평균 CPU(%)' },
  { color: '#FFA726', key: 'Peak_IO_ms', label: '최대 IO(ms)' },
];

/** 서버 상태 문자열에 맞는 색 */
function stateColor(state?: string) {
  if (!state) return 'default';
  const s = state.toUpperCase();
  if (s.includes('CRITICAL') || s.includes('BAD')) return 'error';
  if (s.includes('WARN')) return 'warning';
  return 'success';
}

async function loadData() {
  loading.value = true;
  try {
    const [rt, mon, day, k] = await Promise.all([
      getServerReport<Record<string, any>[]>('REALTIME'),
      getServerReport<Record<string, any>[]>('MONITORING'),
      getServerReport<Record<string, any>[]>('DAILY'),
      getServerReport<Record<string, any>[]>('KPI'),
    ]);

    realtime.value = rt?.[0] ?? {};
    monitoring.value = mon ?? [];
    daily.value = day ?? [];
    kpi.value = k?.[0] ?? {};
  } finally {
    loading.value = false;
  }
}

/** 1분마다 자동 새로고침. 원본과 같은 주기. */
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
          <Tag :color="stateColor(realtime.Server_State)">
            {{ realtime.Server_State ?? '상태 미상' }}
          </Tag>
          <span class="text-xs text-muted-foreground">
            {{ realtime.CHECK_TIME }}
          </span>
        </Space>
        <Space>
          <span class="text-sm">자동 새로고침</span>
          <Switch :checked="autoRefresh" @change="onAutoRefreshChange as any" />
          <Button :loading="loading" @click="loadData">새로고침</Button>
        </Space>
      </div>
    </Card>

    <Spin :spinning="loading">
      <Row :gutter="[12, 12]">
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :precision="1"
              :value="Number(realtime.CPU_SQL_USAGE ?? 0)"
              suffix="%"
              title="SQL CPU"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :precision="1"
              :value="Number(realtime.AVG_IO_LATENCY_MS ?? 0)"
              suffix="ms"
              title="평균 IO 지연"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic :value="Number(realtime.PLE ?? 0)" title="PLE" />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :value="Number(realtime.BATCH_REQUESTS_SEC ?? 0)"
              title="Batch/sec"
            />
          </Card>
        </Col>
      </Row>

      <Card class="mt-3" size="small" title="최근 추이 (MONITORING)">
        <OadrSeriesChart
          :fields="MONITORING_FIELDS"
          :rows="monitoring"
          second-axis-name="PLE"
          x-field="CHECK_TIME"
        />
      </Card>

      <Row :gutter="[12, 12]" class="mt-3">
        <Col :lg="14" :xs="24">
          <Card size="small" title="시간대별 (DAILY)">
            <OadrSeriesChart
              :fields="DAILY_FIELDS"
              :rows="daily"
              type="bar"
              x-field="TimeSlot"
            />
          </Card>
        </Col>

        <Col :lg="10" :xs="24">
          <Card size="small" title="KPI">
            <Row :gutter="[12, 12]">
              <Col :span="12">
                <Statistic :value="Number(kpi.Total_Checks ?? 0)" title="점검 횟수" />
              </Col>
              <Col :span="12">
                <Statistic :value="Number(kpi.Min_PLE ?? 0)" title="최저 PLE" />
              </Col>
              <Col :span="12">
                <Statistic
                  :precision="3"
                  :value="Number(kpi.Avg_DataFile_Stall_sec ?? 0)"
                  suffix="s"
                  title="데이터파일 지연"
                />
              </Col>
              <Col :span="12">
                <Statistic
                  :precision="3"
                  :value="Number(kpi.Avg_Log_Stall_sec ?? 0)"
                  suffix="s"
                  title="로그 지연"
                />
              </Col>
            </Row>
          </Card>
        </Col>
      </Row>

      <Card class="mt-3" size="small" title="원본 데이터 (MONITORING)">
        <OadrTable :rows="monitoring" />
      </Card>
    </Spin>
  </Page>
</template>
