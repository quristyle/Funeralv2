<script lang="ts" setup>
import type { LifeBirthdayApi } from '#/api/life/birthday';

import { computed, onMounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';

import { Page, useVbenModal } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import { Button, Card, Space, Spin } from 'ant-design-vue';

import { getBirthdayCalendar } from '#/api/life/birthday';

import BirthdayEditModal from './modules/birthday-edit-modal.vue';

/**
 * [생일 캘린더]
 *
 * 원본(GHUB BirthdayCalendar.vue — FullCalendar). FullCalendar 의존성을 들이지 않고
 * helpdesk schedule 의 42칸(6주) 직접 그리기 격자를 본떴다.
 *
 * 원본과 다른 점:
 * - 소속(COMPANY_TYPE) 필터 콤보는 뺐다 — 달력 이벤트에는 companyCode 가 없어
 *   옵션을 만들 데이터가 없다 (API 의 companyCode 파라미터 자체는 남아 있다).
 * - 음력 → 올해 양력 환산 미리보기는 korean-lunar-calendar 대신
 *   서버가 준 발생일(이벤트의 start)을 팝업에 넘겨 보여 준다.
 * - 빈 날짜 칸을 클릭하면 그 날짜로 신규 등록 팝업을 연다.
 */

const route = useRoute();

const loading = ref(false);
const events = ref<LifeBirthdayApi.CalendarEvent[]>([]);

const today = new Date();
const currentYear = ref(today.getFullYear());
const currentMonth = ref(today.getMonth());

const DAY_LABELS = ['일', '월', '화', '수', '목', '금', '토'];

function toIsoDate(d: Date) {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/**
 * 'YYYY-MM-DD' 날짜 키. 서버가 UTC ISO 로 내려줘도 현지 시각의 연·월·일을 쓴다
 * (helpdesk schedule 의 dayKey 패턴 — 하루 밀림 방지).
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

/** 42칸(6주) 격자 — 앞뒤 달 날짜도 흐리게 함께 그린다 */
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

/** 날짜별 이벤트 맵 */
const eventsByDate = computed(() => {
  const map = new Map<string, LifeBirthdayApi.CalendarEvent[]>();
  events.value.forEach((ev) => {
    const key = dayKey(ev.start);
    if (!key) return;
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(ev);
  });
  return map;
});

function eventsOf(date: string) {
  return eventsByDate.value.get(date) ?? [];
}

/** 보이는 42칸 범위로 이벤트를 불러온다 (end 는 FullCalendar 식 미포함 상한) */
async function loadEvents() {
  const cells = calendarDays.value;
  const start = cells[0]!.date;
  const after = new Date(`${cells.at(-1)!.date}T00:00:00`);
  after.setDate(after.getDate() + 1);

  loading.value = true;
  try {
    events.value = (await getBirthdayCalendar(start, toIsoDate(after))) ?? [];
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

watch([currentYear, currentMonth], loadEvents);

// ── 생일 등록 · 수정 팝업 ────────────────────────────────

const [EditModal, editModalApi] = useVbenModal({
  connectedComponent: BirthdayEditModal,
  destroyOnClose: true,
});

/** 생일 칩 클릭 → 수정 팝업 (원본 eventClick) */
function onEventClick(ev: LifeBirthdayApi.CalendarEvent) {
  editModalApi
    .setData({
      id: ev.extendedProps.dbId,
      subjectId: ev.extendedProps.userId,
      name: ev.title.replace(' 생일', ''),
      birthDate: ev.extendedProps.originalBirthDate,
      isLunar: ev.extendedProps.isLunar,
      isCelebrated: true,
      // 서버가 환산해 둔 올해 발생일 — 음력 미리보기에 쓴다
      occurrenceDate: ev.start,
    })
    .open();
}

/** 빈 날짜 칸 클릭 → 그 날짜로 신규 등록 */
function onCellClick(date: string) {
  editModalApi
    .setData({
      id: 0,
      subjectId: '',
      name: '',
      birthDate: date,
      isLunar: false,
      isCelebrated: true,
    })
    .open();
}

onMounted(async () => {
  // 라우트 쿼리에 날짜가 있으면 그 달로 이동 (원본과 동일).
  // 연·월이 바뀌면 watch 가 loadEvents 를 부르므로 여기서 중복 호출하지 않는다.
  if (route.query.date) {
    const target = new Date(String(route.query.date));
    if (!Number.isNaN(target.getTime())) {
      const moved =
        target.getFullYear() !== currentYear.value ||
        target.getMonth() !== currentMonth.value;
      currentYear.value = target.getFullYear();
      currentMonth.value = target.getMonth();
      if (moved) return;
    }
  }
  await loadEvents();
});
</script>

<template>
  <Page auto-content-height>
    <!-- 상단: 제목 + 월 이동 -->
    <Card :body-style="{ padding: '10px 14px' }" class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <span class="flex items-center gap-2 text-base font-bold">
          <IconifyIcon class="size-5 text-pink-500" icon="lucide:calendar-heart" />
          월별 생일자 캘린더
        </span>
        <Space>
          <Button @click="moveMonth(-1)">◀</Button>
          <span class="min-w-[110px] text-center font-medium">
            {{ monthLabel }}
          </span>
          <Button @click="moveMonth(1)">▶</Button>
          <Button @click="goToday">오늘</Button>
          <Button :loading="loading" @click="loadEvents">
            <IconifyIcon class="size-4" icon="lucide:refresh-cw" />
          </Button>
        </Space>
      </div>
    </Card>

    <Spin :spinning="loading">
      <Card :body-style="{ padding: '8px' }" size="small">
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

          <div
            v-for="cell in calendarDays"
            :key="cell.date"
            class="relative min-h-[96px] cursor-pointer rounded border border-border p-1 transition-colors hover:bg-accent/40"
            :class="
              cell.isCurrentMonth ? 'bg-background' : 'bg-muted/30 opacity-60'
            "
            @click="onCellClick(cell.date)"
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

            <!-- 그날 생일 -->
            <div class="max-h-[78px] space-y-0.5 overflow-y-auto pr-5 pt-0.5">
              <div
                v-for="ev in eventsOf(cell.date)"
                :key="`${cell.date}-${ev.id}`"
                class="flex cursor-pointer items-center gap-1 truncate rounded-sm border-l-2 bg-pink-100/70 px-1 py-0.5 text-[10px] leading-tight text-pink-900 transition-all hover:brightness-95 dark:bg-pink-900/40 dark:text-pink-100"
                :style="{ borderLeftColor: ev.borderColor || ev.backgroundColor }"
                :title="`${ev.title}${ev.extendedProps.isLunar ? ' (음력)' : ''}`"
                @click.stop="onEventClick(ev)"
              >
                <span class="shrink-0">🎂</span>
                <span class="truncate">
                  {{ ev.title }}{{ ev.extendedProps.isLunar ? ' (음)' : '' }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </Card>
    </Spin>

    <!-- 생일 등록 · 수정 팝업 -->
    <EditModal @success="loadEvents" />
  </Page>
</template>
