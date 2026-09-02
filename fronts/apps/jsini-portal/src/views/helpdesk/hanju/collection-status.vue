<script lang="ts" setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import {
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
  Tag,
  Tree,
} from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import GridIconButton from '#/components/GridIconButton.vue';
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
 *
 * ------------------------------------------------------------
 * [2026-08-30] 아래쪽 '설비 목록' 표를 ant-design-vue `<Table>` 에서
 * `useVbenVxeGrid` 로 옮겼다. 정렬·필터는 공통 레이어
 * (`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * **가져오기 방식은 그대로다** — `P_QURI_MC` 가 준 전량을 화면이 쥐고,
 * 위쪽 조회 줄(사용중만 · 유지보수 제외 · 이중화만 · 검색어)이 걸러 낸 것을
 * 표에 넘긴다. 원본의 프런트 페이징(20건)은 없앴다.
 * 행을 누르면 그 설비가 선택되는 것도 그대로다(`cellClick`).
 * ------------------------------------------------------------
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

const [McGrid, mcGridApi] = useVbenVxeGrid({
  gridEvents: {
    // 원본의 `custom-row` 클릭과 같은 자리 — 행을 누르면 그 설비를 고른다.
    cellClick: ({ row }: any) => {
      if (row?.mc_id) selectedMcId.value = String(row.mc_id);
    },
  },
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      { field: 'mc_id', title: '설비 ID', width: 110 },
      { field: 'DESCRIPT', minWidth: 200, title: '설명' },
      { field: 'GAUGE_SECTION', title: '구역', width: 130 },
      { field: 'MODEL', title: '모델', width: 110 },
      { field: 'com_name', title: '고객사', width: 120 },
      {
        field: 'dst_mc_id',
        slots: { default: 'dst_mc_id' },
        title: '이중화',
        width: 110,
      },
      {
        field: 'Maintenance_YN',
        // 값이 Y/N 둘뿐이라 고르는 칸으로 둔다.
        // 빈 값(`''`)짜리 항목은 넣지 않는다 — 공통 레이어가 빈 값을 '전체'로
        // 읽어서, 넣으면 맨 위의 '전체' 항목과 같은 값이 되어 겹친다.
        params: {
          filterOptions: [
            { label: 'Y', value: 'Y' },
            { label: 'N', value: 'N' },
          ],
        },
        slots: { default: 'Maintenance_YN' },
        title: '유지보수',
        width: 90,
      },
    ],
    // 행 배열은 `:table-data` 로 간다. 여기는 빈 배열이 바탕값이다.
    data: [],
    emptyText: '조건에 맞는 설비가 없습니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 위쪽 '새로고침' 이 부르는 것과 같은 함수를 준다.
    gridFeatures: { onRefresh: () => reloadAll() },
    height: 400,
    // 전량 조회다. 페이저를 끄지 않으면 한 줄도 안 그려진다.
    pagerConfig: { enabled: false },
    // 원본의 `row-key` 를 그대로 옮겼다.
    rowConfig: { keyField: 'tag_id' },
  } as any,
});

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
watch(loadingList, (value) => mcGridApi.setLoading(value));

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
          <GridIconButton
            :loading="loadingList"
            icon="vxe-icon-repeat"
            title="새로고침"
            @click="reloadAll"
          />
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
          <McGrid class="cursor-pointer" :table-data="filteredMc">
            <template #Maintenance_YN="{ row }">
              <Tag :color="row.Maintenance_YN === 'Y' ? 'warning' : 'default'">
                {{ row.Maintenance_YN }}
              </Tag>
            </template>
            <template #dst_mc_id="{ row }">
              <Tag v-if="String(row.dst_mc_id ?? '').trim()" color="purple">
                {{ row.dst_mc_id }}
              </Tag>
              <span v-else class="text-muted-foreground">-</span>
            </template>
          </McGrid>
        </Card>
      </Col>
    </Row>
  </Page>
</template>
