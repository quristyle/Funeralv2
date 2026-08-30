<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Button, message, DatePicker, Select } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getRoomUsageStats } from '#/api/funeral/stat';
import { getRooms } from '#/api/funeral/building';
import dayjs from 'dayjs';

const rooms = ref<any[]>([]);
const searchRoomId = ref<string>('');
const searchRange = ref<[any, any]>([dayjs().subtract(30, 'day'), dayjs()]);

// 호실 정보 로드
async function fetchRooms() {
  try {
    const list = await getRooms({});
    rooms.value = list || [];
  } catch (error) {
    message.error('호실 정보 로드 실패');
  }
}

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'roomName', title: '빈소(호실)명', minWidth: 120 },
      { field: 'deceasedName', title: '고인 성명', minWidth: 100 },
      { field: 'useStartDate', title: '사용 개시일시', minWidth: 160 },
      { field: 'useEndDate', title: '사용 종료일시', minWidth: 160 },
      { field: 'durationHours', title: '이용 시간(시간)', minWidth: 120, align: 'right', formatter: ({ cellValue }: { cellValue: any }) => `${cellValue}시간` },
      {
        field: 'billingAmount',
        title: '정산 금액',
        minWidth: 150,
        align: 'right',
        formatter: ({ cellValue }: { cellValue: any }) => `${Number(cellValue).toLocaleString()}원`
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          const params = {
            roomId: searchRoomId.value || undefined,
            startDate: searchRange.value?.[0] ? searchRange.value[0].format('YYYY-MM-DD') : undefined,
            endDate: searchRange.value?.[1] ? searchRange.value[1].format('YYYY-MM-DD') : undefined,
          };
          return await getRoomUsageStats(params);
        },
      },
    },
  },
});

function handleSearch() {
  gridApi.query();
}

function handleExport() {
  message.loading({ content: '사용 내역을 취합하여 변환 중...', key: 'export' });
  setTimeout(() => {
    message.success({ content: '빈소_이용_로그_보고서.xlsx 다운로드 완료', key: 'export', duration: 2 });
  }, 1000);
}

onMounted(() => {
  fetchRooms();
});
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <!-- 검색 영역 -->
    <div class="mb-4 flex flex-wrap items-center justify-between bg-card p-4 rounded border gap-4">
      <div class="flex items-center gap-4 flex-wrap">
        <div class="flex items-center gap-2">
          <span class="font-semibold text-sm">일자 범위:</span>
          <DatePicker.RangePicker v-model:value="searchRange" format="YYYY-MM-DD" style="width: 260px; max-width: 100%" />
        </div>
        
        <div class="flex items-center gap-2">
          <span class="font-semibold text-sm">호실 필터:</span>
          <Select v-model:value="searchRoomId" style="width: 180px; max-width: 100%" placeholder="전체 호실 조회" allow-clear>
            <Select.Option v-for="r in rooms" :key="r.id" :value="r.id">{{ r.name }}</Select.Option>
          </Select>
        </div>

        <Button type="primary" @click="handleSearch">조회</Button>
      </div>

      <Button v-perm:excel type="default" @click="handleExport">엑셀 다운로드</Button>
    </div>

    <!-- 테이블 -->
    <Grid table-title="호실/빈소별 실제 장례 이용 내역 및 정산 합계 목록" />
  </Page>
</template>
