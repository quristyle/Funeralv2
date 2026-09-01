<script lang="ts" setup>
/**
 * 호실 히스토리 — 옛 `page/room/room_goin_hist.jsp` 와 `page/build/room_goin.jsp`.
 *
 * 옛 화면은 호실을 거쳐 간 고인을 사진·성명·성별·나이·상태·입실·출상·발인·장지 순으로
 * 늘어놓았다. 컬럼 구성을 그대로 따르되, 옛 `t_room_goin` 이 비워 두던 기간 칸은
 * 현 `deceased_rooms` 의 start_time/end_time 으로 채운다.
 */
import { onMounted, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Button, DatePicker, Select, Tag, message } from 'ant-design-vue';
import dayjs from 'dayjs';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import ImagePreview from '#/components/ImagePreview.vue';
import { getRoomHistories } from '#/api/funeral/info';
import { getBuildings, getRooms } from '#/api/funeral/building';

const buildings = ref<any[]>([]);
const rooms = ref<any[]>([]);
const searchBuildingId = ref<string | undefined>();
const searchRoomId = ref<string | undefined>();
const searchRange = ref<[any, any] | undefined>([dayjs().subtract(1, 'year'), dayjs()]);

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'photo', title: '사진', width: 80, slots: { default: 'photo' } },
      { field: 'buildingName', title: '건물', width: 130 },
      { field: 'floorName', title: '층', width: 90 },
      { field: 'roomName', title: '호실', width: 140 },
      { field: 'deceasedName', title: '성명', width: 120 },
      { field: 'gender', title: '성별', width: 70, formatter: fmtGender },
      { field: 'age', title: '나이', width: 70, align: 'right' },
      { field: 'inUse', title: '상태', width: 90, slots: { default: 'state' } },
      { field: 'startTime', title: '입실', width: 160, formatter: fmtDateTime },
      { field: 'endTime', title: '출상', width: 160, formatter: fmtDateTime },
      { field: 'useDays', title: '사용일수', width: 90, align: 'right', formatter: fmtDays },
      { field: 'funeralDate', title: '발인', width: 160, formatter: fmtDateTime },
      { field: 'burialPlot', title: '장지', minWidth: 160 },
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () =>
          await getRoomHistories({
            buildingId: searchBuildingId.value || undefined,
            roomId: searchRoomId.value || undefined,
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

function fmtDays({ cellValue }: { cellValue: any }) {
  return cellValue ? `${cellValue}일` : '-';
}

async function fetchBuildings() {
  try {
    buildings.value = (await getBuildings()) || [];
  } catch {
    message.error('건물 목록을 불러오지 못했습니다.');
  }
}

/** 건물을 고르면 그 건물의 호실만 남긴다. 옛 검색폼도 이렇게 물려 있었다. */
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
    <Grid table-title="호실 히스토리">
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
        <Tag v-if="row.inUse" color="processing">사용 중</Tag>
        <Tag v-else color="default">출상</Tag>
      </template>
    </Grid>
  </Page>
</template>
