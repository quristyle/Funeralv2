<script lang="ts" setup>
import { ref, onMounted, watch } from 'vue';
import { Page } from '@vben/common-ui';
import { Select, Button, Card, Timeline, TimelineItem } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getRoomHistories } from '#/api/funeral/info';
import { getRooms } from '#/api/funeral/building';

const rooms = ref<any[]>([]);
const filterRoomId = ref<string>('');
const timelineLogs = ref<any[]>([]);

// 호실 로드
async function fetchRooms() {
  try {
    const list = await getRooms({});
    rooms.value = list || [];
    if (rooms.value.length > 0 && rooms.value[0]?.id) {
      filterRoomId.value = rooms.value[0].id;
    }
  } catch (error) {
    console.error('호실 로드 실패');
  }
}

// 타임라인 데이터 가져오기
async function fetchTimeline() {
  try {
    const list = await getRoomHistories(filterRoomId.value || undefined);
    timelineLogs.value = list || [];
  } catch (error) {
    console.error('타임라인 로드 실패');
  }
}

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'roomName', title: '호실명', minWidth: 120 },
      {
        field: 'actionType',
        title: '동작/변경 사항',
        minWidth: 130,
        formatter: ({ cellValue }: { cellValue: any }) => {
          if (cellValue === 'ENTER') return '사용 등록(입원)';
          if (cellValue === 'LEAVE') return '발인 완료(퇴원)';
          if (cellValue === 'CLEAN') return '실내 소독 및 청소';
          if (cellValue === 'REPAIR') return '장비 점검/보수';
          return cellValue;
        }
      },
      { field: 'actorName', title: '조치 작업자', minWidth: 120 },
      { field: 'remark', title: '비고 특이사항', minWidth: 200 },
      { field: 'createdAt', title: '조치 시각', minWidth: 160 }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getRoomHistories(filterRoomId.value || undefined);
        },
      },
    },
  },
});

watch(filterRoomId, () => {
  gridApi.query();
  fetchTimeline();
});

onMounted(() => {
  fetchRooms();
});
</script>

<template>
  <Page auto-content-height>
    <div class="mb-4 flex items-center justify-between bg-card p-4 rounded border">
      <div class="flex items-center gap-2">
        <span class="font-semibold text-sm">호실 타겟 필터:</span>
        <Select v-model:value="filterRoomId" style="width: 200px" placeholder="호실 선택">
          <Select.Option v-for="r in rooms" :key="r.id" :value="r.id">{{ r.name }}</Select.Option>
        </Select>
      </div>
      <Button type="primary" @click="gridApi.query()">로그 동기화</Button>
    </div>

    <div class="grid grid-cols-3 gap-4 h-full">
      <!-- 좌측: 타임라인 요약 -->
      <Card title="상태 변경 타임라인 추적" class="col-span-1 overflow-y-auto max-h-[600px]">
        <Timeline v-if="timelineLogs.length > 0" class="pt-4">
          <TimelineItem v-for="log in timelineLogs" :key="log.id" :color="log.actionType === 'REPAIR' ? 'red' : 'blue'">
            <div class="text-xs font-bold text-muted-foreground">{{ log.createdAt }}</div>
            <div class="text-sm font-semibold mt-1">
              {{ log.actionType === 'ENTER' ? '빈소 입실' : log.actionType === 'LEAVE' ? '발인 퇴실' : log.actionType === 'CLEAN' ? '소독 완료' : '장비 수리' }}
            </div>
            <div class="text-xs text-muted-foreground mt-0.5">작업자: {{ log.actorName }}</div>
          </TimelineItem>
        </Timeline>
        <div v-else class="text-center py-10 text-muted-foreground text-xs">선택된 호실의 로그가 없습니다.</div>
      </Card>

      <!-- 우측: 그리드 테이블 로그 -->
      <div class="col-span-2">
        <Grid table-title="호실별 상세 장례 서비스 동작 히스토리" />
      </div>
    </div>
  </Page>
</template>
