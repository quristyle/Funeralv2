<script lang="ts" setup>
/**
 * 과금 내역 — 옛 `page/user/goin_profile_useinfo2.jsp`(고인별 비용) 를 목록으로 옮긴 것.
 *
 * 옛 시스템은 고인 한 명당 기본료 · 환경부담금 · 시설관리비 세 줄을 두고
 * `gp_day_apply` 가 켜져 있으면 사용일수를 곱했다. 그 셈법을 그대로 따른다.
 * 비용이 등록되지 않은 고인에게는 백엔드가 옛 기본 단가로 항목을 만들어 보여 준다 —
 * 저장된 값이 아니므로 비고에 그렇게 적힌다.
 */
import { computed, onMounted, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Button, Card, DatePicker, Modal, Select, Statistic, Tag, message } from 'ant-design-vue';
import dayjs from 'dayjs';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import type { StatApi } from '#/api/funeral/stat';
import { getBillingStats, getStatSummary } from '#/api/funeral/stat';
import { getBuildings } from '#/api/funeral/building';

const buildings = ref<any[]>([]);
const searchBuildingId = ref<string | undefined>();
const searchRange = ref<[any, any] | undefined>([dayjs().subtract(3, 'month'), dayjs()]);

const summary = ref<StatApi.Summary | null>(null);

/** 항목 상세 팝업 */
const showDetail = ref(false);
const detail = ref<StatApi.Billing | null>(null);

/**
 * 상세 팝업 안의 항목 표. 준수사항 6번대로 그리드 하나만 쓴다.
 * 팝업 안이라 부모가 높이를 주지 않으므로 숫자로 준다.
 */
const [DetailGrid] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'title', title: '항목', minWidth: 140 },
      { field: 'unitPrice', title: '단가', width: 120, align: 'right', formatter: fmtMoney },
      {
        field: 'applyPerDay',
        title: '일수 적용',
        width: 100,
        align: 'center',
        slots: { default: 'applyPerDay' },
      },
      { field: 'amount', title: '금액', width: 130, align: 'right', formatter: fmtMoney },
      { field: 'remark', title: '비고', minWidth: 180 },
    ],
    emptyText: '등록된 비용 항목이 없습니다.',
    height: 260,
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'title' },
  } as any,
});

const detailTotal = computed(() =>
  (detail.value?.items ?? []).reduce((sum, i) => sum + i.amount, 0),
);

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'deceasedName', title: '고인 성명', width: 130 },
      { field: 'buildingName', title: '건물', width: 130 },
      { field: 'roomName', title: '호실', width: 130 },
      { field: 'startTime', title: '입실', width: 160, formatter: fmtDateTime },
      { field: 'endTime', title: '퇴실', width: 160, formatter: fmtDateTime },
      { field: 'useDays', title: '사용일수', width: 90, align: 'right', formatter: fmtDays },
      { field: 'itemCount', title: '항목수', width: 80, align: 'right', slots: { default: 'itemCount' } },
      { field: 'totalAmount', title: '합계', width: 140, align: 'right', formatter: fmtMoney },
      { field: 'action', title: '상세', width: 80, fixed: 'right', slots: { default: 'action' } },
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          const params = {
            buildingId: searchBuildingId.value || undefined,
            from: searchRange.value?.[0]?.toISOString(),
            to: searchRange.value?.[1]?.toISOString(),
          };
          // 요약도 같은 조건으로 다시 읽는다.
          void loadSummary(params);
          return await getBillingStats(params);
        },
      },
    },
  },
});

function fmtDateTime({ cellValue }: { cellValue: any }) {
  return cellValue ? dayjs(cellValue).format('YYYY-MM-DD HH:mm') : '-';
}

function fmtDays({ cellValue }: { cellValue: any }) {
  return cellValue ? `${cellValue}일` : '-';
}

function fmtMoney({ cellValue }: { cellValue: any }) {
  return `${Number(cellValue || 0).toLocaleString()}원`;
}

function money(value?: number) {
  return `${Number(value || 0).toLocaleString()}원`;
}

async function loadSummary(params: any) {
  try {
    summary.value = await getStatSummary(params);
  } catch {
    summary.value = null;
  }
}

async function fetchBuildings() {
  try {
    buildings.value = (await getBuildings()) || [];
  } catch {
    message.error('건물 목록을 불러오지 못했습니다.');
  }
}

function openDetail(row: any) {
  detail.value = row;
  showDetail.value = true;
}

onMounted(fetchBuildings);
</script>

<template>
  <Page auto-content-height>
    <div class="mb-3 grid grid-cols-2 gap-3 sm:grid-cols-4">
      <Card size="small"><Statistic title="고인" :value="summary?.deceasedCount ?? 0" suffix="명" /></Card>
      <Card size="small"><Statistic title="사용 건수" :value="summary?.roomUsageCount ?? 0" suffix="건" /></Card>
      <Card size="small"><Statistic title="사용 일수" :value="summary?.totalUseDays ?? 0" suffix="일" /></Card>
      <Card size="small">
        <Statistic title="합계" :value="summary?.totalAmount ?? 0" suffix="원" :precision="0" />
      </Card>
    </div>

    <Grid table-title="과금 내역">
      <template #toolbar-tools>
        <div class="flex flex-wrap items-center gap-2">
          <Select
            v-model:value="searchBuildingId"
            class="w-36"
            allow-clear
            placeholder="건물 전체"
            :options="buildings.map((b) => ({ label: b.name, value: b.id }))"
            @change="gridApi.query()"
          />
          <DatePicker.RangePicker v-model:value="searchRange" class="w-64" @change="gridApi.query()" />
          <Button type="primary" @click="gridApi.query()">조회</Button>
        </div>
      </template>

      <template #itemCount="{ row }">
        {{ row.items?.length ?? 0 }}
      </template>

      <template #action="{ row }">
        <Button type="link" size="small" @click="openDetail(row)">보기</Button>
      </template>
    </Grid>

    <Modal
      v-model:open="showDetail"
      :title="`과금 상세 — ${detail?.deceasedName ?? ''}`"
      :footer="null"
      width="760px"
      destroy-on-close
    >
      <div v-if="detail" class="space-y-3">
        <div class="flex flex-wrap gap-4 rounded border bg-muted/30 p-3 text-xs">
          <span>건물 <b>{{ detail.buildingName || '-' }}</b></span>
          <span>호실 <b>{{ detail.roomName || '-' }}</b></span>
          <span>입실 <b>{{ detail.startTime ? dayjs(detail.startTime).format('YYYY-MM-DD HH:mm') : '-' }}</b></span>
          <span>퇴실 <b>{{ detail.endTime ? dayjs(detail.endTime).format('YYYY-MM-DD HH:mm') : '사용 중' }}</b></span>
          <span>사용일수 <b>{{ detail.useDays }}일</b></span>
        </div>

        <DetailGrid :table-data="detail.items">
          <template #applyPerDay="{ row }">
            <Tag v-if="row.applyPerDay" color="processing">일수 적용</Tag>
            <span v-else class="text-xs text-muted-foreground">고정</span>
          </template>
        </DetailGrid>

        <div class="flex justify-end border-t pt-2 text-sm">
          <span class="mr-2 text-muted-foreground">합계</span>
          <b>{{ money(detailTotal) }}</b>
        </div>
      </div>
    </Modal>
  </Page>
</template>
