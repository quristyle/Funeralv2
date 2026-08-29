<script lang="ts" setup>
import type { VxeTableGridOptions } from '#/adapter/vxe-table';

import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import { Button, message, Popconfirm, Select, Tag, Tooltip } from 'ant-design-vue';
import dayjs from 'dayjs';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import {
  deleteEvent,
  getEvents,
  getLocations,
  type LifeWeatherApi,
} from '#/api/life/weather';

/**
 * [날씨 기준 부합 기록]
 *
 * 원본: ghubfront WeatherEventList.vue + components/weather/WeatherEventTimeline.vue.
 * 설정된 판정 기준에 부합하여 발생한 이벤트·알림 발송 이력을 확인한다.
 * 상단에 오늘 발생분의 시간대별 타임라인을 붙였고, 목록은 서버 페이징
 * ({items, totalCount}) 을 vxe pagerConfig 에 연동했다.
 */

// ── 작업 권고 태그 색 ────────────────────────────────────────
const WORK_STATUS_COLOR: Record<string, string> = {
  ALLOW: 'success',
  CAUTION: 'warning',
  RESTRICTED: 'error',
  SUSPENDED: 'error',
};

// ── 타임라인 (오늘 발생분) ───────────────────────────────────
interface TimelineItem {
  description: string;
  id: string;
  time: string;
  title: string;
}

const locations = ref<LifeWeatherApi.Location[]>([]);
const timelineLocationId = ref<number | undefined>();
const timelineLoading = ref(false);
const hourlyGroups = ref<Map<string, TimelineItem[]>>(new Map());

/** 이벤트 제목 → 색 테마 (원본 getEventTheme 과 동일한 규칙) */
function getEventTheme(title: string) {
  if (title.includes('강풍')) return { bg: 'bg-indigo-500', label: '강풍' };
  if (title.includes('강수') || title.includes('호우'))
    return { bg: 'bg-blue-500', label: '강수' };
  if (title.includes('기온') || title.includes('폭염') || title.includes('한파'))
    return { bg: 'bg-red-500', label: '기온' };
  if (title.includes('습도')) return { bg: 'bg-green-500', label: '습도' };
  if (title.includes('적설') || title.includes('대설'))
    return { bg: 'bg-cyan-500', label: '적설' };
  return { bg: 'bg-amber-500', label: title };
}

const legendItems = computed(() => {
  const titles = new Set<string>();
  hourlyGroups.value.forEach((items) =>
    items.forEach((item) => titles.add(item.title)),
  );
  return [...titles].map((title) => ({ theme: getEventTheme(title), title }));
});

async function fetchTimeline() {
  timelineLoading.value = true;
  try {
    const today = dayjs().format('YYYY-MM-DD');
    const res = await getEvents({
      endDate: today,
      locationId: timelineLocationId.value,
      page: 1,
      pageSize: 100,
      startDate: today,
    });

    const rawItems: TimelineItem[] = (res?.items ?? [])
      .map((e) => ({
        description: `${e.measuredValue}${e.weatherStandard?.unit ?? ''}`,
        id: `E-${e.id}`,
        time: e.eventTime,
        title: e.weatherStandard?.name ?? '기상 감지',
      }))
      .sort((a, b) => (dayjs(a.time).isBefore(dayjs(b.time)) ? -1 : 1));

    // 같은 제목·값이 연달아 이어지면 하나만 남긴다 (원본과 동일)
    const deduplicated: TimelineItem[] = [];
    for (const item of rawItems) {
      const prev = deduplicated.at(-1);
      if (!prev || prev.title !== item.title || prev.description !== item.description) {
        deduplicated.push(item);
      }
    }

    const groups = new Map<string, TimelineItem[]>();
    for (const item of deduplicated) {
      const hour = dayjs(item.time).format('H');
      if (!groups.has(hour)) groups.set(hour, []);
      groups.get(hour)!.push(item);
    }
    hourlyGroups.value = new Map(
      [...groups.entries()].sort((a, b) => Number(a[0]) - Number(b[0])),
    );
  } catch {
    hourlyGroups.value = new Map();
  } finally {
    timelineLoading.value = false;
  }
}

async function fetchLocations() {
  try {
    const rows = await getLocations();
    locations.value = rows.filter((l) => l.isActive);
  } catch {
    // 타임라인 지역 필터는 보조 기능이라 실패해도 화면을 막지 않는다
  }
}

// ── 목록 그리드 ──────────────────────────────────────────────
const [Grid, gridApi] = useVbenVxeGrid({
  formOptions: {
    wrapperClass: 'grid-cols-1 md:grid-cols-2 lg:grid-cols-4',
    schema: [
      {
        component: 'RangePicker',
        componentProps: { valueFormat: 'YYYY-MM-DD' },
        fieldName: 'range',
        label: '기간',
      },
    ],
    resetButtonOptions: { text: '초기화' },
    submitButtonOptions: { text: '조회' },
  },
  gridOptions: {
    columns: [
      {
        field: 'eventTime',
        formatter: ({ cellValue }) =>
          cellValue ? dayjs(cellValue).format('YYYY-MM-DD HH:mm:ss') : '',
        title: '발생 시각',
        width: 160,
      },
      { field: 'location', slots: { default: 'location' }, title: '지역', width: 120 },
      {
        field: 'standard',
        minWidth: 200,
        slots: { default: 'standard' },
        title: '기준 명칭',
      },
      {
        align: 'center',
        field: 'measuredValue',
        slots: { default: 'measured' },
        title: '측정값',
        width: 110,
      },
      {
        align: 'center',
        field: 'workStatus',
        slots: { default: 'workStatus' },
        title: '작업 권고',
        width: 120,
      },
      {
        align: 'center',
        field: 'isNotified',
        slots: { default: 'notified' },
        title: '알림 여부',
        width: 100,
      },
      {
        align: 'center',
        field: 'action',
        fixed: 'right',
        slots: { default: 'action' },
        title: '관리',
        width: 80,
      },
    ],
    height: 'auto',
    pagerConfig: { enabled: true, pageSize: 20 },
    proxyConfig: {
      ajax: {
        query: async ({ page }, formValues) => {
          const params: Parameters<typeof getEvents>[0] = {
            page: page.currentPage || 1,
            pageSize: page.pageSize || 20,
          };
          if (formValues?.range?.length === 2) {
            params.startDate = formValues.range[0];
            params.endDate = formValues.range[1];
          }
          const res = await getEvents(params);
          return { items: res?.items ?? [], totalCount: res?.totalCount ?? 0 };
        },
      },
      response: { result: 'items', total: 'totalCount' },
    },
    rowConfig: { keyField: 'id' },
  } as VxeTableGridOptions,
  showSearchForm: true,
});

async function onDelete(row: LifeWeatherApi.EventRecord) {
  try {
    await deleteEvent(row.id);
    message.success('삭제되었습니다.');
    gridApi.query();
    fetchTimeline();
  } catch {
    message.error('삭제에 실패했습니다.');
  }
}

onMounted(() => {
  fetchLocations();
  fetchTimeline();
});
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <!-- 오늘의 발생 타임라인 -->
    <div class="bg-card mb-4 rounded border p-3">
      <div class="mb-2 flex flex-wrap items-center justify-between gap-2">
        <div class="flex items-center gap-2">
          <span class="text-sm font-bold">오늘 발생 타임라인</span>
          <Select
            v-model:value="timelineLocationId"
            :options="locations.map((l) => ({ label: l.name, value: l.id }))"
            allow-clear
            class="w-40"
            placeholder="전체 지역"
            size="small"
            @change="fetchTimeline"
          />
          <Button size="small" @click="fetchTimeline">
            <IconifyIcon class="size-4" icon="lucide:refresh-cw" />
          </Button>
        </div>
        <div v-if="legendItems.length > 0" class="flex flex-wrap gap-x-2 gap-y-1">
          <div
            v-for="item in legendItems"
            :key="item.title"
            class="flex items-center gap-1"
          >
            <div :class="['h-2 w-2 rounded-full', item.theme.bg]"></div>
            <span class="text-[10px] text-gray-500">{{ item.theme.label }}</span>
          </div>
        </div>
      </div>

      <div
        v-if="timelineLoading"
        class="flex items-center justify-center py-3 text-xs text-gray-400"
      >
        <span class="animate-pulse">로딩 중...</span>
      </div>
      <div
        v-else-if="hourlyGroups.size === 0"
        class="py-3 text-center text-xs text-gray-400"
      >
        오늘 감지된 이벤트가 없습니다.
      </div>
      <div v-else class="relative overflow-x-auto pb-1">
        <div class="flex min-w-max gap-1 px-1">
          <div
            v-for="[hour, items] in hourlyGroups"
            :key="hour"
            class="relative flex w-12 flex-col items-center"
          >
            <div class="mb-1 text-[10px] font-bold text-gray-400">{{ hour }}시</div>
            <div class="flex w-full flex-col gap-0.5">
              <Tooltip
                v-for="item in items"
                :key="item.id"
                :title="`${item.title}: ${item.description}`"
              >
                <div
                  :class="[
                    'truncate rounded-sm px-0.5 py-0.5 text-center text-[9px] font-bold text-white',
                    getEventTheme(item.title).bg,
                  ]"
                >
                  {{ item.description }}
                </div>
              </Tooltip>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 이벤트 목록 -->
    <Grid table-title="기준 부합 기록">
      <template #location="{ row }">
        <span class="font-bold text-blue-600 dark:text-blue-400">
          {{ row.weatherInfo?.location }}
        </span>
      </template>

      <template #standard="{ row }">
        <div class="flex flex-col text-left">
          <span class="font-bold">{{ row.weatherStandard?.name }}</span>
          <span class="text-[10px] text-gray-400">
            {{ row.weatherStandard?.conditionText }}
          </span>
        </div>
      </template>

      <template #measured="{ row }">
        <span class="text-base font-black text-orange-500">
          {{ row.measuredValue }}
        </span>
        <span class="ml-1 text-xs text-gray-400">
          {{ row.weatherStandard?.unit }}
        </span>
      </template>

      <template #workStatus="{ row }">
        <Tag
          v-if="row.weatherStandard?.workStatus"
          :color="WORK_STATUS_COLOR[row.weatherStandard.workStatus] ?? 'default'"
        >
          {{ row.weatherStandard.workStatus }}
        </Tag>
      </template>

      <template #notified="{ row }">
        <Tag :color="row.isNotified ? 'success' : 'default'">
          {{ row.isNotified ? '발송' : '미발송' }}
        </Tag>
      </template>

      <template #action="{ row }">
        <Popconfirm
          placement="topLeft"
          title="이 기록을 삭제하시겠습니까?"
          @confirm="onDelete(row)"
        >
          <Tooltip v-perm:delete title="삭제">
            <Button danger size="small" type="link">
              <IconifyIcon class="size-4" icon="lucide:trash-2" />
            </Button>
          </Tooltip>
        </Popconfirm>
      </template>
    </Grid>
  </Page>
</template>
