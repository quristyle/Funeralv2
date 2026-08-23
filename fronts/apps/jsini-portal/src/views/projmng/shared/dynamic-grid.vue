<script setup lang="ts">
/**
 * 메타 구동 그리드 — 이식 전 `QuriDynamicGrid` 를 옮긴 것.
 *
 * 프로젝트관리 화면들은 컬럼을 코드에 적어 두지 않는다.
 * 저장 프로시저가 결과와 함께 컬럼 메타(`cols`)를 돌려주고, 그리드가 그것을 보고 그린다.
 *
 *   cols : { cm_cd: 'System.String', cm_srt: 'System.Int32', cre_dt: 'System.DateTime' }
 *   data : [ { cm_cd: 'A', cm_srt: 1, cre_dt: '2025-05-02T00:00:00' }, ... ]
 *
 * 덕분에 화면 하나하나가 컬럼을 알 필요가 없다. 이 그리드가 서면 화면은
 * "어떤 프로시저를 어떤 파라미터로 부르는가" 만 적으면 된다.
 *
 * 편집은 행 단위다. 편집한 행에는 `quri_ischange` 표시가 붙고,
 * 저장할 때 `dbSave` 가 그 표시가 붙은 행만 추려 보낸다.
 */
import type { ProjMngResult, ProjMngRow } from '#/api/projmng';

import { computed, getCurrentInstance, ref, watch } from 'vue';

import { IconifyIcon } from '@vben/icons';

import {
  Checkbox,
  DatePicker,
  Input,
  InputNumber,
  Modal,
  Select,
} from 'ant-design-vue';
import dayjs from 'dayjs';
import { VxeColumn, VxeTable } from 'vxe-table';

import { CHANGE_FLAG, getCommon } from '#/api/projmng';

interface Props {
  /** 프로시저 응답 봉투. `cols` 로 컬럼을, `data` 로 행을 그린다 */
  result?: null | ProjMngResult;
  /** 숨길 컬럼. 쉼표로 구분 — 예: `"cm_rid,cm_prop"` */
  hiddenCols?: string;
  /**
   * 드롭다운으로 편집할 컬럼. `컬럼명|공통코드ID` 를 쉼표로 구분한다.
   * 예: `"cm_type|CODE_TYPE,db_type|db"`
   */
  dropdownCols?: string;
  /** 읽기 전용 컬럼. 편집 모드에서도 입력기를 띄우지 않는다 */
  readonlyCols?: string;
  loading?: boolean;
  height?: number | string;
  /** 푸터에 띄우는 보조 메시지 */
  footerMessage?: string;
  footerLoading?: boolean;
  /** 엑셀 파일명 (확장자 제외) */
  exportName?: string;
  /** 행 추가·복사 버튼을 감출 때 */
  hideAdd?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  result: null,
  hiddenCols: '',
  dropdownCols: '',
  readonlyCols: '',
  loading: false,
  height: undefined,
  footerMessage: '',
  footerLoading: false,
  exportName: 'projmng',
  hideAdd: false,
});

const emit = defineEmits<{
  /** 행 편집을 마쳤다. 부모가 dbSave 를 부른다 */
  (e: 'save', row: ProjMngRow): void;
  /** 행 삭제 확인을 받았다 */
  (e: 'delete', row: ProjMngRow): void;
  /** 별도 동작(체크 아이콘) */
  (e: 'action', row: ProjMngRow): void;
  /** 행을 새로 넣었다. 부모가 부모키 등 기본값을 채울 수 있다 */
  (e: 'add', row: ProjMngRow): void;
  /** 행을 복제했다 */
  (e: 'copy', row: ProjMngRow): void;
  /** 행을 선택했다 */
  (e: 'rowClick', row: null | ProjMngRow): void;
}>();

/**
 * 동작 버튼은 부모가 그 이벤트를 받을 때만 보여 준다.
 * 이식 전 `SaveBtnEvent.HasDelegate` 와 같은 판단이다 —
 * 화면마다 `:show-edit` 같은 플래그를 또 넘기지 않아도 된다.
 */
const instance = getCurrentInstance();
const hasHandler = (name: string) =>
  Boolean((instance?.vnode.props as Record<string, unknown>)?.[name]);

const canSave = computed(() => hasHandler('onSave'));
const canDelete = computed(() => hasHandler('onDelete'));
const canAction = computed(() => hasHandler('onAction'));
const hasActionColumn = computed(
  () => canSave.value || canDelete.value || canAction.value,
);

const tableRef = ref();
const filterVisible = ref(false);
const currentRow = ref<null | ProjMngRow>(null);

/** 행 배열. 부모의 `result.data` 를 그대로 쓴다 — 추가·삭제가 부모에도 보여야 한다. */
const rows = computed<ProjMngRow[]>(() => props.result?.data ?? []);

const splitList = (value?: string) =>
  (value ?? '')
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean);

const hiddenSet = computed(() => new Set(splitList(props.hiddenCols)));
const readonlySet = computed(() => new Set(splitList(props.readonlyCols)));

/** 컬럼명 → 공통코드 ID */
const dropdownMap = computed(() => {
  const map = new Map<string, string>();
  splitList(props.dropdownCols).forEach((entry) => {
    const [col, codeId] = entry.split('|');
    if (col && codeId) map.set(col.trim(), codeId.trim());
  });
  return map;
});

/** 드롭다운 컬럼이 쓸 선택 목록. 공통코드를 한 번만 읽어 캐시한다. */
const codeOptions = ref<Record<string, { label: string; value: string }[]>>({});

watch(
  dropdownMap,
  async (map) => {
    for (const [col, codeId] of map.entries()) {
      if (codeOptions.value[col]) continue;
      const items = await getCommon(codeId);
      codeOptions.value = {
        ...codeOptions.value,
        [col]: items.map((it) => ({ label: it.name, value: it.code })),
      };
    }
  },
  { immediate: true },
);

/** 그릴 컬럼 목록. 메타의 순서를 그대로 지킨다. */
const columns = computed(() => {
  const cols = props.result?.cols ?? {};
  return Object.entries(cols)
    .filter(([name]) => !hiddenSet.value.has(name))
    .map(([name, type]) => ({ name, type, width: widthOf(type) }));
});

function widthOf(type: string) {
  switch (type) {
    case 'System.Boolean': {
      return 70;
    }
    case 'System.DateTime': {
      return 140;
    }
    case 'System.Int16':
    case 'System.Int32':
    case 'System.Int64': {
      return 90;
    }
    default: {
      return 140;
    }
  }
}

function isNumeric(type: string) {
  return (
    type === 'System.Int16' ||
    type === 'System.Int32' ||
    type === 'System.Int64' ||
    type === 'System.Decimal' ||
    type === 'System.Double'
  );
}

/** 화면에 보여 줄 값. 날짜는 yyyy-MM-dd 로, 불리언은 체크 표시로 바꾼다. */
function displayValue(row: ProjMngRow, name: string, type: string) {
  const raw = row?.[name];
  if (raw === null || raw === undefined || raw === '') return '';
  if (type === 'System.DateTime') {
    const d = dayjs(String(raw));
    return d.isValid() ? d.format('YYYY-MM-DD') : String(raw);
  }
  return String(raw);
}

function boolOf(row: ProjMngRow, name: string) {
  const raw = row?.[name];
  return raw === true || raw === 'true' || raw === 'True';
}

/** ant-design-vue 의 DatePicker 는 빈 값을 undefined 로 받는다(null 은 타입이 다르다). */
function dateValue(row: ProjMngRow, name: string) {
  const raw = row?.[name];
  if (!raw) return undefined;
  const d = dayjs(String(raw));
  return d.isValid() ? d : undefined;
}

function setDateValue(row: ProjMngRow, name: string, value: any) {
  row[name] = value ? dayjs(value).format('YYYY-MM-DD') : '';
}

// ============================================================
// 편집
// ============================================================

function markChanged(row: ProjMngRow) {
  row[CHANGE_FLAG] = true;
}

async function editRow(row: ProjMngRow) {
  markChanged(row);
  await tableRef.value?.setEditRow(row);
}

async function cancelEdit(row: ProjMngRow) {
  delete row[CHANGE_FLAG];
  await tableRef.value?.clearEdit();
}

async function saveRow(row: ProjMngRow) {
  await tableRef.value?.clearEdit();
  emit('save', row);
}

function actionRow(row: ProjMngRow) {
  emit('action', row);
}

function confirmDelete(row: ProjMngRow) {
  Modal.confirm({
    title: '삭제하겠습니까?',
    okText: '삭제',
    cancelText: '취소',
    okType: 'danger',
    onOk: () => {
      const index = rows.value.indexOf(row);
      if (index >= 0) rows.value.splice(index, 1);
      emit('delete', row);
    },
  });
}

/** 메타를 보고 빈 행을 만든다. 컬럼이 모두 있어야 편집기가 뜬다. */
function createEmptyRow(): ProjMngRow {
  const row: ProjMngRow = {};
  Object.keys(props.result?.cols ?? {}).forEach((name) => {
    row[name] = '';
  });
  return row;
}

/** 선택한 행 자리에 빈 행을 끼운다. 선택이 없으면 맨 앞이다. */
async function insertRow() {
  const index = currentRow.value ? rows.value.indexOf(currentRow.value) : 0;
  const row = createEmptyRow();
  rows.value.splice(Math.max(index, 0), 0, row);
  emit('add', row);
  await editRow(row);
}

/** 선택한 행을 복제해 바로 아래에 넣는다. 숨긴 컬럼은 복제하지 않는다. */
async function copyRow() {
  const source = currentRow.value;
  if (!source) return;

  const index = rows.value.indexOf(source);
  const row = createEmptyRow();
  Object.keys(row).forEach((name) => {
    if (hiddenSet.value.has(name)) return;
    row[name] = source[name];
  });

  rows.value.splice(Math.max(index, 0) + 1, 0, row);
  emit('copy', row);
  await editRow(row);
}

function onCurrentChange({ row }: { row: ProjMngRow }) {
  currentRow.value = row;
  emit('rowClick', row);
}

function onCellDblclick({ row }: { row: ProjMngRow }) {
  if (canSave.value) editRow(row);
}

function isEditing(row: ProjMngRow) {
  return Boolean(tableRef.value?.isEditByRow?.(row));
}

// ============================================================
// 엑셀
// ============================================================

function exportExcel() {
  tableRef.value?.exportData({
    type: 'xlsx',
    filename: props.exportName,
    // 숨긴 컬럼과 동작 컬럼은 제외된다(보이는 컬럼만 내보낸다).
    isHeader: true,
    original: false,
  });
}

/** 부모가 선택 행을 직접 다뤄야 할 때 쓴다. */
defineExpose({
  currentRow,
  insertRow,
  copyRow,
  editRow,
  reload: () => tableRef.value?.clearEdit(),
});
</script>

<template>
  <div class="flex h-full min-h-0 flex-col">
    <VxeTable
      ref="tableRef"
      :data="rows"
      :loading="loading"
      :height="height ?? 'auto'"
      :row-config="{ isCurrent: true, isHover: true }"
      :column-config="{ resizable: true }"
      :edit-config="{ trigger: 'manual', mode: 'row', showStatus: true }"
      :filter-config="{ showIcon: filterVisible }"
      :empty-text="'조회된 자료가 없습니다.'"
      border
      show-overflow
      size="mini"
      class="flex-1"
      @current-change="onCurrentChange"
      @cell-dblclick="onCellDblclick"
    >
      <!-- 동작 컬럼. 부모가 이벤트를 받을 때만 나온다. -->
      <VxeColumn
        v-if="hasActionColumn"
        title=""
        :width="34 * ((canSave ? 1 : 0) + (canAction ? 1 : 0) + (canDelete ? 1 : 0)) + 12"
        fixed="left"
        align="center"
      >
        <template #default="{ row }">
          <div class="flex items-center justify-center gap-1">
            <template v-if="isEditing(row)">
              <a
                v-if="canSave"
                class="text-primary"
                title="저장"
                @click.stop="saveRow(row)"
              >
                <IconifyIcon icon="lucide:save" class="size-4" />
              </a>
              <a
                v-if="canSave"
                class="text-muted-foreground"
                title="취소"
                @click.stop="cancelEdit(row)"
              >
                <IconifyIcon icon="lucide:x" class="size-4" />
              </a>
            </template>
            <template v-else>
              <a
                v-if="canSave"
                class="text-muted-foreground hover:text-primary"
                title="편집"
                @click.stop="editRow(row)"
              >
                <IconifyIcon icon="lucide:pencil" class="size-4" />
              </a>
              <a
                v-if="canAction"
                class="text-muted-foreground hover:text-primary"
                title="실행"
                @click.stop="actionRow(row)"
              >
                <IconifyIcon icon="lucide:check-square" class="size-4" />
              </a>
              <a
                v-if="canDelete"
                class="text-muted-foreground hover:text-red-500"
                title="삭제"
                @click.stop="confirmDelete(row)"
              >
                <IconifyIcon icon="lucide:minus-square" class="size-4" />
              </a>
            </template>
          </div>
        </template>
      </VxeColumn>

      <!-- 메타에서 만든 데이터 컬럼 -->
      <VxeColumn
        v-for="col in columns"
        :key="col.name"
        :field="col.name"
        :title="col.name"
        :min-width="col.width"
        :edit-render="{}"
        :filters="filterVisible ? [{ data: '' }] : undefined"
        :filter-method="
          filterVisible
            ? ({ option, row }: any) =>
                String(row[col.name] ?? '')
                  .toLowerCase()
                  .includes(String(option.data ?? '').toLowerCase())
            : undefined
        "
        sortable
        align="center"
      >
        <template #default="{ row }">
          <IconifyIcon
            v-if="col.type === 'System.Boolean'"
            v-show="boolOf(row, col.name)"
            icon="lucide:check"
            class="mx-auto size-4"
          />
          <span v-else>{{ displayValue(row, col.name, col.type) }}</span>
        </template>

        <template #edit="{ row }">
          <!-- 읽기 전용 컬럼은 편집 중에도 값만 보여 준다 -->
          <span v-if="readonlySet.has(col.name)" class="text-muted-foreground">
            {{ displayValue(row, col.name, col.type) }}
          </span>

          <Select
            v-else-if="dropdownMap.has(col.name)"
            :value="(row[col.name] as string) ?? ''"
            :options="codeOptions[col.name] ?? []"
            size="small"
            class="w-full"
            allow-clear
            show-search
            :filter-option="
              (input: string, option: any) =>
                String(option?.label ?? '')
                  .toLowerCase()
                  .includes(input.toLowerCase())
            "
            @change="(value: any) => (row[col.name] = value ?? '')"
          />

          <DatePicker
            v-else-if="col.type === 'System.DateTime'"
            :value="dateValue(row, col.name)"
            value-format="YYYY-MM-DD"
            size="small"
            class="w-full"
            @change="(value: any) => setDateValue(row, col.name, value)"
          />

          <Checkbox
            v-else-if="col.type === 'System.Boolean'"
            :checked="boolOf(row, col.name)"
            @change="(e: any) => (row[col.name] = e.target.checked)"
          />

          <InputNumber
            v-else-if="isNumeric(col.type)"
            :value="row[col.name] === '' ? undefined : Number(row[col.name])"
            size="small"
            class="w-full"
            @change="(value: any) => (row[col.name] = value ?? '')"
          />

          <Input
            v-else
            :value="(row[col.name] as string) ?? ''"
            size="small"
            @update:value="(value: string) => (row[col.name] = value)"
          />
        </template>
      </VxeColumn>
    </VxeTable>

    <!-- 푸터. 이식 전 FooterTemplate 과 같은 구성이다. -->
    <div
      class="border-border bg-card flex items-center gap-2 border-t px-2 py-1 text-xs"
    >
      <template v-if="hasActionColumn && !hideAdd">
        <a
          class="text-muted-foreground hover:text-primary"
          title="행 추가"
          @click="insertRow()"
        >
          <IconifyIcon icon="lucide:plus-square" class="size-4" />
        </a>
        <a
          class="text-muted-foreground hover:text-primary"
          title="선택 행 복사"
          @click="copyRow()"
        >
          <IconifyIcon icon="lucide:copy" class="size-4" />
        </a>
      </template>

      <a
        class="text-muted-foreground hover:text-primary"
        title="엑셀 내보내기"
        @click="exportExcel()"
      >
        <IconifyIcon icon="lucide:file-spreadsheet" class="size-4" />
      </a>

      <span class="flex-1"></span>

      <span v-if="footerMessage" class="flex items-center gap-1">
        <IconifyIcon
          v-if="footerLoading"
          icon="lucide:loader-circle"
          class="size-3 animate-spin"
        />
        <span class="bg-muted rounded px-1.5 py-0.5">{{ footerMessage }}</span>
      </span>

      <span class="text-muted-foreground">{{ rows.length }} 건</span>

      <a
        class="text-muted-foreground hover:text-primary"
        title="필터 표시"
        @click="filterVisible = !filterVisible"
      >
        <IconifyIcon icon="lucide:filter" class="size-4" />
      </a>
    </div>
  </div>
</template>
