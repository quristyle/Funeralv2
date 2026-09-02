<script lang="ts" setup>
import type { Checklist } from '#/api/helpdesk';

import { computed, onMounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Checkbox,
  Col,
  Empty,
  Form,
  FormItem,
  Input,
  InputNumber,
  message,
  Modal,
  Popconfirm,
  Progress,
  Row,
  Segmented,
  Space,
  Spin,
  Statistic,
  Tag,
} from 'ant-design-vue';
import GridIconButton from '#/components/GridIconButton.vue';

import {
  createChecklist,
  deleteChecklist,
  getChecklists,
  updateChecklist,
} from '#/api/helpdesk';

import { formatDateTime } from '../shared/constants';

/**
 * [체크리스트]
 *
 * 원본(ChecklistManagement.vue). 카테고리별로 묶어 보여주고 체크 즉시 저장한다.
 */

const loading = ref(false);
const saving = ref(false);
const items = ref<Checklist[]>([]);
const keyword = ref('');
const filter = ref<'all' | 'done' | 'todo'>('all');

const FILTER_OPTIONS = [
  { label: '전체', value: 'all' },
  { label: '미완료', value: 'todo' },
  { label: '완료', value: 'done' },
];

// 분류 이름 일괄 변경. 원본의 rename 다이얼로그.
const renameOpen = ref(false);
const renameFrom = ref('');
const renameTo = ref('');

const modalOpen = ref(false);
const editing = reactive<Partial<Checklist>>({});
const isEdit = computed(() => Boolean(editing.id));

const filteredItems = computed(() => {
  const kw = keyword.value.trim().toLowerCase();
  return items.value.filter((item) => {
    if (filter.value === 'done' && !item.isChecked) return false;
    if (filter.value === 'todo' && item.isChecked) return false;
    if (!kw) return true;
    return `${item.itemName} ${item.note ?? ''} ${item.category ?? ''}`
      .toLowerCase()
      .includes(kw);
  });
});

/** 카테고리별로 묶는다. 원본의 rowGroup 표시와 같은 구성. */
const grouped = computed(() => {
  const map = new Map<string, Checklist[]>();
  filteredItems.value.forEach((item) => {
    const key = item.category ?? '(분류 없음)';
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(item);
  });

  return [...map.entries()]
    .map(([category, list]) => ({
      category,
      items: list.toSorted((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0)),
    }))
    .toSorted((a, b) => a.category.localeCompare(b.category));
});

const progress = computed(() => {
  const total = items.value.length;
  const done = items.value.filter((i) => i.isChecked).length;
  return {
    done,
    rate: total > 0 ? Math.round((done / total) * 100) : 0,
    total,
  };
});

async function loadData() {
  loading.value = true;
  try {
    items.value = (await getChecklists()) ?? [];
  } finally {
    loading.value = false;
  }
}

/** 체크박스를 누르면 바로 저장한다. */
async function toggleChecked(item: Checklist, checked: boolean) {
  await updateChecklist(item.id, {
    ...item,
    completedAt: checked ? new Date().toISOString() : null,
    isChecked: checked,
  });
  await loadData();
}

function openCreate(category?: string) {
  Object.keys(editing).forEach((k) => delete (editing as any)[k]);
  Object.assign(editing, {
    category: category ?? '',
    isChecked: false,
    itemName: '',
    note: '',
    sortOrder: 0,
  });
  modalOpen.value = true;
}

function openEdit(item: Checklist) {
  Object.keys(editing).forEach((k) => delete (editing as any)[k]);
  Object.assign(editing, { ...item });
  modalOpen.value = true;
}

async function onSave() {
  if (!editing.itemName?.trim()) {
    message.warning('항목명을 입력하세요.');
    return;
  }

  saving.value = true;
  try {
    await (isEdit.value
      ? updateChecklist(editing.id!, { ...editing })
      : createChecklist({ ...editing }));
    message.success(`항목을 ${isEdit.value ? '수정' : '등록'}했습니다.`);
    modalOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

function openRename(category: string) {
  renameFrom.value = category;
  renameTo.value = category;
  renameOpen.value = true;
}

/**
 * 한 분류에 속한 항목을 모두 새 이름으로 바꾼다.
 * 서버에 분류 전용 API 가 없어 항목을 하나씩 수정한다(원본과 같은 방식).
 */
async function applyRename() {
  const next = renameTo.value.trim();
  if (!next) {
    message.warning('새 분류명을 입력하세요.');
    return;
  }

  saving.value = true;
  try {
    const targets = items.value.filter((i) => (i.category ?? '') === renameFrom.value);
    await Promise.all(
      targets.map((item) => updateChecklist(item.id, { ...item, category: next })),
    );
    message.success(`${targets.length}개 항목의 분류를 바꿨습니다.`);
    renameOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function onDelete(item: Checklist) {
  await deleteChecklist(item.id);
  message.success('항목을 삭제했습니다.');
  await loadData();
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height>
    <Row :gutter="[12, 12]">
      <Col :lg="8" :xs="12">
        <Card size="small">
          <Statistic :value="progress.total" title="전체 항목" />
        </Card>
      </Col>
      <Col :lg="8" :xs="12">
        <Card size="small">
          <Statistic
            :value="progress.done"
            :value-style="{ color: '#22C55E' }"
            title="완료"
          />
        </Card>
      </Col>
      <Col :lg="8" :xs="24">
        <Card size="small">
          <div class="mb-1 text-xs text-muted-foreground">진행률</div>
          <Progress :percent="progress.rate" />
        </Card>
      </Col>
    </Row>

    <Card class="mt-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space wrap>
          <Input
            v-model:value="keyword"
            allow-clear
            placeholder="항목 · 분류 검색"
            style="width: 220px"
          />
          <Segmented v-model:value="filter" :options="FILTER_OPTIONS" />
        </Space>
        <GridIconButton
          v-perm:create
          icon="vxe-icon-add"
          title="항목 등록"
          @click="openCreate()"
        />
      </div>
    </Card>

    <Spin :spinning="loading">
      <Empty
        v-if="grouped.length === 0"
        class="mt-6"
        description="표시할 항목이 없습니다."
      />

      <Card
        v-for="group in grouped"
        :key="group.category"
        class="mt-3"
        size="small"
        :title="group.category"
      >
        <template #extra>
          <Space>
            <Button
              v-perm:update
              size="small"
              type="link"
              @click="openRename(group.category)"
            >
              분류명 변경
            </Button>
            <Button
              v-perm:create
              size="small"
              type="link"
              @click="openCreate(group.category)"
            >
              이 분류에 추가
            </Button>
          </Space>
        </template>

        <div
          v-for="item in group.items"
          :key="item.id"
          class="flex items-center gap-3 border-b border-border py-2 last:border-b-0"
        >
          <Checkbox
            :checked="item.isChecked"
            @change="(e: any) => toggleChecked(item, e.target.checked)"
          />
          <div class="min-w-0 flex-1">
            <div
              class="truncate"
              :class="item.isChecked ? 'line-through opacity-60' : ''"
            >
              {{ item.itemName }}
            </div>
            <div v-if="item.note" class="truncate text-xs text-muted-foreground">
              {{ item.note }}
            </div>
          </div>
          <Tag v-if="item.completedAt">
            {{ formatDateTime(item.completedAt) }}
          </Tag>
          <Space>
            <Button v-perm:update size="small" type="link" @click="openEdit(item)">
              수정
            </Button>
            <Popconfirm
              cancel-text="취소"
              ok-text="삭제"
              title="항목을 삭제할까요?"
              @confirm="onDelete(item)"
            >
              <Button danger size="small" type="link">삭제</Button>
            </Popconfirm>
          </Space>
        </div>
      </Card>
    </Spin>

    <Modal
      v-model:open="renameOpen"
      :confirm-loading="saving"
      cancel-text="취소"
      ok-text="변경"
      title="분류명 변경"
      @ok="applyRename"
    >
      <Form layout="vertical">
        <FormItem label="현재 분류명">
          <Input :value="renameFrom" disabled />
        </FormItem>
        <FormItem label="새 분류명" required>
          <Input v-model:value="renameTo" @press-enter="applyRename" />
        </FormItem>
      </Form>
      <span class="text-xs text-muted-foreground">
        이 분류에 속한 모든 항목의 분류명이 함께 바뀝니다.
      </span>
    </Modal>

    <Modal
      v-model:open="modalOpen"
      :confirm-loading="saving"
      :title="isEdit ? '항목 수정' : '항목 등록'"
      cancel-text="취소"
      ok-text="저장"
      @ok="onSave"
    >
      <Form layout="vertical">
        <FormItem label="분류">
          <Input v-model:value="editing.category" placeholder="01. 인프라/네트워크" />
        </FormItem>
        <FormItem label="항목명" required>
          <Input v-model:value="editing.itemName" />
        </FormItem>
        <FormItem label="비고">
          <Input v-model:value="editing.note" />
        </FormItem>
        <FormItem label="정렬 순서">
          <InputNumber v-model:value="editing.sortOrder" style="width: 100%" />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>
