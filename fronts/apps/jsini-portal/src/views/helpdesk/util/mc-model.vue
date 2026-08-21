<script lang="ts" setup>
import type { McModel, ParseItem, TagItem } from '#/api/helpdesk';

import { computed, onMounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Col,
  Empty,
  Form,
  FormItem,
  Input,
  InputNumber,
  List,
  ListItem,
  message,
  Modal,
  Popconfirm,
  Row,
  Space,
  Spin,
  Table,
  TabPane,
  Tabs,
} from 'ant-design-vue';

import {
  createAckFind,
  createMcModel,
  createParseItem,
  createTagItem,
  deleteAckFind,
  deleteMcModel,
  deleteParseItem,
  deleteTagItem,
  getMcModelsFull,
  updateMcModel,
  updateParseItem,
  updateTagItem,
} from '#/api/helpdesk';

/**
 * [MC 모델 관리]
 *
 * 원본(utils/McModelManager.vue + BinaryParser.vue 의 모델 편집 탭).
 * 모델 → 파싱 항목 → 태그 항목의 3단 구조를 편집한다.
 */

const loading = ref(false);
const saving = ref(false);
const models = ref<any[]>([]);
const selectedModelId = ref<number | undefined>();

const selectedModel = computed(() =>
  models.value.find((m) => m.id === selectedModelId.value),
);
const parseItems = computed<ParseItem[]>(
  () => selectedModel.value?.parseItems ?? [],
);
const ackFinds = computed<any[]>(() => selectedModel.value?.ackFinds ?? []);

// ── 모델 ──────────────────────────────────────────────────
const modelModalOpen = ref(false);
const modelForm = reactive<Partial<McModel>>({});
const isEditModel = computed(() => Boolean(modelForm.id));

// ── 파싱 항목 ─────────────────────────────────────────────
const itemModalOpen = ref(false);
const itemForm = reactive<Partial<ParseItem>>({});
const isEditItem = computed(() => Boolean(itemForm.id));

// ── 태그 항목 ─────────────────────────────────────────────
const tagModalOpen = ref(false);
const tagForm = reactive<Partial<TagItem> & { parseItemId?: number }>({});
const isEditTag = computed(() => Boolean(tagForm.id));

const itemColumns = [
  { dataIndex: 'itemName', key: 'itemName', title: '항목명' },
  { dataIndex: 'offset', key: 'offset', title: '오프셋', width: 90 },
  { dataIndex: 'length', key: 'length', title: '길이', width: 80 },
  { dataIndex: 'dataType', key: 'dataType', title: '타입', width: 110 },
  { dataIndex: 'sortOrder', key: 'sortOrder', title: '순서', width: 80 },
  { key: 'action', title: '', width: 160 },
];

const ackColumns = [
  { dataIndex: 'pattern', key: 'pattern', title: '패턴' },
  { key: 'action', title: '', width: 90 },
];

async function loadData() {
  loading.value = true;
  try {
    models.value = (await getMcModelsFull()) ?? [];
    if (!selectedModelId.value) selectedModelId.value = models.value[0]?.id;
  } finally {
    loading.value = false;
  }
}

// ── 모델 CRUD ─────────────────────────────────────────────
function openCreateModel() {
  Object.keys(modelForm).forEach((k) => delete (modelForm as any)[k]);
  Object.assign(modelForm, { description: '', modelName: '' });
  modelModalOpen.value = true;
}

function openEditModel(model: any) {
  Object.keys(modelForm).forEach((k) => delete (modelForm as any)[k]);
  Object.assign(modelForm, {
    description: model.description,
    id: model.id,
    modelName: model.modelName,
  });
  modelModalOpen.value = true;
}

async function saveModel() {
  if (!modelForm.modelName?.trim()) {
    message.warning('모델명을 입력하세요.');
    return;
  }

  saving.value = true;
  try {
    await (isEditModel.value
      ? updateMcModel(modelForm.id!, { ...modelForm })
      : createMcModel({ ...modelForm }));
    message.success(`모델을 ${isEditModel.value ? '수정' : '등록'}했습니다.`);
    modelModalOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function removeModel(model: any) {
  await deleteMcModel(model.id);
  message.success('모델을 삭제했습니다.');
  if (selectedModelId.value === model.id) selectedModelId.value = undefined;
  await loadData();
}

// ── 파싱 항목 CRUD ────────────────────────────────────────
function openCreateItem() {
  Object.keys(itemForm).forEach((k) => delete (itemForm as any)[k]);
  Object.assign(itemForm, {
    dataType: 'HEX',
    itemName: '',
    length: 1,
    offset: 0,
    sortOrder: parseItems.value.length + 1,
  });
  itemModalOpen.value = true;
}

function openEditItem(item: any) {
  Object.keys(itemForm).forEach((k) => delete (itemForm as any)[k]);
  Object.assign(itemForm, { ...item });
  itemModalOpen.value = true;
}

async function saveItem() {
  if (!selectedModelId.value) return;
  if (!itemForm.itemName?.trim()) {
    message.warning('항목명을 입력하세요.');
    return;
  }

  saving.value = true;
  try {
    await (isEditItem.value
      ? updateParseItem(itemForm.id!, { ...itemForm })
      : createParseItem(selectedModelId.value, { ...itemForm }));
    message.success(`항목을 ${isEditItem.value ? '수정' : '등록'}했습니다.`);
    itemModalOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function removeItem(item: any) {
  await deleteParseItem(item.id);
  message.success('항목을 삭제했습니다.');
  await loadData();
}

// ── 태그 항목 CRUD ────────────────────────────────────────
function openCreateTag(parseItem: any) {
  Object.keys(tagForm).forEach((k) => delete (tagForm as any)[k]);
  Object.assign(tagForm, {
    parseItemId: parseItem.id,
    tagName: '',
    tagValue: '',
  });
  tagModalOpen.value = true;
}

function openEditTag(tag: any) {
  Object.keys(tagForm).forEach((k) => delete (tagForm as any)[k]);
  Object.assign(tagForm, { ...tag });
  tagModalOpen.value = true;
}

async function saveTag() {
  if (!tagForm.tagName?.trim()) {
    message.warning('태그명을 입력하세요.');
    return;
  }

  saving.value = true;
  try {
    await (isEditTag.value
      ? updateTagItem(tagForm.id!, { ...tagForm })
      : createTagItem(tagForm.parseItemId!, { ...tagForm }));
    message.success(`태그를 ${isEditTag.value ? '수정' : '등록'}했습니다.`);
    tagModalOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function removeTag(tag: any) {
  await deleteTagItem(tag.id);
  message.success('태그를 삭제했습니다.');
  await loadData();
}

// ── ACK 규칙 ──────────────────────────────────────────────
const ackPattern = ref('');

async function addAck() {
  if (!selectedModelId.value || !ackPattern.value.trim()) return;

  await createAckFind(selectedModelId.value, { pattern: ackPattern.value });
  ackPattern.value = '';
  message.success('ACK 규칙을 추가했습니다.');
  await loadData();
}

async function removeAck(ack: any) {
  await deleteAckFind(ack.id);
  message.success('ACK 규칙을 삭제했습니다.');
  await loadData();
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height>
    <Row :gutter="[12, 12]">
      <Col :lg="6" :xs="24">
        <Card :body-style="{ padding: 0 }" size="small" title="모델">
          <template #extra>
            <Button size="small" type="primary" @click="openCreateModel">
              추가
            </Button>
          </template>

          <Spin :spinning="loading">
            <List
              :data-source="models"
              :locale="{ emptyText: '등록된 모델이 없습니다.' }"
              size="small"
            >
              <template #renderItem="{ item }">
                <ListItem
                  class="cursor-pointer px-3"
                  :class="item.id === selectedModelId ? 'bg-accent' : ''"
                  @click="selectedModelId = item.id"
                >
                  <div class="min-w-0 flex-1">
                    <div class="truncate font-medium">{{ item.modelName }}</div>
                    <div class="truncate text-xs text-muted-foreground">
                      {{ item.description }}
                    </div>
                  </div>
                  <Space @click.stop>
                    <Button size="small" type="link" @click="openEditModel(item)">
                      수정
                    </Button>
                    <Popconfirm
                      cancel-text="취소"
                      ok-text="삭제"
                      title="모델을 삭제할까요?"
                      @confirm="removeModel(item)"
                    >
                      <Button danger size="small" type="link">삭제</Button>
                    </Popconfirm>
                  </Space>
                </ListItem>
              </template>
            </List>
          </Spin>
        </Card>
      </Col>

      <Col :lg="18" :xs="24">
        <Card size="small">
          <Empty v-if="!selectedModel" description="모델을 선택하세요." />

          <Tabs v-else>
            <TabPane key="items" tab="파싱 항목">
              <div class="mb-2 flex justify-end">
                <Button type="primary" @click="openCreateItem">항목 추가</Button>
              </div>

              <Table
                :columns="itemColumns"
                :data-source="parseItems"
                :pagination="false"
                row-key="id"
                size="small"
              >
                <template #emptyText>
                  <Empty description="파싱 항목이 없습니다." />
                </template>

                <template #expandedRowRender="{ record }">
                  <div class="mb-2 flex items-center justify-between">
                    <span class="text-xs text-muted-foreground">태그 항목</span>
                    <Button size="small" @click="openCreateTag(record)">
                      태그 추가
                    </Button>
                  </div>
                  <div
                    v-for="tag in record.tagItems ?? []"
                    :key="tag.id"
                    class="flex items-center gap-2 border-b border-border py-1 text-xs last:border-b-0"
                  >
                    <span class="w-40 font-medium">{{ tag.tagName }}</span>
                    <span class="flex-1">{{ tag.tagValue }}</span>
                    <Button size="small" type="link" @click="openEditTag(tag)">
                      수정
                    </Button>
                    <Popconfirm
                      cancel-text="취소"
                      ok-text="삭제"
                      title="태그를 삭제할까요?"
                      @confirm="removeTag(tag)"
                    >
                      <Button danger size="small" type="link">삭제</Button>
                    </Popconfirm>
                  </div>
                </template>

                <template #bodyCell="{ column, record }">
                  <template v-if="column.key === 'action'">
                    <Space>
                      <Button size="small" type="link" @click="openEditItem(record)">
                        수정
                      </Button>
                      <Popconfirm
                        cancel-text="취소"
                        ok-text="삭제"
                        title="항목을 삭제할까요?"
                        @confirm="removeItem(record)"
                      >
                        <Button danger size="small" type="link">삭제</Button>
                      </Popconfirm>
                    </Space>
                  </template>
                </template>
              </Table>
            </TabPane>

            <TabPane key="ack" tab="ACK 규칙">
              <Space class="mb-2">
                <Input
                  v-model:value="ackPattern"
                  placeholder="패턴"
                  style="width: 260px"
                  @press-enter="addAck"
                />
                <Button type="primary" @click="addAck">추가</Button>
              </Space>

              <Table
                :columns="ackColumns"
                :data-source="ackFinds"
                :pagination="false"
                row-key="id"
                size="small"
              >
                <template #emptyText>
                  <Empty description="ACK 규칙이 없습니다." />
                </template>
                <template #bodyCell="{ column, record }">
                  <template v-if="column.key === 'action'">
                    <Popconfirm
                      cancel-text="취소"
                      ok-text="삭제"
                      title="규칙을 삭제할까요?"
                      @confirm="removeAck(record)"
                    >
                      <Button danger size="small" type="link">삭제</Button>
                    </Popconfirm>
                  </template>
                </template>
              </Table>
            </TabPane>
          </Tabs>
        </Card>
      </Col>
    </Row>

    <Modal
      v-model:open="modelModalOpen"
      :confirm-loading="saving"
      :title="isEditModel ? '모델 수정' : '모델 등록'"
      cancel-text="취소"
      ok-text="저장"
      @ok="saveModel"
    >
      <Form layout="vertical">
        <FormItem label="모델명" required>
          <Input v-model:value="modelForm.modelName" />
        </FormItem>
        <FormItem label="설명">
          <Input v-model:value="modelForm.description" />
        </FormItem>
      </Form>
    </Modal>

    <Modal
      v-model:open="itemModalOpen"
      :confirm-loading="saving"
      :title="isEditItem ? '파싱 항목 수정' : '파싱 항목 등록'"
      cancel-text="취소"
      ok-text="저장"
      @ok="saveItem"
    >
      <Form layout="vertical">
        <FormItem label="항목명" required>
          <Input v-model:value="itemForm.itemName" />
        </FormItem>
        <FormItem label="오프셋">
          <InputNumber v-model:value="itemForm.offset" :min="0" style="width: 100%" />
        </FormItem>
        <FormItem label="길이">
          <InputNumber v-model:value="itemForm.length" :min="1" style="width: 100%" />
        </FormItem>
        <FormItem label="데이터 타입">
          <Input v-model:value="itemForm.dataType" placeholder="HEX / DEC / ASCII" />
        </FormItem>
        <FormItem label="정렬 순서">
          <InputNumber v-model:value="itemForm.sortOrder" style="width: 100%" />
        </FormItem>
      </Form>
    </Modal>

    <Modal
      v-model:open="tagModalOpen"
      :confirm-loading="saving"
      :title="isEditTag ? '태그 수정' : '태그 등록'"
      cancel-text="취소"
      ok-text="저장"
      @ok="saveTag"
    >
      <Form layout="vertical">
        <FormItem label="태그명" required>
          <Input v-model:value="tagForm.tagName" />
        </FormItem>
        <FormItem label="값">
          <Input v-model:value="tagForm.tagValue" />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>
