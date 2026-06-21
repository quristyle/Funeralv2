<script lang="ts" setup>
import { ref, onMounted, watch } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, Select } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getRooms, createRoom, updateRoom, deleteRoom, getFloors } from '#/api/building';

const floors = ref<any[]>([]);
const filterFloorId = ref<string>('');

const [RoomModal, roomModalApi] = useVbenModal({
  title: '호실 정보 설정',
  destroyOnClose: true,
});

const formModel = ref({
  id: '',
  buildingId: '',
  floorId: '',
  name: '',
  code: '',
  roomType: 'FUNERAL_HALL', // FUNERAL_HALL: 빈소, MORTUARY: 안치실, PARCAP: 참관실 등
  status: 'ACTIVE' as 'ACTIVE' | 'INACTIVE',
  remark: ''
});

// 층 목록 로드
async function fetchFloors() {
  try {
    const list = await getFloors();
    floors.value = list || [];
    if (floors.value.length > 0 && floors.value[0]?.id) {
      filterFloorId.value = floors.value[0].id;
    }
  } catch (error) {
    message.error('층 목록 로드 실패');
  }
}

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '호실명', minWidth: 150 },
      { field: 'code', title: '호실코드', minWidth: 120 },
      {
        field: 'roomType',
        title: '호실 유형',
        minWidth: 120,
        formatter: ({ cellValue }: { cellValue: any }) => {
          if (cellValue === 'FUNERAL_HALL') return '빈소';
          if (cellValue === 'MORTUARY') return '안치실';
          if (cellValue === 'PARCAP') return '참관실';
          return cellValue;
        }
      },
      {
        field: 'status',
        title: '상태',
        minWidth: 100,
        slots: { default: 'status-tag' }
      },
      { field: 'remark', title: '설명', minWidth: 200 },
      {
        field: 'action',
        title: '작업',
        width: 150,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getRooms(filterFloorId.value);
        },
      },
    },
  },
});

watch(filterFloorId, () => {
  gridApi.query();
});

function onCreate() {
  // 기본 설정 적용
  const currentFloor = floors.value.find(f => f.id === filterFloorId.value);
  formModel.value = {
    id: '',
    buildingId: currentFloor?.buildingId || '',
    floorId: filterFloorId.value,
    name: '',
    code: '',
    roomType: 'FUNERAL_HALL',
    status: 'ACTIVE',
    remark: ''
  };
  roomModalApi.open();
}

function onEdit(row: any) {
  formModel.value = { ...row };
  roomModalApi.open();
}

async function onDelete(row: any) {
  try {
    await deleteRoom(row.id);
    message.success('호실 정보가 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

async function handleSave() {
  try {
    if (formModel.value.id) {
      await updateRoom(formModel.value.id, formModel.value);
      message.success('호실 정보가 수정되었습니다.');
    } else {
      await createRoom(formModel.value);
      message.success('호실 정보가 등록되었습니다.');
    }
    roomModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('저장 실패');
  }
}

onMounted(() => {
  fetchFloors();
});
</script>

<template>
  <Page auto-content-height>
    <div class="mb-4 flex items-center justify-between bg-card p-4 rounded border">
      <div class="flex items-center gap-2">
        <span class="font-semibold text-sm">층 필터:</span>
        <Select v-model:value="filterFloorId" style="width: 200px" placeholder="층 선택">
          <Select.Option v-for="f in floors" :key="f.id" :value="f.id">{{ f.buildingName }} - {{ f.name }}</Select.Option>
        </Select>
      </div>
      <Button type="primary" @click="onCreate">
        <Plus class="size-5 mr-1" />
        신규 호실 등록
      </Button>
    </div>

    <Grid table-title="호실 정보 목록">
      <template #status-tag="{ row }">
        <span
          :class="['px-2 py-1 rounded text-xs font-semibold', row.status === 'ACTIVE' ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800']"
        >
          {{ row.status === 'ACTIVE' ? '사용중' : '사용안함' }}
        </span>
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="onEdit(row)">수정</Button>
          <Popconfirm title="해당 호실을 삭제하시겠습니까?" @confirm="onDelete(row)">
            <Button type="link" size="small" danger>삭제</Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <RoomModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="배정 층" required>
            <Select v-model:value="formModel.floorId" :disabled="!!formModel.id">
              <Select.Option v-for="f in floors" :key="f.id" :value="f.id">{{ f.buildingName }} - {{ f.name }}</Select.Option>
            </Select>
          </Form.Item>
          <Form.Item label="호실명" required>
            <Input v-model:value="formModel.name" placeholder="예: 101호 빈소, 안치실 A 등" />
          </Form.Item>
          <Form.Item label="호실코드" required>
            <Input v-model:value="formModel.code" placeholder="예: ROOM_101" />
          </Form.Item>
          <Form.Item label="호실 유형">
            <Select v-model:value="formModel.roomType">
              <Select.Option value="FUNERAL_HALL">빈소</Select.Option>
              <Select.Option value="MORTUARY">안치실</Select.Option>
              <Select.Option value="PARCAP">참관실</Select.Option>
            </Select>
          </Form.Item>
          <Form.Item label="사용 여부">
            <Select v-model:value="formModel.status">
              <Select.Option value="ACTIVE">사용</Select.Option>
              <Select.Option value="INACTIVE">미사용</Select.Option>
            </Select>
          </Form.Item>
          <Form.Item label="설명">
            <Input.TextArea v-model:value="formModel.remark" placeholder="설명 입력" />
          </Form.Item>
        </Form>
      </div>
    </RoomModal>
  </Page>
</template>
