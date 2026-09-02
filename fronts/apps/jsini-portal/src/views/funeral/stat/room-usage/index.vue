<script lang="ts" setup>
/**
 * 빈소 사용 내역 — 호실을 언제부터 언제까지 누가 썼고 얼마가 나왔는지.
 *
 * 옛 시스템은 `t_room_goin` 에 고인↔호실 연결만 두고 기간은 비워 두었다
 * (10,385행이 모두 use_days=1 이었다). 현 `deceased_rooms` 는 시작·종료 시각이 있어
 * 실제 사용일수를 셀 수 있다. 하루가 안 돼도 하루로 세는 옛 규칙은 그대로 따른다.
 */
import { onMounted, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Card, DatePicker, Select, Statistic, Tag, message } from 'ant-design-vue';
import GridIconButton from '#/components/GridIconButton.vue';
import dayjs from 'dayjs';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import type { StatApi } from '#/api/funeral/stat';
import { getRoomUsageStats, getStatSummary } from '#/api/funeral/stat';
import { getBuildings, getRooms } from '#/api/funeral/building';

const buildings = ref<any[]>([]);
const rooms = ref<any[]>([]);
const searchBuildingId = ref<string | undefined>();
const searchRoomId = ref<string | undefined>();
const searchRange = ref<[any, any] | undefined>([dayjs().subtract(3, 'month'), dayjs()]);

const summary = ref<StatApi.Summary | null>(null);

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'buildingName', title: '건물', width: 130 },
      { field: 'floorName', title: '층', width: 90 },
      { field: 'roomName', title: '빈소(호실)', width: 140 },
      { field: 'deceasedName', title: '고인 성명', width: 120 },
      { field: 'startTime', title: '사용 개시', width: 160, formatter: fmtDateTime },
      { field: 'endTime', title: '사용 종료', width: 160, formatter: fmtEnd },
      { field: 'useDays', title: '사용일수', width: 90, align: 'right', formatter: fmtDays },
      { field: 'inUse', title: '상태', width: 90, slots: { default: 'state' } },
      { field: 'billingAmount', title: '정산 금액', width: 150, align: 'right', formatter: fmtMoney },
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          const params = {
            buildingId: searchBuildingId.value || undefined,
            roomId: searchRoomId.value || undefined,
            from: searchRange.value?.[0]?.toISOString(),
            to: searchRange.value?.[1]?.toISOString(),
          };
          void loadSummary(params);
          return await getRoomUsageStats(params);
        },
      },
    },
  },
});

function fmtDateTime({ cellValue }: { cellValue: any }) {
  return cellValue ? dayjs(cellValue).format('YYYY-MM-DD HH:mm') : '-';
}

function fmtEnd({ cellValue }: { cellValue: any }) {
  return cellValue ? dayjs(cellValue).format('YYYY-MM-DD HH:mm') : '사용 중';
}

function fmtDays({ cellValue }: { cellValue: any }) {
  return cellValue ? `${cellValue}일` : '-';
}

function fmtMoney({ cellValue }: { cellValue: any }) {
  return `${Number(cellValue || 0).toLocaleString()}원`;
}

async function loadSummary(params: any) {
  try {
    summary.value = await getStatSummary({
      buildingId: params.buildingId,
      from: params.from,
      to: params.to,
    });
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

async function fetchRooms() {
  try {
    rooms.value = (await getRooms({ buildingId: searchBuildingId.value })) || [];
  } catch {
    rooms.value = [];
  }
}

async function handleBuildingChange() {
  searchRoomId.value = undefined;
  await fetchRooms();
  gridApi.query();
}

onMounted(async () => {
  await fetchBuildings();
  await fetchRooms();
});
</script>

<template>
  <Page auto-content-height>
    <div class="mb-3 grid grid-cols-2 gap-3 sm:grid-cols-4">
      <Card size="small"><Statistic title="고인" :value="summary?.deceasedCount ?? 0" suffix="명" /></Card>
      <Card size="small"><Statistic title="사용 건수" :value="summary?.roomUsageCount ?? 0" suffix="건" /></Card>
      <Card size="small"><Statistic title="사용 일수" :value="summary?.totalUseDays ?? 0" suffix="일" /></Card>
      <Card size="small"><Statistic title="정산 합계" :value="summary?.totalAmount ?? 0" suffix="원" /></Card>
    </div>

    <Grid table-title="빈소 사용 내역">
      <template #toolbar-tools>
        <div class="flex flex-wrap items-center gap-2">
          <Select
            v-model:value="searchBuildingId"
            class="w-36"
            allow-clear
            placeholder="건물 전체"
            :options="buildings.map((b) => ({ label: b.name, value: b.id }))"
            @change="handleBuildingChange"
          />
          <Select
            v-model:value="searchRoomId"
            class="w-36"
            allow-clear
            placeholder="호실 전체"
            :options="rooms.map((r) => ({ label: r.name, value: r.id }))"
            @change="gridApi.query()"
          />
          <DatePicker.RangePicker v-model:value="searchRange" class="w-64" @change="gridApi.query()" />
          <GridIconButton
            icon="vxe-icon-search"
            title="조회"
            @click="gridApi.query()"
          />
        </div>
      </template>

      <template #state="{ row }">
        <Tag v-if="row.inUse" color="processing">사용 중</Tag>
        <Tag v-else color="default">종료</Tag>
      </template>
    </Grid>
  </Page>
</template>
