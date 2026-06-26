<script lang="ts" setup>
import { ref, watch } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { IconifyIcon, Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, Tooltip } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getBuildings, createBuilding, updateBuilding, deleteBuilding } from '#/api/building';
import BizSelect from '#/components/BizSelect.vue';

const filterCompanyId = ref<string>('');

const [BuildingModal, buildingModalApi] = useVbenModal({
  title: '건물 정보 설정',
  destroyOnClose: true,
  onConfirm: async () => {
    await handleSave();
  }
});

const formModel = ref({
  id: '',
  companyId: '',
  name: '',
  shortName: '',
  abbreviation: '',
  address: '',
  zipCode: '',
  addressDetail: '',
  remark: ''
});

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '건물명', minWidth: 150 },
      { field: 'shortName', title: '짧은명칭', minWidth: 120 },
      { field: 'abbreviation', title: '약어', minWidth: 100 },
      { field: 'address', title: '주소', minWidth: 250 },
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
          return await getBuildings(filterCompanyId.value);
        },
      },
    },
  },
});

watch(filterCompanyId, () => {
  gridApi.query();
});

function onCreate() {
  formModel.value = {
    id: '',
    companyId: filterCompanyId.value,
    name: '',
    shortName: '',
    abbreviation: '',
    address: '',
    zipCode: '',
    addressDetail: '',
    remark: ''
  };
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
    <div class="mb-4 flex items-center justify-between bg-card p-4 rounded-lg shadow-sm border border-border">
      <div class="flex items-center gap-4">
        <div class="flex items-center gap-2">
          <span class="font-semibold text-sm">회사 필터:</span>
          <BizSelect
            v-model:value="filterCompanyId"
            type="company"
            auto-select-first
            placeholder="회사 선택"
            class="w-64"
            show-search
            option-filter-prop="label"
          />
        </div>
      </div>
      <Button type="primary" @click="onCreate">
        <Plus class="size-5 mr-1" />
        신규 건물 등록
      </Button>
    </div>

    <Grid table-title="건물 정보 목록">
      <template #action="{ row }">
        <div class="flex gap-2 justify-center">
          <Tooltip title="수정">
            <Button type="link" size="small" @click="onEdit(row)">
              <IconifyIcon icon="lucide:edit" class="size-4" />
            </Button>
          </Tooltip>
          <Popconfirm title="해당 건물을 삭제하시겠습니까?" @confirm="onDelete(row)" placement="topLeft">
            <Tooltip title="삭제">
              <Button type="link" size="small" danger>
                <IconifyIcon icon="lucide:trash-2" class="size-4" />
              </Button>
            </Tooltip>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <BuildingModal>
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="소속 회사" required>
            <BizSelect
              type="company"
              v-model:value="formModel.companyId"
              placeholder="회사를 선택해주세요"
            />
          </Form.Item>
          <Form.Item label="건물명" required>
            <Input v-model:value="formModel.name" placeholder="예: 본관, 신관, 장례식장 A동" />
          </Form.Item>
          <div class="grid grid-cols-2 gap-x-4">
            <Form.Item label="짧은 명칭">
              <Input v-model:value="formModel.shortName" placeholder="예: 본관" />
            </Form.Item>
            <Form.Item label="약어 (3자리 영문)">
              <Input v-model:value="formModel.abbreviation" placeholder="예: MAN" :maxlength="3" />
            </Form.Item>
          </div>
          <Form.Item label="주소">
            <Input v-model:value="formModel.address" placeholder="주소 입력" />
          </Form.Item>
          <Form.Item label="비고/설명">
            <Input.TextArea v-model:value="formModel.remark" placeholder="특이 사항 입력" />
          </Form.Item>
        </Form>
      </div>
    </BuildingModal>
  </Page>
</template>