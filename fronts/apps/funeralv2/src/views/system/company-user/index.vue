<script lang="ts" setup>
import { ref, watch, onMounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Card, Button, Popconfirm, message, Table, Modal } from 'ant-design-vue';
import { Plus, IconifyIcon } from '@vben/icons';
import { useVbenVxeGrid, type VxeTableGridColumns } from '#/adapter/vxe-table';
import { getCompanyList, getCompanyUsers, getEligibleCompanyUsers, assignCompanyUsers, removeCompanyUsers } from '#/api/system/company';
import type { SystemRolePermissionApi } from '#/api/system/role-permission';

// 현재 선택된 회사 ID 및 회사명
const selectedCompanyId = ref<string>('');
const selectedCompanyName = ref<string>('');

// 매핑된 사용자 목록 상태
const mappedUsers = ref<SystemRolePermissionApi.RoleUser[]>([]);
const loadingMapped = ref<boolean>(false);

// 추가 지정 모달 상태
const isModalVisible = ref<boolean>(false);
const eligibleUsers = ref<SystemRolePermissionApi.RoleUser[]>([]);
const loadingEligible = ref<boolean>(false);
const selectedRowKeys = ref<any[]>([]);

// 매핑된 사용자 테이블 컬럼 정의
const mappedColumns = [
  { title: '사용자 ID', dataIndex: 'loginId', key: 'loginId' },
  { title: '사용자명', dataIndex: 'userName', key: 'userName' },
  { title: '소속 부서', dataIndex: 'deptName', key: 'deptName' },
  { title: '이메일', dataIndex: 'email', key: 'email' },
  { title: '연락처', dataIndex: 'phone', key: 'phone' },
  { title: '작업', key: 'action', width: 100, align: 'center' as const },
];

// 추가 모달 테이블 컬럼 정의
const eligibleColumns = [
  { title: '사용자 ID', dataIndex: 'loginId', key: 'loginId' },
  { title: '사용자명', dataIndex: 'userName', key: 'userName' },
  { title: '이메일', dataIndex: 'email', key: 'email' },
];

// 회사 목록 그리드 컬럼 설정 (좌측)
const columns: VxeTableGridColumns = [
  { field: 'name', title: '회사명', minWidth: 150 },
  { field: 'shortName', title: '짧은명칭', width: 100 },
];

// VXETable 좌측 회사 목록 설정
const [Grid] = useVbenVxeGrid({
  gridOptions: {
    columns: columns,
    height: 'auto',
    rowConfig: {
      isCurrent: true,
      keyField: 'id',
    },
    proxyConfig: {
      ajax: {
        query: async () => {
          const res = await getCompanyList();
          // 백엔드 반환 타입이 PagedResult 또는 Array일 수 있으므로 유연하게 대처
          if (res && 'items' in res) {
            return res.items;
          }
          return res || [];
        },
      },
    },
  },
  gridEvents: {
    cellClick: ({ row }: any) => {
      if (row) {
        selectedCompanyId.value = row.id;
        selectedCompanyName.value = row.name;
      }
    },
  },
});

// 지정 사용자 로드
async function fetchMappedUsers() {
  if (!selectedCompanyId.value) {
    mappedUsers.value = [];
    return;
  }
  loadingMapped.value = true;
  try {
    const res = await getCompanyUsers(selectedCompanyId.value);
    mappedUsers.value = (res as any)?.result ?? res ?? [];
  } catch (error) {
    console.error(error);
    message.error('지정 사용자 목록 로드 실패');
  } finally {
    loadingMapped.value = false;
  }
}

// 추가 가능 사용자 로드 (회사 미지정 계정)
async function fetchEligibleUsers() {
  loadingEligible.value = true;
  try {
    const res = await getEligibleCompanyUsers();
    eligibleUsers.value = (res as any)?.result ?? res ?? [];
  } catch (error) {
    console.error(error);
    message.error('추가 가능 사용자 목록 로드 실패');
  } finally {
    loadingEligible.value = false;
  }
}

// 소속 사용자 해제 처리
async function handleRemove(userId: string) {
  try {
    await removeCompanyUsers([userId]);
    message.success('사용자의 회사 소속이 해제되었습니다.');
    await fetchMappedUsers();
  } catch (error) {
    console.error(error);
    message.error('해제 실패');
  }
}

// 추가 지정 모달 열기
function openAssignModal() {
  selectedRowKeys.value = [];
  fetchEligibleUsers();
  isModalVisible.value = true;
}

// 사용자 추가 지정 확인
async function handleAssignConfirm() {
  if (selectedRowKeys.value.length === 0) {
    message.warning('추가할 사용자를 선택해 주세요.');
    return;
  }

  try {
    await assignCompanyUsers(selectedCompanyId.value, selectedRowKeys.value as string[]);
    message.success('사용자가 회사 소속으로 지정되었습니다.');
    isModalVisible.value = false;
    await fetchMappedUsers();
  } catch (error) {
    console.error(error);
    message.error('사용자 지정 추가 실패');
  }
}

// 모달 테이블 체크박스 변경 핸들러
function onSelectChange(keys: any[]) {
  selectedRowKeys.value = keys;
}

// 회사 선택 변경 감시
watch(selectedCompanyId, () => {
  fetchMappedUsers();
});

onMounted(() => {
  // 컴포넌트 마운트 시 초기화 동작이 필요한 경우 작성
});
</script>

<template>
  <Page auto-content-height>
    <div class="grid grid-cols-12 gap-4 h-full">
      <!-- 좌측: 회사 목록 그리드 -->
      <div class="col-span-12 lg:col-span-4 h-full flex flex-col">
        <Card class="flex-1 flex flex-col h-full overflow-hidden" title="회사 목록" :body-style="{ flex: 1, padding: 0, overflow: 'hidden' }">
          <Grid class="h-full w-full" />
        </Card>
      </div>

      <!-- 우측: 매핑 상세 및 설정 -->
      <div class="col-span-12 lg:col-span-8 h-full flex flex-col">
        <Card
          class="flex-1 flex flex-col h-full overflow-hidden"
          :title="selectedCompanyName ? `[${selectedCompanyName}] 소속 사용자 관리` : '회사를 선택해 주세요'"
          :body-style="{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden', padding: '12px 24px' }"
        >
          <template v-if="selectedCompanyId">
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
              <div class="flex-1 overflow-auto border rounded-lg bg-white">
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
                      <Popconfirm
                        title="해당 사용자의 회사 지정을 해제하시겠습니까?"
                        ok-text="예"
                        cancel-text="아니오"
                        @confirm="handleRemove(record.id)"
                      >
                        <Button type="link" danger size="small">
                          <template #icon>
                            <IconifyIcon class="size-3.5 mr-0.5 inline" icon="lucide:trash-2" />
                          </template>
                          해제
                        </Button>
                      </Popconfirm>
                    </template>
                  </template>
                </Table>
              </div>
            </div>
          </template>
          
          <template v-else>
            <div class="flex-1 flex flex-col justify-center items-center text-gray-400 py-20">
              <span class="text-lg font-medium">좌측 목록에서 사용자를 설정할 회사를 클릭해 주세요.</span>
            </div>
          </template>
        </Card>
      </div>
    </div>

    <!-- 사용자 추가 모달 -->
    <Modal
      v-model:open="isModalVisible"
      title="회사에 사용자 추가 지정"
      width="700px"
      @ok="handleAssignConfirm"
    >
      <div class="py-2">
        <div class="mb-2 text-gray-500 text-xs">
          * 현재 소속된 회사가 없는 사용자들만 목록에 노출됩니다.
        </div>
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
  </Page>
</template>

<style lang="css" scoped>
.custom-table :deep(.ant-table-pagination) {
  margin: 16px 24px;
}
</style>
