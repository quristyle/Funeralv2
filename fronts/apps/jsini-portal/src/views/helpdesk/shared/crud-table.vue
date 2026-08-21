<script lang="ts" setup generic="T extends { id: number }">
import { computed, onMounted, reactive, ref } from 'vue';

import {
  Button,
  Card,
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
  Table,
} from 'ant-design-vue';

/**
 * 헬프데스크 조직 화면들이 공유하는 단순 CRUD 표.
 *
 * 회사·팀·담당자·고객 화면이 모두 "목록 + 키워드 검색 + 모달 폼 + 삭제" 라는 같은 모양이라
 * 화면마다 같은 코드를 네 번 쓰지 않고 이 컴포넌트에 필드 정의만 넘긴다.
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
  type?: 'number' | 'password' | 'select' | 'text';
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
    form[f.key] = undefined;
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
  const missing = visibleFields.value.find(
    (f) => f.required && !String(form[f.key] ?? '').trim(),
  );
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
  <div>
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space>
          <Input
            v-model:value="keyword"
            allow-clear
            placeholder="검색어"
            style="width: 220px"
          />
          <Button :loading="loading" @click="loadData">새로고침</Button>
        </Space>
        <Button type="primary" @click="openCreate">
          {{ entityName }} 등록
        </Button>
      </div>
    </Card>

    <Card :body-style="{ padding: 0 }" size="small">
      <Table
        :columns="[...columns, { key: 'action', title: '', width: 120 }]"
        :data-source="filteredRows"
        :loading="loading"
        row-key="id"
        size="small"
      >
        <template #emptyText>
          <Empty :description="`등록된 ${entityName}이(가) 없습니다.`" />
        </template>

        <template #bodyCell="{ column, record, text }">
          <template v-if="column.key === 'action'">
            <Space>
              <Button size="small" type="link" @click="openEdit(record)">
                수정
              </Button>
              <Popconfirm
                cancel-text="취소"
                ok-text="삭제"
                :title="`${entityName}을(를) 삭제할까요?`"
                @confirm="onDelete(record)"
              >
                <Button danger size="small" type="link">삭제</Button>
              </Popconfirm>
            </Space>
          </template>
          <!-- 화면마다 다른 셀은 부모가 채워 넣는다 -->
          <slot :column="column" name="cell" :record="record" :text="text" />
        </template>
      </Table>
    </Card>

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
          <InputNumber
            v-else-if="field.type === 'number'"
            v-model:value="form[field.key]"
            style="width: 100%"
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
