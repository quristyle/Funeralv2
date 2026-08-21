<script lang="ts" setup>
import { ref, onMounted, computed } from 'vue';
import { Page } from '@vben/common-ui';
import { Card, Row, Col, Progress, Table, Badge, message } from 'ant-design-vue';
import { getFuneralStatuses } from '#/api/funeral/status';

const list = ref<any[]>([]);
const loading = ref<boolean>(false);

async function fetchStatuses() {
  loading.value = true;
  try {
    const data = await getFuneralStatuses();
    list.value = data || [];
  } catch (error) {
    message.error('현황 데이터 조회 실패');
  } finally {
    loading.value = false;
  }
}

// 통계치 계산
const stats = computed(() => {
  const total = list.value.length;
  const using = list.value.filter(item => item.status === 'USING').length;
  const empty = total - using;
  const rate = total > 0 ? Math.round((using / total) * 100) : 0;
  return { total, using, empty, rate };
});

const columns = [
  { title: '빈소명', dataIndex: 'roomName', key: 'roomName', width: 120 },
  { title: '상태', dataIndex: 'status', key: 'status', width: 100 },
  { title: '고인명', dataIndex: 'deceasedName', key: 'deceasedName', width: 120 },
  { title: '상주', dataIndex: 'chiefMourner', key: 'chiefMourner', width: 180 },
  { title: '입관 일시', dataIndex: 'coffinTime', key: 'coffinTime', width: 160 },
  { title: '발인 일시', dataIndex: 'dischargeTime', key: 'dischargeTime', width: 160 },
  { title: '장지', dataIndex: 'burialPlace', key: 'burialPlace', width: 180 }
];

onMounted(() => {
  fetchStatuses();
});
</script>

<template>
  <Page auto-content-height>
    <!-- 통계 요약 카드 영역 -->
    <Row :gutter="16" class="mb-6">
      <Col :span="6">
        <Card title="전체 빈소" class="text-center">
          <div class="text-3xl font-extrabold text-primary">{{ stats.total }}개소</div>
        </Card>
      </Col>
      <Col :span="6">
        <Card title="사용 중 빈소" class="text-center">
          <div class="text-3xl font-extrabold text-red-600">{{ stats.using }}개소</div>
        </Card>
      </Col>
      <Col :span="6">
        <Card title="잔여 빈소" class="text-center">
          <div class="text-3xl font-extrabold text-green-600">{{ stats.empty }}개소</div>
        </Card>
      </Col>
      <Col :span="6">
        <Card title="빈소 가동률" class="text-center">
          <div class="flex items-center justify-center gap-4">
            <Progress type="circle" :percent="stats.rate" :size="50" stroke-color="#ff4d4f" />
            <span class="text-2xl font-bold">{{ stats.rate }}%</span>
          </div>
        </Card>
      </Col>
    </Row>

    <!-- 현황 테이블 -->
    <Card title="빈소 통합 실시간 이용 현황" :loading="loading">
      <Table :data-source="list" :columns="columns" :pagination="false" row-key="roomId" size="middle">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <Badge
              :status="record.status === 'USING' ? 'error' : 'default'"
              :text="record.status === 'USING' ? '이용중' : '대기중'"
            />
          </template>
          <template v-else-if="column.key === 'deceasedName'">
            <span v-if="record.status === 'USING'" class="font-semibold text-foreground">
              {{ record.deceasedName }}
              <span class="text-xs text-muted-foreground ml-1">({{ record.deceasedGender === 'MALE' ? '남' : '여' }}, {{ record.deceasedAge }}세)</span>
            </span>
            <span v-else class="text-muted-foreground">-</span>
          </template>
          <template v-else-if="!(column as any).dataIndex || !record[(column as any).dataIndex]">
            <span class="text-muted-foreground">-</span>
          </template>
        </template>
      </Table>
    </Card>
  </Page>
</template>
