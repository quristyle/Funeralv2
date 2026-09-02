<script lang="ts" setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import { Card, Input, Space, Switch, Tag } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import GridIconButton from '#/components/GridIconButton.vue';
import { oadrGet } from '#/api/helpdesk';

import { formatDateTime } from '../shared/constants';

/**
 * [FMS 상태 로그]
 *
 * 원본(hanju/FmsStatusLog.vue). OADR 의 `/fms_chk` 를 읽어
 * 태그별 A/B 값 차이(gap)와 판정(chk)을 보여준다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * 원본 컬럼의 `sorter` 는 걷어냈다. 위쪽 검색칸은 설비 ID·태그·판정을 한꺼번에
 * 훑는 조회 조건이라 그대로 남겼다.
 *
 * **가져오기 방식은 그대로다** — 전량을 한 번에 받아 화면이 쥔다.
 * 원본의 프런트 페이징(50건)은 없앴다(전역 기본값이 페이저 꺼짐).
 * ------------------------------------------------------------
 */

const loading = ref(false);
const autoRefresh = ref(false);
const rows = ref<Record<string, any>[]>([]);
const keyword = ref('');
const lastFetchTime = ref('');

let timer: null | ReturnType<typeof setInterval> = null;

// 판정 칸은 고르는 칸(`filterOptions`)으로 두지 않았다 — 외부 시스템(OADR)이
// OK/NG 말고 다른 글자를 돌려주는 일이 있어 목록을 못 박지 않는다.
const [Grid, gridApi] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      { field: 'mcId', title: '설비 ID', width: 110 },
      { field: 'tagId', title: '태그', width: 140 },
      { field: 'tagValueA', title: 'A 값', width: 110 },
      {
        field: 'saveDtimeA',
        params: { filterText: (row: any) => formatDateTime(row.saveDtimeA) },
        slots: { default: 'saveDtimeA' },
        title: 'A 시각',
        width: 160,
      },
      { field: 'tagValueB', title: 'B 값', width: 110 },
      {
        field: 'saveDtimeB',
        params: { filterText: (row: any) => formatDateTime(row.saveDtimeB) },
        slots: { default: 'saveDtimeB' },
        title: 'B 시각',
        width: 160,
      },
      { field: 'gap', title: '차이', width: 100 },
      { field: 'chk', slots: { default: 'chk' }, title: '판정', width: 90 },
    ],
    // 행 배열은 `:table-data` 로 간다. 여기는 빈 배열이 바탕값이다.
    data: [],
    emptyText: '수집된 로그가 없습니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 위쪽 '새로고침' · 자동 갱신 타이머가 부르는 것과 같은 함수를 준다.
    gridFeatures: { onRefresh: () => loadData() },
    height: 'auto',
    // 전량 조회다. 페이저를 끄지 않으면 한 줄도 안 그려진다.
    pagerConfig: { enabled: false },
    // 설비 ID 하나에 태그가 여럿이라 한 칸으로는 행을 가릴 수 없다.
    // `keyField` 를 적지 않으면 vxe 가 내부 키를 붙여 준다.
  } as any,
});

const filteredRows = computed(() => {
  const kw = keyword.value.trim().toLowerCase();
  if (!kw) return rows.value;
  return rows.value.filter((r) =>
    `${r.mcId} ${r.tagId} ${r.chk}`.toLowerCase().includes(kw),
  );
});

/** 판정값이 정상인지에 따라 색을 준다. */
function chkColor(chk: any) {
  const v = String(chk ?? '').toUpperCase();
  if (v === 'OK' || v === 'Y') return 'success';
  if (v === 'NG' || v === 'N') return 'error';
  return 'default';
}

async function loadData() {
  loading.value = true;
  try {
    rows.value = (await oadrGet<Record<string, any>[]>('/fms_chk')) ?? [];
    lastFetchTime.value = new Date().toLocaleTimeString('ko-KR');
  } catch {
    rows.value = [];
  } finally {
    loading.value = false;
  }
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

watch(loading, (value) => gridApi.setLoading(value));

onMounted(loadData);
onBeforeUnmount(stopTimer);
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space wrap>
          <Input
            v-model:value="keyword"
            allow-clear
            placeholder="설비 ID · 태그 · 판정"
            style="width: 240px"
          />
          <span class="text-xs text-muted-foreground">
            {{ filteredRows.length }}건
            <template v-if="lastFetchTime"> · {{ lastFetchTime }} 기준</template>
          </span>
        </Space>
        <Space>
          <span class="text-sm">자동 새로고침</span>
          <Switch :checked="autoRefresh" @change="onAutoRefreshChange as any" />
          <GridIconButton
            :loading="loading"
            icon="vxe-icon-repeat"
            title="새로고침"
            @click="loadData"
          />
        </Space>
      </div>
    </Card>

    <Grid :table-data="filteredRows">
      <template #saveDtimeA="{ row }">{{ formatDateTime(row.saveDtimeA) }}</template>
      <template #saveDtimeB="{ row }">{{ formatDateTime(row.saveDtimeB) }}</template>
      <template #chk="{ row }">
        <Tag :color="chkColor(row.chk)">{{ row.chk }}</Tag>
      </template>
    </Grid>
  </Page>
</template>
