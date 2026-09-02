<script setup lang="ts">
/**
 * [일정 (스케줄러)]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjScheduler.razor` (`/proj-scheduler`).
 * 프로시저: `sp_proj_wbs_exec`
 *
 * WBS 항목을 달 단위 달력에 뿌린다. 빈 칸을 누르면 새 일정,
 * 일정을 누르면 수정 창이 열린다. 일정을 끌어 다른 날로 옮기면 바로 저장한다.
 *
 * 원본은 Radzen Scheduler 를 썼는데 포털에 그 부품이 없다. 달력은 직접 그렸다 —
 * 원본이 실제로 쓰던 것은 월 뷰와 상태별 색, 드래그 이동뿐이라 옮기는 데 무리가 없었다.
 *
 * 모바일(터치) 대응: HTML5 drag&drop 과 더블클릭은 모바일 브라우저에서 안 되므로,
 * 칸을 한 번 탭해 날짜를 고른 뒤 도구줄의 [일정 등록]으로 새 일정을 만들고,
 * 날짜 이동은 일정 탭 → 편집 팝업의 계획 시작·종료 DatePicker 로 한다.
 */
import type { ProjMngRow } from '#/api/projmng';

import { computed, ref } from 'vue';

import { Page } from '@vben/common-ui';

import dayjs from 'dayjs';

import { dbCont, dbSave } from '#/api/projmng';

import GridIconButton from '#/components/GridIconButton.vue';
import { CodeSelect, SearchBar } from '../shared';
import AppointmentForm from './modules/appointment-form.vue';

const PROC = 'sp_proj_wbs_exec';

const projectCode = ref('');
const completeState = ref('');
const scheduleType = ref('');

const cursor = ref(dayjs().startOf('month'));
const rows = ref<ProjMngRow[]>([]);
const loading = ref(false);

const formOpen = ref(false);
const editing = ref<null | ProjMngRow>(null);
const draftStart = ref<null | string>(null);

/**
 * 마지막으로 탭(클릭)한 달력 칸.
 * 모바일은 더블탭이 불편해서, 칸을 한 번 탭해 날짜를 고른 뒤
 * 도구줄의 [일정 등록] 버튼으로 등록한다 (데스크톱 더블클릭은 그대로 둔다).
 */
const selectedDate = ref<dayjs.Dayjs | null>(null);

/** 상태별 색. 원본이 쓰던 의미(준비/진행/지연/완료)를 그대로 옮겼다. */
const STATE_STYLE: Record<string, string> = {
  READY: 'bg-amber-500/80 text-white',
  RUNNING: 'bg-sky-500/80 text-white',
  OVER: 'bg-red-500/80 text-white',
  COMP: 'bg-slate-400/80 text-white',
  DELETE: 'bg-red-700/80 text-white line-through',
};

async function search() {
  loading.value = true;
  try {
    const result = await dbCont(PROC, {
      prj_rid: projectCode.value,
      compstat: completeState.value,
      schedule_type: scheduleType.value,
    });
    rows.value = result.data ?? [];
  } finally {
    loading.value = false;
  }
}

/** 달력에 그릴 6주 격자. 월 시작 주의 일요일부터 42칸이다. */
const weeks = computed(() => {
  const first = cursor.value.startOf('month').startOf('week');
  return Array.from({ length: 6 }, (_, w) =>
    Array.from({ length: 7 }, (_, d) => first.add(w * 7 + d, 'day')),
  );
});

/** 날짜별 일정. 계획 시작~종료 구간에 걸치는 날 모두에 표시한다. */
const byDate = computed(() => {
  const map = new Map<string, ProjMngRow[]>();

  rows.value.forEach((row) => {
    const from = dayjs(String(row.plan_sdt ?? ''));
    const to = dayjs(String(row.plan_edt ?? row.plan_sdt ?? ''));
    if (!from.isValid()) return;

    const last = to.isValid() && to.isAfter(from) ? to : from;
    for (let d = from; !d.isAfter(last, 'day'); d = d.add(1, 'day')) {
      const key = d.format('YYYY-MM-DD');
      const bucket = map.get(key);
      if (bucket) bucket.push(row);
      else map.set(key, [row]);
    }
  });

  return map;
});

function eventsOf(date: dayjs.Dayjs) {
  return byDate.value.get(date.format('YYYY-MM-DD')) ?? [];
}

function openNew(date: dayjs.Dayjs) {
  editing.value = null;
  draftStart.value = date.format('YYYY-MM-DD');
  formOpen.value = true;
}

function openEdit(row: ProjMngRow) {
  editing.value = row;
  draftStart.value = null;
  formOpen.value = true;
}

/**
 * 드래그로 옮긴다. 기간은 유지하고 시작일만 옮긴 뒤 바로 저장한다 (원본과 같다).
 * HTML5 drag&drop 은 모바일에서 dragstart 자체가 안 떠 데스크톱 전용이다 —
 * 모바일은 편집 팝업(AppointmentForm)의 계획 시작·종료로 날짜를 바꾼다.
 */
async function onDrop(date: dayjs.Dayjs, event: DragEvent) {
  const wbsId = event.dataTransfer?.getData('text/plain');
  const row = rows.value.find((item) => String(item.wbs_id ?? '') === wbsId);
  if (!row) return;

  const from = dayjs(String(row.plan_sdt ?? ''));
  const to = dayjs(String(row.plan_edt ?? row.plan_sdt ?? ''));
  const span = from.isValid() && to.isValid() ? to.diff(from, 'day') : 0;

  row.plan_sdt = date.format('YYYY-MM-DD');
  row.plan_edt = date.add(Math.max(span, 0), 'day').format('YYYY-MM-DD');

  await dbSave(PROC, row, [{ ...row, quri_ischange: true }]);
  await search();
}

function onDragStart(row: ProjMngRow, event: DragEvent) {
  event.dataTransfer?.setData('text/plain', String(row.wbs_id ?? ''));
}
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <SearchBar class="mb-2">
      <CodeSelect v-model="projectCode" code-id="projlist" @change="search" />
      <CodeSelect v-model="completeState" code-id="compstat" show-all />
      <CodeSelect v-model="scheduleType" code-id="schedule_type" show-all />
      <template #actions>
        <GridIconButton
          icon="vxe-icon-arrow-left"
          title="이전"
          @click="cursor = cursor.subtract(1, 'month')"
        />
        <span class="w-24 text-center text-sm font-semibold">
          {{ cursor.format('YYYY-MM') }}
        </span>
        <GridIconButton
          icon="vxe-icon-arrow-right"
          title="다음"
          @click="cursor = cursor.add(1, 'month')"
        />
        <!-- 마지막으로 탭한 날짜(없으면 오늘)로 연다 — 모바일의 단일 탭 등록 경로 -->
        <GridIconButton
          v-perm:create
          icon="vxe-icon-add"
          title="일정 등록"
          @click="openNew(selectedDate ?? dayjs())"
        />
        <GridIconButton
          v-perm:search
          :loading="loading"
          icon="vxe-icon-search"
          title="조회"
          @click="search"
        />
      </template>
    </SearchBar>

    <div class="border-border flex h-full flex-col overflow-hidden rounded-md border">
      <div class="bg-muted grid grid-cols-7 text-center text-xs font-semibold">
        <div v-for="day in ['일', '월', '화', '수', '목', '금', '토']" :key="day" class="py-1">
          {{ day }}
        </div>
      </div>

      <div class="grid flex-1 grid-cols-7 grid-rows-6">
        <!-- 데스크톱은 빈 칸 더블클릭으로 바로 등록,
             모바일은 칸을 한 번 탭해 고른 뒤 도구줄의 [일정 등록]을 누른다 -->
        <div
          v-for="date in weeks.flat()"
          :key="date.format('YYYY-MM-DD')"
          class="border-border min-h-0 overflow-auto border-r border-t p-1"
          :class="[
            date.month() === cursor.month() ? '' : 'bg-muted/40 text-muted-foreground',
            date.isSame(dayjs(), 'day') ? 'bg-amber-200/20' : '',
            selectedDate?.isSame(date, 'day') ? 'ring-primary/60 ring-1 ring-inset' : '',
          ]"
          @click="selectedDate = date"
          @dblclick="openNew(date)"
          @dragover.prevent
          @drop.prevent="onDrop(date, $event)"
        >
          <div class="mb-1 text-right text-[11px]">{{ date.date() }}</div>

          <div
            v-for="row in eventsOf(date)"
            :key="String(row.wbs_id) + date.format('DD')"
            class="mb-0.5 cursor-pointer truncate rounded px-1 py-0.5 text-[11px]"
            :class="STATE_STYLE[String(row.wbs_state ?? '')] ?? 'bg-primary/70 text-white'"
            draggable="true"
            :title="String(row.wbs_nm ?? '')"
            @click.stop="openEdit(row)"
            @dragstart="onDragStart(row, $event)"
          >
            {{ row.wbs_nm ?? '(제목 없음)' }}
          </div>
        </div>
      </div>
    </div>

    <AppointmentForm
      v-model:open="formOpen"
      :appointment="editing"
      :start="draftStart"
      :project-code="projectCode"
      @done="search"
    />
  </Page>
</template>
