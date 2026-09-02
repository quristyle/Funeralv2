<script lang="ts" setup>
import type {
  AckFindPayload,
  McModel,
  ParseItemPayload,
  TagItemPayload,
} from '#/api/helpdesk';

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
  Select,
  Space,
  Spin,
  TabPane,
  Tabs,
  Tag,
} from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import GridIconButton from '#/components/GridIconButton.vue';
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
  TAG_DATA_TYPES,
  updateAckFind,
  updateMcModel,
  updateParseItem,
  updateTagItem,
} from '#/api/helpdesk';

import { formatDateTime } from '../shared/constants';

/**
 * [MC 모델 관리]
 *
 * 원본(JinReception utils/McModelManager.vue + BinaryParser.vue 의 규격 등록 팝업).
 * 프로토콜 규격을 모델 → 파싱 항목 → 태그 항목 3단으로 편집한다.
 *
 * 필드명은 서버 응답 그대로 쓴다: mcName / startKey / pTYPE / keyIdx / keys /
 * blocParseType / blocParseLength / tagIdx / tagLength / dataType / sortNo.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 세 개를 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * 표가 탭 안에 있어 부모가 높이를 주지 못한다. 그래서 `height` 를 숫자로 준다.
 * 가져오기는 그대로다 — 모델 하나를 통째로 받아 화면에서 갈라 쓴다.
 * ------------------------------------------------------------
 */

const loading = ref(false);
const saving = ref(false);
const models = ref<McModel[]>([]);
const selectedModelId = ref<number | undefined>();

const selectedModel = computed(() =>
  models.value.find((m) => m.id === selectedModelId.value),
);
const parseItems = computed(() => selectedModel.value?.parseItems ?? []);
const ackFinds = computed(() => selectedModel.value?.ackFinds ?? []);
const samples = computed(() => selectedModel.value?.samples ?? []);

const PTYPE_OPTIONS = [
  { label: 'RX (수신)', value: 'RX' },
  { label: 'TX (송신)', value: 'TX' },
];

const BLOC_TYPE_OPTIONS = [
  { label: 'number', value: 'number' },
  { label: 'date', value: 'date' },
];

const DATA_TYPE_OPTIONS = TAG_DATA_TYPES.map((t) => ({ label: t, value: t }));

const ARROW_OPTIONS = [
  { label: 'up', value: 'up' },
  { label: 'down', value: 'down' },
];
const EQUALS_OPTIONS = [
  { label: 'equal', value: 'equal' },
  { label: 'not', value: 'not' },
];

// ── 모델 ──────────────────────────────────────────────────
const modelModalOpen = ref(false);
const modelForm = reactive<{ id?: number; mcName: string; startKey: string }>({
  mcName: '',
  startKey: '',
});
const isEditModel = computed(() => Boolean(modelForm.id));

// ── 파싱 항목 ─────────────────────────────────────────────
const itemModalOpen = ref(false);
const itemForm = reactive<ParseItemPayload & { id?: number }>({ desc: '' });
const isEditItem = computed(() => Boolean(itemForm.id));

// ── 태그 항목 ─────────────────────────────────────────────
const tagModalOpen = ref(false);
const tagForm = reactive<TagItemPayload & { id?: number; parseItemId?: number }>(
  { desc: '' },
);
const isEditTag = computed(() => Boolean(tagForm.id));

// ── ACK 규칙 ──────────────────────────────────────────────
const ackModalOpen = ref(false);
const ackForm = reactive<AckFindPayload & { id?: number }>({
  endCalcArrow: 'up',
  endCalcEquals: 'not',
  endCalcIdx: '10',
  endCalcTarget: 'TX',
  endCalcValue: '80',
  startCalcArrow: 'up',
  startCalcEquals: 'not',
  startCalcIdx: '10',
  startCalcTarget: 'TX',
  startCalcValue: '80',
});
const isEditAck = computed(() => Boolean(ackForm.id));

/** 키 바이트 목록을 16진 문자열로 보여준다. */
function keysLabel(keys?: number[]) {
  if (!keys?.length) return '-';
  return keys.map((k) => Number(k).toString(16).padStart(2, '0').toUpperCase()).join(' ');
}

/** ACK 규칙의 조건 한 줄. 표에 그리는 글자와 필터가 훑는 글자를 같게 쓴다. */
function ackCondition(row: any, prefix: 'end' | 'start') {
  const at = (name: string) => row[`${prefix}Calc${name}`];
  return `${at('Target')}[${at('Idx')}] ${at('Equals')} ${at('Value')} (${at('Arrow')})`;
}

// ── 표 세 개 ──────────────────────────────────────────────
// 탭 안이라 부모가 높이를 주지 못한다. 숫자로 준다.
const GRID_HEIGHT = 420;

const [ItemGrid] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      // 하위 태그를 펼치는 칸. 원본의 `expandedRowRender` 자리다.
      { type: 'expand', slots: { content: 'tags' }, width: 40 },
      { field: 'desc', minWidth: 180, title: '설명' },
      {
        field: 'pTYPE',
        params: {
          filterOptions: [
            { label: 'RX', value: 'RX' },
            { label: 'TX', value: 'TX' },
          ],
        },
        slots: { default: 'pTYPE' },
        title: '구분',
        width: 80,
      },
      { field: 'keyIdx', title: '키 위치', width: 90 },
      {
        field: 'keys',
        params: { filterText: (row: any) => keysLabel(row.keys) },
        slots: { default: 'keys' },
        title: '키 바이트',
        width: 140,
      },
      {
        field: 'blocParseType',
        params: {
          filterOptions: [
            { label: 'number', value: 'number' },
            { label: 'date', value: 'date' },
          ],
        },
        title: '블록 방식',
        width: 110,
      },
      { field: 'blocParseLength', title: '블록 길이', width: 110 },
      { field: 'action', slots: { default: 'action' }, title: '', width: 160 },
    ],
    data: [],
    emptyText: '파싱 항목이 없습니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 표 셋 모두 모델 하나를 통째로 받아 갈라 쓰므로 셋 다 같은 함수를 준다.
    // [추가] 아이콘은 탭 위쪽의 등록 아이콘과 같은 함수를 부른다.
    gridFeatures: { onCreate: () => openCreateItem(), onRefresh: () => loadData() },
    height: GRID_HEIGHT,
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'id' },
  } as any,
});

const [AckGrid] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      {
        field: 'start',
        minWidth: 220,
        // 값 하나가 아니라 여러 칸을 엮어 그리는 칸이다. 필터가 훑을 글자를 직접 준다.
        params: { filterText: (row: any) => ackCondition(row, 'start'), sort: false },
        slots: { default: 'start' },
        title: '시작 조건',
      },
      {
        field: 'end',
        minWidth: 220,
        params: { filterText: (row: any) => ackCondition(row, 'end'), sort: false },
        slots: { default: 'end' },
        title: '종료 조건',
      },
      { field: 'action', slots: { default: 'action' }, title: '', width: 120 },
    ],
    data: [],
    emptyText: 'ACK 규칙이 없습니다.',
    // 위 표와 같은 조회로 채워진다.
    gridFeatures: { onCreate: () => openCreateAck(), onRefresh: () => loadData() },
    height: GRID_HEIGHT,
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'id' },
  } as any,
});

const [SampleGrid] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'title', minWidth: 220, title: '샘플 제목' },
      {
        field: 'createdAt',
        params: { filterText: (row: any) => formatDateTime(row.createdAt) },
        slots: { default: 'createdAt' },
        title: '저장일',
        width: 170,
      },
    ],
    data: [],
    emptyText: '보관된 샘플이 없습니다. 바이너리 파서에서 저장할 수 있습니다.',
    // 위 표들과 같은 조회로 채워진다.
    gridFeatures: { onRefresh: () => loadData() },
    height: GRID_HEIGHT,
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'id' },
  } as any,
});

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
  delete modelForm.id;
  modelForm.mcName = '';
  modelForm.startKey = '';
  modelModalOpen.value = true;
}

function openEditModel(model: McModel) {
  modelForm.id = model.id;
  modelForm.mcName = model.mcName;
  modelForm.startKey = model.startKey ?? '';
  modelModalOpen.value = true;
}

async function saveModel() {
  if (!modelForm.mcName.trim()) {
    message.warning('모델명을 입력하세요.');
    return;
  }

  saving.value = true;
  try {
    const payload = { mcName: modelForm.mcName, startKey: modelForm.startKey };
    await (isEditModel.value
      ? updateMcModel(modelForm.id!, payload)
      : createMcModel(payload));
    message.success(`모델을 ${isEditModel.value ? '수정' : '등록'}했습니다.`);
    modelModalOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function removeModel(model: McModel) {
  await deleteMcModel(model.id);
  message.success('모델을 삭제했습니다.');
  if (selectedModelId.value === model.id) selectedModelId.value = undefined;
  await loadData();
}

// ── 파싱 항목 CRUD ────────────────────────────────────────
function openCreateItem() {
  delete itemForm.id;
  Object.assign(itemForm, {
    blocParseLength: '8',
    blocParseType: 'number',
    desc: '',
    keyIdx: 0,
    keys: '',
    ptype: 'RX',
  });
  itemModalOpen.value = true;
}

function openEditItem(row: any) {
  Object.assign(itemForm, {
    blocParseLength: row.blocParseLength ?? '8',
    blocParseType: row.blocParseType ?? 'number',
    desc: row.desc ?? '',
    id: row.id,
    keyIdx: row.keyIdx ?? 0,
    // 서버는 숫자 배열로 주고 콤마 문자열로 받는다.
    keys: (row.keys ?? []).join(','),
    ptype: row.pTYPE ?? 'RX',
  });
  itemModalOpen.value = true;
}

async function saveItem() {
  if (!selectedModelId.value) return;
  if (!itemForm.desc.trim()) {
    message.warning('설명을 입력하세요.');
    return;
  }

  saving.value = true;
  try {
    const payload: ParseItemPayload = {
      blocParseLength: itemForm.blocParseLength,
      blocParseType: itemForm.blocParseType,
      desc: itemForm.desc,
      keyIdx: itemForm.keyIdx,
      keys: itemForm.keys,
      ptype: itemForm.ptype,
    };
    await (isEditItem.value
      ? updateParseItem(itemForm.id!, payload)
      : createParseItem(selectedModelId.value, payload));
    message.success(`파싱 항목을 ${isEditItem.value ? '수정' : '등록'}했습니다.`);
    itemModalOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function removeItem(row: any) {
  await deleteParseItem(row.id);
  message.success('파싱 항목을 삭제했습니다.');
  await loadData();
}

// ── 태그 항목 CRUD ────────────────────────────────────────
function openCreateTag(parseItem: any) {
  delete tagForm.id;
  Object.assign(tagForm, {
    dataType: 'NUMBER',
    desc: '',
    parseItemId: parseItem.id,
    sortNo: (parseItem.tagItems?.length ?? 0) + 1,
    tagIdx: 0,
    tagLength: 1,
  });
  tagModalOpen.value = true;
}

function openEditTag(tag: any) {
  Object.assign(tagForm, {
    dataType: tag.dataType ?? 'NUMBER',
    desc: tag.desc ?? '',
    id: tag.id,
    parseItemId: tag.parseItemId,
    sortNo: tag.sortNo ?? 0,
    tagIdx: tag.tagIdx ?? 0,
    tagLength: tag.tagLength ?? 1,
  });
  tagModalOpen.value = true;
}

async function saveTag() {
  if (!tagForm.desc.trim()) {
    message.warning('설명을 입력하세요.');
    return;
  }

  saving.value = true;
  try {
    const payload: TagItemPayload = {
      dataType: tagForm.dataType,
      desc: tagForm.desc,
      sortNo: tagForm.sortNo,
      tagIdx: tagForm.tagIdx,
      tagLength: tagForm.tagLength,
    };
    await (isEditTag.value
      ? updateTagItem(tagForm.id!, payload)
      : createTagItem(tagForm.parseItemId!, payload));
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

// ── ACK 규칙 CRUD ─────────────────────────────────────────
function openCreateAck() {
  delete ackForm.id;
  Object.assign(ackForm, {
    endCalcArrow: 'up',
    endCalcEquals: 'not',
    endCalcIdx: '10',
    endCalcTarget: 'TX',
    endCalcValue: '80',
    startCalcArrow: 'up',
    startCalcEquals: 'not',
    startCalcIdx: '10',
    startCalcTarget: 'TX',
    startCalcValue: '80',
  });
  ackModalOpen.value = true;
}

function openEditAck(row: any) {
  Object.assign(ackForm, { ...row });
  ackModalOpen.value = true;
}

async function saveAck() {
  if (!selectedModelId.value) return;

  saving.value = true;
  try {
    const payload: AckFindPayload = {
      endCalcArrow: ackForm.endCalcArrow,
      endCalcEquals: ackForm.endCalcEquals,
      endCalcIdx: ackForm.endCalcIdx,
      endCalcTarget: ackForm.endCalcTarget,
      endCalcValue: ackForm.endCalcValue,
      startCalcArrow: ackForm.startCalcArrow,
      startCalcEquals: ackForm.startCalcEquals,
      startCalcIdx: ackForm.startCalcIdx,
      startCalcTarget: ackForm.startCalcTarget,
      startCalcValue: ackForm.startCalcValue,
    };
    await (isEditAck.value
      ? updateAckFind(ackForm.id!, payload)
      : createAckFind(selectedModelId.value, payload));
    message.success(`ACK 규칙을 ${isEditAck.value ? '수정' : '등록'}했습니다.`);
    ackModalOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function removeAck(row: any) {
  await deleteAckFind(row.id);
  message.success('ACK 규칙을 삭제했습니다.');
  await loadData();
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height>
    <Row :gutter="[12, 12]">
      <!-- 모델 목록 -->
      <Col :lg="6" :xs="24">
        <Card :body-style="{ padding: 0 }" size="small" title="모델">
          <template #extra>
            <GridIconButton
              v-perm:create
              icon="vxe-icon-add"
              title="모델 추가"
              @click="openCreateModel"
            />
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
                    <div class="truncate font-medium">{{ item.mcName }}</div>
                    <div class="truncate text-xs text-muted-foreground">
                      StartKey: {{ item.startKey || '-' }}
                    </div>
                  </div>
                  <Space @click.stop>
                    <Button v-perm:update size="small" type="link" @click="openEditModel(item)">
                      수정
                    </Button>
                    <Popconfirm
                      v-perm:delete
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

      <!-- 상세 -->
      <Col :lg="18" :xs="24">
        <Card size="small">
          <Empty v-if="!selectedModel" description="모델을 선택하세요." />

          <Tabs v-else>
            <!-- 파싱 항목 -->
            <TabPane key="items" tab="파싱 항목">
              <div class="mb-2 flex justify-end">
                <GridIconButton
                  v-perm:create
                  icon="vxe-icon-add"
                  title="항목 추가"
                  @click="openCreateItem"
                />
              </div>

              <ItemGrid :table-data="parseItems">
                <!-- 하위 태그. 원본의 expandedRowRender 자리다. -->
                <template #tags="{ row }">
                  <div class="mb-2 flex items-center justify-between">
                    <span class="text-xs text-muted-foreground">태그 항목</span>
                    <GridIconButton
                      v-perm:create
                      icon="vxe-icon-add"
                      title="태그 추가"
                      @click="openCreateTag(row)"
                    />
                  </div>

                  <Empty
                    v-if="!row.tagItems?.length"
                    :image="Empty.PRESENTED_IMAGE_SIMPLE"
                    description="태그가 없습니다."
                  />
                  <div
                    v-for="tag in row.tagItems ?? []"
                    :key="tag.id"
                    class="flex items-center gap-2 border-b border-border py-1 text-xs last:border-b-0"
                  >
                    <span class="w-8 text-muted-foreground">{{ tag.sortNo }}</span>
                    <span class="w-44 font-medium">{{ tag.desc }}</span>
                    <Tag>{{ tag.dataType }}</Tag>
                    <span class="text-muted-foreground">
                      idx {{ tag.tagIdx }} · len {{ tag.tagLength }}
                    </span>
                    <span class="flex-1"></span>
                    <Button v-perm:update size="small" type="link" @click="openEditTag(tag)">
                      수정
                    </Button>
                    <Popconfirm
                      v-perm:delete
                      cancel-text="취소"
                      ok-text="삭제"
                      title="태그를 삭제할까요?"
                      @confirm="removeTag(tag)"
                    >
                      <Button danger size="small" type="link">삭제</Button>
                    </Popconfirm>
                  </div>
                </template>

                <template #pTYPE="{ row }">
                  <Tag :color="row.pTYPE === 'RX' ? 'blue' : 'green'">
                    {{ row.pTYPE }}
                  </Tag>
                </template>

                <template #keys="{ row }">
                  <span class="font-mono text-xs">{{ keysLabel(row.keys) }}</span>
                </template>

                <template #action="{ row }">
                  <Space>
                    <Button v-perm:update size="small" type="link" @click="openEditItem(row)">
                      수정
                    </Button>
                    <Popconfirm
                      v-perm:delete
                      cancel-text="취소"
                      ok-text="삭제"
                      title="항목을 삭제할까요?"
                      @confirm="removeItem(row)"
                    >
                      <Button danger size="small" type="link">삭제</Button>
                    </Popconfirm>
                  </Space>
                </template>
              </ItemGrid>
            </TabPane>

            <!-- ACK 규칙 -->
            <TabPane key="ack" tab="ACK 규칙">
              <div class="mb-2 flex justify-end">
                <GridIconButton
                  v-perm:create
                  icon="vxe-icon-add"
                  title="규칙 추가"
                  @click="openCreateAck"
                />
              </div>

              <AckGrid :table-data="ackFinds">
                <template #start="{ row }">
                  <span class="font-mono text-xs">
                    {{ ackCondition(row, 'start') }}
                  </span>
                </template>
                <template #end="{ row }">
                  <span class="font-mono text-xs">
                    {{ ackCondition(row, 'end') }}
                  </span>
                </template>
                <template #action="{ row }">
                  <Space>
                    <Button v-perm:update size="small" type="link" @click="openEditAck(row)">
                      수정
                    </Button>
                    <Popconfirm
                      v-perm:delete
                      cancel-text="취소"
                      ok-text="삭제"
                      title="규칙을 삭제할까요?"
                      @confirm="removeAck(row)"
                    >
                      <Button danger size="small" type="link">삭제</Button>
                    </Popconfirm>
                  </Space>
                </template>
              </AckGrid>
            </TabPane>

            <!-- 보관 샘플 -->
            <TabPane key="samples" tab="보관 샘플">
              <SampleGrid :table-data="samples">
                <template #createdAt="{ row }">
                  {{ formatDateTime(row.createdAt) }}
                </template>
              </SampleGrid>
            </TabPane>
          </Tabs>
        </Card>
      </Col>
    </Row>

    <!-- 모델 -->
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
          <Input v-model:value="modelForm.mcName" placeholder="KEPCO" />
        </FormItem>
        <FormItem label="StartKey">
          <Input
            v-model:value="modelForm.startKey"
            placeholder="전문 시작을 알리는 키 바이트"
          />
        </FormItem>
      </Form>
    </Modal>

    <!-- 파싱 항목 -->
    <Modal
      v-model:open="itemModalOpen"
      :confirm-loading="saving"
      :title="isEditItem ? '파싱 항목 수정' : '파싱 항목 등록'"
      cancel-text="취소"
      ok-text="저장"
      @ok="saveItem"
    >
      <Form layout="vertical">
        <FormItem label="설명" required>
          <Input v-model:value="itemForm.desc" />
        </FormItem>
        <FormItem label="구분">
          <Select v-model:value="itemForm.ptype" :options="PTYPE_OPTIONS" />
        </FormItem>
        <FormItem label="키 바이트 위치 (0부터)">
          <InputNumber v-model:value="itemForm.keyIdx" :min="0" style="width: 100%" />
        </FormItem>
        <FormItem label="키 바이트 값">
          <Input v-model:value="itemForm.keys" placeholder="예: 128,129 (콤마 구분)" />
        </FormItem>
        <FormItem label="블록 해석 방식">
          <Select
            v-model:value="itemForm.blocParseType"
            :options="BLOC_TYPE_OPTIONS"
          />
        </FormItem>
        <FormItem label="블록 길이">
          <Input
            v-model:value="itemForm.blocParseLength"
            placeholder="예: 8 또는 4,2,1,1"
          />
        </FormItem>
      </Form>
    </Modal>

    <!-- 태그 -->
    <Modal
      v-model:open="tagModalOpen"
      :confirm-loading="saving"
      :title="isEditTag ? '태그 수정' : '태그 등록'"
      cancel-text="취소"
      ok-text="저장"
      @ok="saveTag"
    >
      <Form layout="vertical">
        <FormItem label="설명" required>
          <Input v-model:value="tagForm.desc" />
        </FormItem>
        <FormItem label="데이터 타입">
          <Select
            v-model:value="tagForm.dataType"
            :options="DATA_TYPE_OPTIONS"
            option-filter-prop="label"
            show-search
          />
        </FormItem>
        <FormItem label="시작 위치 (tagIdx)">
          <InputNumber v-model:value="tagForm.tagIdx" :min="0" style="width: 100%" />
        </FormItem>
        <FormItem label="길이 (tagLength)">
          <InputNumber
            v-model:value="tagForm.tagLength"
            :min="1"
            style="width: 100%"
          />
        </FormItem>
        <FormItem label="정렬 순서">
          <InputNumber v-model:value="tagForm.sortNo" style="width: 100%" />
        </FormItem>
      </Form>
    </Modal>

    <!-- ACK 규칙 -->
    <Modal
      v-model:open="ackModalOpen"
      :confirm-loading="saving"
      :title="isEditAck ? 'ACK 규칙 수정' : 'ACK 규칙 등록'"
      cancel-text="취소"
      ok-text="저장"
      width="640px"
      @ok="saveAck"
    >
      <Form layout="vertical">
        <div class="mb-2 text-xs font-semibold">시작 조건</div>
        <Row :gutter="8">
          <Col :span="5">
            <FormItem label="방향">
              <Select v-model:value="ackForm.startCalcArrow" :options="ARROW_OPTIONS" />
            </FormItem>
          </Col>
          <Col :span="5">
            <FormItem label="대상">
              <Select
                v-model:value="ackForm.startCalcTarget"
                :options="PTYPE_OPTIONS"
              />
            </FormItem>
          </Col>
          <Col :span="4">
            <FormItem label="위치">
              <Input v-model:value="ackForm.startCalcIdx" />
            </FormItem>
          </Col>
          <Col :span="5">
            <FormItem label="비교">
              <Select
                v-model:value="ackForm.startCalcEquals"
                :options="EQUALS_OPTIONS"
              />
            </FormItem>
          </Col>
          <Col :span="5">
            <FormItem label="값">
              <Input v-model:value="ackForm.startCalcValue" />
            </FormItem>
          </Col>
        </Row>

        <div class="mb-2 mt-2 text-xs font-semibold">종료 조건</div>
        <Row :gutter="8">
          <Col :span="5">
            <FormItem label="방향">
              <Select v-model:value="ackForm.endCalcArrow" :options="ARROW_OPTIONS" />
            </FormItem>
          </Col>
          <Col :span="5">
            <FormItem label="대상">
              <Select v-model:value="ackForm.endCalcTarget" :options="PTYPE_OPTIONS" />
            </FormItem>
          </Col>
          <Col :span="4">
            <FormItem label="위치">
              <Input v-model:value="ackForm.endCalcIdx" />
            </FormItem>
          </Col>
          <Col :span="5">
            <FormItem label="비교">
              <Select
                v-model:value="ackForm.endCalcEquals"
                :options="EQUALS_OPTIONS"
              />
            </FormItem>
          </Col>
          <Col :span="5">
            <FormItem label="값">
              <Input v-model:value="ackForm.endCalcValue" />
            </FormItem>
          </Col>
        </Row>
      </Form>
    </Modal>
  </Page>
</template>
