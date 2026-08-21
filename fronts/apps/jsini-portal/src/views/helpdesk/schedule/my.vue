<script lang="ts" setup>
import type { Schedule } from '#/api/helpdesk';

import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Card,
  Checkbox,
  Col,
  Empty,
  message,
  Row,
  Segmented,
  Space,
  Spin,
  Statistic,
  Tag,
} from 'ant-design-vue';

import { getSchedules, updateSchedule } from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import { formatDate } from '../shared/constants';

/**
 * [내 일정]
 *
 * 원본(MyScheduleView.vue). 달력 대신 기한 기준 목록으로 본다.
 * 고객으로 연결된 계정은 자기 회사 일정과 공통 일정만 보인다.
 */

const helpdesk = useHelpdeskStore();

const loading = ref(false);
const schedules = ref<Schedule[]>([]);
const filter = ref<'all' | 'completed' | 'overdue' | 'upcoming'>('upcoming');

const FILTER_OPTIONS = [
  { label: '예정', value: 'upcoming' },
  { label: '기한 초과', value: 'overdue' },
  { label: '완료', value: 'completed' },
  { label: '전체', value: 'all' },
];

function dayKey(value?: null | string) {
  return value ? String(value).slice(0, 10) : '';
}

const todayKey = new Date().toISOString().split('T')[0]!;

/** 기한이 지났는데 아직 완료되지 않은 일정 */
function isOverdue(s: Schedule) {
  const end = dayKey(s.endDate);
  return !s.isCompleted && Boolean(end) && end < todayKey;
}

const counts = computed(() => ({
  completed: schedules.value.filter((s) => s.isCompleted).length,
  overdue: schedules.value.filter((s) => isOverdue(s)).length,
  total: schedules.value.length,
  upcoming: schedules.value.filter((s) => !s.isCompleted && !isOverdue(s))
    .length,
}));

const filteredSchedules = computed(() => {
  const list = schedules.value.filter((s) => {
    switch (filter.value) {
      case 'completed': {
        return s.isCompleted;
      }
      case 'overdue': {
        return isOverdue(s);
      }
      case 'upcoming': {
        return !s.isCompleted && !isOverdue(s);
      }
      default: {
        return true;
      }
    }
  });

  // 기한이 가까운 순으로 보여준다.
  return list.toSorted((a, b) =>
    dayKey(a.endDate).localeCompare(dayKey(b.endDate)),
  );
});

async function loadData() {
  loading.value = true;
  try {
    const all = (await getSchedules()) ?? [];

    // 고객은 자기 회사 일정과 공통 일정만 본다.
    schedules.value = helpdesk.isAdmin
      ? all
      : all.filter(
          (s) =>
            s.isCommon ||
            (helpdesk.companyId !== undefined &&
              Number(s.companyId) === Number(helpdesk.companyId)),
        );
  } finally {
    loading.value = false;
  }
}

/** 체크박스로 완료 여부를 바로 바꾼다. */
async function toggleCompleted(schedule: Schedule, checked: boolean) {
  await updateSchedule(schedule.id, {
    ...schedule,
    completedDate: checked ? todayKey : null,
    isCompleted: checked,
  });
  message.success(checked ? '완료 처리했습니다.' : '완료를 취소했습니다.');
  await loadData();
}

onMounted(async () => {
  await helpdesk.loadIdentity();
  await loadData();
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Row :gutter="[12, 12]">
      <Col :lg="6" :xs="12">
        <Card size="small">
          <Statistic :value="counts.total" title="전체" />
        </Card>
      </Col>
      <Col :lg="6" :xs="12">
        <Card size="small">
          <Statistic :value="counts.upcoming" title="예정" />
        </Card>
      </Col>
      <Col :lg="6" :xs="12">
        <Card size="small">
          <Statistic
            :value="counts.overdue"
            :value-style="{ color: '#EF4444' }"
            title="기한 초과"
          />
        </Card>
      </Col>
      <Col :lg="6" :xs="12">
        <Card size="small">
          <Statistic
            :value="counts.completed"
            :value-style="{ color: '#22C55E' }"
            title="완료"
          />
        </Card>
      </Col>
    </Row>

    <Card class="mt-3" size="small">
      <Segmented v-model:value="filter" :options="FILTER_OPTIONS" />

      <Spin :spinning="loading">
        <div class="mt-3">
          <template v-if="filteredSchedules.length > 0">
            <div
              v-for="schedule in filteredSchedules"
              :key="schedule.id"
              class="flex items-center gap-3 border-b border-border py-2 last:border-b-0"
            >
              <Checkbox
                :checked="schedule.isCompleted"
                @change="
                  (e: any) => toggleCompleted(schedule, e.target.checked)
                "
              />
              <div class="min-w-0 flex-1">
                <div
                  class="truncate font-medium"
                  :class="schedule.isCompleted ? 'line-through opacity-60' : ''"
                >
                  {{ schedule.title }}
                </div>
                <div class="truncate text-xs text-muted-foreground">
                  {{ schedule.description }}
                </div>
              </div>
              <Space>
                <Tag v-if="schedule.isCommon">공통</Tag>
                <Tag v-if="isOverdue(schedule)" color="error">기한 초과</Tag>
                <span class="text-xs text-muted-foreground">
                  {{ formatDate(schedule.startDate) }} ~
                  {{ formatDate(schedule.endDate) }}
                </span>
              </Space>
            </div>
          </template>
          <Empty v-else description="해당하는 일정이 없습니다." />
        </div>
      </Spin>
    </Card>
  </Page>
</template>
