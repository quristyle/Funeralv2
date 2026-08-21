<script lang="ts" setup>
import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Card,
  Col,
  Empty,
  Row,
  Spin,
  Statistic,
  Table,
  Tag,
} from 'ant-design-vue';

import { getServerReport } from '#/api/helpdesk';

/**
 * [장애 예측]
 *
 * 원본(reports/Prediction.vue). OADR 의 PREDICTION 쿼리는
 * 증상(Symptom)별 발생 횟수(Occurrences)를 돌려준다.
 */

const loading = ref(false);
const symptoms = ref<Record<string, any>[]>([]);
const executive = ref<Record<string, any>>({});

const columns = [
  { dataIndex: 'Symptom', key: 'Symptom', title: '증상' },
  { dataIndex: 'Occurrences', key: 'Occurrences', title: '발생 횟수', width: 120 },
];

/** 발생 횟수에 따라 위험도를 나눈다. */
function riskColor(count: number) {
  if (count >= 10) return 'error';
  if (count >= 3) return 'warning';
  return 'default';
}

async function loadData() {
  loading.value = true;
  try {
    const [pred, exec] = await Promise.all([
      getServerReport<Record<string, any>[]>('PREDICTION'),
      getServerReport<Record<string, any>[]>('EXECUTIVE'),
    ]);
    symptoms.value = pred ?? [];
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
        <span class="text-sm text-muted-foreground">
          최근 관측된 이상 징후로 본 장애 가능성
        </span>
        <Button :loading="loading" @click="loadData">새로고침</Button>
      </div>
    </Card>

    <Spin :spinning="loading">
      <Row :gutter="[12, 12]" class="mb-3">
        <Col :lg="8" :xs="12">
          <Card size="small">
            <Statistic
              :precision="1"
              :value="Number(executive.Server_Health_Score ?? 0)"
              title="서버 건강 점수"
            />
          </Card>
        </Col>
        <Col :lg="8" :xs="12">
          <Card size="small">
            <Statistic :value="symptoms.length" title="감지된 증상" />
          </Card>
        </Col>
      </Row>

      <Alert
        v-if="symptoms.length === 0 && !loading"
        class="mb-3"
        message="감지된 이상 징후가 없습니다."
        show-icon
        type="success"
      />

      <Card :body-style="{ padding: 0 }" size="small" title="증상별 발생 횟수">
        <Table
          :columns="columns"
          :data-source="symptoms"
          :pagination="false"
          row-key="Symptom"
          size="small"
        >
          <template #emptyText>
            <Empty description="데이터가 없습니다." />
          </template>
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'Occurrences'">
              <Tag :color="riskColor(Number(record.Occurrences ?? 0))">
                {{ record.Occurrences }}
              </Tag>
            </template>
          </template>
        </Table>
      </Card>
    </Spin>
  </Page>
</template>
