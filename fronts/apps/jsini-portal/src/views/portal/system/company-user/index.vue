<script lang="ts" setup>
import { ref, watch, onMounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Avatar, Card, Button, message, Table, Modal, Tree } from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import { useVbenVxeGrid, type VxeTableGridColumns } from '#/adapter/vxe-table';
import GridIconButton from '#/components/GridIconButton.vue';
import { getCompanyList } from '#/api/portal/system/company';
import { getDeptList, getDeptUsers, getEligibleDeptUsers, assignDeptUsers, removeDeptUsers } from '#/api/portal/system/dept';
import type { SystemRolePermissionApi } from '#/api/portal/system/role-permission';

/**
 * [회사 · 부서 사용자 배정]
 *
 * ------------------------------------------------------------
 * [2026-08-30] 팝업(사용자 추가 지정) 안의 ant-design-vue `<Table>` 을
 * `useVbenVxeGrid` 로 옮겼다. 정렬·필터는 공통 레이어
 * (`adapter/vxe-grid-features.ts`)가 붙이므로 이 화면에는 그 코드가 없다.
 *
 * 가운데 두 표(소속 · 미소속)는 **일부러 antd `<Table>` 로 남겼다.** 행을 끌어다
 * 놓는 것이 이 화면의 조작 방식인데, 그것은 antd 의 `custom-row` 로 행 엘리먼트에
 * `draggable` 을 직접 붙여서 되는 일이다.
 * ------------------------------------------------------------
 */

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

// ─────────────────────────────────────────────────────────────
// 미소속 사용자 (어느 회사에도 속하지 않은 계정)
//
// 소속 해제는 부서와 **회사를 함께** 비운다(DepartmentService.RemoveUsersFromDeptAsync).
// 그래서 오른쪽에서 끌어낸 사용자가 그대로 이 목록에 들어온다 — 두 목록이 왕복한다.
//
// 회사 조회 인자를 넘기지 않으면 서버가 '부서 없음 + 회사 없음' 만 준다.
// (인자를 넘기면 그 회사 소속이지만 부서만 없는 사용자까지 섞인다 — 그건 아래 모달이 쓴다)
// ─────────────────────────────────────────────────────────────
const unassignedUsers = ref<SystemRolePermissionApi.RoleUser[]>([]);
const loadingUnassigned = ref<boolean>(false);

/** 드래그 중인 사용자와 출발지. 놓을 곳을 강조할 때도 쓴다. */
const dragging = ref<null | { from: 'mapped' | 'unassigned'; id: string }>(null);
/** 지금 마우스가 올라와 있는 놓을 자리 */
const dropTarget = ref<'' | 'mapped' | 'unassigned'>('');
/** 등록·해제가 진행 중인지. 연달아 놓는 것을 막는다. */
const moving = ref<boolean>(false);

// 미소속 목록도 소속 목록과 같은 정보를 보여준다.
// 좁은 칸이라 사진 + 이름/아이디를 한 칸에 묶고, 이메일·연락처를 그 아래에 붙인다.
// 마지막 칸은 [배정] 버튼 — 드래그가 안 되는 터치(모바일)의 대체 경로다.
const unassignedColumns = [
  { title: '사용자', key: 'user' },
  { title: '연락', key: 'contact' },
  { title: '', key: 'action', width: 44 },
];

/**
 * 목록에 쓸 프로필 사진 주소.
 *
 * 원본(`/api/file/download/...`)을 그대로 받으면 목록에서 무겁다. 포털이 이미 쓰는 규칙대로
 * 썸네일 경로로 바꿔 준다(layouts/basic.vue 의 avatar 계산과 같다).
 * 사진이 없으면 빈 값을 주고 화면은 이름 첫 글자를 보여준다.
 */
function avatarUrl(record: any): string {
  const raw = record?.avatar ?? '';
  if (!raw) return '';
  return raw.includes('/api/file/download/')
    ? raw.replace('/api/file/download/', '/api/file/thumbnail/')
    : raw;
}

/** 사진이 없을 때 쓸 글자. 이름 첫 글자, 없으면 아이디 첫 글자. */
function avatarText(record: any): string {
  const name = String(record?.userName || record?.loginId || '?');
  return name.slice(0, 1).toUpperCase();
}

/** 어느 회사에도 속하지 않은 사용자 목록 */
async function fetchUnassignedUsers() {
  loadingUnassigned.value = true;
  try {
    const res = await getEligibleDeptUsers();
    unassignedUsers.value = (res as any)?.result ?? res ?? [];
  } catch (error) {
    console.error(error);
    message.error('미소속 사용자 목록 로드 실패');
  } finally {
    loadingUnassigned.value = false;
  }
}

function onUserDragStart(
  e: DragEvent,
  from: 'mapped' | 'unassigned',
  id: string,
) {
  if (!e.dataTransfer) return;
  // org-chart.vue 와 같은 방식으로 싣는다.
  e.dataTransfer.setData('text/plain', JSON.stringify({ from, id }));
  e.dataTransfer.effectAllowed = 'move';
  dragging.value = { from, id };
}

function onUserDragEnd() {
  dragging.value = null;
  dropTarget.value = '';
}

/**
 * 놓을 수 있는 자리인가.
 *
 * 같은 목록 안에서 놓는 것은 아무 의미가 없으므로 받지 않는다.
 * 소속으로 넣는 쪽은 부서가 선택되어 있어야 한다 — 어디에 넣을지 정해지지 않았으면 안 된다.
 */
function canDropOn(zone: 'mapped' | 'unassigned') {
  if (!dragging.value || moving.value) return false;
  if (dragging.value.from === zone) return false;
  if (zone === 'mapped' && !selectedDeptId.value) return false;
  return true;
}

function onZoneDragOver(e: DragEvent, zone: 'mapped' | 'unassigned') {
  if (!canDropOn(zone)) return;
  // preventDefault 를 불러야 브라우저가 놓기를 허용한다.
  e.preventDefault();
  dropTarget.value = zone;
}

/**
 * 강조를 끈다.
 *
 * `dragleave` 는 카드 **안쪽 요소 사이를 옮겨갈 때도** 발생한다. 그때마다 껐다 켜면
 * 테두리가 깜빡인다. 그래서 정말 카드 밖으로 나간 경우(옮겨간 곳이 이 카드 밖)만 끈다.
 */
function onZoneDragLeave(e: DragEvent, zone: 'mapped' | 'unassigned') {
  if (dropTarget.value !== zone) return;

  const card = e.currentTarget as HTMLElement | null;
  const next = e.relatedTarget as Node | null;
  if (card && next && card.contains(next)) return;

  dropTarget.value = '';
}

/**
 * 사용자 한 명을 선택된 부서 소속으로 등록한다.
 * 드롭(onDropToMapped)과 행의 [배정] 버튼이 **같은 함수**를 쓴다 —
 * 버튼은 드래그가 동작하지 않는 터치(모바일)의 대체 경로다.
 */
async function assignOne(id: string) {
  if (!selectedDeptId.value || moving.value) return;
  moving.value = true;

  try {
    await assignDeptUsers(selectedDeptId.value, [id]);
    message.success(`[${selectedDeptName.value}] 부서 소속으로 등록했습니다.`);
    await Promise.all([fetchMappedUsers(), fetchUnassignedUsers()]);
  } catch (error) {
    console.error(error);
    message.error('소속 등록에 실패했습니다.');
  } finally {
    moving.value = false;
  }
}

/**
 * 사용자 한 명의 부서와 회사 소속을 함께 해제한다.
 * 드롭(onDropToUnassigned)과 행의 [해제] 버튼이 같은 함수를 쓴다.
 */
async function unassignOne(id: string) {
  if (moving.value) return;
  moving.value = true;

  try {
    await removeDeptUsers([id]);
    message.success('소속을 해제했습니다.');
    await Promise.all([fetchMappedUsers(), fetchUnassignedUsers()]);
  } catch (error) {
    console.error(error);
    message.error('소속 해제에 실패했습니다.');
  } finally {
    moving.value = false;
  }
}

/** 미소속 → 소속: 선택된 부서로 등록한다. */
async function onDropToMapped(e: DragEvent) {
  if (!canDropOn('mapped')) return;
  e.preventDefault();

  const id = dragging.value!.id;
  dragging.value = null;
  dropTarget.value = '';
  await assignOne(id);
}

/** 소속 → 미소속: 부서와 회사 소속을 함께 해제한다. */
async function onDropToUnassigned(e: DragEvent) {
  if (!canDropOn('unassigned')) return;
  e.preventDefault();

  const id = dragging.value!.id;
  dragging.value = null;
  dropTarget.value = '';
  await unassignOne(id);
}

/**
 * 표의 각 행을 끌 수 있게 만든다.
 * ant-design-vue Table 은 `customRow` 로 행 엘리먼트에 속성·이벤트를 붙인다.
 */
function rowDragProps(from: 'mapped' | 'unassigned') {
  return (record: any) => ({
    class: 'cursor-move',
    draggable: true,
    onDragend: onUserDragEnd,
    onDragstart: (e: DragEvent) => onUserDragStart(e, from, record.id),
  });
}

// 매핑된 사용자 테이블 컬럼 정의
//
// '작업'(해제) 컬럼은 한 번 없앴다가 아이콘 버튼 하나로 되살렸다.
// 드래그로 해제할 수 있어 데스크톱에서는 필요 없지만, HTML5 drag&drop 은
// 모바일 브라우저에서 동작하지 않아 터치에서는 이 버튼이 유일한 길이다.
const mappedColumns = [
  { title: '사용자', key: 'user' },
  { title: '연락', key: 'contact' },
  { title: '', key: 'action', width: 44 },
];

// 추가 모달 그리드 (사용자 추가 지정)
//
// 팝업 안이라 `page-fill-last` 가 없다 — 높이를 숫자로 준다.
// 체크박스 칸이 antd 의 `:row-selection` 자리다. 고른 행은 `getCheckboxRecords()` 로 읽는다.
const [EligibleGrid, eligibleApi] = useVbenVxeGrid({
  gridOptions: {
    checkboxConfig: { highlight: true, keyField: 'id' },
    columns: [
      { type: 'checkbox', width: 44 },
      { field: 'loginId', minWidth: 130, title: '사용자 ID' },
      { field: 'userName', minWidth: 120, title: '사용자명' },
      { field: 'email', minWidth: 180, title: '이메일' },
    ],
    emptyText: '추가할 수 있는 사용자가 없습니다.',
    height: 380,
    // 전량 조회다. 켜 두면 vxe 가 응답을 `{result,page}` 로 읽어 배열이 통째로 빠진다.
    pagerConfig: { enabled: false },
    proxyConfig: {
      ajax: {
        query: async () => {
          try {
            const res = await getEligibleDeptUsers(selectedCompanyId.value);
            eligibleUsers.value = (res as any)?.result ?? res ?? [];
          } catch (error) {
            console.error(error);
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

// 회사 목록 그리드 컬럼 설정 (좌측)
//
// 사용자 수를 함께 보여 준다. 이 화면은 "사람이 어디에 붙어 있는지" 를 다루는데,
// 회사를 하나하나 눌러 봐야 인원을 알 수 있으면 14개 회사를 전부 훑게 된다.
// 부서 수도 같이 보여 주어 "부서는 있는데 사람이 없는 회사" 를 바로 알 수 있게 한다.
const columns: VxeTableGridColumns = [
  { field: 'name', title: '회사명', minWidth: 130 },
  {
    field: 'deptCount',
    title: '부서',
    width: 60,
    align: 'right',
    // 0 을 '0' 으로 그대로 두면 눈이 숫자를 하나하나 읽는다. 없으면 비워 둔다.
    formatter: ({ cellValue }: any) => (cellValue > 0 ? String(cellValue) : '-'),
  },
  {
    field: 'userCount',
    title: '사용자',
    width: 70,
    align: 'right',
    formatter: ({ cellValue }: any) => (cellValue > 0 ? String(cellValue) : '-'),
  },
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
//
// 부서명 옆에 소속 인원을 붙인다. 어느 부서에 사람이 있는지 눌러 보지 않고 알아야
// "어디로 옮길지" 를 판단할 수 있다.
//
// 하위 부서가 있으면 **직접 인원과 전체 인원을 함께** 보여 준다(`3 / 12`).
// 접어 둔 상태에서 상위 부서만 보면 그 아래 인원이 안 보여서, 사람이 없는 조직으로
// 착각하게 되기 때문이다.
function mapToTree(list: any[]): any[] {
  return list.map((item) => {
    const own = Number(item.userCount ?? 0);
    const total = Number(item.totalUserCount ?? own);
    const hasChildren = !!item.children?.length;

    return {
      ...item,
      key: item.id,
      title: item.name,
      /** 트리에 그릴 인원 표시. 없으면 빈 문자열이라 화면이 조용하다. */
      countLabel:
        hasChildren && total !== own
          ? `${own} / ${total}`
          : own > 0
            ? String(own)
            : '',
      children: item.children ? mapToTree(item.children) : undefined,
    };
  });
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

// 소속 해제는 미소속 목록으로 끌어다 놓는 것으로 한다(onDropToUnassigned).
// 표의 '해제' 버튼과 그 처리는 없앴다 — 같은 일을 두 곳에서 하지 않는다.

// 추가 지정 모달 열기
function openAssignModal() {
  isModalVisible.value = true;
  // 팝업이 뜬 뒤에 조회해야 그리드가 자리를 잡은 상태에서 그려진다.
  setTimeout(() => eligibleApi.query(), 0);
}

// 사용자 추가 지정 확인
async function handleAssignConfirm() {
  const selected = eligibleApi.grid?.getCheckboxRecords() ?? [];
  if (selected.length === 0) {
    message.warning('추가할 사용자를 선택해 주세요.');
    return;
  }

  try {
    await assignDeptUsers(
      selectedDeptId.value,
      selected.map((row: any) => row.id as string),
    );
    message.success('사용자가 부서 소속으로 지정되었습니다.');
    isModalVisible.value = false;
    await Promise.all([fetchMappedUsers(), fetchUnassignedUsers()]);
  } catch (error) {
    console.error(error);
    message.error('사용자 지정 추가 실패');
  }
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
  // 미소속 목록은 회사·부서 선택과 무관하다. 화면을 열 때 한 번 받아 둔다.
  fetchUnassignedUsers();
});
</script>

<template>
  <Page auto-content-height>
    <!--
      `grid-rows-[minmax(0,1fr)]` — 행 트랙을 컨테이너 높이에 못 박는다.
      안 그러면 트랙이 `auto`(내용 크기)라, `h-full` 자식 안의 vxe 그리드가
      잰 높이를 다시 내용으로 만들며 표가 무한히 길어진다
      (역할 관리 화면에서 실측·수정한 것과 같은 구조다).
    -->
    <div class="grid grid-cols-12 gap-4 h-full md:grid-rows-[minmax(0,1fr)]">
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
                  <!--
                    소속 인원. 하위 부서가 있으면 '직접 / 전체' 로 보여 준다.
                    인원이 없는 부서는 아무것도 붙이지 않아 목록이 조용하다.
                  -->
                  <span
                    v-if="node.countLabel"
                    class="text-muted-foreground ml-auto shrink-0 text-xs"
                  >
                    {{ node.countLabel }}명
                  </span>
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

      <!--
        3. 소속 사용자 (우측)

        오른쪽 두 칸은 서로 끌어다 놓을 수 있다.
          미소속 → 소속 : 선택한 부서로 등록 (회사도 함께 지정된다)
          소속 → 미소속 : 부서와 회사 소속을 함께 해제
        받을 수 있는 자리만 강조된다(canDropOn).
      -->
      <div class="col-span-12 md:col-span-3 h-full flex flex-col">
        <Card
          class="flex-1 flex flex-col h-full overflow-hidden transition-colors"
          :class="
            dropTarget === 'mapped'
              ? 'ring-2 ring-primary'
              : dragging?.from === 'unassigned' && selectedDeptId
                ? 'ring-1 ring-primary/40'
                : ''
          "
          :title="selectedDeptName ? `[${selectedDeptName}] 소속 사용자` : '소속 사용자'"
          :body-style="{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden', padding: '12px 16px' }"
          @dragover="onZoneDragOver($event, 'mapped')"
          @dragleave="onZoneDragLeave($event, 'mapped')"
          @drop="onDropToMapped"
        >
          <template v-if="selectedDeptId">
            <div class="flex-1 flex flex-col overflow-hidden h-full">
              <!-- 상단 툴바 -->
              <div class="flex justify-between items-center mb-3 pt-1 gap-2">
                <span class="text-xs text-gray-400">
                  오른쪽에서 끌어다 놓거나 행의 버튼으로 등록·해제합니다.
                </span>
                <GridIconButton
                  icon="vxe-icon-add-user"
                  title="사용자 추가 지정"
                  @click="openAssignModal"
                />
              </div>

              <!-- 지정 사용자 그리드 -->
              <div class="flex-1 overflow-auto border rounded-lg">
                <Table
                  :columns="mappedColumns"
                  :custom-row="rowDragProps('mapped')"
                  :data-source="mappedUsers"
                  :loading="loadingMapped"
                  :pagination="{ pageSize: 10 }"
                  row-key="id"
                  size="small"
                  class="custom-table"
                >
                  <template #bodyCell="{ column, record }">
                    <!-- 사진 + 이름 + 아이디 -->
                    <template v-if="column.key === 'user'">
                      <div class="flex items-center gap-2 min-w-0">
                        <Avatar :size="28" :src="avatarUrl(record) || undefined">
                          {{ avatarText(record) }}
                        </Avatar>
                        <div class="min-w-0">
                          <div class="text-sm truncate" :title="record.userName">
                            {{ record.userName || '-' }}
                          </div>
                          <div class="text-[11px] text-gray-400 truncate" :title="record.loginId">
                            {{ record.loginId }}
                          </div>
                        </div>
                      </div>
                    </template>
                    <!-- 이메일 + 연락처 -->
                    <template v-else-if="column.key === 'contact'">
                      <div class="min-w-0">
                        <div class="text-xs truncate" :title="record.email">
                          {{ record.email || '-' }}
                        </div>
                        <div class="text-[11px] text-gray-400 truncate" :title="record.phone">
                          {{ record.phone || '-' }}
                        </div>
                      </div>
                    </template>
                    <!-- 해제 — 드래그가 안 되는 터치의 대체 경로. 드롭과 같은 API 를 부른다. -->
                    <template v-else-if="column.key === 'action'">
                      <Button
                        danger
                        size="small"
                        type="text"
                        :disabled="moving"
                        title="소속 해제"
                        @click="unassignOne(record.id)"
                      >
                        <template #icon>
                          <IconifyIcon class="size-4" icon="lucide:user-minus" />
                        </template>
                      </Button>
                    </template>
                  </template>
                  <template #emptyText>
                    <div class="py-8 text-center text-gray-400 text-sm">
                      소속 사용자가 없습니다.
                    </div>
                  </template>
                </Table>
              </div>
            </div>
          </template>

          <template v-else>
            <div class="flex-1 flex flex-col justify-center items-center text-gray-400 py-20 text-center px-4">
              <span class="text-sm font-medium">
                조직도에서 사용자를 설정할 부서를 클릭해 주세요.
              </span>
            </div>
          </template>
        </Card>
      </div>

      <!-- 4. 미소속 사용자 (맨 오른쪽) -->
      <div class="col-span-12 md:col-span-3 h-full flex flex-col">
        <Card
          class="flex-1 flex flex-col h-full overflow-hidden transition-colors"
          :class="
            dropTarget === 'unassigned'
              ? 'ring-2 ring-primary'
              : dragging?.from === 'mapped'
                ? 'ring-1 ring-primary/40'
                : ''
          "
          title="미소속 사용자"
          :body-style="{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden', padding: '12px 16px' }"
          @dragover="onZoneDragOver($event, 'unassigned')"
          @dragleave="onZoneDragLeave($event, 'unassigned')"
          @drop="onDropToUnassigned"
        >
          <template #extra>
            <GridIconButton
              :loading="loadingUnassigned"
              icon="vxe-icon-repeat"
              title="미소속 사용자 새로고침"
              @click="fetchUnassignedUsers"
            />
          </template>

          <div class="flex-1 flex flex-col overflow-hidden h-full">
            <div class="mb-3 pt-1 text-xs text-gray-400">
              어느 회사에도 소속되지 않은 사용자입니다.
              왼쪽으로 끌어다 놓거나 행의 버튼을 누르면 부서에 등록됩니다.
            </div>

            <div class="flex-1 overflow-auto border rounded-lg">
              <Table
                :columns="unassignedColumns"
                :custom-row="rowDragProps('unassigned')"
                :data-source="unassignedUsers"
                :loading="loadingUnassigned"
                :pagination="{ pageSize: 10 }"
                row-key="id"
                size="small"
                class="custom-table"
              >
                <template #bodyCell="{ column, record }">
                  <!-- 소속 목록과 같은 구성이다. 두 표를 나란히 보므로 모양이 같아야 한다. -->
                  <template v-if="column.key === 'user'">
                    <div class="flex items-center gap-2 min-w-0">
                      <Avatar :size="28" :src="avatarUrl(record) || undefined">
                        {{ avatarText(record) }}
                      </Avatar>
                      <div class="min-w-0">
                        <div class="text-sm truncate" :title="record.userName">
                          {{ record.userName || '-' }}
                        </div>
                        <div class="text-[11px] text-gray-400 truncate" :title="record.loginId">
                          {{ record.loginId }}
                        </div>
                      </div>
                    </div>
                  </template>
                  <template v-else-if="column.key === 'contact'">
                    <div class="min-w-0">
                      <div class="text-xs truncate" :title="record.email">
                        {{ record.email || '-' }}
                      </div>
                      <div class="text-[11px] text-gray-400 truncate" :title="record.phone">
                        {{ record.phone || '-' }}
                      </div>
                    </div>
                  </template>
                  <!-- 배정 — 드래그가 안 되는 터치의 대체 경로. 부서를 먼저 골라야 켜진다. -->
                  <template v-else-if="column.key === 'action'">
                    <Button
                      size="small"
                      type="text"
                      :disabled="!selectedDeptId || moving"
                      :title="
                        selectedDeptId
                          ? `[${selectedDeptName}] 부서로 배정`
                          : '조직도에서 부서를 먼저 선택해 주세요'
                      "
                      @click="assignOne(record.id)"
                    >
                      <template #icon>
                        <IconifyIcon class="size-4" icon="lucide:user-plus" />
                      </template>
                    </Button>
                  </template>
                </template>
                <template #emptyText>
                  <div class="py-8 text-center text-gray-400 text-sm">
                    미소속 사용자가 없습니다.
                  </div>
                </template>
              </Table>
            </div>
          </div>
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
        <EligibleGrid />
      </div>
    </Modal>
  </Page>
</template>

<style lang="css" scoped>
.custom-table :deep(.ant-table-pagination) {
  margin: 16px 24px;
}
</style>
