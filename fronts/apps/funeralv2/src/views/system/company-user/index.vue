<script lang="ts" setup>
import { ref, watch, onMounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Card, Button, Popconfirm, message, Table, Modal, Tree } from 'ant-design-vue';
import { Plus, IconifyIcon } from '@vben/icons';
import { useVbenVxeGrid, type VxeTableGridColumns } from '#/adapter/vxe-table';
import { getCompanyList } from '#/api/system/company';
import { getDeptList, getDeptUsers, getEligibleDeptUsers, assignDeptUsers, removeDeptUsers } from '#/api/system/dept';
import type { SystemRolePermissionApi } from '#/api/system/role-permission';

// 현재 선택된 회사 ID 및 회사명
const selectedCompanyId = ref<string>('');
const selectedCompanyName = ref<string>('');

// 현재 선택된 부서 ID 및 부서명
const selectedDeptId = ref<string>('');
const selectedDeptName = ref<string>('');

// 부서 트리 상태
const deptTreeData = ref<any[]>([]);
const expandedKeys = ref<any[]>([]);

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

// 부서 데이터를 트리 형식으로 변환
function mapToTree(list: any[]): any[] {
  return list.map((item) => ({
    ...item,
    key: item.id,
    title: item.name,
    children: item.children ? mapToTree(item.children) : undefined,
  }));
}

// 모든 트리 키 수집 (전체 확장용)
function getAllKeys(list: any[]): any[] {
  const keys: any[] = [];
  function traverse(nodes: any[]) {
    for (const node of nodes) {
      keys.push(node.key);
      if (node.children && node.children.length > 0) {
        traverse(node.children);
      }
    }
  }
  traverse(list);
  return keys;
}

// 부서 트리 로드
async function fetchDeptTree() {
  if (!selectedCompanyId.value) {
    deptTreeData.value = [];
    expandedKeys.value = [];
    selectedDeptId.value = '';
    selectedDeptName.value = '';
    return;
  }
  try {
    const res = await getDeptList(selectedCompanyId.value);
    deptTreeData.value = mapToTree((res as any)?.result ?? res ?? []);
    expandedKeys.value = getAllKeys(deptTreeData.value);
    // 회사 변경 시 부서 선택값들 초기화
    selectedDeptId.value = '';
    selectedDeptName.value = '';
  } catch (error) {
    console.error(error);
    message.error('부서 목록 로드 실패');
  }
}

// 부서 선택 이벤트 핸들러
function onDeptSelect(keys: any[], info: any) {
  if (keys && keys.length > 0) {
    selectedDeptId.value = keys[0] as string;
    selectedDeptName.value = info.node.name || info.node.title;
  } else {
    selectedDeptId.value = '';
    selectedDeptName.value = '';
  }
}

// 지정 사용자 로드 (부서 기준)
async function fetchMappedUsers() {
  if (!selectedDeptId.value) {
    mappedUsers.value = [];
    return;
  }
  loadingMapped.value = true;
  try {
    const res = await getDeptUsers(selectedDeptId.value);
    mappedUsers.value = (res as any)?.result ?? res ?? [];
  } catch (error) {
    console.error(error);
    message.error('지정 사용자 목록 로드 실패');
  } finally {
    loadingMapped.value = false;
  }
}

// 추가 가능 사용자 로드 (부서 미지정 계정)
async function fetchEligibleUsers() {
  loadingEligible.value = true;
  try {
    const res = await getEligibleDeptUsers(selectedCompanyId.value);
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
    await removeDeptUsers([userId]);
    message.success('사용자가 부서에서 성공적으로 해제되었습니다.');
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
    await assignDeptUsers(selectedDeptId.value, selectedRowKeys.value as string[]);
    message.success('사용자가 부서 소속으로 지정되었습니다.');
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

// 회사 선택 변경 감시 -> 부서 리스트 갱신
watch(selectedCompanyId, () => {
  fetchDeptTree();
});

// 부서 선택 변경 감시 -> 사용자 리스트 갱신
watch(selectedDeptId, () => {
  fetchMappedUsers();
});

onMounted(() => {
  // 컴포넌트 마운트 시 동작
});
</script>

<template>
  <Page auto-content-height>
    <div class="grid grid-cols-12 gap-4 h-full">
      <!-- 1. 회사 목록 (좌측) -->
      <div class="col-span-12 md:col-span-3 h-full flex flex-col">
        <Card class="flex-1 flex flex-col h-full overflow-hidden" title="회사 목록" :body-style="{ flex: 1, padding: 0, overflow: 'hidden' }">
          <Grid class="h-full w-full" />
        </Card>
      </div>

      <!-- 2. 부서 조직도 (중앙) -->
      <div class="col-span-12 md:col-span-3 h-full flex flex-col">
        <Card 
          class="flex-1 flex flex-col h-full overflow-hidden" 
          :title="selectedCompanyName ? `[${selectedCompanyName}] 부서 조직도` : '부서 조직도'" 
          :body-style="{ flex: 1, padding: '16px', overflow: 'auto' }"
        >
          <template v-if="selectedCompanyId">
            <Tree
              v-if="deptTreeData.length > 0"
              v-model:expandedKeys="expandedKeys"
              :tree-data="deptTreeData"
              block-node
              @select="onDeptSelect"
            >
              <template #title="node">
                <div class="flex items-center gap-1.5 py-0.5">
                  <IconifyIcon icon="lucide:folder" class="size-4 text-primary" />
                  <span>{{ node.title }}</span>
                </div>
              </template>
            </Tree>
            <div v-else class="flex justify-center items-center h-40 text-gray-400">
              등록된 부서가 없습니다.
            </div>
          </template>
          <template v-else>
            <div class="flex justify-center items-center h-40 text-gray-400">
              좌측에서 회사를 선택해 주세요.
            </div>
          </template>
        </Card>
      </div>

      <!-- 3. 소속 사용자 관리 (우측) -->
      <div class="col-span-12 md:col-span-6 h-full flex flex-col">
        <Card
          class="flex-1 flex flex-col h-full overflow-hidden"
          :title="selectedDeptName ? `[${selectedDeptName}] 부서 소속 사용자 관리` : '소속 사용자 관리'"
          :body-style="{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden', padding: '12px 24px' }"
        >
          <template v-if="selectedDeptId">
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
                        title="해당 사용자의 부서 소속을 해제하시겠습니까?"
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
              <span class="text-lg font-medium">조직도에서 사용자를 설정할 부서를 클릭해 주세요.</span>
            </div>
          </template>
        </Card>
      </div>
    </div>

    <!-- 사용자 추가 지정 모달 -->
    <Modal
      v-model:open="isModalVisible"
      :title="selectedDeptName ? `[${selectedDeptName}] 부서에 사용자 추가 지정` : '부서에 사용자 추가 지정'"
      width="700px"
      @ok="handleAssignConfirm"
    >
      <div class="py-2">
        <div class="mb-2 text-gray-500 text-xs">
          * 현재 해당 회사의 부서가 지정되지 않았거나 소속 회사가 없는 사용자들만 목록에 노출됩니다.
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
