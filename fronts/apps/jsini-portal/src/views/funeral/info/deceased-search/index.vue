<script lang="ts" setup>
/**
 * 고인 정보 조회 — 옛 `page/room/goin4room.jsp`.
 *
 * 옛 화면은 회사 · 건물 · 호실 세 단계로 좁혀 들어가고 표에는 사진 · 성명 · 성별 ·
 * 나이 · 입실 · 발인 · 장지 · 상태를 뒀다. 여기서는 이름 검색을 앞에 두고
 * 건물 · 호실 · 기간을 곁들인다 — 실제로 찾을 때 이름부터 치기 때문이다.
 */
import { onMounted, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Button, DatePicker, Input, Select, Tag, message } from 'ant-design-vue';
import dayjs from 'dayjs';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import ImagePreview from '#/components/ImagePreview.vue';
import { searchDeceased } from '#/api/funeral/info';
import { getBuildings, getRooms } from '#/api/funeral/building';

const buildings = ref<any[]>([]);
const rooms = ref<any[]>([]);
const keyword = ref<string>('');
const searchBuildingId = ref<string | undefined>();
const searchRoomId = ref<string | undefined>();
const searchStatus = ref<string | undefined>();
const searchRange = ref<[any, any] | undefined>();

const STATUS_OPTIONS = [
  { label: '입원 중', value: 'IN_HOSPITAL' },
  { label: '퇴원', value: 'DISCHARGED' },
  { label: '발인 완료', value: 'FUNERAL_DEPARTURE_COMPLETED' },
  { label: '완료', value: 'COMPLETED' },
];

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'photo', title: '사진', width: 80, slots: { default: 'photo' } },
      { field: 'name', title: '성명', width: 120 },
      { field: 'gender', title: '성별', width: 70, formatter: fmtGender },
      { field: 'age', title: '나이', width: 70, align: 'right' },
      { field: 'religion', title: '종교', width: 90 },
      { field: 'buildingName', title: '건물', width: 130 },
      { field: 'roomName', title: '호실', width: 130 },
      { field: 'status', title: '상태', width: 120, slots: { default: 'state' } },
      { field: 'deathDate', title: '사망일시', width: 160, formatter: fmtDateTime },
      { field: 'startTime', title: '입실', width: 160, formatter: fmtDateTime },
      { field: 'burialDate', title: '발인', width: 160, formatter: fmtDateTime },
      { field: 'burialPlot', title: '장지', minWidth: 150 },
      { field: 'mournerNames', title: '상주', minWidth: 160 },
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () =>
          await searchDeceased({
            keyword: keyword.value?.trim() || undefined,
            buildingId: searchBuildingId.value || undefined,
            roomId: searchRoomId.value || undefined,
            status: searchStatus.value || undefined,
            from: searchRange.value?.[0]?.toISOString(),
            to: searchRange.value?.[1]?.toISOString(),
          }),
      },
    },
  },
});

function fmtDateTime({ cellValue }: { cellValue: any }) {
  return cellValue ? dayjs(cellValue).format('YYYY-MM-DD HH:mm') : '-';
}

function fmtGender({ cellValue }: { cellValue: any }) {
  if (cellValue === 'MALE') return '남';
  if (cellValue === 'FEMALE') return '여';
  return cellValue || '-';
}

function statusLabel(value?: string) {
  return STATUS_OPTIONS.find((o) => o.value === value)?.label ?? value ?? '-';
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
    <Grid table-title="고인 정보 조회">
      <template #toolbar-tools>
        <div class="flex flex-wrap items-center gap-2">
          <Input
            v-model:value="keyword"
            class="w-44"
            allow-clear
            placeholder="성명 · 장지 · 비고"
            @press-enter="gridApi.query()"
          />
          <Select
            v-model:value="searchBuildingId"
            class="w-32"
            allow-clear
            placeholder="건물"
            :options="buildings.map((b) => ({ label: b.name, value: b.id }))"
            @change="handleBuildingChange"
          />
          <Select
            v-model:value="searchRoomId"
            class="w-32"
            allow-clear
            placeholder="호실"
            :options="rooms.map((r) => ({ label: r.name, value: r.id }))"
            @change="gridApi.query()"
          />
          <Select
            v-model:value="searchStatus"
            class="w-32"
            allow-clear
            placeholder="상태"
            :options="STATUS_OPTIONS"
            @change="gridApi.query()"
          />
          <DatePicker.RangePicker
            v-model:value="searchRange"
            class="w-60"
            :placeholder="['사망일 시작', '종료']"
            @change="gridApi.query()"
          />
          <Button type="primary" @click="gridApi.query()">조회</Button>
        </div>
      </template>

      <template #photo="{ row }">
        <ImagePreview
          :src="row.memorialPhotoFileId ? `/api/file/thumbnail/${row.memorialPhotoFileId}` : row.memorialPhotoUrl"
          :fallback-src="row.memorialPhotoUrl"
          :preview-src="row.memorialPhotoUrl"
          :width="48"
          :height="60"
          fallback-text="🕯"
        />
      </template>

      <template #state="{ row }">
        <Tag v-if="row.roomId" color="processing">{{ statusLabel(row.status) }}</Tag>
        <Tag v-else color="default">{{ statusLabel(row.status) }}</Tag>
      </template>
    </Grid>
  </Page>
</template>
