<script lang="ts" setup>
import { ref, onMounted, watch } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, InputNumber, Select } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getFloors, createFloor, updateFloor, deleteFloor, getBuildings } from '#/api/building';

const buildings = ref<any[]>([]);
const filterBuildingId = ref<string>('');

const [FloorModal, floorModalApi] = useVbenModal({
  title: '층 정보 설정',
  destroyOnClose: true,
});

const formModel = ref({
  id: '',
  buildingId: '',
  name: '',
  code: '',
  sortOrder: 1,
  remark: ''
});

// 건물 목록 로드
async function fetchBuildings() {
  try {
    const list = await getBuildings();
    buildings.value = list || [];
    if (buildings.value.length > 0 && buildings.value[0]?.id) {
      filterBuildingId.value = buildings.value[0].id;
    }
  } catch (error) {
    message.error('건물 목록 로드 실패');
  }
}

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'buildingName', title: '건물명', minWidth: 150 },
      { field: 'name', title: '층 명칭', minWidth: 120 },
      { field: 'code', title: '층 코드', minWidth: 100 },
      { field: 'sortOrder', title: '정렬 순서', minWidth: 100 },
      { field: 'remark', title: '비고', minWidth: 200 },
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
          return await getFloors(filterBuildingId.value);
        },
      },
    },
  },
});

// 필터링 건물 변경 시 그리드 갱신
watch(filterBuildingId, () => {
  gridApi.query();
});

function onCreate() {
  formModel.value = {
    id: '',
    buildingId: filterBuildingId.value,
    name: '',
    code: '',
    sortOrder: 1,
    remark: ''
  };
  floorModalApi.open();
}

function onEdit(row: any) {
  formModel.value = { ...row };
  floorModalApi.open();
}

async function onDelete(row: any) {
  try {
    await deleteFloor(row.id);
    message.success('층 정보가 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

async function handleSave() {
  try {
    if (formModel.value.id) {
      await updateFloor(formModel.value.id, formModel.value);
      message.success('층 정보가 수정되었습니다.');
    } else {
      await createFloor(formModel.value);
      message.success('층 정보가 등록되었습니다.');
    }
    floorModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('저장 실패');
  }
}

onMounted(() => {
  fetchBuildings();
});
</script>

<template>
  <Page auto-content-height>
    <div class="mb-4 flex items-center justify-between bg-card p-4 rounded border">
      <div class="flex items-center gap-2">
        <span class="font-semibold text-sm">건물 필터:</span>
        <Select v-model:value="filterBuildingId" style="width: 200px" placeholder="건물 선택">
          <Select.Option v-for="b in buildings" :key="b.id" :value="b.id">{{ b.name }}</Select.Option>
        </Select>
      </div>
      <Button type="primary" @click="onCreate">
        <Plus class="size-5 mr-1" />
        신규 층 등록
      </Button>
    </div>

    <Grid table-title="층 정보 목록">
      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="onEdit(row)">수정</Button>
          <Popconfirm title="해당 층을 삭제하시겠습니까?" @confirm="onDelete(row)">
            <Button type="link" size="small" danger>삭제</Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <FloorModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="소속 건물" required>
            <Select v-model:value="formModel.buildingId">
              <Select.Option v-for="b in buildings" :key="b.id" :value="b.id">{{ b.name }}</Select.Option>
            </Select>
          </Form.Item>
          <Form.Item label="층 명칭" required>
            <Input v-model:value="formModel.name" placeholder="예: 지하 1층, 2층 등" />
          </Form.Item>
          <Form.Item label="층 코드" required>
            <Input v-model:value="formModel.code" placeholder="예: B1F, 2F" />
          </Form.Item>
          <Form.Item label="정렬 순서">
            <InputNumber v-model:value="formModel.sortOrder" :min="1" style="width: 100%" />
          </Form.Item>
          <Form.Item label="비고/설명">
            <Input.TextArea v-model:value="formModel.remark" placeholder="특이 사항 입력" />
          </Form.Item>
        </Form>
      </div>
    </FloorModal>
  </Page>
</template>
