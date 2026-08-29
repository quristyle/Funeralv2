<script lang="ts" setup>
import { computed, reactive, ref } from 'vue';

import { useVbenModal } from '@vben/common-ui';

import {
  Button,
  Checkbox,
  DatePicker,
  Form,
  FormItem,
  Input,
  message,
  Popconfirm,
} from 'ant-design-vue';
import dayjs from 'dayjs';

import { resetBirthday, updateBirthday, upsertBirthday } from '#/api/life/birthday';

/**
 * [생일 등록 · 수정 팝업]
 *
 * 원본(GHUB BirthdayCalendar.vue · MonthlyBirthdayWidget.vue 의 el-dialog)을
 * useVbenModal 로 옮겼다. 목록 · 달력 두 화면이 함께 쓴다.
 *
 * 원본과 다른 점:
 * - 음력 → 올해 양력 환산 미리보기는 korean-lunar-calendar 로 프론트에서 계산했지만,
 *   여기서는 서버가 내려준 올해 발생일(occurrenceDate)을 보여 준다 (라이브러리 안 들임).
 *   따라서 날짜를 고치는 중에는 미리보기가 갱신되지 않고, 저장된 값 기준이다.
 * - id 가 없으면 신규 등록(upsertBirthday)으로 동작한다 (달력 빈 날짜 클릭).
 * - 초기화(resetBirthday) 버튼을 추가했다 — 명단 행은 남기고 생일 정보만 지운다.
 */

/** 팝업으로 넘겨받는 데이터 (id 가 0 이면 신규 등록) */
interface BirthdayEditData {
  id: number;
  subjectId: string;
  name: string;
  birthDate: string;
  isLunar: boolean;
  isCelebrated: boolean;
  /** 올해 실제 발생일 (음력이면 양력 환산) — 미리보기 용 */
  occurrenceDate?: null | string;
}

const emit = defineEmits<{ (e: 'success'): void }>();

const saving = ref(false);

const form = reactive<BirthdayEditData>({
  id: 0,
  subjectId: '',
  name: '',
  birthDate: '',
  isLunar: false,
  isCelebrated: true,
  occurrenceDate: undefined,
});

const isNew = computed(() => !form.id);
const title = computed(() => (isNew.value ? '생일 등록' : '생일 정보 수정'));

/** 서버가 계산해 둔 올해 발생일 안내 (저장된 값 기준) */
const occurrenceHint = computed(() => {
  if (!form.isLunar || !form.occurrenceDate) return '';
  const d = dayjs(form.occurrenceDate);
  return d.isValid() ? `${d.month() + 1}월 ${d.date()}일` : '';
});

const [Modal, modalApi] = useVbenModal<BirthdayEditData>({
  destroyOnClose: true,
  onConfirm: onSave,
  onOpenChange(isOpen) {
    if (!isOpen) return;
    const data = modalApi.getData();
    Object.assign(form, {
      id: data?.id ?? 0,
      subjectId: data?.subjectId ?? '',
      name: data?.name ?? '',
      // 서버 시각은 UTC ISO 일 수 있어 dayjs 로 현지 날짜만 잘라 쓴다.
      birthDate: data?.birthDate ? dayjs(data.birthDate).format('YYYY-MM-DD') : '',
      isLunar: data?.isLunar ?? false,
      isCelebrated: data?.isCelebrated ?? true,
      occurrenceDate: data?.occurrenceDate ?? undefined,
    });
  },
});

async function onSave() {
  if (isNew.value && (!form.subjectId.trim() || !form.name.trim())) {
    message.warning('로그인 ID 와 이름을 입력하세요.');
    return;
  }
  if (!form.birthDate) {
    message.warning('생년월일을 선택하세요.');
    return;
  }

  const payload = {
    subjectId: form.subjectId,
    name: form.name,
    birthDate: form.birthDate,
    isLunar: form.isLunar,
    isCelebrated: form.isCelebrated,
  };

  saving.value = true;
  modalApi.lock();
  try {
    await (isNew.value ? upsertBirthday(payload) : updateBirthday(form.id, payload));
    message.success(`생일 정보가 ${isNew.value ? '등록' : '수정'}되었습니다.`);
    modalApi.close();
    emit('success');
  } finally {
    saving.value = false;
    modalApi.lock(false);
  }
}

/** 생일 정보 초기화 — 명단 행은 남는다 */
async function onReset() {
  if (!form.id) return;
  await resetBirthday(form.id);
  message.success('생일 정보를 초기화했습니다.');
  modalApi.close();
  emit('success');
}
</script>

<template>
  <Modal :title="title" :confirm-loading="saving">
    <div class="px-4 pt-2">
      <Form layout="vertical">
        <!-- 기존 인원이면 이름 · ID 는 표시만 한다 (원본과 동일) -->
        <div v-if="!isNew" class="mb-4 text-sm text-muted-foreground">
          <span class="font-bold text-foreground">{{ form.name }}</span>
          ({{ form.subjectId }})
        </div>

        <template v-else>
          <FormItem label="로그인 ID" required>
            <Input v-model:value="form.subjectId" placeholder="예: hong.gildong" />
          </FormItem>
          <FormItem label="이름" required>
            <Input v-model:value="form.name" placeholder="이름" />
          </FormItem>
        </template>

        <FormItem label="생년월일 (입력값이 곧 음력/양력 기준이 됩니다)" required>
          <DatePicker
            v-model:value="form.birthDate"
            style="width: 100%"
            value-format="YYYY-MM-DD"
          />
          <!-- 음력 → 올해 양력 환산 안내: 서버가 계산한 발생일(저장값 기준) -->
          <div
            v-if="occurrenceHint"
            class="mt-2 rounded border border-border bg-muted/30 p-3 text-sm text-muted-foreground"
          >
            <div class="mb-1 font-semibold text-foreground">올해 생일 변환 정보</div>
            저장된 음력 생일 기준, 올해는
            <span class="font-bold text-primary">양력 {{ occurrenceHint }}</span>
            입니다. (날짜를 바꾸면 저장 후에 다시 계산됩니다)
          </div>
        </FormItem>

        <FormItem>
          <Checkbox v-model:checked="form.isLunar">음력 (Lunar)</Checkbox>
        </FormItem>
        <FormItem>
          <Checkbox v-model:checked="form.isCelebrated">축하 대상에 포함</Checkbox>
        </FormItem>
      </Form>
    </div>

    <template #prepend-footer>
      <div class="flex-auto">
        <Popconfirm
          v-if="!isNew"
          v-perm:delete
          cancel-text="취소"
          ok-text="초기화"
          title="생일 정보를 초기화할까요? (명단 행은 남습니다)"
          @confirm="onReset"
        >
          <Button danger>초기화</Button>
        </Popconfirm>
      </div>
    </template>
  </Modal>
</template>
