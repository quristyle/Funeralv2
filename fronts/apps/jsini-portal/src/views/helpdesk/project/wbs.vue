<script lang="ts" setup>
import type { Wbs, WbsTreeNode } from '#/api/helpdesk';

import { computed, reactive, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  DatePicker,
  Form,
  FormItem,
  Input,
  InputNumber,
  message,
  Modal,
  Popconfirm,
  Progress,
  Select,
  Space,
  Tag,
} from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { createWbs, deleteWbs, getWbsTree, updateWbs } from '#/api/helpdesk';
import BizSelect from '#/components/BizSelect.vue';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import { formatDate } from '../shared/constants';

/**
 * [WBS]
 *
 * 원본(Wbs.vue)의 TreeTable 을 트리 그리드로 옮겼다.
 * 서버가 이미 계층 구조로 내려주므로 그 형태를 표가 쓰는 모양으로만 바꾼다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * 트리는 `treeConfig.transform: false` 다 — 서버가 준 `children` 을 그대로 쓴다.
 * 그래서 머리글 필터줄은 **최상위 작업을 기준으로** 걸린다(하위는 부모를 따라온다).
 * ------------------------------------------------------------
 */

const loading = ref(false);
const saving = ref(false);
const selectedProjectId = ref<number | undefined>();
const treeNodes = ref<WbsTreeNode[]>([]);

const STATUS_OPTIONS = [
  { label: 'Pending', value: 'Pending' },
  { label: 'In Progress', value: 'In Progress' },
  { label: 'Completed', value: 'Completed' },
  { label: 'On Hold', value: 'On Hold' },
];

const PRIORITY_OPTIONS = [
  { label: 'Low', value: 'Low' },
  { label: 'Medium', value: 'Medium' },
  { label: 'High', value: 'High' },
];

const [Grid, gridApi] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      // 펼침 화살표가 붙는 칸.
      { field: 'wbsName', minWidth: 240, title: '작업명', treeNode: true },
      { field: 'wbsCode', title: '코드', width: 150 },
      {
        field: 'status',
        params: { filterOptions: STATUS_OPTIONS },
        slots: { default: 'status' },
        title: '상태',
        width: 110,
      },
      {
        field: 'progress',
        // 막대 그림 칸이라 걸러 봐야 읽히지 않는다.
        params: { filter: false },
        slots: { default: 'progress' },
        title: '진행률',
        width: 130,
      },
      {
        field: 'planStart',
        params: { filterText: (row: any) => formatDate(row.planStart) },
        slots: { default: 'planStart' },
        title: '계획 시작',
        width: 110,
      },
      {
        field: 'planEnd',
        params: { filterText: (row: any) => formatDate(row.planEnd) },
        slots: { default: 'planEnd' },
        title: '계획 종료',
        width: 110,
      },
      { field: 'action', slots: { default: 'action' }, title: '', width: 150 },
    ],
    data: [],
    emptyText: '등록된 WBS 항목이 없습니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 고른 프로젝트의 트리를 다시 읽는 함수를 준다.
    gridFeatures: { onRefresh: () => loadWbs() },
    height: 'auto',
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'key' },
    treeConfig: {
      childrenField: 'children',
      expandAll: true,
      rowField: 'key',
      // 서버가 이미 계층으로 내려준다. 평면 데이터를 조립할 일이 없다.
      transform: false,
    },
  } as any,
});

/** 서버 트리({key,data,children})를 표가 쓰는 평평한 행 + children 구조로 바꾼다. */
function toRows(nodes: WbsTreeNode[]): any[] {
  return nodes.map((node) => ({
    ...node.data,
    children: node.children?.length ? toRows(node.children) : undefined,
    key: node.key,
  }));
}

const rows = computed(() => toRows(treeNodes.value));

const modalOpen = ref(false);
// DatePicker 는 null 을 받지 않으므로 폼 상태에서는 undefined 로만 다룬다.
const editing = reactive<
  Omit<Partial<Wbs>, 'planEnd' | 'planStart'> & {
    parentWbsId?: null | number;
    planEnd?: string;
    planStart?: string;
  }
>({});
const isEdit = computed(() => Boolean(editing.wbsRid));

async function loadWbs() {
  if (!selectedProjectId.value) {
    treeNodes.value = [];
    return;
  }

  loading.value = true;
  try {
    treeNodes.value = (await getWbsTree(selectedProjectId.value)) ?? [];
  } finally {
    loading.value = false;
  }
}

function openCreate(parent?: any) {
  Object.keys(editing).forEach((k) => delete (editing as any)[k]);
  Object.assign(editing, {
    parentWbsId: parent?.wbsRid ?? null,
    priority: 'Medium',
    progress: 0,
    projectId: selectedProjectId.value,
    status: 'Pending',
    wbsCode: '',
    wbsName: '',
  });
  modalOpen.value = true;
}

function openEdit(row: any) {
  Object.keys(editing).forEach((k) => delete (editing as any)[k]);
  Object.assign(editing, {
    parentWbsId: row.parentWbsId,
    planEnd: row.planEnd ? String(row.planEnd).slice(0, 10) : undefined,
    planStart: row.planStart ? String(row.planStart).slice(0, 10) : undefined,
    priority: row.priority,
    progress: row.progress ?? 0,
    projectId: row.projectId,
    status: row.status,
    wbsCode: row.wbsCode,
    wbsName: row.wbsName,
    wbsRid: row.wbsRid,
  });
  modalOpen.value = true;
}

async function onSave() {
  if (!editing.wbsName?.trim()) {
    message.warning('작업명을 입력하세요.');
    return;
  }

  saving.value = true;
  try {
    await (isEdit.value
      ? updateWbs(editing.wbsRid!, { ...editing })
      : createWbs({ ...editing, projectId: selectedProjectId.value! }));
    message.success(`WBS 항목을 ${isEdit.value ? '수정' : '등록'}했습니다.`);
    modalOpen.value = false;
    await loadWbs();
  } finally {
    saving.value = false;
  }
}

async function onDelete(row: any) {
  await deleteWbs(row.wbsRid);
  message.success('WBS 항목을 삭제했습니다.');
  await loadWbs();
}

/** 진행률에 따른 색. 완료는 초록, 지연 위험은 주황. */
function progressStatus(row: any) {
  if ((row.progress ?? 0) >= 100) return 'success';
  return 'normal';
}

watch(selectedProjectId, loadWbs);
watch(loading, (value) => gridApi.setLoading(value));
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <HelpdeskAccountNotice />

    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <!-- BizSelect 는 너비 100% 라 바깥에서 폭을 정한다 -->
        <div style="width: 240px">
          <BizSelect
            v-model:value="selectedProjectId"
            auto-select-first
            option-filter-prop="label"
            placeholder="프로젝트"
            show-search
            type="helpdesk_project"
          />
        </div>
        <Button
          :disabled="!selectedProjectId"
          type="primary"
          @click="openCreate()"
        >
          최상위 작업 추가
        </Button>
      </div>
    </Card>

    <!-- 표를 카드로 감싸지 않는다 — 감싸면 page-fill-last 가 표에 높이를 못 준다. -->
    <Grid :table-data="rows">
      <template #status="{ row }">
        <Tag>{{ row.status }}</Tag>
      </template>
      <template #progress="{ row }">
        <Progress
          :percent="row.progress ?? 0"
          :status="progressStatus(row)"
          size="small"
        />
      </template>
      <template #planStart="{ row }">{{ formatDate(row.planStart) }}</template>
      <template #planEnd="{ row }">{{ formatDate(row.planEnd) }}</template>
      <template #action="{ row }">
        <Space>
          <Button v-perm:create size="small" type="link" @click="openCreate(row)">
            하위
          </Button>
          <Button v-perm:update size="small" type="link" @click="openEdit(row)">
            수정
          </Button>
          <Popconfirm
            cancel-text="취소"
            ok-text="삭제"
            title="이 작업을 삭제할까요?"
            @confirm="onDelete(row)"
          >
            <Button danger size="small" type="link">삭제</Button>
          </Popconfirm>
        </Space>
      </template>
    </Grid>

    <Modal
      v-model:open="modalOpen"
      :confirm-loading="saving"
      :title="isEdit ? 'WBS 수정' : 'WBS 등록'"
      cancel-text="취소"
      ok-text="저장"
      @ok="onSave"
    >
      <Form layout="vertical">
        <FormItem label="작업명" required>
          <Input v-model:value="editing.wbsName" />
        </FormItem>
        <FormItem label="코드">
          <Input v-model:value="editing.wbsCode" />
        </FormItem>
        <FormItem label="상태">
          <Select v-model:value="editing.status" :options="STATUS_OPTIONS" />
        </FormItem>
        <FormItem label="우선순위">
          <Select v-model:value="editing.priority" :options="PRIORITY_OPTIONS" />
        </FormItem>
        <FormItem label="진행률(%)">
          <InputNumber
            v-model:value="editing.progress"
            :max="100"
            :min="0"
            style="width: 100%"
          />
        </FormItem>
        <FormItem label="계획 시작일">
          <DatePicker
            v-model:value="editing.planStart"
            style="width: 100%"
            value-format="YYYY-MM-DD"
          />
        </FormItem>
        <FormItem label="계획 종료일">
          <DatePicker
            v-model:value="editing.planEnd"
            style="width: 100%"
            value-format="YYYY-MM-DD"
          />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>
