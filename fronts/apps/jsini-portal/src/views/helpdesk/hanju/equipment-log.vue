<script lang="ts" setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import { Alert, Button, Card, Input, Space, Switch, Tag } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { oadrGet } from '#/api/helpdesk';

/**
 * [설비 상태 로그]
 *
 * 원본(hanju/EquipmentStatusLog.vue). OADR 의 `/health/equipment-status-log` 를 읽는다.
 *
 * 이 엔드포인트는 현재 외부 시스템 쪽에서 500 을 돌려주고 있다(원본에서도 동일).
 * 화면은 그대로 옮기되, 오류 응답이면 서버가 준 메시지를 그대로 보여준다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * 원본 컬럼의 `sorter` 는 걷어냈다 — 이름줄을 누르면 서는 것이 공통 동작이다.
 * 위쪽 검색칸은 여러 칸을 한꺼번에 훑는 조회 조건이라 그대로 남겼다.
 *
 * **가져오기 방식은 그대로다** — 전량을 한 번에 받아 화면이 쥔다.
 * 원본의 프런트 페이징(50건)은 없앴다(전역 기본값이 페이저 꺼짐).
 * ------------------------------------------------------------
 */

const loading = ref(false);
const autoRefresh = ref(true);
const rows = ref<Record<string, any>[]>([]);
const keyword = ref('');
const errorMessage = ref('');
const lastFetchTime = ref('');

let timer: null | ReturnType<typeof setInterval> = null;

/** 상태별로 묶어 보기. 원본의 isGrouped 토글. */
const isGrouped = ref(false);

// 원본(EquipmentStatusLog.vue)과 같은 컬럼 구성.
// 상태 칸은 고르는 칸(`filterOptions`)으로 두지 않았다 — 값이 외부 시스템(OADR)이
// 돌려주는 자유 문구라 목록을 못 박으면 새 값이 걸러지지 않는다.
const [Grid, gridApi] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      { field: 'gaugeSection', title: '구분', width: 120 },
      {
        field: 'statusText',
        slots: { default: 'statusText' },
        title: '상태',
        width: 110,
      },
      { field: 'comSrv', title: 'srv', width: 90 },
      { field: 'mcName', minWidth: 180, title: 'Name' },
      { field: 'comPort', title: 'vcom', width: 90 },
      { field: 'mcId', title: '장비 ID', width: 130 },
      { field: 'ccnt', title: '시간', width: 80 },
      { field: 'maxQty', title: 'PQty', width: 100 },
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
    rowConfig: { keyField: 'mcId' },
  } as any,
});

const filteredRows = computed(() => {
  const kw = keyword.value.trim().toLowerCase();
  const list = kw
    ? rows.value.filter((r) =>
        Object.values(r).some((v) =>
          String(v ?? '')
            .toLowerCase()
            .includes(kw),
        ),
      )
    : rows.value;

  // 그룹 보기에서는 상태 기준으로 정렬해 같은 상태끼리 모인다(원본과 동일).
  return isGrouped.value
    ? list.toSorted((a, b) =>
        String(a.statusText ?? '').localeCompare(String(b.statusText ?? '')),
      )
    : list;
});

/** 상태별 건수. 그룹 보기 머리말에 쓴다. */
const groupCounts = computed(() => {
  const map = new Map<string, number>();
  filteredRows.value.forEach((r) => {
    const key = String(r.statusText ?? '(미지정)');
    map.set(key, (map.get(key) ?? 0) + 1);
  });
  return [...map.entries()];
});

function statusColor(text?: string) {
  const s = (text ?? '').toLowerCase();
  if (s.includes('정상') || s.includes('ok')) return 'success';
  if (s.includes('지연') || s.includes('warn')) return 'warning';
  if (s.includes('중단') || s.includes('오류') || s.includes('error'))
    return 'error';
  return 'default';
}

async function loadData() {
  loading.value = true;
  errorMessage.value = '';
  try {
    const result = await oadrGet<any>('/health/equipment-status-log');

    // 외부 시스템이 RFC 9110 문제 상세(problem details) 형태로 오류를 돌려주는 경우가 있다.
    if (result && !Array.isArray(result) && result.status >= 400) {
      rows.value = [];
      errorMessage.value =
        result.detail ?? result.title ?? '설비 상태 로그를 불러오지 못했습니다.';
      return;
    }

    rows.value = Array.isArray(result) ? result : (result?.items ?? []);
    lastFetchTime.value = new Date().toLocaleTimeString('ko-KR');
  } catch (error) {
    rows.value = [];
    errorMessage.value =
      (error as Error).message ?? '설비 상태 로그를 불러오지 못했습니다.';
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

onMounted(async () => {
  await loadData();
  if (autoRefresh.value) startTimer();
});

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
            placeholder="설비 · 구역 · 상태 검색"
            style="width: 240px"
          />
          <span class="text-xs text-muted-foreground">
            {{ filteredRows.length }}건
            <template v-if="lastFetchTime"> · {{ lastFetchTime }} 기준</template>
          </span>
        </Space>
        <Space>
          <span class="text-sm">상태별 묶기</span>
          <Switch v-model:checked="isGrouped" />
          <span class="text-sm">자동 새로고침</span>
          <Switch :checked="autoRefresh" @change="onAutoRefreshChange as any" />
          <Button :loading="loading" @click="loadData">새로고침</Button>
        </Space>
      </div>
    </Card>

    <Alert
      v-if="errorMessage"
      class="mb-3"
      description="한주 OADR 시스템이 오류를 반환했습니다. 외부 시스템 상태를 확인해 주세요."
      :message="errorMessage"
      show-icon
      type="error"
    />

    <!-- 그룹 보기일 때 상태별 건수 요약 -->
    <Card v-if="isGrouped" class="mb-3" size="small">
      <Space wrap>
        <Tag
          v-for="[state, count] in groupCounts"
          :key="state"
          :color="statusColor(state)"
        >
          {{ state }} {{ count }}건
        </Tag>
      </Space>
    </Card>

    <Grid :table-data="filteredRows">
      <template #statusText="{ row }">
        <Tag :color="statusColor(row.statusText)">{{ row.statusText }}</Tag>
      </template>
    </Grid>
  </Page>
</template>
