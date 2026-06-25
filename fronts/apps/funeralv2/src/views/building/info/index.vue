<script lang="ts" setup>
import { ref, watch } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { IconifyIcon, Plus } from '@vben/icons'; 
import { Button, message, Popconfirm, Form, Input, Tooltip, InputNumber } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getBuildings, createBuilding, updateBuilding, deleteBuilding } from '#/api/building';
import BizSelect from '#/components/BizSelect.vue';

import AddressSearchInput from '#/components/AddressSearchInput.vue';

const selectedCompanyId = ref<string>('');

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
  sortOrder: 1,
  zipCode: '',
  address: '',
  addressDetail: '',
  remark: ''
});

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '건물명', minWidth: 150 },
      { field: 'shortName', title: '짧은건물명', minWidth: 120 },
      { field: 'sortOrder', title: '정렬 순서', width: 100 },
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
          if (!selectedCompanyId.value) {
            return [];
          }
          return await getBuildings(selectedCompanyId.value);
        },
      },
    },
  },
});

function onCompanyChange() {
  gridApi.query();
}

watch(selectedCompanyId, (newVal) => {
  if (newVal) {
    gridApi.query();
  }
});

function onAddressSelected(data: { zipCode: string; address: string }) {
  formModel.value.zipCode = data.zipCode;
  formModel.value.address = data.address;
}

function onCreate() {
  if (!selectedCompanyId.value) {
    message.warning('회사를 먼저 선택해주세요.');
    return;
  }
  formModel.value = {
    id: '',
    companyId: selectedCompanyId.value,
    name: '',
    shortName: '',
    sortOrder: 1,
    zipCode: '',
    address: '',
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
    if (!formModel.value.companyId && selectedCompanyId.value) {
      formModel.value.companyId = selectedCompanyId.value;
    }
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
    <div class="mb-4 flex items-center gap-4 bg-card p-4 rounded-lg shadow-sm border border-border">
      <span class="text-sm font-medium">회사 선택 :</span>
      <BizSelect
        v-model:value="selectedCompanyId"
        type="company"
        auto-select-first
        placeholder="회사를 선택해주세요"
        class="w-64"
        show-search
        option-filter-prop="label"
        @change="onCompanyChange"
      />
    </div>

    <Grid table-title="건물 정보 목록">
      <template #toolbar-tools>
        <Button type="primary" @click="onCreate">
          <Plus class="size-5 mr-1" />
          신규 건물 등록
        </Button>
      </template>

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
            <Input v-model:value="formModel.name" placeholder="예: 본관, 신관 등" />
          </Form.Item>
          <Form.Item label="짧은건물명">
            <Input v-model:value="formModel.shortName" placeholder="예: 본관, 신관 등 짧은 명칭" />
          </Form.Item>
          <Form.Item label="정렬 순서">
            <InputNumber v-model:value="formModel.sortOrder" :min="1" style="width: 100%" />
          </Form.Item>
          <Form.Item label="우편번호">
            <AddressSearchInput
              v-model="formModel.zipCode"
              @selected="onAddressSelected"
              placeholder="우편번호"
            />
          </Form.Item>
          <Form.Item label="주소">
            <Input v-model:value="formModel.address" placeholder="기본 주소" disabled />
          </Form.Item>
          <Form.Item label="상세주소">
            <Input v-model:value="formModel.addressDetail" placeholder="상세 주소를 입력하세요" />
          </Form.Item>
          <Form.Item label="비고/설명">
            <Input.TextArea v-model:value="formModel.remark" placeholder="건물 특이 사항 입력" />
          </Form.Item>
        </Form>
      </div>
    </BuildingModal>
  </Page>
</template>
