<script lang="ts" setup>
import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button, Card, Col, Row, Spin, Statistic } from 'ant-design-vue';

import { getServerReport } from '#/api/helpdesk';

import OadrSeriesChart from './modules/oadr-series-chart.vue';
import OadrTable from './modules/oadr-table.vue';

/**
 * [용량 계획]
 *
 * 원본(reports/CapacityPlanning.vue). 한주 OADR 시스템의 P_QURI_SERVER_REPORT 를
 * QueryType=MONTHLY 로 호출한다.
 */

const loading = ref(false);
const rows = ref<Record<string, any>[]>([]);
const executive = ref<Record<string, any>>({});
const memory = ref<Record<string, any>>({});

const FIELDS = [
  { color: '#42A5F5', key: 'Avg_PLE', label: '평균 PLE' },
  { color: '#EF4444', key: 'Min_PLE', label: '최저 PLE' },
];

async function loadData() {
  loading.value = true;
  try {
    const [monthly, exec, mem] = await Promise.all([
      getServerReport<Record<string, any>[]>('MONTHLY'),
      getServerReport<Record<string, any>[]>('EXECUTIVE'),
      getServerReport<Record<string, any>[]>('MEM_DETAIL'),
    ]);
    rows.value = monthly ?? [];
    executive.value = exec?.[0] ?? {};
    memory.value = mem?.[0] ?? {};
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
        <span class="text-sm text-muted-foreground">메모리·용량 추세로 본 증설 판단 근거</span>
        <Button :loading="loading" @click="loadData">새로고침</Button>
      </div>
    </Card>

    <Spin :spinning="loading">
      <Row :gutter="[12, 12]" class="mb-3">
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :precision="1"
              :value="Number(executive.Server_Health_Score ?? 0)"
              title="서버 건강 점수"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :value="Number(memory.BufferPool_MB ?? 0)"
              suffix="MB"
              title="버퍼 풀"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :value="Number(memory.Available_RAM_MB ?? 0)"
              suffix="MB"
              title="가용 메모리"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :value="memory.Memory_State ?? '-'"
              title="메모리 상태"
            />
          </Card>
        </Col>
      </Row>

      <Card size="small" title="PLE 추세">
        <OadrSeriesChart
          :fields="FIELDS"
          :rows="rows"
          type="line"
          x-field="LogDate"
        />
      </Card>

      <Card class="mt-3" size="small" title="원본 데이터">
        <OadrTable :rows="rows" />
      </Card>
    </Spin>
  </Page>
</template>
