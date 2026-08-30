<script lang="ts" setup>
import { ref, reactive, computed, watch, onMounted } from 'vue';
import { Button, Input, Select, Tag, Tooltip, message, Modal, Table, type TableColumnsType } from 'ant-design-vue';
import { Plus, IconifyIcon } from '@vben/icons';
import { getRoleUsers, getEligibleUsers, assignRoleUsers, removeRoleUser, type SystemRolePermissionApi } from '#/api/portal/system/role-permission';

const props = defineProps({
  roleId: {
    type: String,
    required: true,
  },
});

// ==========================================
// 1. 매핑된 사용자(지정 사용자) 상태 및 필터
// ==========================================
const mappedUsers = ref<SystemRolePermissionApi.RoleUser[]>([]);
const loadingMapped = ref(false);

const mappedFilter = reactive({
  loginId: '',
  userName: '',
  companyName: undefined as string | undefined,
  deptName: undefined as string | undefined,
});

function resetMappedFilter() {
  mappedFilter.loginId = '';
  mappedFilter.userName = '';
  mappedFilter.companyName = undefined;
  mappedFilter.deptName = undefined;
}

const mappedCompanyOptions = computed(() => {
  const hasEmpty = mappedUsers.value.some((u) => !u.companyName || u.companyName.trim() === '');
  const list = Array.from(
    new Set(
      mappedUsers.value
        .map((u) => u.companyName)
        .filter((val): val is string => Boolean(val && val.trim())),
    ),
  );
  const options = [{ label: '전체 회사', value: '' }];
  if (hasEmpty) {
    options.push({ label: '(소속 없음 / 빈 값)', value: '__EMPTY__' });
  }
  options.push(...list.map((c) => ({ label: c, value: c })));
  return options;
});

const mappedDeptOptions = computed(() => {
  const currentUsers = mappedUsers.value.filter((u) => {
    if (!mappedFilter.companyName || mappedFilter.companyName === '') return true;
    if (mappedFilter.companyName === '__EMPTY__') return !u.companyName || u.companyName.trim() === '';
    return u.companyName === mappedFilter.companyName;
  });

  const hasEmpty = currentUsers.some((u) => !u.deptName || u.deptName.trim() === '');
  const list = Array.from(
    new Set(
      currentUsers
        .map((u) => u.deptName)
        .filter((val): val is string => Boolean(val && val.trim())),
    ),
  );

  const options = [{ label: '전체 부서', value: '' }];
  if (hasEmpty) {
    options.push({ label: '(부서 없음 / 빈 값)', value: '__EMPTY__' });
  }
  options.push(...list.map((d) => ({ label: d, value: d })));
  return options;
});

const filteredMappedUsers = computed(() => {
  return mappedUsers.value.filter((user) => {
    if (
      mappedFilter.loginId.trim() &&
      !user.loginId?.toLowerCase().includes(mappedFilter.loginId.trim().toLowerCase())
    ) {
      return false;
    }
    if (
      mappedFilter.userName.trim() &&
      !user.userName?.toLowerCase().includes(mappedFilter.userName.trim().toLowerCase())
    ) {
      return false;
    }
    if (mappedFilter.companyName && mappedFilter.companyName !== '') {
      if (mappedFilter.companyName === '__EMPTY__') {
        if (user.companyName && user.companyName.trim() !== '') {
          return false;
        }
      } else if (user.companyName !== mappedFilter.companyName) {
        return false;
      }
    }
    if (mappedFilter.deptName && mappedFilter.deptName !== '') {
      if (mappedFilter.deptName === '__EMPTY__') {
        if (user.deptName && user.deptName.trim() !== '') {
          return false;
        }
      } else if (user.deptName !== mappedFilter.deptName) {
        return false;
      }
    }
    return true;
  });
});

// 2줄 헤더(1행: 컬럼명/정렬, 2행: 필터 행)를 위한 Grouped Columns 정의
const mappedColumns: TableColumnsType<SystemRolePermissionApi.RoleUser> = [
  {
    title: '사용자 ID',
    key: 'loginId_group',
    dataIndex: 'loginId',
    sorter: (a, b) => String(a.loginId ?? '').localeCompare(String(b.loginId ?? '')),
    align: 'center',
    children: [
      {
        title: 'loginId_filter',
        key: 'loginId_filter',
        dataIndex: 'loginId',
        width: 140,
        align: 'center',
      },
    ],
  },
  {
    title: '사용자명',
    key: 'userName_group',
    dataIndex: 'userName',
    sorter: (a, b) => String(a.userName ?? '').localeCompare(String(b.userName ?? '')),
    align: 'center',
    children: [
      {
        title: 'userName_filter',
        key: 'userName_filter',
        dataIndex: 'userName',
        width: 140,
        align: 'center',
      },
    ],
  },
  {
    title: '소속 회사',
    key: 'companyName_group',
    dataIndex: 'companyName',
    sorter: (a, b) => String(a.companyName ?? '').localeCompare(String(b.companyName ?? '')),
    align: 'center',
    children: [
      {
        title: 'companyName_filter',
        key: 'companyName_filter',
        dataIndex: 'companyName',
        width: 160,
        align: 'center',
      },
    ],
  },
  {
    title: '소속 부서',
    key: 'deptName_group',
    dataIndex: 'deptName',
    sorter: (a, b) => String(a.deptName ?? '').localeCompare(String(b.deptName ?? '')),
    align: 'center',
    children: [
      {
        title: 'deptName_filter',
        key: 'deptName_filter',
        dataIndex: 'deptName',
        width: 160,
        align: 'center',
      },
    ],
  },
  {
    title: '이메일',
    key: 'email_group',
    dataIndex: 'email',
    sorter: (a, b) => String(a.email ?? '').localeCompare(String(b.email ?? '')),
    align: 'center',
    children: [
      {
        title: 'email_filter',
        key: 'email_filter',
        dataIndex: 'email',
        width: 160,
        align: 'center',
      },
    ],
  },
  {
    title: '연락처',
    key: 'phone_group',
    dataIndex: 'phone',
    sorter: (a, b) => String(a.phone ?? '').localeCompare(String(b.phone ?? '')),
    align: 'center',
    children: [
      {
        title: 'phone_filter',
        key: 'phone_filter',
        dataIndex: 'phone',
        width: 130,
        align: 'center',
      },
    ],
  },
  {
    title: '작업',
    key: 'action_group',
    align: 'center',
    width: 90,
    children: [
      {
        title: 'action_filter',
        key: 'action_filter',
        width: 90,
        align: 'center',
      },
    ],
  },
];

// ==========================================
// 2. 추가 모달(사용자 검색 팝업) 상태 및 즉시 필터
// ==========================================
const isModalVisible = ref(false);
const eligibleUsers = ref<SystemRolePermissionApi.RoleUser[]>([]);
const loadingEligible = ref(false);
const selectedRowKeys = ref<(string | number)[]>([]);

const eligibleFilter = reactive({
  loginId: '',
  userName: '',
  companyName: undefined as string | undefined,
  deptName: undefined as string | undefined,
  roleName: undefined as string | undefined,
});

function resetEligibleFilter() {
  eligibleFilter.loginId = '';
  eligibleFilter.userName = '';
  eligibleFilter.companyName = undefined;
  eligibleFilter.deptName = undefined;
  eligibleFilter.roleName = undefined;
}

const eligibleCompanyOptions = computed(() => {
  const hasEmpty = eligibleUsers.value.some((u) => !u.companyName || u.companyName.trim() === '');
  const list = Array.from(
    new Set(
      eligibleUsers.value
        .map((u) => u.companyName)
        .filter((val): val is string => Boolean(val && val.trim())),
    ),
  );
  const options = [{ label: '전체 회사', value: '' }];
  if (hasEmpty) {
    options.push({ label: '(소속 없음 / 빈 값)', value: '__EMPTY__' });
  }
  options.push(...list.map((c) => ({ label: c, value: c })));
  return options;
});

const eligibleDeptOptions = computed(() => {
  const currentUsers = eligibleUsers.value.filter((u) => {
    if (!eligibleFilter.companyName || eligibleFilter.companyName === '') return true;
    if (eligibleFilter.companyName === '__EMPTY__') return !u.companyName || u.companyName.trim() === '';
    return u.companyName === eligibleFilter.companyName;
  });

  const hasEmpty = currentUsers.some((u) => !u.deptName || u.deptName.trim() === '');
  const list = Array.from(
    new Set(
      currentUsers
        .map((u) => u.deptName)
        .filter((val): val is string => Boolean(val && val.trim())),
    ),
  );

  const options = [{ label: '전체 부서', value: '' }];
  if (hasEmpty) {
    options.push({ label: '(부서 없음 / 빈 값)', value: '__EMPTY__' });
  }
  options.push(...list.map((d) => ({ label: d, value: d })));
  return options;
});

const eligibleRoleOptions = computed(() => {
  const hasEmpty = eligibleUsers.value.some((u) => !u.roles || u.roles.length === 0);
  const roleSet = new Set<string>();
  eligibleUsers.value.forEach((u) => {
    u.roles?.forEach((r) => {
      if (r && r.trim()) roleSet.add(r.trim());
    });
  });

  const options = [{ label: '전체 역할', value: '' }];
  if (hasEmpty) {
    options.push({ label: '(역할 없음 / 미지정)', value: '__EMPTY__' });
  }
  options.push(...Array.from(roleSet).map((r) => ({ label: r, value: r })));
  return options;
});

const filteredEligibleUsers = computed(() => {
  return eligibleUsers.value.filter((user) => {
    if (
      eligibleFilter.loginId.trim() &&
      !user.loginId?.toLowerCase().includes(eligibleFilter.loginId.trim().toLowerCase())
    ) {
      return false;
    }
    if (
      eligibleFilter.userName.trim() &&
      !user.userName?.toLowerCase().includes(eligibleFilter.userName.trim().toLowerCase())
    ) {
      return false;
    }
    if (eligibleFilter.companyName && eligibleFilter.companyName !== '') {
      if (eligibleFilter.companyName === '__EMPTY__') {
        if (user.companyName && user.companyName.trim() !== '') {
          return false;
        }
      } else if (user.companyName !== eligibleFilter.companyName) {
        return false;
      }
    }
    if (eligibleFilter.deptName && eligibleFilter.deptName !== '') {
      if (eligibleFilter.deptName === '__EMPTY__') {
        if (user.deptName && user.deptName.trim() !== '') {
          return false;
        }
      } else if (user.deptName !== eligibleFilter.deptName) {
        return false;
      }
    }
    if (eligibleFilter.roleName && eligibleFilter.roleName !== '') {
      if (eligibleFilter.roleName === '__EMPTY__') {
        if (user.roles && user.roles.length > 0) {
          return false;
        }
      } else if (
        !user.roles?.includes(eligibleFilter.roleName) &&
        !user.roleNames?.toLowerCase().includes(eligibleFilter.roleName.toLowerCase())
      ) {
        return false;
      }
    }
    return true;
  });
});

// 2줄 헤더(1행: 컬럼명/정렬, 2행: 필터 행)를 위한 Grouped Columns 정의 (모달)
const eligibleColumns: TableColumnsType<SystemRolePermissionApi.RoleUser> = [
  {
    title: '사용자 ID',
    key: 'loginId_group',
    dataIndex: 'loginId',
    sorter: (a, b) => String(a.loginId ?? '').localeCompare(String(b.loginId ?? '')),
    align: 'center',
    width: 120,
    children: [
      {
        title: 'loginId_filter',
        key: 'loginId_filter',
        dataIndex: 'loginId',
        width: 120,
        align: 'center',
      },
    ],
  },
  {
    title: '사용자명',
    key: 'userName_group',
    dataIndex: 'userName',
    sorter: (a, b) => String(a.userName ?? '').localeCompare(String(b.userName ?? '')),
    align: 'center',
    width: 120,
    children: [
      {
        title: 'userName_filter',
        key: 'userName_filter',
        dataIndex: 'userName',
        width: 120,
        align: 'center',
      },
    ],
  },
  {
    title: '소속 회사',
    key: 'companyName_group',
    dataIndex: 'companyName',
    sorter: (a, b) => String(a.companyName ?? '').localeCompare(String(b.companyName ?? '')),
    align: 'center',
    width: 140,
    children: [
      {
        title: 'companyName_filter',
        key: 'companyName_filter',
        dataIndex: 'companyName',
        width: 140,
        align: 'center',
      },
    ],
  },
  {
    title: '소속 부서',
    key: 'deptName_group',
    dataIndex: 'deptName',
    sorter: (a, b) => String(a.deptName ?? '').localeCompare(String(b.deptName ?? '')),
    align: 'center',
    width: 130,
    children: [
      {
        title: 'deptName_filter',
        key: 'deptName_filter',
        dataIndex: 'deptName',
        width: 130,
        align: 'center',
      },
    ],
  },
  {
    title: '할당된 역할',
    key: 'roles_group',
    dataIndex: 'roleNames',
    sorter: (a, b) => String(a.roleNames ?? '').localeCompare(String(b.roleNames ?? '')),
    align: 'center',
    children: [
      {
        title: 'roles_filter',
        key: 'roles_filter',
        dataIndex: 'roleNames',
        align: 'center',
      },
    ],
  },
];

// ==========================================
// 3. API 통신 및 이벤트 핸들러
// ==========================================
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

async function handleRemove(userId: string) {
  try {
    await removeRoleUser(props.roleId, userId);
    message.success('사용자 지정이 해제되었습니다.');
    fetchMappedUsers();
  } catch (error) {
    message.error('해제 실패');
  }
}

function openAssignModal() {
  selectedRowKeys.value = [];
  resetEligibleFilter();
  fetchEligibleUsers();
  isModalVisible.value = true;
}

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

function onSelectChange(keys: (string | number)[]) {
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
    <div class="flex justify-between items-center mb-3 pt-2">
      <div class="flex items-center gap-2">
        <span class="text-sm font-medium text-slate-600 dark:text-slate-300">
          지정 사용자 목록
          <span class="text-xs text-slate-400 font-normal">
            (총 {{ mappedUsers.length }}명<template v-if="filteredMappedUsers.length !== mappedUsers.length">, 검색됨 {{ filteredMappedUsers.length }}명</template>)
          </span>
        </span>
        <div class="flex items-center gap-1">
          <Tooltip title="새로고침 (재조회)">
            <Button size="small" :loading="loadingMapped" @click="fetchMappedUsers">
              <template #icon>
                <IconifyIcon icon="lucide:refresh-cw" class="size-3.5 inline-block" />
              </template>
            </Button>
          </Tooltip>
          <Tooltip title="필터 초기화">
            <Button size="small" @click="resetMappedFilter">
              <template #icon>
                <IconifyIcon icon="lucide:filter-x" class="size-3.5 inline-block" />
              </template>
            </Button>
          </Tooltip>
        </div>
      </div>

      <Button type="primary" @click="openAssignModal">
        <template #icon>
          <Plus class="size-4 mr-1 inline-block align-text-bottom" />
        </template>
        사용자 추가 지정
      </Button>
    </div>

    <!-- 지정 사용자 그리드 -->
    <div class="flex-1 overflow-auto border rounded-lg">
      <Table
        :columns="mappedColumns"
        :data-source="filteredMappedUsers"
        :loading="loadingMapped"
        :pagination="{ pageSize: 10, showSizeChanger: true, pageSizeOptions: ['10', '20', '50'] }"
        row-key="id"
        size="middle"
        bordered
        class="custom-table"
      >
        <!-- 2줄 헤더: 1행(컬럼 타이틀) / 2행(별도 필터 Row) -->
        <template #headerCell="{ column }">
          <!-- 1행: 컬럼 타이틀 -->
          <template v-if="column.key === 'loginId_group'">사용자 ID</template>
          <template v-else-if="column.key === 'userName_group'">사용자명</template>
          <template v-else-if="column.key === 'companyName_group'">소속 회사</template>
          <template v-else-if="column.key === 'deptName_group'">소속 부서</template>
          <template v-else-if="column.key === 'email_group'">이메일</template>
          <template v-else-if="column.key === 'phone_group'">연락처</template>
          <template v-else-if="column.key === 'action_group'">작업</template>

          <!-- 2행: 독립된 필터 입력 Row -->
          <template v-else-if="column.key === 'loginId_filter'">
            <Input
              v-model:value="mappedFilter.loginId"
              placeholder="ID 검색"
              size="small"
              allow-clear
              class="text-xs"
            />
          </template>
          <template v-else-if="column.key === 'userName_filter'">
            <Input
              v-model:value="mappedFilter.userName"
              placeholder="이름 검색"
              size="small"
              allow-clear
              class="text-xs"
            />
          </template>
          <template v-else-if="column.key === 'companyName_filter'">
            <Select
              v-model:value="mappedFilter.companyName"
              :options="mappedCompanyOptions"
              placeholder="전체"
              size="small"
              allow-clear
              class="w-full text-xs"
            />
          </template>
          <template v-else-if="column.key === 'deptName_filter'">
            <Select
              v-model:value="mappedFilter.deptName"
              :options="mappedDeptOptions"
              placeholder="전체"
              size="small"
              allow-clear
              class="w-full text-xs"
            />
          </template>
          <template v-else-if="column.key === 'email_filter' || column.key === 'phone_filter' || column.key === 'action_filter'">
            <span class="text-slate-300 text-xs">-</span>
          </template>
        </template>

        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'action_filter' || column.key === 'action_group' || column.key === 'action'">
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

    <!-- 사용자 추가 모달 (사용자 검색 팝업) -->
    <Modal
      v-model:open="isModalVisible"
      title="역할에 사용자 추가 지정"
      width="840px"
      @ok="handleAssignConfirm"
    >
      <div class="py-1">
        <!-- 팝업 내 필터 가이드 및 버튼 영역 -->
        <div class="flex justify-between items-center mb-2">
          <span class="text-xs text-slate-500">
            총 {{ eligibleUsers.length }}명 중 {{ filteredEligibleUsers.length }}명 표시됨
            <template v-if="selectedRowKeys.length > 0">
              (선택: <span class="text-blue-600 font-semibold">{{ selectedRowKeys.length }}</span>명)
            </template>
          </span>
          <div class="flex items-center gap-1">
            <Tooltip title="새로고침 (재조회)">
              <Button size="small" :loading="loadingEligible" @click="fetchEligibleUsers">
                <template #icon>
                  <IconifyIcon icon="lucide:refresh-cw" class="size-3.5 inline-block" />
                </template>
              </Button>
            </Tooltip>
            <Tooltip title="필터 초기화">
              <Button size="small" @click="resetEligibleFilter">
                <template #icon>
                  <IconifyIcon icon="lucide:filter-x" class="size-3.5 inline-block" />
                </template>
              </Button>
            </Tooltip>
          </div>
        </div>

        <Table
          :columns="eligibleColumns"
          :data-source="filteredEligibleUsers"
          :loading="loadingEligible"
          :row-selection="{ selectedRowKeys: selectedRowKeys, onChange: onSelectChange, columnWidth: 48 }"
          :pagination="false"
          :scroll="{ y: 380 }"
          :virtual="true"
          row-key="id"
          size="middle"
          bordered
          class="custom-modal-table"
        >
          <!-- 2줄 헤더: 1행(컬럼 타이틀) / 2행(별도 필터 Row) -->
          <template #headerCell="{ column }">
            <!-- 1행: 컬럼 타이틀 -->
            <template v-if="column.key === 'loginId_group'">사용자 ID</template>
            <template v-else-if="column.key === 'userName_group'">사용자명</template>
            <template v-else-if="column.key === 'companyName_group'">소속 회사</template>
            <template v-else-if="column.key === 'deptName_group'">소속 부서</template>
            <template v-else-if="column.key === 'roles_group'">할당된 역할</template>

            <!-- 2행: 독립된 필터 입력 Row -->
            <template v-else-if="column.key === 'loginId_filter'">
              <Input
                v-model:value="eligibleFilter.loginId"
                placeholder="ID 검색"
                size="small"
                allow-clear
                class="text-xs"
              />
            </template>
            <template v-else-if="column.key === 'userName_filter'">
              <Input
                v-model:value="eligibleFilter.userName"
                placeholder="이름 검색"
                size="small"
                allow-clear
                class="text-xs"
              />
            </template>
            <template v-else-if="column.key === 'companyName_filter'">
              <Select
                v-model:value="eligibleFilter.companyName"
                :options="eligibleCompanyOptions"
                placeholder="전체"
                size="small"
                allow-clear
                class="w-full text-xs"
              />
            </template>
            <template v-else-if="column.key === 'deptName_filter'">
              <Select
                v-model:value="eligibleFilter.deptName"
                :options="eligibleDeptOptions"
                placeholder="전체"
                size="small"
                allow-clear
                class="w-full text-xs"
              />
            </template>
            <template v-else-if="column.key === 'roles_filter'">
              <Select
                v-model:value="eligibleFilter.roleName"
                :options="eligibleRoleOptions"
                placeholder="전체"
                size="small"
                allow-clear
                class="w-full text-xs"
              />
            </template>
          </template>

          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'roles_filter' || column.key === 'roles_group' || column.dataIndex === 'roleNames'">
              <div class="flex flex-wrap gap-1 justify-center items-center">
                <template v-if="record.roles && record.roles.length > 0">
                  <Tag v-for="r in record.roles" :key="r" color="blue" class="m-0 text-xs">
                    {{ r }}
                  </Tag>
                </template>
                <span v-else class="text-slate-300 text-xs">-</span>
              </div>
            </template>
          </template>
        </Table>
      </div>
    </Modal>
  </div>
</template>

<style lang="css" scoped>
.custom-table :deep(.ant-table-pagination),
.custom-modal-table :deep(.ant-table-pagination) {
  margin: 12px 16px;
}

/* 모달 테이블 가로 스크롤 방지 */
.custom-modal-table :deep(.ant-table-body),
.custom-modal-table :deep(.ant-table-header),
.custom-modal-table :deep(.ant-table-content),
.custom-modal-table :deep(.ant-table-container) {
  overflow-x: hidden !important;
}

/* 헤더 1행(타이틀) 스타일 */
:deep(.ant-table-thead > tr:first-child > th) {
  text-align: center;
  font-weight: 600;
  background-color: #fafafa;
}

/* 헤더 2행(필터 row) 스타일 */
:deep(.ant-table-thead > tr:nth-child(2) > th) {
  padding: 4px 6px !important;
  background-color: #f8fafc;
}
</style>
