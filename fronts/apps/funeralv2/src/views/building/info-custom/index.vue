<script lang="ts" setup>
import { ref } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getBuildings, createBuilding, updateBuilding, deleteBuilding } from '#/api/building';

const [BuildingModal, buildingModalApi] = useVbenModal({
  title: '건물 정보 설정',
  destroyOnClose: true,
});

const formModel = ref({
  id: '',
  name: '',
  code: '',
  address: '',
  remark: ''
});

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '건물명', minWidth: 150 },
      { field: 'code', title: '건물코드', minWidth: 120 },
      { field: 'address', title: '주소', minWidth: 200 },
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
          return await getBuildings();
        },
      },
    },
  },
});

function onCreate() {
  formModel.value = { id: '', name: '', code: '', address: '', remark: '' };
  buildingModalApi.open();
}

function onEdit(row: any) {
  formModel.value = { ...row };
  buildingModalApi.open();
}

async function onDelete(row: any) {
  try {
    await deleteBuilding(row.id);
    message.success('건물 정보가 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

async function handleSave() {
  try {
    if (formModel.value.id) {
      await updateBuilding(formModel.value.id, formModel.value);
      message.success('건물 정보가 수정되었습니다.');
    } else {
      await createBuilding(formModel.value);
      message.success('건물 정보가 등록되었습니다.');
    }
    buildingModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('저장 실패');
  }
}
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="건물 정보 목록">
      <template #toolbar-tools>
        <Button type="primary" @click="onCreate">
          <Plus class="size-5 mr-1" />
          신규 건물 등록
        </Button>
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="onEdit(row)">수정</Button>
          <Popconfirm title="해당 건물을 삭제하시겠습니까?" @confirm="onDelete(row)">
            <Button type="link" size="small" danger>삭제</Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <BuildingModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="건물명" required>
            <Input v-model:value="formModel.name" placeholder="예: 본관, 신관 등" />
          </Form.Item>
          <Form.Item label="건물코드" required>
            <Input v-model:value="formModel.code" placeholder="예: MAIN_BLDG" :disabled="!!formModel.id" />
          </Form.Item>
          <Form.Item label="주소">
            <Input v-model:value="formModel.address" placeholder="건물 위치 주소 입력" />
          </Form.Item>
          <Form.Item label="비고/설명">
            <Input.TextArea v-model:value="formModel.remark" placeholder="건물 특이 사항 입력" />
          </Form.Item>
        </Form>
      </div>
    </BuildingModal>
  </Page>
</template>
