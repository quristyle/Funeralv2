<script lang="ts" setup>
/**
 * 호실 히스토리 — 옛 `page/room/room_goin_hist.jsp` 와 `page/build/room_goin.jsp`.
 *
 * 옛 화면은 호실을 거쳐 간 고인을 사진·성명·성별·나이·상태·입실·출상·발인·장지 순으로
 * 늘어놓았다. 컬럼 구성을 그대로 따르되, 옛 `t_room_goin` 이 비워 두던 기간 칸은
 * 현 `deceased_rooms` 의 start_time/end_time 으로 채운다.
 *
 * ── 2026-09-03 개선 ────────────────────────────────────────
 *
 * **사진을 키웠다.** 48×60 이던 것을 72×90 으로 두고 행 높이를 함께 올렸다.
 * 영정 사진은 얼굴을 확인하는 것이 목적인데 48px 로는 누구인지 가려지지 않았다.
 * 눌러서 원본을 크게 보는 것은 전부터 되던 것이다(`ImagePreview` 의 미리보기).
 *
 * **찾는 길을 넓혔다.** 전에는 건물·호실·기간 셋뿐이라, 이름만 아는 고인이
 * 어느 호실에 있었는지 찾으려면 호실을 하나씩 훑어야 했다.
 *   · 성명으로 찾기 (백엔드 `keyword`)
 *   · 사용 중 / 출상 가리기 (백엔드 `inUse`)
 *   · 기간 프리셋 — 1·3·6개월 · 1년 · 전체
 *   · 결과 요약 — 몇 건이고 그중 사용 중이 몇인지
 *   · 호실 칸에 건물·층을 함께 적어 이름이 겹치는 호실을 가릴 수 있게 했다
 *
 * 정렬·필터는 화면에 적지 않는다 — 공통 레이어가 붙인다(준수사항 6).
 */
import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { DatePicker, Input, Segmented, Select, Tag } from 'ant-design-vue';
import dayjs from 'dayjs';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getBuildings, getRooms } from '#/api/funeral/building';
import { getRoomHistories } from '#/api/funeral/info';
import GridIconButton from '#/components/GridIconButton.vue';
import ImagePreview from '#/components/ImagePreview.vue';

/** 사진 크기. 행 높이는 이 값에 맞춰 정한다. */
const PHOTO_WIDTH = 72;
const PHOTO_HEIGHT = 90;

const buildings = ref<any[]>([]);
const rooms = ref<any[]>([]);

const searchBuildingId = ref<string | undefined>();
const searchRoomId = ref<string | undefined>();
const searchKeyword = ref('');
/** '' 전체 · 'inUse' 사용 중 · 'departed' 출상 */
const searchState = ref<'' | 'departed' | 'inUse'>('');
const searchRange = ref<[any, any] | undefined>([
  dayjs().subtract(1, 'year'),
  dayjs(),
]);

/** 조회된 행. 요약 줄이 이것을 센다. */
const rows = ref<any[]>([]);

const summary = computed(() => {
  const total = rows.value.length;
  const inUse = rows.value.filter((r) => r.inUse).length;
  return { departed: total - inUse, inUse, total };
});

/**
 * 기간 프리셋.
 *
 * 기본이 최근 1년이라, 그보다 오래된 것을 찾으려면 날짜를 두 번 골라야 했다.
 * `months: null` 은 기간을 아예 걸지 않는 '전체' 다.
 */
const RANGE_PRESETS = [
  { label: '1개월', months: 1 },
  { label: '3개월', months: 3 },
  { label: '6개월', months: 6 },
  { label: '1년', months: 12 },
  { label: '전체', months: null },
] as const;

const STATE_OPTIONS = [
  { label: '전체', value: '' },
  { label: '사용 중', value: 'inUse' },
  { label: '출상', value: 'departed' },
];

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      {
        field: 'photo',
        title: '사진',
        width: PHOTO_WIDTH + 24,
        align: 'center',
        // 이미지 칸이라 정렬·필터가 뜻이 없다.
        params: { filter: false, sort: false },
        slots: { default: 'photo' },
      },
      {
        field: 'roomName',
        title: '호실',
        minWidth: 150,
        // 한 칸에 호실·건물·층을 함께 그린다. 필터가 훑을 글자도 셋을 합쳐 준다 —
        // '본관' 이나 '2층' 으로도 걸러진다.
        params: {
          filterText: (row: any) =>
            [row.roomName, row.buildingName, row.floorName]
              .filter(Boolean)
              .join(' '),
        },
        slots: { default: 'room' },
      },
      { field: 'deceasedName', title: '성명', width: 120 },
      {
        field: 'gender',
        title: '성별',
        width: 80,
        formatter: fmtGender,
        // 값이 정해진 칸이라 고르는 칸으로 둔다(준수사항 6).
        params: {
          filterOptions: [
            { label: '남', value: 'MALE' },
            { label: '여', value: 'FEMALE' },
          ],
        },
      },
      { field: 'age', title: '나이', width: 70, align: 'right' },
      {
        field: 'inUse',
        title: '상태',
        width: 90,
        params: {
          filterOptions: [
            { label: '사용 중', value: true },
            { label: '출상', value: false },
          ],
          filterText: (row: any) => (row.inUse ? '사용 중' : '출상'),
        },
        slots: { default: 'state' },
      },
      { field: 'startTime', title: '입실', width: 150, formatter: fmtDateTime },
      { field: 'endTime', title: '출상', width: 150, formatter: fmtDateTime },
      {
        field: 'useDays',
        title: '사용일수',
        width: 90,
        align: 'right',
        formatter: fmtDays,
      },
      {
        field: 'departureDate',
        title: '발인',
        width: 150,
        formatter: fmtDateTime,
      },
      { field: 'burialPlot', title: '장지', minWidth: 140 },
    ],
    height: 'auto',
    // 사진을 키운 만큼 행도 높여야 한다. 그러지 않으면 사진이 칸에 잘린다.
    rowConfig: { height: PHOTO_HEIGHT + 14 },
    proxyConfig: {
      ajax: {
        query: async () => {
          const list =
            (await getRoomHistories({
              buildingId: searchBuildingId.value || undefined,
              roomId: searchRoomId.value || undefined,
              from: searchRange.value?.[0]?.toISOString(),
              to: searchRange.value?.[1]?.toISOString(),
              keyword: searchKeyword.value.trim() || undefined,
              // 세 갈래(전체·사용 중·출상)를 bool 하나로 옮긴다.
              inUse:
                searchState.value === ''
                  ? undefined
                  : searchState.value === 'inUse',
            })) || [];
          rows.value = list;
          return list;
        },
      },
    },
  } as any,
});

function fmtDateTime({ cellValue }: { cellValue: any }) {
  return cellValue ? dayjs(cellValue).format('YYYY-MM-DD HH:mm') : '-';
}

function fmtGender({ cellValue }: { cellValue: any }) {
  if (cellValue === 'MALE') return '남';
  if (cellValue === 'FEMALE') return '여';
  return cellValue || '-';
}

function fmtDays({ cellValue }: { cellValue: any }) {
  return cellValue ? `${cellValue}일` : '-';
}

async function fetchBuildings() {
  // 건물 목록을 못 받아도 화면은 쓸 수 있다(건물 전체로 조회된다).
  // 그래서 오류를 띄우지 않는다 — 예전에는 토스트가 떴다.
  buildings.value = (await getBuildings().catch(() => [])) || [];
}

/** 건물을 고르면 그 건물의 호실만 남긴다. 옛 검색폼도 이렇게 물려 있었다. */
async function fetchRooms() {
  rooms.value =
    (await getRooms({ buildingId: searchBuildingId.value }).catch(() => [])) ||
    [];
}

/** 호실 고르는 칸의 이름. 이름이 겹치는 호실을 층으로 가린다. */
const roomOptions = computed(() =>
  rooms.value.map((r) => ({
    label: r.floorName ? `${r.name} (${r.floorName})` : r.name,
    value: r.id,
  })),
);

async function handleBuildingChange() {
  searchRoomId.value = undefined;
  await fetchRooms();
  gridApi.query();
}

function applyPreset(months: null | number) {
  searchRange.value =
    months === null ? undefined : [dayjs().subtract(months, 'month'), dayjs()];
  gridApi.query();
}

/** 지금 걸린 기간이 어느 프리셋인지 (눌린 표시를 하려고). */
const activePreset = computed(() => {
  if (!searchRange.value) return '전체';
  const [from, to] = searchRange.value;
  if (!from || !to) return '';
  // 오늘까지가 아니면 프리셋이 아니라 직접 고른 기간이다.
  if (!dayjs(to).isSame(dayjs(), 'day')) return '';
  const found = RANGE_PRESETS.find(
    (p) => p.months !== null && dayjs(from).isSame(dayjs().subtract(p.months, 'month'), 'day'),
  );
  return found?.label ?? '';
});

function handleReset() {
  searchBuildingId.value = undefined;
  searchRoomId.value = undefined;
  searchKeyword.value = '';
  searchState.value = '';
  searchRange.value = [dayjs().subtract(1, 'year'), dayjs()];
  fetchRooms();
  gridApi.query();
}

onMounted(async () => {
  await fetchBuildings();
  await fetchRooms();
});
</script>

<template>
  <!--
    조회 조건 줄과 목록을 함께 두는 구조라 `page-fill-last` 를 준다.
    빼면 목록이 영역 전체 높이를 차지해 조건 줄만큼 넘치고, 그러면 내용 영역이
    통째로 스크롤되며 조건 줄이 위로 밀려 나간다 (준수사항 4).
  -->
  <Page auto-content-height content-class="page-fill-last">
    <div class="bg-card mb-3 flex flex-col gap-2 rounded-lg border px-3 py-2.5">
      <!-- 첫 줄 — 어디의 누구를 -->
      <div class="flex flex-wrap items-center gap-2">
        <Select
          v-model:value="searchBuildingId"
          class="w-36"
          allow-clear
          show-search
          option-filter-prop="label"
          placeholder="건물 전체"
          :options="buildings.map((b) => ({ label: b.name, value: b.id }))"
          @change="handleBuildingChange"
        />
        <Select
          v-model:value="searchRoomId"
          class="w-44"
          allow-clear
          show-search
          option-filter-prop="label"
          placeholder="호실 전체"
          :options="roomOptions"
          @change="gridApi.query()"
        />
        <Input
          v-model:value="searchKeyword"
          class="w-40"
          allow-clear
          placeholder="고인 성명"
          @press-enter="gridApi.query()"
        />
        <Segmented
          v-model:value="searchState"
          :options="STATE_OPTIONS"
          @change="gridApi.query()"
        />
        <GridIconButton
          icon="vxe-icon-search"
          title="조회"
          @click="gridApi.query()"
        />
        <GridIconButton
          icon="vxe-icon-repeat"
          title="조건 초기화"
          @click="handleReset"
        />
      </div>

      <!-- 두 번째 줄 — 언제부터 언제까지 · 결과 요약 -->
      <div class="flex flex-wrap items-center gap-2">
        <div class="flex items-center gap-1">
          <button
            v-for="preset in RANGE_PRESETS"
            :key="preset.label"
            type="button"
            class="rounded border px-2 py-0.5 text-xs transition-colors"
            :class="
              activePreset === preset.label
                ? 'border-primary bg-primary/10 text-primary'
                : 'border-border text-muted-foreground hover:border-primary/50'
            "
            @click="applyPreset(preset.months)"
          >
            {{ preset.label }}
          </button>
        </div>

        <DatePicker.RangePicker
          v-model:value="searchRange"
          class="w-60"
          @change="gridApi.query()"
        />

        <div class="ml-auto flex items-center gap-2 text-xs">
          <span class="text-muted-foreground">
            총 <b class="text-foreground">{{ summary.total }}</b>건
          </span>
          <Tag color="processing">사용 중 {{ summary.inUse }}</Tag>
          <Tag color="default">출상 {{ summary.departed }}</Tag>
        </div>
      </div>
    </div>

    <Grid table-title="호실 히스토리" class="h-auto min-h-0 flex-1">
      <template #photo="{ row }">
        <!-- 눌러서 원본을 크게 볼 수 있다 (antd Image 미리보기). -->
        <ImagePreview
          :src="
            row.memorialPhotoFileId
              ? `/api/file/thumbnail/${row.memorialPhotoFileId}`
              : row.memorialPhotoUrl
          "
          :fallback-src="row.memorialPhotoUrl"
          :preview-src="row.memorialPhotoUrl"
          :width="PHOTO_WIDTH"
          :height="PHOTO_HEIGHT"
          fit="cover"
          fallback-text="🕯"
        />
      </template>

      <template #room="{ row }">
        <div class="leading-tight">
          <div class="font-medium">{{ row.roomName }}</div>
          <div class="text-muted-foreground text-xs">
            {{ [row.buildingName, row.floorName].filter(Boolean).join(' · ') || '-' }}
          </div>
        </div>
      </template>

      <template #state="{ row }">
        <Tag v-if="row.inUse" color="processing">사용 중</Tag>
        <Tag v-else color="default">출상</Tag>
      </template>
    </Grid>
  </Page>
</template>
