<script lang="ts" setup>
/**
 * [역할 - 지정 사용자 탭]
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 *
 * 이 화면이 "머리글 아래 필터 전용 행" 의 본이 된 화면이다. 그 모양을
 * 공통 레이어(`adapter/vxe-grid-features.ts`)로 올렸으므로, 여기서는
 * **그리는 코드가 전부 없어졌다.**
 *
 * 지워진 것 (전부 공통 레이어가 대신한다):
 *   · 2줄 머리글을 만들기 위한 `children` 묶음 컬럼 정의 (두 벌, 200여 줄)
 *   · `#headerCell` 안에서 칸마다 입력기를 그리던 분기
 *   · 필터 상태(`mappedFilter` · `eligibleFilter`)와 그것으로 거르던 `computed`
 *   · 컬럼마다 손으로 적던 `sorter`
 *
 * 남긴 것: 회사·부서는 **고르는 칸**으로 두었다. 값이 정해져 있고 목록에서
 * 고르는 편이 빠르다. 목록은 자료가 오면 만들어 넣는다(`applyFilterOptions`).
 * ------------------------------------------------------------
 */
import type { SystemRolePermissionApi } from '#/api/portal/system/role-permission';

import { computed, onMounted, ref, watch } from 'vue';

import { IconifyIcon, Plus } from '@vben/icons';

import { Button, message, Modal, Tag, Tooltip } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import {
  assignRoleUsers,
  getEligibleUsers,
  getRoleUsers,
  removeRoleUser,
} from '#/api/portal/system/role-permission';

const props = defineProps({
  roleId: {
    required: true,
    type: String,
  },
});

type RoleUser = SystemRolePermissionApi.RoleUser;

const mappedUsers = ref<RoleUser[]>([]);
const eligibleUsers = ref<RoleUser[]>([]);
const isModalVisible = ref(false);

/**
 * 고르는 칸의 목록을 자료에서 만든다.
 *
 * 빈 값이 섞여 있으면 '(없음)' 을 앞에 둔다 — 공통 레이어의 고르는 칸은
 * `null` · `''` 를 같은 것으로 보므로 값 `''` 하나로 둘 다 잡힌다.
 */
function optionsOf(rows: RoleUser[], field: 'companyName' | 'deptName') {
  const values = rows.map((r) => r[field]);
  const named = [
    ...new Set(values.filter((v): v is string => Boolean(v && v.trim()))),
  ].sort((a, b) => a.localeCompare(b));

  const options = named.map((v) => ({ label: v, value: v }));
  if (values.some((v) => !v || !String(v).trim())) {
    options.unshift({ label: '(없음)', value: '' });
  }
  return options;
}

/** 두 그리드가 같은 칸 구성을 쓴다. 모달 쪽에만 '역할' 이 하나 더 붙는다. */
function buildColumns(rows: RoleUser[], forModal: boolean) {
  const columns: any[] = [
    { field: 'loginId', minWidth: 130, title: '사용자 ID' },
    { field: 'userName', minWidth: 120, title: '사용자명' },
    {
      field: 'companyName',
      minWidth: 150,
      params: { filterOptions: optionsOf(rows, 'companyName') },
      title: '소속 회사',
    },
    {
      field: 'deptName',
      minWidth: 150,
      params: { filterOptions: optionsOf(rows, 'deptName') },
      title: '소속 부서',
    },
  ];

  if (forModal) {
    columns.push({
      field: 'roleNames',
      minWidth: 200,
      // 역할은 여러 개다. 고르는 칸으로 두면 한 번에 하나만 되므로 글자로 찾게 둔다.
      params: { filterText: (row: RoleUser) => (row.roles ?? []).join(' ') },
      slots: { default: 'roles' },
      title: '할당된 역할',
    });
  } else {
    columns.push(
      { field: 'email', minWidth: 170, title: '이메일' },
      { field: 'phone', minWidth: 130, title: '연락처' },
      {
        field: 'action',
        fixed: 'right',
        slots: { default: 'action' },
        title: '작업',
        width: 90,
      },
    );
  }

  return columns;
}

// ==========================================
// 지정 사용자 목록
// ==========================================

const [Grid, gridApi] = useVbenVxeGrid<RoleUser>({
  gridOptions: {
    columns: buildColumns([], false),
    emptyText: '지정된 사용자가 없습니다.',
    height: 'auto',
    // 전량 조회다. 켜 두면 vxe 가 응답을 `{result,page}` 로 읽어 배열이 통째로 빠진다.
    pagerConfig: { enabled: false },
    proxyConfig: {
      ajax: {
        query: async () => {
          if (!props.roleId) return [];
          try {
            const res = await getRoleUsers(props.roleId);
            mappedUsers.value = (res as any)?.result ?? res ?? [];
          } catch {
            message.error('지정 사용자 목록 로드 실패');
            mappedUsers.value = [];
          }
          return mappedUsers.value;
        },
      },
    },
    rowConfig: { keyField: 'id' },
  },
});

// ==========================================
// 사용자 추가 모달
// ==========================================

const [EligibleGrid, eligibleApi] = useVbenVxeGrid<RoleUser>({
  gridOptions: {
    checkboxConfig: { highlight: true, keyField: 'id' },
    columns: [
      { type: 'checkbox', width: 44 },
      ...buildColumns([], true),
    ],
    emptyText: '추가할 수 있는 사용자가 없습니다.',
    // 팝업 안이라 `page-fill-last` 가 없다. 높이를 숫자로 준다.
    height: 380,
    pagerConfig: { enabled: false },
    proxyConfig: {
      ajax: {
        query: async () => {
          if (!props.roleId) return [];
          try {
            const res = await getEligibleUsers(props.roleId);
            eligibleUsers.value = (res as any)?.result ?? res ?? [];
          } catch {
            message.error('추가 가능 사용자 목록 로드 실패');
            eligibleUsers.value = [];
          }
          return eligibleUsers.value;
        },
      },
    },
    rowConfig: { keyField: 'id' },
  },
});

const mappedCount = computed(() => mappedUsers.value.length);

/**
 * 고르는 칸의 목록은 자료를 봐야 만들 수 있다.
 *
 * **조회 함수 안에서 컬럼을 갈아끼우면 안 된다** — 조회가 끝나기 전에 설정이
 * 바뀌면서 방금 받은 행이 지워진다(실제로 그래서 목록이 비어 보였다).
 * 자료가 자리를 잡은 뒤에 따로 넣는다.
 */
watch(mappedUsers, (rows) => {
  gridApi.setGridOptions({ columns: buildColumns(rows, false) });
});

watch(eligibleUsers, (rows) => {
  eligibleApi.setGridOptions({
    columns: [{ type: 'checkbox', width: 44 }, ...buildColumns(rows, true)],
  });
});

async function handleRemove(userId: string) {
  try {
    await removeRoleUser(props.roleId, userId);
    message.success('사용자 지정이 해제되었습니다.');
    gridApi.query();
  } catch {
    message.error('해제 실패');
  }
}

function openAssignModal() {
  isModalVisible.value = true;
  // 팝업이 뜬 뒤에 조회해야 그리드가 자리를 잡은 상태에서 그려진다.
  setTimeout(() => eligibleApi.query(), 0);
}

async function handleAssignConfirm() {
  const selected = eligibleApi.grid?.getCheckboxRecords() ?? [];
  if (selected.length === 0) {
    message.warning('추가할 사용자를 선택해 주세요.');
    return;
  }

  try {
    await assignRoleUsers(
      props.roleId,
      selected.map((row: RoleUser) => row.id as string),
    );
    message.success('사용자가 지정되었습니다.');
    isModalVisible.value = false;
    gridApi.query();
  } catch {
    message.error('사용자 지정 추가 실패');
  }
}

watch(
  () => props.roleId,
  () => gridApi.query(),
);

onMounted(() => gridApi.query());
</script>

<template>
  <div class="flex h-full flex-1 flex-col overflow-hidden">
    <!-- 상단 툴바 -->
    <div class="mb-3 flex items-center justify-between pt-2">
      <div class="flex items-center gap-2">
        <span class="text-sm font-medium text-slate-600 dark:text-slate-300">
          지정 사용자 목록
          <span class="text-xs font-normal text-slate-400">
            (총 {{ mappedCount }}명)
          </span>
        </span>
        <Tooltip title="새로고침 (재조회)">
          <Button size="small" @click="gridApi.query()">
            <template #icon>
              <IconifyIcon
                icon="lucide:refresh-cw"
                class="inline-block size-3.5"
              />
            </template>
          </Button>
        </Tooltip>
      </div>

      <Button type="primary" @click="openAssignModal">
        <template #icon>
          <Plus class="mr-1 inline-block size-4 align-text-bottom" />
        </template>
        사용자 추가 지정
      </Button>
    </div>

    <!-- 지정 사용자 그리드 -->
    <!--
      `h-auto` 를 반드시 준다.

      프레임워크가 그리드 바깥에 `h-full`(높이 100%) 을 붙이는데, 이 자리는
      **위에 도구 줄이 하나 있어서** 100% 가 곧 남은 높이가 아니다. 그대로 두면
      그리드가 도구 줄 높이만큼 넘치고, vxe 가 그 넘친 크기를 다시 재면서
      **표가 몇 번에 걸쳐 길어진다**(실측 815 → 827 → 831px, 왼쪽 그리드는 807px).

      클래스 병합이 tailwind-merge 라 `h-auto` 가 `h-full` 을 밀어낸다.
      그러면 `flex-1` 이 남은 높이를 정확히 준다.
    -->
    <Grid class="h-auto min-h-0 flex-1">
      <template #action="{ row }">
        <Button danger size="small" type="link" @click="handleRemove(row.id)">
          <template #icon>
            <IconifyIcon class="mr-0.5 inline size-3.5" icon="lucide:trash-2" />
          </template>
          해제
        </Button>
      </template>
    </Grid>

    <!-- 사용자 추가 모달 (사용자 검색 팝업) -->
    <Modal
      v-model:open="isModalVisible"
      title="역할에 사용자 추가 지정"
      width="900px"
      @ok="handleAssignConfirm"
    >
      <div class="py-1">
        <EligibleGrid>
          <template #roles="{ row }">
            <div class="flex flex-wrap items-center justify-center gap-1">
              <template v-if="row.roles && row.roles.length > 0">
                <Tag v-for="r in row.roles" :key="r" color="blue" class="m-0">
                  {{ r }}
                </Tag>
              </template>
              <span v-else class="text-slate-300">-</span>
            </div>
          </template>
        </EligibleGrid>
      </div>
    </Modal>
  </div>
</template>
