<script lang="ts" setup>
import type { Schedule } from '#/api/helpdesk';

import { computed, onMounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Checkbox,
  DatePicker,
  Empty,
  Form,
  FormItem,
  Input,
  message,
  Modal,
  Popconfirm,
  RadioButton,
  RadioGroup,
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
 * [일정 달력]
 *
 * 원본(JinReception ScheduleView.vue `/schedules`, MyScheduleView.vue `/my-schedules`).
 * 두 화면은 "내 회사 일정만 보는가"만 다르고 달력·드래그·편집 동작이 같아 하나로 합쳤다.
 *
 * 왼쪽은 달력, 오른쪽은 그달 상관없이 전체 일정을 시작일 순으로 늘어놓은 목록이다.
 * 목록에서는 마우스를 올리면 완료 처리와 삭제를 바로 할 수 있다.
 * 달력에서 일정을 다른 날로 끌어다 놓으면 옮길지 복사할지 물어본다.
 */

const props = withDefaults(
  defineProps<{
    /** 내 소속 회사 일정과 공통 일정만 보여줄지 (원본 MyScheduleView) */
    myOnly?: boolean;
  }>(),
  { myOnly: false },
);

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
  { label: '전체', value: 'all' },
  { label: '미완료', value: 'incomplete' },
  { label: '완료', value: 'completed' },
];

const DAY_LABELS = ['일', '월', '화', '수', '목', '금', '토'];

/**
 * 마지막으로 탭(클릭)한 달력 칸.
 * 모바일은 더블탭이 불편해서, 칸을 한 번 탭해 날짜를 고른 뒤
 * 도구줄의 [일정 등록] 버튼으로 등록한다 (데스크톱 더블클릭은 그대로 둔다).
 */
const selectedDate = ref<null | string>(null);

const modalOpen = ref(false);
const editing = reactive<{
  companyId: number | undefined;
  completedDate: string | undefined;
  description: string;
  endDate: string;
  id: null | string;
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

function toIsoDate(d: Date) {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/**
 * 'YYYY-MM-DD' 날짜 키.
 *
 * 서버는 UTC(`2026-02-28T15:00:00Z`)로 내려준다. 이걸 문자열로 잘라 쓰면
 * 한국 시각 기준 3월 1일 일정이 2월 28일로 하루 밀린다.
 * 그래서 Date 로 파싱해 현지 시각의 연·월·일을 쓴다(원본도 `new Date()` 로 다뤘다).
 */
function dayKey(value?: null | string) {
  if (!value) return '';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return String(value).slice(0, 10);
  return toIsoDate(d);
}

const todayKey = toIsoDate(new Date());

const monthLabel = computed(
  () => `${currentYear.value}년 ${currentMonth.value + 1}월`,
);

/** 회사 아이디로 회사명을 찾는다. 공통 일정은 '공통'. */
function companyName(id?: null | number) {
  if (!id) return '공통';
  const found = helpdesk.companies.find((c) => c.id === id);
  return found?.name ?? '알수없음';
}

/** 목록·달력에 함께 쓰는 범위 필터 (내 일정 여부) */
function inScope(s: Schedule) {
  if (!props.myOnly || s.isCommon) return true;
  return (
    helpdesk.companyId !== undefined &&
    Number(s.companyId) === Number(helpdesk.companyId)
  );
}

/** 완료 여부와 범위 필터를 통과한 일정 */
const visibleSchedules = computed(() =>
  schedules.value.filter((s) => {
    if (completionFilter.value === 'completed' && !s.isCompleted) return false;
    if (completionFilter.value === 'incomplete' && s.isCompleted) return false;
    return inScope(s);
  }),
);

/** 오른쪽 목록. 시작일 순으로 늘어놓는다. */
const sortedSchedules = computed(() =>
  [...visibleSchedules.value].sort((a, b) => {
    const ak = dayKey(a.startDate);
    const bk = dayKey(b.startDate);
    return ak === bk ? a.title.localeCompare(b.title) : ak.localeCompare(bk);
  }),
);

/** 상단 요약 건수 */
const counts = computed(() => {
  const list = schedules.value.filter((s) => inScope(s));
  const done = list.filter((s) => s.isCompleted).length;
  return { done, todo: list.length - done, total: list.length };
});

/**
 * 달력 격자. 원본과 같이 항상 42칸(6주)을 만들고,
 * 앞뒤로 걸친 이전·다음 달 날짜도 흐리게 보여준다.
 * 그 칸에도 일정이 뜨고 드롭도 된다 — 달 경계를 넘겨 옮길 수 있어야 하기 때문.
 */
const calendarDays = computed(() => {
  const first = new Date(currentYear.value, currentMonth.value, 1);
  const cursor = new Date(first);
  cursor.setDate(1 - first.getDay());

  const cells: {
    date: string;
    day: number;
    isCurrentMonth: boolean;
    isSaturday: boolean;
    isSunday: boolean;
    isToday: boolean;
  }[] = [];

  for (let i = 0; i < 42; i++) {
    const dow = cursor.getDay();
    const date = toIsoDate(cursor);
    cells.push({
      date,
      day: cursor.getDate(),
      isCurrentMonth: cursor.getMonth() === currentMonth.value,
      isSaturday: dow === 6,
      isSunday: dow === 0,
      isToday: date === todayKey,
    });
    cursor.setDate(cursor.getDate() + 1);
  }
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

function schedulesOf(date: string) {
  return schedulesByDate.value.get(date) ?? [];
}

/** 목록에 보이는 기간 표기. 하루짜리면 하나만. */
function formatDateRange(s: Schedule) {
  const opts: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short' };
  const start = dayKey(s.startDate);
  const end = dayKey(s.endDate) || start;
  if (!start) return '';

  const startStr = new Date(`${start}T00:00:00`).toLocaleDateString(
    'ko-KR',
    opts,
  );
  const endStr = new Date(`${end}T00:00:00`).toLocaleDateString('ko-KR', opts);
  return startStr === endStr ? startStr : `${startStr} - ${endStr}`;
}

/** 일정 칩에 달아 줄 안내 */
function scheduleHint(s: Schedule) {
  const owner = s.isCommon ? '공통' : companyName(s.companyId);
  return `[${owner}] ${s.title}${s.isCompleted ? ' (완료)' : ''}`;
}

async function loadData() {
  loading.value = true;
  try {
    // '내 일정'은 서버에서 내 회사 것으로 먼저 좁힌다.
    const companyId = props.myOnly
      ? helpdesk.companyId
      : filterCompanyId.value;

    schedules.value =
      (await getSchedules(companyId ? { companyId } : undefined)) ?? [];
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

function openCreate(date?: string) {
  // 날짜를 안 주면 마지막으로 탭한 칸(없으면 오늘)으로 연다.
  const day = date ?? selectedDate.value ?? toIsoDate(new Date());
  Object.assign(editing, {
    companyId: undefined,
    completedDate: undefined,
    description: '',
    endDate: day,
    id: null,
    isCommon: true,
    isCompleted: false,
    startDate: day,
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

/** 완료로 바꾸면 완료일을 오늘로 채워 준다(원본 onCompletedChange). */
function onCompletedChange(checked: boolean) {
  editing.completedDate = checked
    ? (editing.completedDate ?? toIsoDate(new Date()))
    : undefined;
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

// ── 목록의 퀵 액션 ────────────────────────────────────────

/** 완료/미완료 토글. 완료로 바꾸면 완료일을 오늘로 찍는다. */
async function toggleComplete(s: Schedule) {
  const next = !s.isCompleted;
  try {
    await updateSchedule(s.id, {
      ...s,
      completedDate: next ? toIsoDate(new Date()) : null,
      isCompleted: next,
    });
    message.success(`일정을 ${next ? '완료' : '미완료'} 처리했습니다.`);
    await loadData();
  } catch {
    message.error('상태 변경에 실패했습니다.');
  }
}

async function quickDelete(s: Schedule) {
  try {
    await deleteSchedule(s.id);
    message.success('일정을 삭제했습니다.');
    await loadData();
  } catch {
    message.error('삭제에 실패했습니다.');
  }
}

// ── 드래그 앤 드롭 ────────────────────────────────────────
// HTML5 drag&drop 은 모바일 브라우저에서 dragstart 자체가 안 떠서 데스크톱 전용이다.
// 모바일은 일정을 탭해 편집 팝업을 열고 시작일·종료일 DatePicker 로 날짜를 바꾼다.

const dragging = ref<{ fromDate: string; schedule: Schedule } | null>(null);
/** 드롭 대상으로 지나가는 칸. 하이라이트에 쓴다. */
const dragOverDate = ref<null | string>(null);

/** 드롭 뒤 '이동/복사'를 고르는 사이 물고 있는 정보 */
const dropContext = ref<{
  fromDate: string;
  schedule: Schedule;
  toDate: string;
} | null>(null);
const dropModalOpen = ref(false);
const processingDrop = ref(false);

function onDragStart(schedule: Schedule, fromDate: string) {
  dragging.value = { fromDate, schedule };
}

function onDragOver(date: string) {
  dragOverDate.value = date;
}

function onDragLeave() {
  dragOverDate.value = null;
}

/** 놓은 날짜를 기억해 두고 이동할지 복사할지 물어본다(원본과 동일). */
function onDrop(toDate: string) {
  const drag = dragging.value;
  dragging.value = null;
  dragOverDate.value = null;
  if (!drag || toDate === drag.fromDate) return;

  dropContext.value = { fromDate: drag.fromDate, schedule: drag.schedule, toDate };
  dropModalOpen.value = true;
}

/**
 * 끌어다 놓은 날짜만큼 시작·종료일을 함께 옮긴다(기간 길이는 유지).
 * @param isCopy 참이면 원본을 남기고 새 일정으로 만든다.
 */
async function processScheduleMove(isCopy: boolean) {
  const ctx = dropContext.value;
  if (!ctx) return;

  const shiftDays = Math.round(
    (new Date(`${ctx.toDate}T00:00:00`).getTime() -
      new Date(`${ctx.fromDate}T00:00:00`).getTime()) /
      86_400_000,
  );

  const shift = (value?: null | string) => {
    const key = dayKey(value);
    if (!key) return key;
    const d = new Date(`${key}T00:00:00`);
    d.setDate(d.getDate() + shiftDays);
    return toIsoDate(d);
  };

  const moved = {
    ...ctx.schedule,
    endDate: shift(ctx.schedule.endDate),
    startDate: shift(ctx.schedule.startDate),
  };

  processingDrop.value = true;
  try {
    if (isCopy) {
      const { id: _id, ...rest } = moved;
      await createSchedule(rest);
      message.success('일정을 복사했습니다.');
    } else {
      await updateSchedule(ctx.schedule.id, moved);
      message.success('일정을 옮겼습니다.');
    }
    dropModalOpen.value = false;
    await loadData();
  } catch {
    message.error(`일정 ${isCopy ? '복사' : '이동'}에 실패했습니다.`);
  } finally {
    processingDrop.value = false;
  }
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

    <!-- 상단: 월 이동 + 필터 -->
    <Card class="mb-3" size="small">
      <div class="mb-2 flex flex-wrap gap-3 text-xs text-muted-foreground">
        <span>전체 {{ counts.total }}건</span>
        <span>미완료 {{ counts.todo }}건</span>
        <span>완료 {{ counts.done }}건</span>
      </div>

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
            v-if="!props.myOnly"
            v-model:value="filterCompanyId"
            :options="helpdesk.companyOptions"
            allow-clear
            option-filter-prop="label"
            placeholder="회사"
            show-search
            style="width: 160px"
            @change="loadData"
          />
          <RadioGroup v-model:value="completionFilter" button-style="solid">
            <RadioButton
              v-for="opt in COMPLETION_OPTIONS"
              :key="opt.value"
              :value="opt.value"
            >
              {{ opt.label }}
            </RadioButton>
          </RadioGroup>
          <!-- 마지막으로 탭한 날짜(없으면 오늘)로 연다 — 모바일의 단일 탭 등록 경로 -->
          <Button v-perm:create type="primary" @click="openCreate()">
            일정 등록
          </Button>
        </Space>
      </div>
    </Card>

    <Spin :spinning="loading">
      <div class="flex flex-col gap-3 lg:flex-row">
        <!-- 왼쪽: 달력 -->
        <Card
          :body-style="{ padding: '8px' }"
          class="min-w-0 flex-1"
          size="small"
        >
          <div class="grid grid-cols-7 gap-1">
            <div
              v-for="(label, index) in DAY_LABELS"
              :key="label"
              class="py-1 text-center text-xs font-semibold"
              :class="
                index === 0
                  ? 'text-red-500'
                  : index === 6
                    ? 'text-blue-500'
                    : 'text-muted-foreground'
              "
            >
              {{ label }}
            </div>

            <!-- 데스크톱은 빈 칸 더블클릭으로 바로 등록,
                 모바일은 칸을 한 번 탭해 고른 뒤 도구줄의 [일정 등록]을 누른다 -->
            <div
              v-for="cell in calendarDays"
              :key="cell.date"
              class="relative min-h-[110px] rounded border border-border p-1 transition-colors"
              :class="[
                cell.isCurrentMonth ? 'bg-background' : 'bg-muted/30 opacity-60',
                dragOverDate === cell.date ? 'ring-2 ring-primary' : '',
                selectedDate === cell.date ? 'ring-1 ring-primary/60' : '',
              ]"
              @click="selectedDate = cell.date"
              @dblclick="openCreate(cell.date)"
              @dragleave="onDragLeave"
              @dragover.prevent="onDragOver(cell.date)"
              @drop="onDrop(cell.date)"
            >
              <!-- 날짜 -->
              <span
                class="absolute right-1 top-1 z-10 flex size-5 select-none items-center justify-center rounded-sm text-[10px] font-semibold"
                :class="[
                  cell.isToday
                    ? 'bg-blue-500 text-white'
                    : cell.isSunday
                      ? 'text-red-500'
                      : cell.isSaturday
                        ? 'text-blue-500'
                        : 'text-muted-foreground',
                ]"
              >
                {{ cell.day }}
              </span>

              <!-- 그날 일정 -->
              <div class="max-h-[92px] space-y-0.5 overflow-y-auto pr-5 pt-0.5">
                <div
                  v-for="schedule in schedulesOf(cell.date)"
                  :key="`${cell.date}-${schedule.id}`"
                  class="flex cursor-pointer items-center gap-1 truncate rounded-sm border-l-2 px-1 py-0.5 text-[10px] leading-tight transition-all hover:brightness-95"
                  :class="[
                    schedule.isCommon
                      ? 'border-blue-500 bg-blue-100/70 text-blue-900 dark:bg-blue-900/40 dark:text-blue-100'
                      : 'border-green-500 bg-green-100/70 text-green-900 dark:bg-green-900/40 dark:text-green-100',
                    schedule.isCompleted ? 'opacity-40 grayscale' : '',
                  ]"
                  draggable="true"
                  :title="scheduleHint(schedule)"
                  @click.stop="openEdit(schedule)"
                  @dragstart="onDragStart(schedule, cell.date)"
                >
                  <span v-if="schedule.isCompleted" class="shrink-0">✓</span>
                  <span :class="{ 'line-through': schedule.isCompleted }">
                    {{ schedule.title }}
                  </span>
                </div>
              </div>
            </div>
          </div>
        </Card>

        <!-- 오른쪽: 일정 목록 -->
        <Card
          :body-style="{ padding: '8px' }"
          class="w-full shrink-0 lg:w-80"
          size="small"
        >
          <div
            class="mb-2 flex items-center justify-between border-b border-border pb-2"
          >
            <span class="text-sm font-semibold">일정 목록</span>
            <span class="font-mono text-xs text-muted-foreground">
              {{ sortedSchedules.length }}
            </span>
          </div>

          <Empty
            v-if="sortedSchedules.length === 0"
            :image="Empty.PRESENTED_IMAGE_SIMPLE"
            description="등록된 일정이 없습니다."
          />

          <div v-else class="max-h-[620px] space-y-1.5 overflow-y-auto pr-1">
            <button
              v-for="schedule in sortedSchedules"
              :key="schedule.id"
              class="group relative w-full rounded border-l-4 p-2.5 text-left shadow-sm transition-all hover:bg-accent"
              :class="[
                schedule.isCommon
                  ? 'border-blue-500 bg-blue-50/30 dark:bg-blue-900/10'
                  : 'border-green-500 bg-green-50/30 dark:bg-green-900/10',
                schedule.isCompleted ? 'opacity-50 grayscale' : '',
              ]"
              type="button"
              @click="openEdit(schedule)"
            >
              <div class="flex items-start justify-between gap-2">
                <div class="min-w-0 flex-1">
                  <div class="mb-1 flex items-center gap-1.5">
                    <span
                      class="rounded px-1.5 text-[9px] font-bold uppercase"
                      :class="
                        schedule.isCompleted
                          ? 'text-muted-foreground'
                          : schedule.isCommon
                            ? 'text-blue-500'
                            : 'text-green-500'
                      "
                    >
                      {{
                        schedule.isCommon
                          ? '공통'
                          : companyName(schedule.companyId)
                      }}
                    </span>
                    <span class="text-[10px] font-medium text-muted-foreground">
                      {{ formatDateRange(schedule) }}
                    </span>
                  </div>
                  <div
                    class="truncate text-[13px] font-semibold leading-tight"
                    :class="{ 'line-through': schedule.isCompleted }"
                  >
                    {{ schedule.title }}
                  </div>
                </div>

                <!-- 마우스를 올리면 보이는 퀵 액션 — hover 가 없는 모바일(<768px)에서는 항상 보인다 -->
                <div
                  class="flex items-center gap-1 opacity-0 transition-opacity group-hover:opacity-100 max-md:opacity-100"
                >
                  <Button
                    v-perm:update
                    size="small"
                    :title="
                      schedule.isCompleted ? '미완료로 변경' : '완료 처리'
                    "
                    type="text"
                    @click.stop="toggleComplete(schedule)"
                  >
                    {{ schedule.isCompleted ? '↺' : '✓' }}
                  </Button>
                  <Popconfirm
                    v-perm:delete
                    cancel-text="취소"
                    ok-text="삭제"
                    :title="`'${schedule.title}' 일정을 삭제할까요?`"
                    @confirm="quickDelete(schedule)"
                  >
                    <Button
                      danger
                      size="small"
                      title="삭제"
                      type="text"
                      @click.stop
                    >
                      ✕
                    </Button>
                  </Popconfirm>
                </div>
              </div>
            </button>
          </div>
        </Card>
      </div>
    </Spin>

    <!-- 일정 등록·수정 -->
    <Modal
      v-model:open="modalOpen"
      :confirm-loading="saving"
      cancel-text="취소"
      ok-text="저장"
      :title="editing.id ? '일정 수정' : '일정 등록'"
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
        <FormItem
          extra="회사를 선택하지 않으면 '공통 일정'으로 등록됩니다."
        >
          <Checkbox v-model:checked="editing.isCommon">공통 일정</Checkbox>
        </FormItem>
        <FormItem v-if="!editing.isCommon" label="회사">
          <Select
            v-model:value="editing.companyId"
            allow-clear
            :options="helpdesk.companyOptions"
            option-filter-prop="label"
            show-search
          />
        </FormItem>
        <FormItem>
          <Checkbox
            :checked="editing.isCompleted"
            @change="
              (e: any) => {
                editing.isCompleted = e.target.checked;
                onCompletedChange(e.target.checked);
              }
            "
          >
            처리 완료
          </Checkbox>
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
            v-perm:delete
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

    <!-- 드롭 후: 옮길지 복사할지 -->
    <Modal
      v-model:open="dropModalOpen"
      :footer="null"
      title="일정 이동 / 복사"
      width="360px"
    >
      <p class="mb-4 text-center">선택한 일정을 어떻게 처리할까요?</p>
      <div class="flex flex-col gap-2">
        <Button
          block
          :loading="processingDrop"
          type="primary"
          @click="processScheduleMove(false)"
        >
          이동하기
        </Button>
        <Button
          block
          :loading="processingDrop"
          @click="processScheduleMove(true)"
        >
          복사하기
        </Button>
        <Button block type="text" @click="dropModalOpen = false">취소</Button>
      </div>
    </Modal>
  </Page>
</template>
