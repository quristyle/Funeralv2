<script lang="ts" setup>
import { ref } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, Select } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getRoleList, createRole, updateRole, deleteRole } from '#/api/system/role';

const [RoleModal, roleModalApi] = useVbenModal({
  title: '롤 권한 정보 설정',
  destroyOnClose: true,
});

// 폼 바인딩 데이터
const formModel = ref({
  id: '',
  name: '',
  code: '',
  status: 1 as 0 | 1,
  remark: ''
});

// 테이블 정의
const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '롤 명칭', minWidth: 150 },
      { field: 'code', title: '롤 코드', minWidth: 150 },
      { field: 'status', title: '사용상태', minWidth: 100, formatter: ({ cellValue }: { cellValue: any }) => (cellValue === 1 ? '사용' : '미사용') },
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
          return await getRoleList({});
        },
      },
    },
  },
});

function onCreate() {
  formModel.value = { id: '', name: '', code: '', status: 1, remark: '' };
  roleModalApi.open();
}

function onEdit(row: any) {
  formModel.value = { ...row };
  roleModalApi.open();
}

async function onDelete(row: any) {
  try {
    await deleteRole(row.id);
    message.success('롤이 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 작업 실패');
  }
}

async function handleSave() {
  try {
    if (formModel.value.id) {
      await updateRole(formModel.value.id, formModel.value);
      message.success('롤 정보가 수정되었습니다.');
    } else {
      await createRole(formModel.value);
      message.success('롤이 생성되었습니다.');
    }
    roleModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('저장 실패');
  }
}
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="권한(롤) 목록">
      <template #toolbar-tools>
        <Button type="primary" @click="onCreate">
          <Plus class="size-5 mr-1" />
          신규 롤 생성
        </Button>
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="onEdit(row)">수정</Button>
          <Popconfirm title="정말 삭제하시겠습니까?" @confirm="onDelete(row)">
            <Button type="link" size="small" danger>삭제</Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <!-- 롤 등록/수정 모달 -->
    <RoleModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="롤 명칭" required>
            <Input v-model:value="formModel.name" placeholder="예: 시스템 관리자" />
          </Form.Item>
          <Form.Item label="롤 코드" required>
            <Input v-model:value="formModel.code" placeholder="예: ROLE_ADMIN" :disabled="!!formModel.id" />
          </Form.Item>
          <Form.Item label="사용 상태">
            <Select v-model:value="formModel.status">
              <Select.Option :value="1">사용</Select.Option>
              <Select.Option :value="0">미사용</Select.Option>
            </Select>
          </Form.Item>
          <Form.Item label="설명">
            <Input.TextArea v-model:value="formModel.remark" placeholder="롤에 대한 상세 설명을 입력하세요" />
          </Form.Item>
        </Form>
      </div>
    </RoleModal>
  </Page>
</template>
