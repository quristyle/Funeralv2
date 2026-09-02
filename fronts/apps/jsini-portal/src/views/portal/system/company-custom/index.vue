<script lang="ts" setup>
import { ref } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Button, message, Popconfirm, Form, Input } from 'ant-design-vue';
import GridIconButton from '#/components/GridIconButton.vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getCompanyList, createCompany, updateCompany, deleteCompany } from '#/api/portal/system/company';

const [CompanyModal, companyModalApi] = useVbenModal({
  title: '회사 기본 정보 관리',
  destroyOnClose: true,
});

const formModel = ref({
  id: '',
  name: '',
  bizNumber: '',
  representative: '',
  phone: '',
  address: '',
  remark: ''
});

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '회사명', minWidth: 150 },
      { field: 'businessNumber', title: '사업자등록번호', minWidth: 140 },
      { field: 'representative', title: '대표자명', minWidth: 100 },
      { field: 'phone', title: '전화번호', minWidth: 120 },
      { field: 'address', title: '회사 소재지', minWidth: 200 },
      { field: 'remark', title: '설명', minWidth: 180 },
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
          const res = await getCompanyList();
          return res.items || [];
        },
      },
    },
  },
});

function onCreate() {
  formModel.value = {
    id: '',
    name: '',
    bizNumber: '',
    representative: '',
    phone: '',
    address: '',
    remark: ''
  };
  companyModalApi.open();
}

function onEdit(row: any) {
  formModel.value = {
    id: row.id,
    name: row.name,
    bizNumber: row.businessNumber || '',
    representative: row.representative || '',
    phone: row.phone || '',
    address: row.address || '',
    remark: row.remark || ''
  };
  companyModalApi.open();
}

async function onDelete(row: any) {
  try {
    await deleteCompany(row.id);
    message.success('회사 정보가 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

async function handleSave() {
  try {
    if (!formModel.value.name || !formModel.value.bizNumber) {
      message.warning('회사명과 사업자등록번호는 필수 기입 사항입니다.');
      return;
    }

    const params = {
      name: formModel.value.name,
      businessNumber: formModel.value.bizNumber,
      representative: formModel.value.representative,
      status: 1, // 활성화
      remark: formModel.value.remark
    };

    if (formModel.value.id) {
      await updateCompany(formModel.value.id, params);
      message.success('회사 정보가 수정되었습니다.');
    } else {
      await createCompany(params);
      message.success('회사가 성공적으로 등록되었습니다.');
    }
    companyModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('저장 실패');
  }
}
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="장례 서비스 파트너/회사 정보 목록">
      <template #toolbar-tools>
        <GridIconButton
          v-perm:create
          icon="vxe-icon-add"
          title="신규 회사 등록"
          @click="onCreate"
        />
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="onEdit(row)">수정</Button>
          <Popconfirm title="해당 회사를 삭제하시겠습니까?" @confirm="onDelete(row)">
            <Button v-perm:delete type="link" size="small" danger>삭제</Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <CompanyModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="회사명" required>
            <Input v-model:value="formModel.name" placeholder="파트너 회사 명칭" />
          </Form.Item>
          
          <Form.Item label="사업자등록번호" required>
            <Input v-model:value="formModel.bizNumber" placeholder="예: 123-45-67890" />
          </Form.Item>

          <div class="grid grid-cols-2 gap-4">
            <Form.Item label="대표자 성명">
              <Input v-model:value="formModel.representative" placeholder="대표자 이름" />
            </Form.Item>
            <Form.Item label="회사 연락처">
              <Input v-model:value="formModel.phone" placeholder="전화번호" />
            </Form.Item>
          </div>

          <Form.Item label="회사 주소">
            <Input v-model:value="formModel.address" placeholder="소재지 주소" />
          </Form.Item>

          <Form.Item label="비고/특이사항">
            <Input.TextArea v-model:value="formModel.remark" placeholder="간단한 메모 및 비고 입력" />
          </Form.Item>
        </Form>
      </div>
    </CompanyModal>
  </Page>
</template>
