<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, Select, Badge } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getAccounts, createAccount, updateAccount, deleteAccount } from '#/api/system/account';
import { getDeptList } from '#/api/system/dept';

const departments = ref<any[]>([]);
const [AccountModal, accountModalApi] = useVbenModal({
  title: '사용자 계정 정보 설정',
  destroyOnClose: true,
});

const formModel = ref({
  id: '',
  loginId: '',
  userName: '',
  email: '',
  phone: '',
  status: 'ACTIVE' as 'ACTIVE' | 'LOCKED' | 'DISABLED',
  deptId: '',
});

// 부서 목록 로드
async function fetchDepts() {
  try {
    const list = await getDeptList();
    departments.value = list || [];
  } catch (error) {
    message.error('부서 목록 로드 실패');
  }
}

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'userName', title: '사용자명', minWidth: 120 },
      { field: 'loginId', title: '로그인 ID', minWidth: 120 },
      { field: 'deptName', title: '소속 부서', minWidth: 150 },
      { field: 'email', title: '이메일', minWidth: 180 },
      { field: 'phone', title: '연락처', minWidth: 130 },
      {
        field: 'status',
        title: '계정 상태',
        minWidth: 120,
        slots: { default: 'status-tag' }
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
          return await getAccounts();
        },
      },
    },
  },
});

function onCreate() {
  formModel.value = {
    id: '',
    loginId: '',
    userName: '',
    email: '',
    phone: '',
    status: 'ACTIVE',
    deptId: '',
  };
  accountModalApi.open();
}

function onEdit(row: any) {
  formModel.value = { ...row };
  accountModalApi.open();
}

async function onDelete(row: any) {
  try {
    await deleteAccount(row.id);
    message.success('계정이 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

async function handleSave() {
  try {
    if (!formModel.value.loginId || !formModel.value.userName) {
      message.warning('로그인 ID와 사용자명은 필수 기입 사항입니다.');
      return;
    }
    const dept = departments.value.find(d => d.id === formModel.value.deptId);
    const postData = {
      ...formModel.value,
      deptName: dept?.name || ''
    };

    if (formModel.value.id) {
      await updateAccount(formModel.value.id, postData);
      message.success('계정 정보가 변경되었습니다.');
    } else {
      await createAccount(postData);
      message.success('신규 계정이 등록되었습니다.');
    }
    accountModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('저장 실패');
  }
}

onMounted(() => {
  fetchDepts();
});
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="어드민 사용자 계정 목록">
      <template #toolbar-tools>
        <Button type="primary" @click="onCreate">
          <Plus class="size-5 mr-1" />
          신규 계정 등록
        </Button>
      </template>

      <template #status-tag="{ row }">
        <Badge
          v-if="row.status === 'ACTIVE'"
          status="success"
          text="정상 작동"
        />
        <Badge
          v-else-if="row.status === 'LOCKED'"
          status="warning"
          text="일시 잠금"
        />
        <Badge
          v-else
          status="error"
          text="비활성화"
        />
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="onEdit(row)">수정</Button>
          <Popconfirm title="해당 계정을 삭제하시겠습니까?" @confirm="onDelete(row)">
            <Button type="link" size="small" danger>삭제</Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <AccountModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="로그인 ID" required>
            <Input v-model:value="formModel.loginId" placeholder="아이디 기입" :disabled="!!formModel.id" />
          </Form.Item>
          
          <Form.Item label="사용자명(성명)" required>
            <Input v-model:value="formModel.userName" placeholder="이름 기입" />
          </Form.Item>

          <Form.Item label="소속 부서">
            <Select v-model:value="formModel.deptId" placeholder="부서 선택">
              <Select.Option v-for="d in departments" :key="d.id" :value="d.id">{{ d.name }}</Select.Option>
            </Select>
          </Form.Item>

          <Form.Item label="이메일">
            <Input v-model:value="formModel.email" placeholder="example@email.com" />
          </Form.Item>

          <Form.Item label="연락처">
            <Input v-model:value="formModel.phone" placeholder="전화번호 기입" />
          </Form.Item>

          <Form.Item label="계정 잠금 상태">
            <Select v-model:value="formModel.status">
              <Select.Option value="ACTIVE">정상 (ACTIVE)</Select.Option>
              <Select.Option value="LOCKED">잠금 (LOCKED)</Select.Option>
              <Select.Option value="DISABLED">영구정지 (DISABLED)</Select.Option>
            </Select>
          </Form.Item>
        </Form>
      </div>
    </AccountModal>
  </Page>
</template>
