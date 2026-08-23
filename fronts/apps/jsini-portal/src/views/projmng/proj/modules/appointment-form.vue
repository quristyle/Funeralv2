<script setup lang="ts">
/**
 * [일정 편집]
 *
 * 원본: ProjMngWasm `Pages/Proj/EditAppointmentPage.razor`.
 * 프로시저: `sp_proj_wbs_exec`
 *
 * 스케줄러에서 빈 칸을 끌면 새 일정, 일정을 누르면 수정으로 열린다.
 * 상태(`wbs_state`)는 원본과 같은 규칙으로 자동 계산한다.
 *   · 완료 체크 → COMP (개발 시작/종료일이 없으면 오늘로 채운다)
 *   · 개발 시작·종료 모두 없음 → READY
 *   · 종료만 없음 → RUNNING
 *   · 둘 다 있음 → COMP
 */
import type { ProjMngRow } from '#/api/projmng';

import { computed, ref, watch } from 'vue';

import {
  Button,
  Checkbox,
  DatePicker,
  Form,
  FormItem,
  Input,
  Modal,
  Textarea,
} from 'ant-design-vue';
import dayjs from 'dayjs';

import { dbDelete, dbSave } from '#/api/projmng';

import { CodeSelect } from '../../shared';

interface Props {
  open: boolean;
  /** 수정할 일정. 없으면 새로 만든다 */
  appointment?: null | ProjMngRow;
  /** 새 일정의 기본 시작·종료일 */
  start?: null | string;
  end?: null | string;
  projectCode?: string;
}

const props = withDefaults(defineProps<Props>(), {
  appointment: null,
  start: null,
  end: null,
  projectCode: '',
});

const emit = defineEmits<{
  (e: 'update:open', value: boolean): void;
  /** 저장·삭제가 끝났다. 부모가 목록을 다시 읽는다 */
  (e: 'done', action: 'delete' | 'save'): void;
}>();

const PROC = 'sp_proj_wbs_exec';

const model = ref<ProjMngRow>({});
const isComplete = ref(false);
const saving = ref(false);

const isNew = computed(() => !model.value.wbs_id);

watch(
  () => [props.open, props.appointment],
  () => {
    if (!props.open) return;

    model.value = props.appointment
      ? { ...props.appointment }
      : {
          plan_sdt: props.start ?? '',
          plan_edt: props.end ?? props.start ?? '',
          prj_rid: props.projectCode,
          wbs_state: 'READY',
        };
    isComplete.value = String(model.value.wbs_state ?? '') === 'COMP';
  },
  { immediate: true },
);

function asDate(value: unknown) {
  if (!value) return undefined;
  const d = dayjs(String(value));
  return d.isValid() ? d : undefined;
}

function setDate(name: string, value: any) {
  model.value[name] = value ? dayjs(value).format('YYYY-MM-DD') : '';
}

/** 원본 `OnSubmit` 의 상태 계산 규칙을 그대로 옮겼다. */
function resolveState() {
  const row = model.value;
  const today = dayjs().format('YYYY-MM-DD');

  if (isComplete.value) {
    if (!row.dev_sdt) row.dev_sdt = today;
    if (!row.dev_edt) row.dev_edt = today;
    if (String(row.plan_sdt ?? '') > String(row.dev_edt)) row.plan_sdt = row.dev_edt;
    if (String(row.plan_edt ?? '') > String(row.dev_edt)) row.plan_edt = row.dev_edt;
    row.wbs_state = 'COMP';
    return;
  }

  if (!row.dev_sdt && !row.dev_edt) row.wbs_state = 'READY';
  else if (row.dev_edt) row.wbs_state = 'COMP';
  else row.wbs_state = 'RUNNING';
}

async function submit() {
  resolveState();
  saving.value = true;
  try {
    const saved = await dbSave(PROC, model.value, [
      { ...model.value, quri_ischange: true },
    ]);
    if (saved.code < 0) return;

    // 새로 만든 경우 서버가 준 키를 받아 둔다 (원본과 같다).
    const newId = saved.data?.[0]?.wbs_id;
    if (isNew.value && newId) model.value.wbs_id = newId;

    emit('done', 'save');
    emit('update:open', false);
  } finally {
    saving.value = false;
  }
}

/** 오늘 날짜로 계획을 옮기고 바로 저장한다 (원본 OnTodayAndSave). */
async function submitToday() {
  const today = dayjs().format('YYYY-MM-DD');
  model.value.plan_sdt = today;
  model.value.plan_edt = today;
  await submit();
}

async function remove() {
  model.value.wbs_state = 'DELETE';
  const deleted = await dbDelete(PROC, model.value);
  if (deleted.code < 0) return;
  emit('done', 'delete');
  emit('update:open', false);
}
</script>

<template>
  <Modal
    :open="open"
    :title="isNew ? '일정 추가' : '일정 수정'"
    :confirm-loading="saving"
    width="560px"
    @cancel="emit('update:open', false)"
  >
    <Form :label-col="{ span: 6 }" size="small">
      <FormItem label="프로젝트">
        <CodeSelect
          :model-value="String(model.prj_rid ?? '')"
          code-id="projlist"
          :auto-select-first="false"
          width="100%"
          @update:model-value="(v) => (model.prj_rid = v)"
        />
      </FormItem>
      <FormItem label="구분">
        <CodeSelect
          :model-value="String(model.schedule_type ?? '')"
          code-id="schedule_type"
          :auto-select-first="false"
          width="100%"
          @update:model-value="(v) => (model.schedule_type = v)"
        />
      </FormItem>
      <FormItem label="제목">
        <Input
          :value="String(model.wbs_nm ?? '')"
          @update:value="(v) => (model.wbs_nm = v)"
        />
      </FormItem>
      <FormItem label="담당자">
        <Input
          :value="String(model.dev_user ?? '')"
          @update:value="(v) => (model.dev_user = v)"
        />
      </FormItem>
      <FormItem label="계획 시작">
        <DatePicker
          :value="asDate(model.plan_sdt)"
          class="w-full"
          @change="(v) => setDate('plan_sdt', v)"
        />
      </FormItem>
      <FormItem label="계획 종료">
        <DatePicker
          :value="asDate(model.plan_edt)"
          class="w-full"
          @change="(v) => setDate('plan_edt', v)"
        />
      </FormItem>
      <FormItem label="개발 시작">
        <DatePicker
          :value="asDate(model.dev_sdt)"
          class="w-full"
          @change="(v) => setDate('dev_sdt', v)"
        />
      </FormItem>
      <FormItem label="개발 종료">
        <DatePicker
          :value="asDate(model.dev_edt)"
          class="w-full"
          @change="(v) => setDate('dev_edt', v)"
        />
      </FormItem>
      <FormItem label="완료">
        <Checkbox v-model:checked="isComplete">완료 처리</Checkbox>
        <span class="text-muted-foreground ml-2 text-xs">
          현재 상태: {{ model.wbs_state ?? '-' }}
        </span>
      </FormItem>
      <FormItem label="비고">
        <Textarea
          :value="String(model.remark ?? '')"
          :rows="3"
          @update:value="(v) => (model.remark = v)"
        />
      </FormItem>
    </Form>

    <template #footer>
      <Button v-if="!isNew" danger size="small" @click="remove">삭제</Button>
      <Button size="small" @click="submitToday">오늘로 옮겨 저장</Button>
      <Button size="small" @click="emit('update:open', false)">취소</Button>
      <Button size="small" type="primary" :loading="saving" @click="submit">
        저장
      </Button>
    </template>
  </Modal>
</template>
