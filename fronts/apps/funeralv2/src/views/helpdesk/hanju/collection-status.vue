<script lang="ts" setup>
import { computed, onMounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Empty,
  Input,
  Select,
  Space,
  Spin,
  Table,
  Tag,
} from 'ant-design-vue';

import { executeProcedure } from '#/api/helpdesk';

import OadrSeriesChart from '../report/modules/oadr-series-chart.vue';

/**
 * [수집 현황]
 *
 * 원본(hanju/CollectionStatus.vue).
 *  - P_QURI_MC : 설비·태그 목록
 *  - P_QURI_HOUR : 선택한 설비의 시간대별 수집량
 */

const loadingList = ref(false);
const loadingHour = ref(false);
const mcList = ref<Record<string, any>[]>([]);
const hourly = ref<Record<string, any>[]>([]);
const keyword = ref('');
const selectedMcId = ref<string | undefined>();

const mcColumns = [
  { dataIndex: 'mc_id', key: 'mc_id', title: '설비 ID', width: 110 },
  { dataIndex: 'DESCRIPT', key: 'DESCRIPT', title: '설명', ellipsis: true },
  { dataIndex: 'GAUGE_SECTION', key: 'GAUGE_SECTION', title: '구역', width: 130 },
  { dataIndex: 'MODEL', key: 'MODEL', title: '모델', width: 120 },
  { dataIndex: 'com_name', key: 'com_name', title: '고객사', width: 130 },
  {
    dataIndex: 'Maintenance_YN',
    key: 'Maintenance_YN',
    title: '유지보수',
    width: 90,
  },
];

/** 차트에 그릴 수집량 컬럼은 응답에서 자동으로 고른다. */
const hourFields = computed(() => {
  const first = hourly.value[0];
  if (!first) return [];

  const xKeys = new Set(['HOUR', 'Hour', 'TimeSlot', 'hour']);
  return Object.keys(first)
    .filter((k) => !xKeys.has(k) && typeof first[k] === 'number')
    .slice(0, 4)
    .map((k) => ({ key: k, label: k }));
});

/** 시간 축으로 쓸 컬럼 이름 */
const hourXField = computed(() => {
  const first = hourly.value[0];
  if (!first) return 'HOUR';
  return (
    ['HOUR', 'Hour', 'TimeSlot', 'hour'].find((k) => k in first) ??
    Object.keys(first)[0]!
  );
});

const filteredMc = computed(() => {
  const kw = keyword.value.trim().toLowerCase();
  if (!kw) return mcList.value;
  return mcList.value.filter((m) =>
    `${m.mc_id} ${m.DESCRIPT} ${m.GAUGE_SECTION} ${m.com_name}`
      .toLowerCase()
      .includes(kw),
  );
});

const mcOptions = computed(() =>
  mcList.value.map((m) => ({
    label: `${m.mc_id} · ${m.DESCRIPT ?? ''}`,
    value: String(m.mc_id),
  })),
);

async function loadMcList() {
  loadingList.value = true;
  try {
    mcList.value =
      (await executeProcedure<Record<string, any>[]>('P_QURI_MC')) ?? [];
    if (!selectedMcId.value && mcList.value[0]) {
      selectedMcId.value = String(mcList.value[0].mc_id);
    }
  } finally {
    loadingList.value = false;
  }
}

async function loadHourly() {
  if (!selectedMcId.value) {
    hourly.value = [];
    return;
  }

  loadingHour.value = true;
  try {
    hourly.value =
      (await executeProcedure<Record<string, any>[]>('P_QURI_HOUR', [
        { name: '@MCID', value: selectedMcId.value },
      ])) ?? [];
  } catch {
    hourly.value = [];
  } finally {
    loadingHour.value = false;
  }
}

watch(selectedMcId, loadHourly);

onMounted(async () => {
  await loadMcList();
  await loadHourly();
});
</script>

<template>
  <Page auto-content-height>
    <Card class="mb-3" size="small">
      <Space wrap>
        <Select
          v-model:value="selectedMcId"
          :options="mcOptions"
          option-filter-prop="label"
          placeholder="설비 선택"
          show-search
          style="width: 300px"
        />
        <Button :loading="loadingHour" @click="loadHourly">조회</Button>
      </Space>
    </Card>

    <Card class="mb-3" size="small" title="시간대별 수집량">
      <Spin :spinning="loadingHour">
        <OadrSeriesChart
          v-if="hourFields.length > 0"
          :fields="hourFields"
          :rows="hourly"
          type="bar"
          :x-field="hourXField"
        />
        <Empty v-else description="수집 데이터가 없습니다." />
      </Spin>
    </Card>

    <Card :body-style="{ padding: 0 }" size="small">
      <template #title>설비 목록</template>
      <template #extra>
        <Input
          v-model:value="keyword"
          allow-clear
          placeholder="설비 · 구역 · 고객사"
          size="small"
          style="width: 220px"
        />
      </template>

      <Table
        :columns="mcColumns"
        :custom-row="
          (record: any) => ({
            onClick: () => (selectedMcId = String(record.mc_id)),
            style: 'cursor: pointer',
          })
        "
        :data-source="filteredMc"
        :loading="loadingList"
        :pagination="{ pageSize: 20, showSizeChanger: true }"
        :scroll="{ x: 800 }"
        row-key="tag_id"
        size="small"
      >
        <template #emptyText>
          <Empty description="설비가 없습니다." />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'Maintenance_YN'">
            <Tag :color="record.Maintenance_YN === 'Y' ? 'warning' : 'default'">
              {{ record.Maintenance_YN }}
            </Tag>
          </template>
        </template>
      </Table>
    </Card>
  </Page>
</template>
