<script lang="ts" setup>
import type { HelpdeskMenu } from '#/api/helpdesk';

import { computed, onMounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Card,
  Checkbox,
  Empty,
  Form,
  FormItem,
  Input,
  InputNumber,
  message,
  Modal,
  Popconfirm,
  Select,
  Space,
  Switch,
  Table,
} from 'ant-design-vue';

import {
  createHelpdeskMenu,
  deleteHelpdeskMenu,
  getManageMenus,
  updateHelpdeskMenu,
} from '#/api/helpdesk';

/**
 * [메뉴 권한]
 *
 * 원본(MenuManagement.vue). 헬프데스크가 자체적으로 들고 있는 메뉴 테이블을 관리한다.
 * funeralv2 좌측 메뉴(scom.system_menus)와는 별개로, 헬프데스크 역할별 화면 권한의 기준이 된다.
 */

const loading = ref(false);
const saving = ref(false);
const menus = ref<HelpdeskMenu[]>([]);

const modalOpen = ref(false);
// 폼에서는 null 을 쓰지 않는다. AntD 입력 컴포넌트가 null 을 받지 않기 때문.
const editing = reactive<
  Omit<Partial<HelpdeskMenu>, 'icon' | 'parentId' | 'to'> & {
    icon?: string;
    parentId?: number;
    to?: string;
  }
>({});
const isEdit = computed(() => Boolean(editing.id));

const columns = [
  { dataIndex: 'label', key: 'label', title: '메뉴명' },
  { dataIndex: 'to', key: 'to', title: '경로', width: 220 },
  { dataIndex: 'icon', key: 'icon', title: '아이콘', width: 160 },
  { dataIndex: 'sortOrder', key: 'sortOrder', title: '순서', width: 80 },
  { dataIndex: 'visible', key: 'visible', title: '표시', width: 80 },
  { dataIndex: 'isActive', key: 'isActive', title: '사용', width: 80 },
  { key: 'action', title: '', width: 120 },
];

/** 평평한 목록을 parentId 기준 트리로 만든다. */
const treeRows = computed(() => {
  const map = new Map<number, any>();
  const roots: any[] = [];

  menus.value.forEach((m) => map.set(m.id, { ...m, children: [] }));
  menus.value.forEach((m) => {
    const node = map.get(m.id)!;
    const parent = m.parentId ? map.get(m.parentId) : undefined;
    if (parent) {
      parent.children.push(node);
    } else {
      roots.push(node);
    }
  });

  // 자식이 없으면 children 키를 지워 확장 아이콘이 뜨지 않게 한다.
  const prune = (nodes: any[]) => {
    nodes.forEach((n) => {
      if (n.children.length === 0) {
        delete n.children;
      } else {
        prune(n.children);
      }
    });
  };
  prune(roots);

  const bySort = (a: any, b: any) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0);
  roots.sort(bySort);
  return roots;
});

/** 상위 메뉴 셀렉트 옵션 */
const parentOptions = computed(() => [
  { label: '(최상위)', value: undefined },
  ...menus.value
    .filter((m) => m.id !== editing.id)
    .map((m) => ({ label: m.label, value: m.id })),
]);

async function loadData() {
  loading.value = true;
  try {
    menus.value = (await getManageMenus()) ?? [];
  } finally {
    loading.value = false;
  }
}

function openCreate(parent?: any) {
  Object.keys(editing).forEach((k) => delete (editing as any)[k]);
  Object.assign(editing, {
    icon: '',
    isActive: true,
    label: '',
    parentId: parent?.id ?? undefined,
    sortOrder: 0,
    to: '',
    useCreate: false,
    useDelete: false,
    useRead: true,
    useUpdate: false,
    visible: true,
  });
  modalOpen.value = true;
}

function openEdit(row: any) {
  Object.keys(editing).forEach((k) => delete (editing as any)[k]);
  Object.assign(editing, {
    ...row,
    icon: row.icon ?? '',
    parentId: row.parentId ?? undefined,
    to: row.to ?? '',
  });
  delete (editing as any).children;
  modalOpen.value = true;
}

async function onSave() {
  if (!editing.label?.trim()) {
    message.warning('메뉴명을 입력하세요.');
    return;
  }

  saving.value = true;
  try {
    await (isEdit.value
      ? updateHelpdeskMenu(editing.id!, { ...editing })
      : createHelpdeskMenu({ ...editing }));
    message.success(`메뉴를 ${isEdit.value ? '수정' : '등록'}했습니다.`);
    modalOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function onDelete(row: any) {
  await deleteHelpdeskMenu(row.id);
  message.success('메뉴를 삭제했습니다.');
  await loadData();
}

/** 표에서 바로 표시/사용 여부를 켜고 끈다. */
async function toggleFlag(row: any, key: 'isActive' | 'visible', value: boolean) {
  await updateHelpdeskMenu(row.id, { ...row, [key]: value });
  await loadData();
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height>
    <Alert
      class="mb-3"
      description="여기서 관리하는 메뉴는 헬프데스크 역할별 화면 권한(역할 관리)의 기준입니다. funeralv2 좌측 내비게이션 메뉴는 시스템 › 메뉴 관리에서 따로 관리합니다."
      message="헬프데스크 자체 메뉴"
      show-icon
      type="info"
    />

    <Card class="mb-3" size="small">
      <div class="flex justify-end">
        <Button type="primary" @click="openCreate()">최상위 메뉴 추가</Button>
      </div>
    </Card>

    <Card :body-style="{ padding: 0 }" size="small">
      <Table
        :columns="columns"
        :data-source="treeRows"
        :loading="loading"
        :pagination="false"
        row-key="id"
        size="small"
      >
        <template #emptyText>
          <Empty description="등록된 메뉴가 없습니다." />
        </template>

        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'visible'">
            <Switch
              :checked="record.visible"
              size="small"
              @change="(v: any) => toggleFlag(record, 'visible', v)"
            />
          </template>
          <template v-else-if="column.key === 'isActive'">
            <Switch
              :checked="record.isActive"
              size="small"
              @change="(v: any) => toggleFlag(record, 'isActive', v)"
            />
          </template>
          <template v-else-if="column.key === 'action'">
            <Space>
              <Button size="small" type="link" @click="openCreate(record)">
                하위
              </Button>
              <Button size="small" type="link" @click="openEdit(record)">
                수정
              </Button>
              <Popconfirm
                cancel-text="취소"
                ok-text="삭제"
                title="이 메뉴를 삭제할까요?"
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
      :title="isEdit ? '메뉴 수정' : '메뉴 등록'"
      cancel-text="취소"
      ok-text="저장"
      @ok="onSave"
    >
      <Form layout="vertical">
        <FormItem label="메뉴명" required>
          <Input v-model:value="editing.label" />
        </FormItem>
        <FormItem label="상위 메뉴">
          <Select v-model:value="editing.parentId" :options="parentOptions" />
        </FormItem>
        <FormItem label="경로">
          <Input v-model:value="editing.to" placeholder="/mng_request" />
        </FormItem>
        <FormItem label="아이콘">
          <Input v-model:value="editing.icon" placeholder="pi pi-fw pi-list" />
        </FormItem>
        <FormItem label="정렬 순서">
          <InputNumber v-model:value="editing.sortOrder" style="width: 100%" />
        </FormItem>
        <FormItem label="사용 가능한 권한">
          <Space direction="vertical">
            <Checkbox v-model:checked="editing.useRead">조회</Checkbox>
            <Checkbox v-model:checked="editing.useCreate">등록</Checkbox>
            <Checkbox v-model:checked="editing.useUpdate">수정</Checkbox>
            <Checkbox v-model:checked="editing.useDelete">삭제</Checkbox>
          </Space>
        </FormItem>
        <FormItem>
          <Space>
            <Checkbox v-model:checked="editing.visible">메뉴 표시</Checkbox>
            <Checkbox v-model:checked="editing.isActive">사용</Checkbox>
          </Space>
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>
