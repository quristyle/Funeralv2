<script lang="ts" setup generic="T extends { id: number }">
import {
  Comment,
  computed,
  Fragment,
  onMounted,
  reactive,
  ref,
  Text,
  useSlots,
  watch,
} from 'vue';

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
  Select,
  Space,
} from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import GridIconButton from '#/components/GridIconButton.vue';

/**
 * 헬프데스크 조직 화면들이 공유하는 단순 CRUD 표.
 *
 * 회사·팀·담당자·고객 화면이 모두 "목록 + 키워드 검색 + 모달 폼 + 삭제" 라는 같은 모양이라
 * 화면마다 같은 코드를 네 번 쓰지 않고 이 컴포넌트에 필드 정의만 넘긴다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * **부모가 쓰는 것은 하나도 바뀌지 않았다** — props(`columns` 은 여전히
 * ant-design-vue 규격이다) · `cell` 슬롯(`{ column, record, text }`) ·
 * `defineExpose({ loadData })` 가 그대로다. 담당자·팀·프로젝트 화면 셋은
 * 한 줄도 고치지 않았다.
 *
 * 그러려면 antd `#bodyCell` 의 버릇 하나를 그대로 흉내내야 했다 —
 * **부모 슬롯이 그 칸을 채우지 않으면 값을 그대로 그린다.** 부모는 자기가
 * 아는 `column.key` 에서만 무언가를 그리고 나머지는 비워 두기 때문이다.
 * `renderCell` 이 그 판단을 한다.
 * ------------------------------------------------------------
 */

/** 폼에 그릴 입력 필드 정의 */
export interface CrudField {
  /** 셀렉트 옵션 (type 이 select 일 때) */
  options?: { label: string; value: any }[];
  /** 수정 시 값을 바꿀 수 없는 필드 (예: 로그인 아이디) */
  disabledOnEdit?: boolean;
  key: string;
  label: string;
  /** 신규 등록에서만 보여줄 필드 (예: 비밀번호) */
  createOnly?: boolean;
  required?: boolean;
  type?: 'date' | 'multiselect' | 'number' | 'password' | 'select' | 'text';
}

const props = defineProps<{
  /** 표 컬럼 정의 (ant-design-vue Table 규격) */
  columns: Record<string, any>[];
  create: (data: Record<string, any>) => Promise<unknown>;
  entityName: string;
  fetch: () => Promise<T[]>;
  fields: CrudField[];
  remove: (id: number) => Promise<unknown>;
  /** 키워드로 걸러낼 대상 필드 */
  searchKeys?: string[];
  update: (id: number, data: Record<string, any>) => Promise<unknown>;
}>();

const slots = useSlots();

const loading = ref(false);
const saving = ref(false);
const rows = ref<T[]>([]) as any;
const keyword = ref('');

const modalOpen = ref(false);
const editingId = ref<null | number>(null);
const form = reactive<Record<string, any>>({});

const isEdit = computed(() => editingId.value !== null);

/** 화면에서 키워드로 걸러낸 목록. 서버 검색이 없는 소규모 마스터 데이터라 클라이언트에서 거른다. */
const filteredRows = computed(() => {
  const kw = keyword.value.trim().toLowerCase();
  if (!kw) return rows.value;

  const keys = props.searchKeys ?? props.fields.map((f) => f.key);
  return rows.value.filter((row: any) =>
    keys.some((k) => String(row[k] ?? '').toLowerCase().includes(kw)),
  );
});

/** 폼에 보일 필드 — 신규 전용 필드는 수정 시 감춘다. */
const visibleFields = computed(() =>
  props.fields.filter((f) => !f.createOnly || !isEdit.value),
);

// ============================================================
// 표
// ============================================================

/** antd 의 `dataIndex` 를 vxe 의 `field` 로. 배열 경로(`['team','name']`)는 점으로 잇는다. */
function fieldOf(column: Record<string, any>) {
  const index = column.dataIndex;
  if (Array.isArray(index)) return index.join('.');
  if (index === undefined || index === null) return String(column.key ?? '');
  return String(index);
}

/** 그 칸의 값. antd `#bodyCell` 의 `text` 와 같은 것이다. */
function rawValue(row: any, column: Record<string, any>) {
  const index = column.dataIndex;
  if (index === undefined || index === null) return undefined;
  const path = Array.isArray(index) ? index : String(index).split('.');
  return path.reduce(
    (acc: any, key: any) => (acc === null || acc === undefined ? undefined : acc[key]),
    row,
  );
}

/**
 * 값을 사람이 보는 글자로 편다.
 * 중첩 객체·배열(소속 팀 목록 등)도 필터가 훑을 수 있게 한 겹 더 펴 준다.
 */
function cellText(value: any, depth = 0): string {
  if (value === null || value === undefined) return '';
  if (Array.isArray(value)) {
    return value.map((v) => cellText(v, depth)).join(' ');
  }
  if (typeof value === 'object') {
    if (depth >= 2) return '';
    return Object.values(value)
      .map((v) => cellText(v, depth + 1))
      .join(' ');
  }
  return String(value);
}

/**
 * 부모 슬롯이 실제로 무언가를 그렸는가.
 *
 * 부모는 `<template v-if="column.key === 'createdAt'">` 처럼 자기가 아는 칸에서만
 * 그린다. 걸리지 않은 칸에는 `v-if` 자리표시자(주석 노드)만 남는데, 그걸
 * "그린 것"으로 보면 나머지 칸이 전부 빈칸이 된다.
 */
function hasContent(nodes: any): boolean {
  if (nodes === null || nodes === undefined || nodes === false) return false;
  if (Array.isArray(nodes)) return nodes.some((node) => hasContent(node));
  if (typeof nodes === 'string' || typeof nodes === 'number') {
    return String(nodes).trim() !== '';
  }
  if (typeof nodes !== 'object') return false;
  if (nodes.type === Comment) return false;
  if (nodes.type === Text) return String(nodes.children ?? '').trim() !== '';
  if (nodes.type === Fragment) return hasContent(nodes.children);
  return true;
}

/** 값 칸 하나를 그린다. 부모가 채우지 않으면 값을 그대로 쓴다(antd 와 같다). */
function renderCell(column: Record<string, any>, row: any) {
  const text = rawValue(row, column);
  const filled = slots.cell?.({ column, record: row, text });
  return hasContent(filled) ? filled : [cellText(text)];
}

/** 부모가 준 antd 컬럼 + 맨 뒤의 작업 버튼 칸. */
const gridColumns = computed(() => {
  const columns = props.columns.map((column) => {
    const field = fieldOf(column);
    // `dataIndex` 가 없는 칸은 값이 아니라 버튼·링크를 그리는 칸이다(프로젝트 '바로가기').
    const isValue = column.dataIndex !== undefined && column.dataIndex !== null;

    return {
      align: column.align,
      field,
      params: isValue
        ? { filterText: (row: any) => cellText(rawValue(row, column)) }
        : { filter: false, sort: false },
      slots: { default: ({ row }: any) => renderCell(column, row) },
      title: column.title,
      // vxe 는 너비를 안 주면 칸이 눌린다.
      ...(column.width ? { width: column.width } : { minWidth: 140 }),
    };
  });

  // `field: 'action'` 이라 공통 레이어가 정렬·필터에서 알아서 뺀다.
  columns.push({
    field: 'action',
    slots: { default: 'action' },
    title: '',
    width: 120,
  } as any);

  return columns;
});

const [Grid, gridApi] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: gridColumns.value,
    // 행 배열은 `:table-data` 로 간다. 여기 `[]` 는 검색 결과가 비었을 때
    // 표를 비우기 위한 바탕값이다(프레임워크가 빈 배열은 넘겨 주지 않는다).
    data: [],
    emptyText: `등록된 ${props.entityName}이(가) 없습니다.`,
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 위쪽 '새로고침' 과 `defineExpose` 로 내주는 것과 같은 함수다.
    // [추가] 아이콘도 같이 세운다 — 위쪽 등록 아이콘과 같은 함수다.
    gridFeatures: { onCreate: () => openCreate(), onRefresh: () => loadData() },
    height: 'auto',
    // 전량 조회다. 페이저를 켜 두면 vxe 가 응답을 `{result,page}` 로 읽어 한 줄도 안 나온다.
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'id' },
  } as any,
});

// 컬럼을 계산해서 넘기는 화면이 나올 수 있다. 바뀌면 다시 심는다.
watch(gridColumns, (columns) => gridApi.setGridOptions({ columns }));

watch(loading, (value) => gridApi.setLoading(value));

// ============================================================
// 조회 · 편집
// ============================================================

async function loadData() {
  loading.value = true;
  try {
    rows.value = (await props.fetch()) ?? [];
  } finally {
    loading.value = false;
  }
}

function resetForm() {
  props.fields.forEach((f) => {
    form[f.key] = f.type === 'multiselect' ? [] : undefined;
  });
}

function openCreate() {
  editingId.value = null;
  resetForm();
  modalOpen.value = true;
}

function openEdit(row: any) {
  editingId.value = row.id;
  props.fields.forEach((f) => {
    form[f.key] = row[f.key];
  });
  modalOpen.value = true;
}

async function onSave() {
  const missing = visibleFields.value.find((f) => {
    if (!f.required) return false;
    const value = form[f.key];
    return Array.isArray(value)
      ? value.length === 0
      : !String(value ?? '').trim();
  });
  if (missing) {
    message.warning(`${missing.label}을(를) 입력하세요.`);
    return;
  }

  const payload: Record<string, any> = {};
  visibleFields.value.forEach((f) => {
    payload[f.key] = form[f.key];
  });

  saving.value = true;
  try {
    await (isEdit.value
      ? props.update(editingId.value!, payload)
      : props.create(payload));
    message.success(
      `${props.entityName}을(를) ${isEdit.value ? '수정' : '등록'}했습니다.`,
    );
    modalOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function onDelete(row: any) {
  await props.remove(row.id);
  message.success(`${props.entityName}을(를) 삭제했습니다.`);
  await loadData();
}

onMounted(loadData);

defineExpose({ loadData });
</script>

<template>
  <!--
    부모 화면은 `<Page auto-content-height>` 만 주고 `page-fill-last` 는 주지 않는다.
    그래서 여기서 스스로 "조회 줄 + 남은 높이를 채우는 표" 가 된다(준수사항 4).
    모바일에서는 그 규칙을 풀어 일반 흐름으로 둔다 — page-fill-last 와 같은 기준이다.
  -->
  <div class="md:flex md:h-full md:min-h-0 md:flex-col">
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space>
          <Input
            v-model:value="keyword"
            allow-clear
            placeholder="검색어"
            style="width: 220px"
          />
        </Space>
        <!-- 동작 단추는 오른쪽에 모은다 — 새로고침도 동작이다. -->
        <div class="flex items-center gap-2">
          <GridIconButton
            :loading="loading"
            icon="vxe-icon-repeat"
            title="새로고침"
            @click="loadData"
          />
          <GridIconButton
            v-perm:create
            :title="`${entityName} 등록`"
            icon="vxe-icon-add"
            @click="openCreate"
          />
        </div>
      </div>
    </Card>

    <!-- `md:h-auto` 는 그리드 기본값인 `h-full` 을 풀어 준다(page-fill-last 와 같은 처방). -->
    <Grid class="md:h-auto md:min-h-0 md:flex-1" :table-data="filteredRows">
      <template #action="{ row }">
        <Space>
          <Button v-perm:update size="small" type="link" @click="openEdit(row)">
            수정
          </Button>
          <Popconfirm
            v-perm:delete
            cancel-text="취소"
            ok-text="삭제"
            :title="`${entityName}을(를) 삭제할까요?`"
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
      :title="`${entityName} ${isEdit ? '수정' : '등록'}`"
      cancel-text="취소"
      ok-text="저장"
      @ok="onSave"
    >
      <Form layout="vertical">
        <FormItem
          v-for="field in visibleFields"
          :key="field.key"
          :label="field.label"
          :required="field.required"
        >
          <Select
            v-if="field.type === 'select'"
            v-model:value="form[field.key]"
            :options="field.options"
            option-filter-prop="label"
            show-search
          />
          <Select
            v-else-if="field.type === 'multiselect'"
            v-model:value="form[field.key]"
            :options="field.options"
            mode="multiple"
            option-filter-prop="label"
            show-search
          />
          <InputNumber
            v-else-if="field.type === 'number'"
            v-model:value="form[field.key]"
            style="width: 100%"
          />
          <DatePicker
            v-else-if="field.type === 'date'"
            v-model:value="form[field.key]"
            style="width: 100%"
            value-format="YYYY-MM-DD"
          />
          <Input
            v-else-if="field.type === 'password'"
            v-model:value="form[field.key]"
            type="password"
          />
          <Input
            v-else
            v-model:value="form[field.key]"
            :disabled="isEdit && field.disabledOnEdit"
          />
        </FormItem>
      </Form>
    </Modal>
  </div>
</template>
