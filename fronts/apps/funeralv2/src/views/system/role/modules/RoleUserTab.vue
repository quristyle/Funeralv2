<script lang="ts" setup>
import { ref, watch, onMounted } from 'vue';
import { Button, message, Modal, Table } from 'ant-design-vue';
import { Plus, IconifyIcon } from '@vben/icons';
import { getRoleUsers, getEligibleUsers, assignRoleUsers, removeRoleUser, type SystemRolePermissionApi } from '#/api/system/role-permission';

const props = defineProps({
  roleId: {
    type: String,
    required: true,
  },
});

// 매핑된 사용자 목록
const mappedUsers = ref<SystemRolePermissionApi.RoleUser[]>([]);
const loadingMapped = ref(false);

// 추가 모달 관련 상태
const isModalVisible = ref(false);
const eligibleUsers = ref<SystemRolePermissionApi.RoleUser[]>([]);
const loadingEligible = ref(false);
const selectedRowKeys = ref<any[]>([]);

// 매핑된 사용자 컬럼 정의
const mappedColumns = [
  { title: '사용자 ID', dataIndex: 'loginId', key: 'loginId' },
  { title: '사용자명', dataIndex: 'userName', key: 'userName' },
  { title: '소속 회사', dataIndex: 'companyName', key: 'companyName' },
  { title: '소속 부서', dataIndex: 'deptName', key: 'deptName' },
  { title: '이메일', dataIndex: 'email', key: 'email' },
  { title: '연락처', dataIndex: 'phone', key: 'phone' },
  { title: '작업', key: 'action', width: 100, align: 'center' as const },
];

// 추가 모달 테이블 컬럼 정의
const eligibleColumns = [
  { title: '사용자 ID', dataIndex: 'loginId', key: 'loginId' },
  { title: '사용자명', dataIndex: 'userName', key: 'userName' },
  { title: '소속 회사', dataIndex: 'companyName', key: 'companyName' },
  { title: '소속 부서', dataIndex: 'deptName', key: 'deptName' },
];

// 매핑된 사용자 로드
async function fetchMappedUsers() {
  if (!props.roleId) return;
  loadingMapped.value = true;
  try {
    const res = await getRoleUsers(props.roleId);
    mappedUsers.value = (res as any)?.result ?? res ?? [];
  } catch (error) {
    message.error('지정 사용자 목록 로드 실패');
  } finally {
    loadingMapped.value = false;
  }
}

// 할당 가능한 사용자 로드
async function fetchEligibleUsers() {
  if (!props.roleId) return;
  loadingEligible.value = true;
  try {
    const res = await getEligibleUsers(props.roleId);
    eligibleUsers.value = (res as any)?.result ?? res ?? [];
  } catch (error) {
    message.error('추가 가능 사용자 목록 로드 실패');
  } finally {
    loadingEligible.value = false;
  }
}

// 사용자 해제 처리
async function handleRemove(userId: string) {
  try {
    await removeRoleUser(props.roleId, userId);
    message.success('사용자 지정이 해제되었습니다.');
    fetchMappedUsers();
  } catch (error) {
    message.error('해제 실패');
  }
}

// 추가 모달 열기
function openAssignModal() {
  selectedRowKeys.value = [];
  fetchEligibleUsers();
  isModalVisible.value = true;
}

// 사용자 추가 등록 확인
async function handleAssignConfirm() {
  if (selectedRowKeys.value.length === 0) {
    message.warning('추가할 사용자를 선택해 주세요.');
    return;
  }

  try {
    await assignRoleUsers(props.roleId, selectedRowKeys.value as string[]);
    message.success('사용자가 지정되었습니다.');
    isModalVisible.value = false;
    fetchMappedUsers();
  } catch (error) {
    message.error('사용자 지정 추가 실패');
  }
}

// 모달 테이블 체크박스 변경
function onSelectChange(keys: any[]) {
  selectedRowKeys.value = keys;
}

watch(() => props.roleId, () => {
  fetchMappedUsers();
}, { immediate: true });

onMounted(() => {
  fetchMappedUsers();
});
</script>

<template>
  <div class="flex-1 flex flex-col overflow-hidden h-full">
    <!-- 상단 툴바 -->
    <div class="flex justify-end items-center mb-3 pt-2">
      <Button type="primary" @click="openAssignModal">
        <template #icon>
          <Plus class="size-4 mr-1 inline-block align-text-bottom" />
        </template>
        사용자 추가 지정
      </Button>
    </div>

    <!-- 지정 사용자 그리드 -->
    <div class="flex-1 overflow-auto border rounded-lg ">
      <Table
        :columns="mappedColumns"
        :data-source="mappedUsers"
        :loading="loadingMapped"
        :pagination="{ pageSize: 10 }"
        row-key="id"
        size="middle"
        class="custom-table"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'action'">
            <Button type="link" danger size="small" @click="handleRemove(record.id)">
              <template #icon>
                <IconifyIcon class="size-3.5 mr-0.5 inline" icon="lucide:trash-2" />
              </template>
              해제
            </Button>
          </template>
        </template>
      </Table>
    </div>

    <!-- 사용자 추가 모달 -->
    <Modal
      v-model:open="isModalVisible"
      title="역할에 사용자 추가 지정"
      width="700px"
      @ok="handleAssignConfirm"
    >
      <div class="py-2">
        <Table
          :columns="eligibleColumns"
          :data-source="eligibleUsers"
          :loading="loadingEligible"
          :row-selection="{ selectedRowKeys: selectedRowKeys, onChange: onSelectChange }"
          :pagination="{ pageSize: 5 }"
          row-key="id"
          size="middle"
          class="border rounded-md"
        />
      </div>
    </Modal>
  </div>
</template>

<style lang="css" scoped>
.custom-table :deep(.ant-table-pagination) {
  margin: 16px 24px;
}
</style>
