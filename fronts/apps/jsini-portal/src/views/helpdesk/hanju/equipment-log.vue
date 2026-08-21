<script lang="ts" setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Card,
  Empty,
  Input,
  Space,
  Switch,
  Table,
  Tag,
} from 'ant-design-vue';

import { oadrGet } from '#/api/helpdesk';

/**
 * [설비 상태 로그]
 *
 * 원본(hanju/EquipmentStatusLog.vue). OADR 의 `/health/equipment-status-log` 를 읽는다.
 *
 * 이 엔드포인트는 현재 외부 시스템 쪽에서 500 을 돌려주고 있다(원본에서도 동일).
 * 화면은 그대로 옮기되, 오류 응답이면 서버가 준 메시지를 그대로 보여준다.
 */

const loading = ref(false);
const autoRefresh = ref(true);
const rows = ref<Record<string, any>[]>([]);
const keyword = ref('');
const errorMessage = ref('');
const lastFetchTime = ref('');

let timer: null | ReturnType<typeof setInterval> = null;

// 원본(EquipmentStatusLog.vue)과 같은 컬럼 구성
const columns = [
  { dataIndex: 'gaugeSection', key: 'gaugeSection', sorter: true, title: '구분', width: 120 },
  { dataIndex: 'statusText', key: 'statusText', sorter: true, title: '상태', width: 110 },
  { dataIndex: 'comSrv', key: 'comSrv', sorter: true, title: 'srv', width: 90 },
  { dataIndex: 'mcName', key: 'mcName', sorter: true, title: 'Name', ellipsis: true },
  { dataIndex: 'comPort', key: 'comPort', sorter: true, title: 'vcom', width: 90 },
  { dataIndex: 'mcId', key: 'mcId', sorter: true, title: '장비 ID', width: 130 },
  { dataIndex: 'ccnt', key: 'ccnt', sorter: true, title: '시간', width: 80 },
  { dataIndex: 'maxQty', key: 'maxQty', sorter: true, title: 'PQty', width: 100 },
];

/** 상태별로 묶어 보기. 원본의 isGrouped 토글. */
const isGrouped = ref(false);

const filteredRows = computed(() => {
  const kw = keyword.value.trim().toLowerCase();
  const list = kw
    ? rows.value.filter((r) =>
        Object.values(r).some((v) => String(v ?? '').toLowerCase().includes(kw)),
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
  if (s.includes('중단') || s.includes('오류') || s.includes('error')) return 'error';
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
        <Tag v-for="[state, count] in groupCounts" :key="state" :color="statusColor(state)">
          {{ state }} {{ count }}건
        </Tag>
      </Space>
    </Card>

    <Card :body-style="{ padding: 0 }" size="small">
      <Table
        :columns="columns"
        :data-source="filteredRows"
        :loading="loading"
        :pagination="{ pageSize: 50, showSizeChanger: true }"
        :scroll="{ x: 950 }"
        row-key="mcId"
        size="small"
      >
        <template #emptyText>
          <Empty description="수집된 로그가 없습니다." />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'statusText'">
            <Tag :color="statusColor(record.statusText)">
              {{ record.statusText }}
            </Tag>
          </template>
        </template>
      </Table>
    </Card>
  </Page>
</template>
