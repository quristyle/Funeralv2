<script lang="ts" setup>
import { onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import { Button, Card, DatePicker, Select, Space, Tag } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { fetchBizOptions } from '#/api/biz-select';
import { getMyNotifications } from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '#/views/helpdesk/shared/account-notice.vue';
import { formatDateTime } from '#/views/helpdesk/shared/constants';

/**
 * [내 알림함]
 *
 * 원본(NotificationHistory.vue). 담당자는 다른 사용자의 수신 내역도 조회할 수 있다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * **가져오기 방식은 그대로다** — 조회 조건에 맞는 알림을 한 번에 전량 받는다.
 * 그래서 머리글 필터줄이 받아 온 전체를 훑는다.
 * ------------------------------------------------------------
 */

const router = useRouter();
const helpdesk = useHelpdeskStore();

const userOptions = ref<{ label: string; value: number }[]>([]);

/** 표 위에 적던 '총 N개'. 조회할 때마다 다시 센다. */
const total = ref(0);

/** 기본 조회 기간은 최근 7일 */
function isoDaysAgo(days: number) {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d.toISOString().split('T')[0]!;
}

// 셀렉트는 문자열만 다루고, 서버로 보낼 때 boolean 으로 바꾼다.
const filters = reactive<{
  endDate: string;
  isRead: '' | 'false' | 'true';
  startDate: string;
  userId: number | undefined;
}>({
  endDate: new Date().toISOString().split('T')[0]!,
  isRead: '',
  startDate: isoDaysAgo(7),
  userId: undefined,
});

const READ_OPTIONS = [
  { label: '전체', value: '' },
  { label: '읽음', value: 'true' },
  { label: '안읽음', value: 'false' },
];

/** url 이 붙은 알림은 눌러서 그 화면으로 이동한다(원본과 동일). */
function openNotification(row: any) {
  if (row.url) router.push(row.url);
}

// 서버가 주는 필드는 receivedAt / message / url 이다(원본 NotificationHistory.vue 기준).
const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      {
        field: 'receivedAt',
        // 보이는 것은 'YYYY-MM-DD HH:mm' 인데 값은 ISO 문자열이다.
        params: { filterText: (row: any) => formatDateTime(row.receivedAt) },
        slots: { default: 'receivedAt' },
        title: '수신 시간',
        width: 170,
      },
      {
        field: 'message',
        minWidth: 240,
        slots: { default: 'message' },
        title: '내용',
      },
      {
        field: 'isRead',
        params: {
          filterOptions: [
            { label: '읽음', value: true },
            { label: '안읽음', value: false },
          ],
        },
        slots: { default: 'isRead' },
        title: '읽음',
        width: 90,
      },
    ],
    emptyText: '수신한 알림이 없습니다.',
    height: 'auto',
    // 전량 조회다. 페이저를 켠 채로 두면 vxe 가 응답을 `{ result, page }` 로 읽어
    // 배열만 돌려주는 이 query 의 결과가 한 줄도 그려지지 않는다.
    pagerConfig: { enabled: false },
    proxyConfig: {
      ajax: {
        query: async () => {
          const list =
            (await getMyNotifications({
              endDate: filters.endDate,
              isRead:
                filters.isRead === '' ? undefined : filters.isRead === 'true',
              startDate: filters.startDate,
              userId: filters.userId,
            })) ?? [];
          total.value = list.length;
          return list;
        },
      },
    },
    rowConfig: { keyField: 'id' },
  },
});

function loadData() {
  gridApi.query();
}

onMounted(async () => {
  await helpdesk.loadIdentity();

  if (helpdesk.isAdmin) {
    // 담당자+고객 통합 목록. 경로와 라벨/값 필드는 메타데이터(helpdesk_user)가 정한다.
    userOptions.value = await fetchBizOptions('helpdesk_user')
      .then((r) => r.options)
      .catch(() => []);
  }
});
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <HelpdeskAccountNotice />

    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space wrap>
          <DatePicker
            v-model:value="filters.startDate"
            placeholder="시작일"
            value-format="YYYY-MM-DD"
          />
          <DatePicker
            v-model:value="filters.endDate"
            placeholder="종료일"
            value-format="YYYY-MM-DD"
          />
          <Select
            v-model:value="filters.isRead"
            :options="READ_OPTIONS"
            placeholder="읽음 여부"
            style="width: 120px"
          />
          <Select
            v-if="helpdesk.isAdmin"
            v-model:value="filters.userId"
            :options="userOptions"
            allow-clear
            option-filter-prop="label"
            placeholder="사용자"
            show-search
            style="width: 180px"
          />
          <Button type="primary" @click="loadData">조회</Button>
        </Space>
        <span class="text-muted-foreground text-sm">
          알림 목록 (총 {{ total }}개)
        </span>
      </div>
    </Card>

    <Grid>
      <template #receivedAt="{ row }">
        {{ formatDateTime(row.receivedAt) }}
      </template>
      <template #message="{ row }">
        <span
          :class="row.url ? 'cursor-pointer text-primary' : ''"
          @click="openNotification(row)"
        >
          {{ row.message }}
        </span>
      </template>
      <template #isRead="{ row }">
        <Tag :color="row.isRead ? 'success' : 'default'">
          {{ row.isRead ? '읽음' : '안읽음' }}
        </Tag>
      </template>
    </Grid>
  </Page>
</template>
