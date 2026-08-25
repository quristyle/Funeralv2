<script lang="ts" setup>
import type { Wbs, WbsTreeNode } from '#/api/helpdesk';

import { computed, reactive, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  DatePicker,
  Empty,
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
  Table,
  Tag,
} from 'ant-design-vue';

import { createWbs, deleteWbs, getWbsTree, updateWbs } from '#/api/helpdesk';
import BizSelect from '#/components/BizSelect.vue';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import { formatDate } from '../shared/constants';

/**
 * [WBS]
 *
 * 원본(Wbs.vue)의 TreeTable 을 AntD Table 의 트리 모드로 옮겼다.
 * 서버가 이미 계층 구조로 내려주므로 그 형태를 표가 쓰는 모양으로만 바꾼다.
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

const columns = [
  { dataIndex: 'wbsName', key: 'wbsName', title: '작업명' },
  { dataIndex: 'wbsCode', key: 'wbsCode', title: '코드', width: 150 },
  { dataIndex: 'status', key: 'status', title: '상태', width: 110 },
  { dataIndex: 'progress', key: 'progress', title: '진행률', width: 130 },
  { dataIndex: 'planStart', key: 'planStart', title: '계획 시작', width: 110 },
  { dataIndex: 'planEnd', key: 'planEnd', title: '계획 종료', width: 110 },
  { key: 'action', title: '', width: 150 },
];

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
</script>

<template>
  <Page auto-content-height>
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

    <Card :body-style="{ padding: 0 }" size="small">
      <Table
        :columns="columns"
        :data-source="rows"
        :loading="loading"
        :pagination="false"
        :scroll="{ x: 1000 }"
        row-key="key"
        size="small"
      >
        <template #emptyText>
          <Empty description="등록된 WBS 항목이 없습니다." />
        </template>

        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <Tag>{{ record.status }}</Tag>
          </template>
          <template v-else-if="column.key === 'progress'">
            <Progress
              :percent="record.progress ?? 0"
              :status="progressStatus(record)"
              size="small"
            />
          </template>
          <template v-else-if="column.key === 'planStart'">
            {{ formatDate(record.planStart) }}
          </template>
          <template v-else-if="column.key === 'planEnd'">
            {{ formatDate(record.planEnd) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <Space>
              <Button
                v-perm:create
                size="small"
                type="link"
                @click="openCreate(record)"
              >
                하위
              </Button>
              <Button
                v-perm:update
                size="small"
                type="link"
                @click="openEdit(record)"
              >
                수정
              </Button>
              <Popconfirm
                cancel-text="취소"
                ok-text="삭제"
                title="이 작업을 삭제할까요?"
                @confirm="onDelete(record)"
              >
                <Button danger size="small" type="link">삭제</Button>
              </Popconfirm>
            </Space>
          </template>
        </template>
      </Table>
    </Card>

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
