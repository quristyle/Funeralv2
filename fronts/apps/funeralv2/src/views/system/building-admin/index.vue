<script lang="ts" setup>
import { ref, onMounted, watch } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, Select, Badge } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getBuildingAdmins, createBuildingAdmin, updateBuildingAdmin, deleteBuildingAdmin } from '#/api/system/building-admin';
import { getBuildings } from '#/api/building';
import { getAccounts } from '#/api/system/account';

const buildings = ref<any[]>([]);
const filterBuildingId = ref<string>('');
const userList = ref<any[]>([]);

const [AdminModal, adminModalApi] = useVbenModal({
  title: '건물 관리자(현장고객) 배정 설정',
  destroyOnClose: true,
});

const formModel = ref({
  id: '',
  buildingId: '',
  userId: '',
  status: 'ACTIVE' as 'ACTIVE' | 'INACTIVE',
  phone: '',
  remark: ''
});

// 건물 목록 로드
async function fetchBuildings() {
  try {
    const list = await getBuildings();
    buildings.value = list || [];
  } catch (error) {
    message.error('건물 목록 로드 실패');
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
      { field: 'buildingName', title: '소속 건물명', minWidth: 150 },
      { field: 'userName', title: '건물 관리자', minWidth: 120 },
      { field: 'loginId', title: '로그인 ID', minWidth: 120 },
      { field: 'phone', title: '연락처', minWidth: 130 },
      {
        field: 'status',
        title: '상태',
        minWidth: 100,
        slots: { default: 'status-badge' }
      },
      { field: 'remark', title: '비고', minWidth: 180 },
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
          return await getBuildingAdmins(filterBuildingId.value || undefined);
        },
      },
    },
  },
});

watch(filterBuildingId, () => {
  gridApi.query();
});

function onCreate() {
  formModel.value = {
    id: '',
    buildingId: filterBuildingId.value,
    userId: '',
    status: 'ACTIVE',
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
    await deleteBuildingAdmin(row.id);
    message.success('건물 관리자 권한이 회수되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('해제 실패');
  }
}

async function handleSave() {
  try {
    if (!formModel.value.buildingId || !formModel.value.userId) {
      message.warning('건물 및 관리자 계정은 필수 선택 사항입니다.');
      return;
    }
    const user = userList.value.find(u => u.id === formModel.value.userId);
    const postData = {
      ...formModel.value,
      userName: user?.userName || '',
      loginId: user?.loginId || '',
      buildingName: buildings.value.find(b => b.id === formModel.value.buildingId)?.name || ''
    };

    if (formModel.value.id) {
      await updateBuildingAdmin(formModel.value.id, postData);
      message.success('건물 관리자 정보가 수정되었습니다.');
    } else {
      await createBuildingAdmin(postData);
      message.success('건물 관리자 배정이 성공적으로 처리되었습니다.');
    }
    adminModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('저장 실패');
  }
}

onMounted(() => {
  fetchBuildings();
  fetchUsers();
});
</script>

<template>
  <Page auto-content-height>
    <div class="mb-4 flex items-center justify-between bg-card p-4 rounded border">
      <div class="flex items-center gap-2">
        <span class="font-semibold text-sm">건물 필터:</span>
        <Select v-model:value="filterBuildingId" style="width: 220px" placeholder="전체 건물 보기" allow-clear>
          <Select.Option v-for="b in buildings" :key="b.id" :value="b.id">{{ b.name }}</Select.Option>
        </Select>
      </div>
      <Button type="primary" @click="onCreate">
        <Plus class="size-5 mr-1" />
        건물 관리자 등록
      </Button>
    </div>

    <Grid table-title="건물별 현장 고객사 관리자 배정 목록">
      <template #status-badge="{ row }">
        <Badge
          :status="row.status === 'ACTIVE' ? 'success' : 'error'"
          :text="row.status === 'ACTIVE' ? '사용중' : '사용중단'"
        />
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="onEdit(row)">수정</Button>
          <Popconfirm title="해당 계정의 건물 관리자 권한을 해제하시겠습니까?" @confirm="onDelete(row)">
            <Button type="link" size="small" danger>삭제</Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <AdminModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="소속 건물" required>
            <Select v-model:value="formModel.buildingId">
              <Select.Option v-for="b in buildings" :key="b.id" :value="b.id">{{ b.name }}</Select.Option>
            </Select>
          </Form.Item>
          
          <Form.Item label="현장 담당자 계정" required>
            <Select v-model:value="formModel.userId" show-search option-filter-prop="label">
              <Select.Option v-for="u in userList" :key="u.id" :value="u.id" :label="u.userName">{{ u.userName }} ({{ u.loginId }})</Select.Option>
            </Select>
          </Form.Item>

          <Form.Item label="현장 연락처">
            <Input v-model:value="formModel.phone" placeholder="전화번호 기입" />
          </Form.Item>

          <Form.Item label="상태">
            <Select v-model:value="formModel.status">
              <Select.Option value="ACTIVE">활성화</Select.Option>
              <Select.Option value="INACTIVE">비활성화</Select.Option>
            </Select>
          </Form.Item>

          <Form.Item label="비고">
            <Input.TextArea v-model:value="formModel.remark" placeholder="위임 내용 및 관리 내역 작성" />
          </Form.Item>
        </Form>
      </div>
    </AdminModal>
  </Page>
</template>
