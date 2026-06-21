<script lang="ts" setup>
import { ref, onMounted, watch } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, Select, Badge } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getCompanyAdmins, createCompanyAdmin, updateCompanyAdmin, deleteCompanyAdmin } from '#/api/system/company-admin';
import { getCompanyList } from '#/api/system/company';
import { getAccounts } from '#/api/system/account';

const companies = ref<any[]>([]);
const filterCompanyId = ref<string>('');
const userList = ref<any[]>([]);

const [AdminModal, adminModalApi] = useVbenModal({
  title: '회사 관리자 계정 권한 설정',
  destroyOnClose: true,
});

const formModel = ref({
  id: '',
  companyId: '',
  userId: '',
  status: 'ACTIVE' as 'ACTIVE' | 'INACTIVE',
  email: '',
  phone: '',
  remark: ''
});

// 회사 목록 로드
async function fetchCompanies() {
  try {
    const list = await getCompanyList();
    companies.value = list.items || [];
  } catch (error) {
    message.error('회사 목록 로드 실패');
  }
}

// 사용자 목록 로드
async function fetchUsers() {
  try {
    const users = await getAccounts();
    userList.value = users || [];
  } catch (error) {
    message.error('사용자 목록 로드 실패');
  }
}

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'companyName', title: '소속 회사명', minWidth: 150 },
      { field: 'userName', title: '관리자 성명', minWidth: 120 },
      { field: 'loginId', title: '로그인 아이디', minWidth: 120 },
      { field: 'email', title: '이메일 주소', minWidth: 160 },
      { field: 'phone', title: '연락처', minWidth: 130 },
      {
        field: 'status',
        title: '상태',
        minWidth: 100,
        slots: { default: 'status-badge' }
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
          return await getCompanyAdmins(filterCompanyId.value || undefined);
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
    userId: '',
    status: 'ACTIVE',
    email: '',
    phone: '',
    remark: ''
  };
  adminModalApi.open();
}

function onEdit(row: any) {
  formModel.value = { ...row };
  adminModalApi.open();
}

async function onDelete(row: any) {
  try {
    await deleteCompanyAdmin(row.id);
    message.success('관리자 지정이 해제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('해제 실패');
  }
}

async function handleSave() {
  try {
    if (!formModel.value.companyId || !formModel.value.userId) {
      message.warning('회사 및 관리자 계정은 필수 선택 사항입니다.');
      return;
    }
    const user = userList.value.find(u => u.id === formModel.value.userId);
    const postData = {
      ...formModel.value,
      userName: user?.userName || '',
      loginId: user?.loginId || '',
      companyName: companies.value.find(c => c.id === formModel.value.companyId)?.name || ''
    };

    if (formModel.value.id) {
      await updateCompanyAdmin(formModel.value.id, postData);
      message.success('관리자 정보가 수정되었습니다.');
    } else {
      await createCompanyAdmin(postData);
      message.success('관리자가 성공적으로 등록되었습니다.');
    }
    adminModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('저장 실패');
  }
}

onMounted(() => {
  fetchCompanies();
  fetchUsers();
});
</script>

<template>
  <Page auto-content-height>
    <div class="mb-4 flex items-center justify-between bg-card p-4 rounded border">
      <div class="flex items-center gap-2">
        <span class="font-semibold text-sm">회사 필터:</span>
        <Select v-model:value="filterCompanyId" style="width: 220px" placeholder="전체 회사 보기" allow-clear>
          <Select.Option v-for="c in companies" :key="c.id" :value="c.id">{{ c.name }}</Select.Option>
        </Select>
      </div>
      <Button type="primary" @click="onCreate">
        <Plus class="size-5 mr-1" />
        회사 관리자 등록
      </Button>
    </div>

    <Grid table-title="회사 관리자 정보 목록">
      <template #status-badge="{ row }">
        <Badge
          :status="row.status === 'ACTIVE' ? 'success' : 'error'"
          :text="row.status === 'ACTIVE' ? '정상 작동' : '일시 중지'"
        />
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="onEdit(row)">수정</Button>
          <Popconfirm title="해당 계정의 회사 관리자 권한을 회수하시겠습니까?" @confirm="onDelete(row)">
            <Button type="link" size="small" danger>권한 해제</Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <AdminModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="소속 회사" required>
            <Select v-model:value="formModel.companyId">
              <Select.Option v-for="c in companies" :key="c.id" :value="c.id">{{ c.name }}</Select.Option>
            </Select>
          </Form.Item>
          
          <Form.Item label="지정할 사용자 계정" required>
            <Select v-model:value="formModel.userId" show-search option-filter-prop="label">
              <Select.Option v-for="u in userList" :key="u.id" :value="u.id" :label="u.userName">{{ u.userName }} ({{ u.loginId }})</Select.Option>
            </Select>
          </Form.Item>

          <Form.Item label="이메일">
            <Input v-model:value="formModel.email" placeholder="이메일 주소 입력" />
          </Form.Item>
          
          <Form.Item label="연락처">
            <Input v-model:value="formModel.phone" placeholder="예: 010-1234-5678" />
          </Form.Item>

          <Form.Item label="상태">
            <Select v-model:value="formModel.status">
              <Select.Option value="ACTIVE">활성화</Select.Option>
              <Select.Option value="INACTIVE">비활성화</Select.Option>
            </Select>
          </Form.Item>
        </Form>
      </div>
    </AdminModal>
  </Page>
</template>
