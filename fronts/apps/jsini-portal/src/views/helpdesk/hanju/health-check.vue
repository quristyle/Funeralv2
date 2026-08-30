<script lang="ts" setup>
import { onBeforeUnmount, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Card,
  Descriptions,
  DescriptionsItem,
  Space,
  Spin,
  Switch,
  Tag,
} from 'ant-design-vue';
import { VxeGrid } from 'vxe-table';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { oadrGet } from '#/api/helpdesk';

/**
 * [한주 설비 헬스체크]
 *
 * 원본(hanju/HealthCheck.vue). OADR 의 `/health` 와 `/health/All_un_MC` 두 곳을 읽는다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * **가져오기 방식은 그대로다** — 두 곳을 한 번에 읽어 전량을 화면에 올린다.
 * 그래서 페이저가 없고, 머리글 필터줄이 전체를 대상으로 걸린다.
 *
 * 점검 항목을 펼치면 나오는 **설비 상세는 행마다 표가 하나씩** 필요해서
 * `useVbenVxeGrid`(화면당 한 벌)로는 만들 수 없다. 그 자리만 vxe 의 `VxeGrid` 를
 * 직접 쓴다 — 읽기 전용이라 정렬·필터줄이 없어도 되는 자리다.
 * ------------------------------------------------------------
 */

const loading = ref(false);
const autoRefresh = ref(true);
const serverStatus = ref('Unknown');
const mainData = ref<Record<string, any>>({});
const checks = ref<Record<string, any>[]>([]);
const mcStatus = ref('Unknown');
const mcChecks = ref<Record<string, any>[]>([]);
const errorMessage = ref('');

let timer: null | ReturnType<typeof setInterval> = null;

/**
 * 두 표가 같은 구성을 쓴다. 다만 vxe 는 그리드마다 컬럼 정의를 따로 들고 있어야
 * 하므로 배열을 공유하지 않고 새로 만들어 준다.
 *
 * 상태 칸은 고르는 칸(`filterOptions`)으로 두지 않았다 — 값이 외부 시스템(OADR)이
 * 그때그때 돌려주는 글자라 목록을 못 박으면 새 값이 나왔을 때 걸러지지 않는다.
 */
function makeCheckColumns() {
  return [
    { slots: { content: 'detail' }, type: 'expand', width: 40 },
    { field: 'name', title: '항목', width: 220 },
    { field: 'status', slots: { default: 'status' }, title: '상태', width: 110 },
    { field: 'description', minWidth: 200, title: '설명' },
    { field: 'totalCount', title: '전체', width: 80 },
    { field: 'healthyCount', title: '정상', width: 80 },
    { field: 'unhealthyCount', title: '이상', width: 80 },
    { field: 'duration', title: '소요', width: 110 },
  ];
}

const [ServerGrid] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: makeCheckColumns(),
    // 행 배열은 `:table-data` 로 간다. 여기는 빈 배열이 바탕값이다.
    data: [],
    emptyText: '점검 결과가 없습니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 위쪽 '새로고침' · 자동 갱신 타이머가 부르는 것과 같은 함수를 준다.
    gridFeatures: { onRefresh: () => loadData() },
    height: 300,
    // 전량 조회다. 페이저를 끄지 않으면 한 줄도 안 그려진다.
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'name' },
  } as any,
});

const [McGrid] = useVbenVxeGrid({
  gridOptions: {
    columns: makeCheckColumns(),
    data: [],
    emptyText: '점검 결과가 없습니다.',
    // 두 표를 한 번에 읽으므로 재조회도 같은 함수다.
    gridFeatures: { onRefresh: () => loadData() },
    height: 300,
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'name' },
  } as any,
});

/**
 * 점검 항목의 data 안에는 설비 상세가 들어 있다.
 * 서버가 `{ PET1: [ {mcId, mcName, gaudt, status}, ... ] }` 처럼 그룹 키로 감싸 주므로
 * 그룹을 풀어 한 줄씩 펼친다(원본 HealthCheck.vue 가 표로 보여주던 부분).
 */
function detailRows(check: Record<string, any>) {
  const data = check?.data;
  if (!data) return [];
  if (Array.isArray(data)) return data;

  return Object.entries(data).flatMap(([group, list]) =>
    Array.isArray(list)
      ? list.map((item: any) => ({ ...item, __group: group }))
      : [],
  );
}

/** 펼친 줄 안쪽 표의 컬럼. */
const detailColumns = [
  { field: '__group', title: '그룹', width: 90 },
  { field: 'mcId', title: 'ID', width: 100 },
  { field: 'mcName', minWidth: 160, title: '설비명' },
  { field: 'gaudt', title: 'Last', width: 170 },
  {
    field: 'status',
    slots: { default: 'detailStatus' },
    title: '상태',
    width: 100,
  },
];

/** 상태 문자열에 맞는 색 */
function statusColor(status?: string) {
  const s = (status ?? '').toLowerCase();
  if (s === 'healthy') return 'success';
  if (s === 'degraded') return 'warning';
  if (s === 'unhealthy' || s === 'error' || s === 'timeout') return 'error';
  return 'default';
}

async function loadData() {
  loading.value = true;
  errorMessage.value = '';

  const [health, mc] = await Promise.allSettled([
    oadrGet<any>('/health'),
    oadrGet<any>('/health/All_un_MC'),
  ]);

  if (health.status === 'fulfilled') {
    mainData.value = health.value ?? {};
    serverStatus.value = health.value?.healthChecks?.status ?? 'Unknown';
    checks.value = health.value?.healthChecks?.checks ?? [];
  } else {
    serverStatus.value = 'Error';
    checks.value = [];
    errorMessage.value = '서버 헬스체크를 불러오지 못했습니다.';
  }

  if (mc.status === 'fulfilled') {
    mcStatus.value = mc.value?.status ?? 'Unknown';
    mcChecks.value = mc.value?.checks ?? [];
  } else {
    mcStatus.value = 'Error';
    mcChecks.value = [];
  }

  loading.value = false;
}

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
          <span class="text-sm">서버</span>
          <Tag :color="statusColor(serverStatus)">{{ serverStatus }}</Tag>
          <span class="text-sm">설비(MC)</span>
          <Tag :color="statusColor(mcStatus)">{{ mcStatus }}</Tag>
        </Space>
        <Space>
          <span class="text-sm">자동 새로고침</span>
          <Switch :checked="autoRefresh" @change="onAutoRefreshChange as any" />
          <Button :loading="loading" @click="loadData">새로고침</Button>
        </Space>
      </div>
    </Card>

    <Alert
      v-if="errorMessage"
      class="mb-3"
      :message="errorMessage"
      show-icon
      type="error"
    />

    <Spin :spinning="loading">
      <Card class="mb-3" size="small" title="서버 정보">
        <Descriptions :column="{ md: 2, xs: 1 }" size="small">
          <DescriptionsItem label="기동 시각">
            {{ mainData.startTime ?? '-' }}
          </DescriptionsItem>
          <DescriptionsItem label="가동 시간">
            {{ mainData.uptime ?? '-' }}
          </DescriptionsItem>
        </Descriptions>
      </Card>

      <Card :body-style="{ padding: 0 }" size="small" title="서버 점검 항목">
        <ServerGrid :table-data="checks">
          <template #status="{ row }">
            <Tag :color="statusColor(row.status)">{{ row.status }}</Tag>
          </template>

          <!-- 점검 항목을 펼치면 설비 상세가 나온다 -->
          <template #detail="{ row }">
            <VxeGrid
              :columns="detailColumns"
              :data="detailRows(row)"
              empty-text="상세 항목이 없습니다."
              :height="240"
              :pager-config="{ enabled: false }"
              :proxy-config="{ enabled: false }"
              :row-config="{ keyField: 'mcId' }"
              size="mini"
            >
              <template #detailStatus="{ row: r }">
                <Tag :color="statusColor(r.status)">{{ r.status }}</Tag>
              </template>
            </VxeGrid>
          </template>
        </ServerGrid>
      </Card>

      <Card
        :body-style="{ padding: 0 }"
        class="mt-3"
        size="small"
        title="미수집 설비 점검"
      >
        <McGrid :table-data="mcChecks">
          <template #status="{ row }">
            <Tag :color="statusColor(row.status)">{{ row.status }}</Tag>
          </template>

          <template #detail="{ row }">
            <VxeGrid
              :columns="detailColumns"
              :data="detailRows(row)"
              empty-text="상세 항목이 없습니다."
              :height="240"
              :pager-config="{ enabled: false }"
              :proxy-config="{ enabled: false }"
              :row-config="{ keyField: 'mcId' }"
              size="mini"
            >
              <template #detailStatus="{ row: r }">
                <Tag :color="statusColor(r.status)">{{ r.status }}</Tag>
              </template>
            </VxeGrid>
          </template>
        </McGrid>
      </Card>
    </Spin>
  </Page>
</template>
