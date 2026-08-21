<script lang="ts" setup>
import { ref, onMounted, computed } from 'vue';
import { Page } from '@vben/common-ui';
import { Card, Row, Col, Input, Badge, Table, message } from 'ant-design-vue';
import { getDeceasedList } from '#/api/funeral/building';

const deceasedList = ref<any[]>([]);
const loading = ref<boolean>(false);
const searchText = ref<string>('');

async function fetchDeceased() {
  loading.value = true;
  try {
    const data = await getDeceasedList();
    deceasedList.value = data || [];
  } catch (error) {
    message.error('고인 현황 로드 실패');
  } finally {
    loading.value = false;
  }
}

// 검색 필터링
const filteredList = computed(() => {
  if (!searchText.value) return deceasedList.value;
  return deceasedList.value.filter(item =>
    item.name.includes(searchText.value) ||
    (item.roomName && item.roomName.includes(searchText.value))
  );
});

// 단계별 인원 집계
const stats = computed(() => {
  const inHospital = deceasedList.value.filter(item => item.status === 'IN_HOSPITAL').length;
  const discharged = deceasedList.value.filter(item => item.status === 'DISCHARGED').length;
  const completed = deceasedList.value.filter(item => item.status === 'COMPLETED').length;
  return { inHospital, discharged, completed };
});

const columns = [
  { title: '고인명', dataIndex: 'name', key: 'name', width: 120 },
  { title: '성별', dataIndex: 'gender', key: 'gender', width: 100 },
  { title: '연세', dataIndex: 'age', key: 'age', width: 100 },
  { title: '배정 빈소', dataIndex: 'roomName', key: 'roomName', width: 150 },
  { title: '종교', dataIndex: 'religion', key: 'religion', width: 120 },
  { title: '작고 일시', dataIndex: 'deathDate', key: 'deathDate', width: 180 },
  { title: '장례 단계', dataIndex: 'status', key: 'status', width: 130 }
];

onMounted(() => {
  fetchDeceased();
});
</script>

<template>
  <Page auto-content-height>
    <!-- 장례 진행 단계 현황 카드 -->
    <Row :gutter="16" class="mb-6">
      <Col :span="8">
        <Card class="bg-blue-50/50">
          <div class="text-xs text-muted-foreground font-semibold">장례 진행중</div>
          <div class="text-3xl font-extrabold text-blue-600 mt-2">{{ stats.inHospital }}명</div>
        </Card>
      </Col>
      <Col :span="8">
        <Card class="bg-orange-50/50">
          <div class="text-xs text-muted-foreground font-semibold">발인 완료</div>
          <div class="text-3xl font-extrabold text-orange-600 mt-2">{{ stats.discharged }}명</div>
        </Card>
      </Col>
      <Col :span="8">
        <Card class="bg-gray-50/50">
          <div class="text-xs text-muted-foreground font-semibold">장례 정산 완료</div>
          <div class="text-3xl font-extrabold text-gray-600 mt-2">{{ stats.completed }}명</div>
        </Card>
      </Col>
    </Row>

    <!-- 고인 현황 그리드 리스트 -->
    <Card title="실시간 고인 이송 및 진행 현황" :loading="loading">
      <template #extra>
        <Input.Search
          v-model:value="searchText"
          placeholder="고인명 또는 호실명 검색"
          style="width: 250px"
          allow-clear
        />
      </template>

      <Table :data-source="filteredList" :columns="columns" :pagination="{ pageSize: 10 }" row-key="id" size="middle">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'gender'">
            <span>{{ record.gender === 'MALE' ? '남성' : '여성' }}</span>
          </template>
          <template v-else-if="column.key === 'age'">
            <span>{{ record.age }}세</span>
          </template>
          <template v-else-if="column.key === 'status'">
            <Badge
              v-if="record.status === 'IN_HOSPITAL'"
              status="processing"
              text="장례 진행중"
            />
            <Badge
              v-else-if="record.status === 'DISCHARGED'"
              status="warning"
              text="발인 완료"
            />
            <Badge
              v-else
              status="default"
              text="정산 완료"
            />
          </template>
          <template v-else-if="!(column as any).dataIndex || !record[(column as any).dataIndex]">
            <span class="text-muted-foreground">-</span>
          </template>
        </template>
      </Table>
    </Card>
  </Page>
</template>
