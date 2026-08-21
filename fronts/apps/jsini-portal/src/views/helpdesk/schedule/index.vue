<script lang="ts" setup>
import type { Schedule } from '#/api/helpdesk';

import { computed, onMounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Checkbox,
  DatePicker,
  Form,
  FormItem,
  Input,
  message,
  Modal,
  Popconfirm,
  Select,
  Space,
  Spin,
  Textarea,
} from 'ant-design-vue';

import {
  createSchedule,
  deleteSchedule,
  getSchedules,
  updateSchedule,
} from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';

/**
 * [전체 일정]
 *
 * 원본(ScheduleView.vue)의 월간 달력을 옮겼다.
 * 일정을 다른 날짜로 끌어다 놓으면 시작·종료일이 그만큼 함께 이동한다(원본과 동일).
 */

const helpdesk = useHelpdeskStore();

const loading = ref(false);
const saving = ref(false);
const schedules = ref<Schedule[]>([]);

const today = new Date();
const currentYear = ref(today.getFullYear());
const currentMonth = ref(today.getMonth());

const filterCompanyId = ref<number | undefined>();
const completionFilter = ref<'all' | 'completed' | 'incomplete'>('incomplete');

const COMPLETION_OPTIONS = [
  { label: '미완료', value: 'incomplete' },
  { label: '완료', value: 'completed' },
  { label: '전체', value: 'all' },
];

const modalOpen = ref(false);
const editing = reactive<{
  companyId: number | undefined;
  completedDate: string | undefined;
  description: string;
  endDate: string;
  id: null | number;
  isCommon: boolean;
  isCompleted: boolean;
  startDate: string;
  title: string;
}>({
  companyId: undefined,
  completedDate: undefined,
  description: '',
  endDate: '',
  id: null,
  isCommon: true,
  isCompleted: false,
  startDate: '',
  title: '',
});

/** 'YYYY-MM-DD' 로 자른 날짜 키 */
function dayKey(value?: null | string) {
  return value ? String(value).slice(0, 10) : '';
}

function toIsoDate(d: Date) {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

const monthLabel = computed(
  () => `${currentYear.value}년 ${currentMonth.value + 1}월`,
);

/** 완료 여부 필터를 통과한 일정만 남긴다. */
const visibleSchedules = computed(() =>
  schedules.value.filter((s) => {
    if (completionFilter.value === 'completed') return s.isCompleted;
    if (completionFilter.value === 'incomplete') return !s.isCompleted;
    return true;
  }),
);

/** 달력 격자 — 앞뒤 빈칸을 채워 7의 배수로 만든다. */
const calendarDays = computed(() => {
  const year = currentYear.value;
  const month = currentMonth.value;
  const first = new Date(year, month, 1);
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const leading = first.getDay();

  const cells: { date: null | string; day: null | number }[] = [];
  for (let i = 0; i < leading; i++) cells.push({ date: null, day: null });
  for (let d = 1; d <= daysInMonth; d++) {
    cells.push({ date: toIsoDate(new Date(year, month, d)), day: d });
  }
  while (cells.length % 7 !== 0) cells.push({ date: null, day: null });
  return cells;
});

/** 날짜별로 걸쳐 있는 일정을 모아둔다. 시작~종료 사이 모든 날에 표시한다. */
const schedulesByDate = computed(() => {
  const map = new Map<string, Schedule[]>();

  visibleSchedules.value.forEach((s) => {
    const start = dayKey(s.startDate);
    const end = dayKey(s.endDate) || start;
    if (!start) return;

    const cursor = new Date(`${start}T00:00:00`);
    const last = new Date(`${end}T00:00:00`);
    while (cursor <= last) {
      const key = toIsoDate(cursor);
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(s);
      cursor.setDate(cursor.getDate() + 1);
    }
  });

  return map;
});

function schedulesOf(date: null | string) {
  return date ? (schedulesByDate.value.get(date) ?? []) : [];
}

async function loadData() {
  loading.value = true;
  try {
    schedules.value =
      (await getSchedules(
        filterCompanyId.value ? { companyId: filterCompanyId.value } : undefined,
      )) ?? [];
  } finally {
    loading.value = false;
  }
}

function moveMonth(delta: number) {
  const d = new Date(currentYear.value, currentMonth.value + delta, 1);
  currentYear.value = d.getFullYear();
  currentMonth.value = d.getMonth();
}

function goToday() {
  const now = new Date();
  currentYear.value = now.getFullYear();
  currentMonth.value = now.getMonth();
}

function openCreate(date?: null | string) {
  Object.assign(editing, {
    companyId: undefined,
    completedDate: undefined,
    description: '',
    endDate: date ?? toIsoDate(new Date()),
    id: null,
    isCommon: true,
    isCompleted: false,
    startDate: date ?? toIsoDate(new Date()),
    title: '',
  });
  modalOpen.value = true;
}

function openEdit(schedule: Schedule) {
  Object.assign(editing, {
    companyId: schedule.companyId ?? undefined,
    completedDate: dayKey(schedule.completedDate) || undefined,
    description: schedule.description ?? '',
    endDate: dayKey(schedule.endDate),
    id: schedule.id,
    isCommon: schedule.isCommon ?? true,
    isCompleted: schedule.isCompleted ?? false,
    startDate: dayKey(schedule.startDate),
    title: schedule.title,
  });
  modalOpen.value = true;
}

async function onSave() {
  if (!editing.title.trim()) {
    message.warning('제목을 입력하세요.');
    return;
  }

  const payload = {
    companyId: editing.isCommon ? null : (editing.companyId ?? null),
    completedDate: editing.isCompleted ? editing.completedDate : null,
    description: editing.description,
    endDate: editing.endDate,
    isCommon: editing.isCommon,
    isCompleted: editing.isCompleted,
    startDate: editing.startDate,
    title: editing.title,
  };

  saving.value = true;
  try {
    await (editing.id
      ? updateSchedule(editing.id, payload)
      : createSchedule(payload));
    message.success(`일정을 ${editing.id ? '수정' : '등록'}했습니다.`);
    modalOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function onDelete() {
  if (!editing.id) return;
  await deleteSchedule(editing.id);
  message.success('일정을 삭제했습니다.');
  modalOpen.value = false;
  await loadData();
}

// ── 드래그 앤 드롭 ────────────────────────────────────────
const dragging = ref<{ fromDate: string; schedule: Schedule } | null>(null);

function onDragStart(schedule: Schedule, fromDate: string) {
  dragging.value = { fromDate, schedule };
}

/** 끌어다 놓은 날짜만큼 시작·종료일을 함께 옮긴다(기간 길이는 유지). */
async function onDrop(toDate: null | string) {
  const drag = dragging.value;
  dragging.value = null;
  if (!drag || !toDate || toDate === drag.fromDate) return;

  const shiftDays = Math.round(
    (new Date(`${toDate}T00:00:00`).getTime() -
      new Date(`${drag.fromDate}T00:00:00`).getTime()) /
      86_400_000,
  );

  const shift = (value?: null | string) => {
    const key = dayKey(value);
    if (!key) return key;
    const d = new Date(`${key}T00:00:00`);
    d.setDate(d.getDate() + shiftDays);
    return toIsoDate(d);
  };

  await updateSchedule(drag.schedule.id, {
    ...drag.schedule,
    endDate: shift(drag.schedule.endDate),
    startDate: shift(drag.schedule.startDate),
  });
  message.success('일정을 옮겼습니다.');
  await loadData();
}

onMounted(async () => {
  await helpdesk.loadIdentity();
  await helpdesk.loadOrganizations().catch(() => {});
  await loadData();
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space>
          <Button @click="moveMonth(-1)">◀</Button>
          <span class="min-w-[120px] text-center font-medium">
            {{ monthLabel }}
          </span>
          <Button @click="moveMonth(1)">▶</Button>
          <Button @click="goToday">오늘</Button>
        </Space>

        <Space wrap>
          <Select
            v-model:value="filterCompanyId"
            :options="helpdesk.companyOptions"
            allow-clear
            option-filter-prop="label"
            placeholder="회사"
            show-search
            style="width: 160px"
            @change="loadData"
          />
          <Select
            v-model:value="completionFilter"
            :options="COMPLETION_OPTIONS"
            style="width: 110px"
          />
          <Button type="primary" @click="openCreate()">일정 등록</Button>
        </Space>
      </div>
    </Card>

    <Spin :spinning="loading">
      <Card :body-style="{ padding: '8px' }" size="small">
        <div class="grid grid-cols-7 gap-1">
          <div
            v-for="label in ['일', '월', '화', '수', '목', '금', '토']"
            :key="label"
            class="py-1 text-center text-xs font-medium text-muted-foreground"
          >
            {{ label }}
          </div>

          <div
            v-for="(cell, index) in calendarDays"
            :key="index"
            class="min-h-[110px] rounded border border-border p-1"
            :class="cell.date ? 'bg-background' : 'bg-muted/30'"
            @dragover.prevent
            @drop="onDrop(cell.date)"
          >
            <div
              v-if="cell.day"
              class="mb-1 cursor-pointer text-xs text-muted-foreground"
              @click="openCreate(cell.date)"
            >
              {{ cell.day }}
            </div>

            <div
              v-for="schedule in schedulesOf(cell.date)"
              :key="`${cell.date}-${schedule.id}`"
              class="mb-1 cursor-pointer truncate rounded px-1 py-0.5 text-xs"
              :class="
                schedule.isCompleted
                  ? 'bg-green-100 text-green-800 line-through dark:bg-green-900/40 dark:text-green-200'
                  : 'bg-blue-100 text-blue-800 dark:bg-blue-900/40 dark:text-blue-200'
              "
              draggable="true"
              @click="openEdit(schedule)"
              @dragstart="onDragStart(schedule, cell.date!)"
            >
              {{ schedule.title }}
            </div>
          </div>
        </div>
      </Card>
    </Spin>

    <Modal
      v-model:open="modalOpen"
      :confirm-loading="saving"
      :title="editing.id ? '일정 수정' : '일정 등록'"
      cancel-text="취소"
      ok-text="저장"
      @ok="onSave"
    >
      <Form layout="vertical">
        <FormItem label="제목" required>
          <Input v-model:value="editing.title" />
        </FormItem>
        <FormItem label="설명">
          <Textarea v-model:value="editing.description" :rows="3" />
        </FormItem>
        <FormItem label="시작일">
          <DatePicker
            v-model:value="editing.startDate"
            style="width: 100%"
            value-format="YYYY-MM-DD"
          />
        </FormItem>
        <FormItem label="종료일">
          <DatePicker
            v-model:value="editing.endDate"
            style="width: 100%"
            value-format="YYYY-MM-DD"
          />
        </FormItem>
        <FormItem>
          <Checkbox v-model:checked="editing.isCommon">공통 일정</Checkbox>
        </FormItem>
        <FormItem v-if="!editing.isCommon" label="회사">
          <Select
            v-model:value="editing.companyId"
            :options="helpdesk.companyOptions"
            option-filter-prop="label"
            show-search
          />
        </FormItem>
        <FormItem>
          <Checkbox v-model:checked="editing.isCompleted">완료</Checkbox>
        </FormItem>
        <FormItem v-if="editing.isCompleted" label="완료일">
          <DatePicker
            v-model:value="editing.completedDate"
            style="width: 100%"
            value-format="YYYY-MM-DD"
          />
        </FormItem>
      </Form>

      <template #footer>
        <div class="flex justify-between">
          <Popconfirm
            v-if="editing.id"
            cancel-text="취소"
            ok-text="삭제"
            title="일정을 삭제할까요?"
            @confirm="onDelete"
          >
            <Button danger>삭제</Button>
          </Popconfirm>
          <span v-else></span>

          <Space>
            <Button @click="modalOpen = false">취소</Button>
            <Button :loading="saving" type="primary" @click="onSave">
              저장
            </Button>
          </Space>
        </div>
      </template>
    </Modal>
  </Page>
</template>
