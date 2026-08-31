<script lang="ts" setup>
import { ref, watch } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { IconifyIcon, Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, InputNumber, Tooltip } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getFloors, createFloor, updateFloor, deleteFloor } from '#/api/funeral/building';
import BizSelect from '#/components/BizSelect.vue';

const selectedCompanyId = ref<string>('');
const filterBuildingId = ref<string>('');

const [FloorModal, floorModalApi] = useVbenModal({
  title: '층 정보 설정',
  destroyOnClose: true,
  onConfirm: async () => {
    await handleSave();
  }
});

const formModel = ref({
  id: '',
  buildingId: '',
  name: '',
  sortOrder: 1,
  remark: ''
});



const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    editConfig: { trigger: 'click', mode: 'cell' },
    columns: [
      { field: 'buildingName', title: '건물명', minWidth: 150 },
      { 
        field: 'name', 
        title: '층 명칭', 
        minWidth: 120, 
        editRender: { name: 'input', autofocus: true } 
      },
      { 
        field: 'sortOrder', 
        title: '정렬 순서', 
        minWidth: 100, 
        editRender: { name: 'VxeInput', props: { type: 'integer', min: 1 } } 
      },
      { 
        field: 'remark', 
        title: '비고', 
        minWidth: 200, 
        editRender: { name: 'input' } 
      },
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
  gridEvents: {
    'edit-closed': async ({ row }) => {
      try {
        await updateFloor(row.id, {
          buildingId: row.buildingId,
          name: row.name,
          sortOrder: Number(row.sortOrder) || 1,
          remark: row.remark || ''
        });
        message.success('층 정보가 즉시 저장되었습니다.');
      } catch (error) {
        message.error('즉시 저장 실패');
        gridApi.query();
      }
    }
  }
});

// 필터링 건물 변경 시 그리드 갱신
watch(filterBuildingId, () => {
  gridApi.query();
});

// 회사 변경 시 건물 필터 값을 초기화하여 새 회사의 첫 번째 건물이 자동 선택되도록 처리
watch(selectedCompanyId, () => {
  filterBuildingId.value = '';
});

function onCreate() {
  formModel.value = {
    id: '',
    buildingId: filterBuildingId.value,
    name: '',
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


</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <div class="mb-4 flex items-center justify-between bg-card p-4 rounded border">
      <div class="flex items-center gap-4">
        <div class="flex items-center gap-2">
          <span class="font-semibold text-sm">회사 필터:</span>
          <BizSelect
            v-model:value="selectedCompanyId"
            type="funeralCompany"
            auto-select-first
            placeholder="회사 선택"
            class="w-64"
            show-search
            option-filter-prop="label"
          />
        </div>
        <div class="flex items-center gap-2">
          <span class="font-semibold text-sm">건물 필터:</span>
          <BizSelect
            v-model:value="filterBuildingId"
            type="building"
            :params="{ companyId: selectedCompanyId }"
            auto-select-first
            placeholder="건물 선택"
            class="w-64"
            show-search
            option-filter-prop="label"
          />
        </div>
      </div>
      <Button v-perm:create type="primary" @click="onCreate">
        <Plus class="size-5 mr-1" />
        신규 층 등록
      </Button>
    </div>

    <Grid table-title="층 정보 목록">
      <template #action="{ row }">
        <div class="flex gap-2">
          <Tooltip v-perm:update title="수정">
            <Button type="link" size="small" @click="onEdit(row)">
              <IconifyIcon icon="lucide:edit" class="size-4" />
            </Button>
          </Tooltip>
          <Popconfirm title="해당 층을 삭제하시겠습니까?" @confirm="onDelete(row)" placement="topLeft">
            <Tooltip v-perm:delete title="삭제">
              <Button type="link" size="small" danger>
                <IconifyIcon icon="lucide:trash-2" class="size-4" />
              </Button>
            </Tooltip>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <FloorModal>
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="소속 건물" required>
            <BizSelect
              type="building"
              :params="{ companyId: selectedCompanyId }"
              v-model:value="formModel.buildingId"
              placeholder="건물을 선택해주세요"
            />
          </Form.Item>
          <Form.Item label="층 명칭" required>
            <Input v-model:value="formModel.name" placeholder="예: 지하 1층, 2층 등" />
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
