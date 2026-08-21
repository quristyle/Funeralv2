<script lang="ts" setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Checkbox,
  Col,
  Empty,
  Input,
  Row,
  Segmented,
  Space,
  Spin,
  Switch,
  Table,
  Tag,
  Tree,
} from 'ant-design-vue';

import { executeProcedure } from '#/api/helpdesk';

import OadrSeriesChart from '../report/modules/oadr-series-chart.vue';

/**
 * [수집 현황]
 *
 * 원본(JinReception hanju/CollectionStatus.vue, `/collection-status`).
 *
 *  - P_QURI_MC   : 설비·태그 목록 (구역/고객 기준으로 묶어 트리로 본다)
 *  - P_QURI_HOUR : 선택한 설비의 시간대별 수집량
 *
 * 원본과 같은 필터를 제공한다: 사용중만 / 유지보수 제외 / 이중화 대상만.
 * 이중화(dst_mc_id)가 걸린 설비는 원본 설비와 함께 수집량을 비교해 보여준다.
 */

const loadingList = ref(false);
const loadingHour = ref(false);

const mcList = ref<Record<string, any>[]>([]);
const selectedMcId = ref<string | undefined>();
const hourly = ref<Record<string, any>[]>([]);
/** 이중화 대상 설비의 수집량. 원본과 나란히 비교한다. */
const dstHourly = ref<Record<string, any>[]>([]);

const treeFilter = ref('');
const groupingMode = ref<'CUST_ID' | 'GAUGE_SECTION' | 'NONE'>('GAUGE_SECTION');
const GROUPING_OPTIONS = [
  { label: '전체', value: 'NONE' },
  { label: '구역', value: 'GAUGE_SECTION' },
  { label: '고객', value: 'CUST_ID' },
];

const showOnlyActive = ref(true);
const showExcludeMaintenance = ref(true);
const showOnlyReplicated = ref(false);

const autoReload = ref(false);
const remainingSeconds = ref(60);
let reloadTimer: null | ReturnType<typeof setInterval> = null;

const mcColumns = [
  { dataIndex: 'mc_id', key: 'mc_id', title: '설비 ID', width: 110 },
  { dataIndex: 'DESCRIPT', key: 'DESCRIPT', title: '설명', ellipsis: true },
  { dataIndex: 'GAUGE_SECTION', key: 'GAUGE_SECTION', title: '구역', width: 130 },
  { dataIndex: 'MODEL', key: 'MODEL', title: '모델', width: 110 },
  { dataIndex: 'com_name', key: 'com_name', title: '고객사', width: 120 },
  { dataIndex: 'dst_mc_id', key: 'dst_mc_id', title: '이중화', width: 110 },
  {
    dataIndex: 'Maintenance_YN',
    key: 'Maintenance_YN',
    title: '유지보수',
    width: 90,
  },
];

/** 원본과 같은 3중 필터를 통과한 설비만 남긴다. */
const filteredMc = computed(() => {
  const kw = treeFilter.value.trim().toLowerCase();

  return mcList.value.filter((m) => {
    if (showOnlyActive.value && m.USE_FLAG && m.USE_FLAG !== 'Y') return false;
    if (showExcludeMaintenance.value && m.Maintenance_YN === 'Y') return false;
    if (showOnlyReplicated.value && !String(m.dst_mc_id ?? '').trim()) {
      return false;
    }
    if (!kw) return true;

    return `${m.mc_id} ${m.DESCRIPT ?? ''} ${m.GAUGE_SECTION ?? ''} ${m.com_name ?? ''}`
      .toLowerCase()
      .includes(kw);
  });
});

/** 선택한 기준으로 묶은 트리. 원본의 groupingMode 와 같다. */
const treeData = computed(() => {
  if (groupingMode.value === 'NONE') {
    return filteredMc.value.map((m) => ({
      key: String(m.mc_id),
      title: `${m.mc_id} · ${m.DESCRIPT ?? ''}`,
    }));
  }

  const groups = new Map<string, Record<string, any>[]>();
  filteredMc.value.forEach((m) => {
    const key = String(m[groupingMode.value] ?? '(미지정)');
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key)!.push(m);
  });

  return [...groups.entries()]
    .toSorted((a, b) => a[0].localeCompare(b[0]))
    .map(([group, items]) => ({
      children: items.map((m) => ({
        key: String(m.mc_id),
        title: `${m.mc_id} · ${m.DESCRIPT ?? ''}`,
      })),
      key: `group:${group}`,
      selectable: false,
      title: `${group} (${items.length})`,
    }));
});

const selectedMc = computed(() =>
  mcList.value.find((m) => String(m.mc_id) === selectedMcId.value),
);

/** 이중화 대상이 지정된 설비인지 */
const hasReplication = computed(() =>
  Boolean(String(selectedMc.value?.dst_mc_id ?? '').trim()),
);

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

const hourXField = computed(() => {
  const first = hourly.value[0];
  if (!first) return 'HOUR';
  return (
    ['HOUR', 'Hour', 'TimeSlot', 'hour'].find((k) => k in first) ??
    Object.keys(first)[0]!
  );
});

async function loadMcList() {
  loadingList.value = true;
  try {
    mcList.value =
      (await executeProcedure<Record<string, any>[]>('P_QURI_MC')) ?? [];
    if (!selectedMcId.value && filteredMc.value[0]) {
      selectedMcId.value = String(filteredMc.value[0].mc_id);
    }
  } finally {
    loadingList.value = false;
  }
}

async function loadHourly() {
  if (!selectedMcId.value) {
    hourly.value = [];
    dstHourly.value = [];
    return;
  }

  loadingHour.value = true;
  try {
    const calls: Promise<Record<string, any>[]>[] = [
      executeProcedure<Record<string, any>[]>('P_QURI_HOUR', [
        { name: '@MCID', value: selectedMcId.value },
      ]),
    ];

    // 이중화 설비가 있으면 함께 읽어 비교한다(원본과 동일).
    const dstId = String(selectedMc.value?.dst_mc_id ?? '').trim();
    if (dstId) {
      calls.push(
        executeProcedure<Record<string, any>[]>('P_QURI_HOUR', [
          { name: '@MCID', value: dstId },
        ]),
      );
    }

    const [main, dst] = await Promise.all(calls);
    hourly.value = main ?? [];
    dstHourly.value = dst ?? [];
  } catch {
    hourly.value = [];
    dstHourly.value = [];
  } finally {
    loadingHour.value = false;
  }
}

async function reloadAll() {
  await loadMcList();
  await loadHourly();
  remainingSeconds.value = 60;
}

function startTimer() {
  stopTimer();
  remainingSeconds.value = 60;
  reloadTimer = setInterval(() => {
    remainingSeconds.value -= 1;
    if (remainingSeconds.value <= 0) void reloadAll();
  }, 1000);
}

function stopTimer() {
  if (reloadTimer) clearInterval(reloadTimer);
  reloadTimer = null;
}

function onAutoReloadChange(value: boolean) {
  autoReload.value = value;
  if (value) {
    startTimer();
  } else {
    stopTimer();
  }
}

function onTreeSelect(keys: (number | string)[]) {
  const key = keys[0];
  if (key && !String(key).startsWith('group:')) {
    selectedMcId.value = String(key);
  }
}

watch(selectedMcId, loadHourly);

onMounted(async () => {
  await loadMcList();
  await loadHourly();
});

onBeforeUnmount(stopTimer);
</script>

<template>
  <Page auto-content-height>
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space wrap>
          <Segmented v-model:value="groupingMode" :options="GROUPING_OPTIONS" />
          <Input
            v-model:value="treeFilter"
            allow-clear
            placeholder="설비 · 구역 · 고객사"
            style="width: 200px"
          />
        </Space>

        <Space wrap>
          <Checkbox v-model:checked="showOnlyActive">사용중만</Checkbox>
          <Checkbox v-model:checked="showExcludeMaintenance">
            유지보수 제외
          </Checkbox>
          <Checkbox v-model:checked="showOnlyReplicated">이중화만</Checkbox>
        </Space>

        <Space>
          <span class="text-sm">자동 갱신</span>
          <Switch :checked="autoReload" @change="onAutoReloadChange as any" />
          <span v-if="autoReload" class="text-xs text-muted-foreground">
            {{ remainingSeconds }}초
          </span>
          <Button :loading="loadingList" @click="reloadAll">새로고침</Button>
        </Space>
      </div>
    </Card>

    <Row :gutter="[12, 12]">
      <!-- 설비 트리 -->
      <Col :lg="7" :xs="24">
        <Card
          :body-style="{ maxHeight: '520px', overflow: 'auto' }"
          size="small"
          :title="`설비 (${filteredMc.length})`"
        >
          <Spin :spinning="loadingList">
            <Tree
              :selected-keys="selectedMcId ? [selectedMcId] : []"
              :tree-data="treeData"
              default-expand-all
              @select="onTreeSelect"
            />
            <Empty
              v-if="filteredMc.length === 0 && !loadingList"
              description="조건에 맞는 설비가 없습니다."
            />
          </Spin>
        </Card>
      </Col>

      <Col :lg="17" :xs="24">
        <!-- 선택 설비 요약 -->
        <Card v-if="selectedMc" class="mb-3" size="small">
          <Space wrap>
            <Tag color="blue">{{ selectedMc.mc_id }}</Tag>
            <span class="font-medium">{{ selectedMc.DESCRIPT }}</span>
            <span class="text-xs text-muted-foreground">
              {{ selectedMc.GAUGE_SECTION }} · {{ selectedMc.com_name }}
            </span>
            <Tag v-if="hasReplication" color="purple">
              이중화 → {{ selectedMc.dst_mc_id }}
            </Tag>
            <Tag v-if="selectedMc.Maintenance_YN === 'Y'" color="warning">
              유지보수
            </Tag>
          </Space>
        </Card>

        <!-- 수집량 -->
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

        <!-- 이중화 비교 -->
        <Card
          v-if="hasReplication"
          class="mb-3"
          size="small"
          :title="`이중화 대상 수집량 (${selectedMc?.dst_mc_id})`"
        >
          <Spin :spinning="loadingHour">
            <OadrSeriesChart
              v-if="dstHourly.length > 0"
              :fields="hourFields"
              :rows="dstHourly"
              type="bar"
              :x-field="hourXField"
            />
            <Empty v-else description="이중화 대상 수집 데이터가 없습니다." />
          </Spin>
        </Card>

        <!-- 설비 목록 표 -->
        <Card :body-style="{ padding: 0 }" size="small" title="설비 목록">
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
            :scroll="{ x: 900 }"
            row-key="tag_id"
            size="small"
          >
            <template #emptyText>
              <Empty description="조건에 맞는 설비가 없습니다." />
            </template>
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'Maintenance_YN'">
                <Tag :color="record.Maintenance_YN === 'Y' ? 'warning' : 'default'">
                  {{ record.Maintenance_YN }}
                </Tag>
              </template>
              <template v-else-if="column.key === 'dst_mc_id'">
                <Tag v-if="String(record.dst_mc_id ?? '').trim()" color="purple">
                  {{ record.dst_mc_id }}
                </Tag>
                <span v-else class="text-muted-foreground">-</span>
              </template>
            </template>
          </Table>
        </Card>
      </Col>
    </Row>
  </Page>
</template>
