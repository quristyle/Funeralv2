<script lang="ts" setup>
import { onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  DatePicker,
  Empty,
  Select,
  Space,
  Table,
  Tag,
} from 'ant-design-vue';

import { fetchBizOptions } from '#/api/biz-select';
import { getMyNotifications } from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '#/views/helpdesk/shared/account-notice.vue';
import { formatDateTime } from '#/views/helpdesk/shared/constants';

/**
 * [내 알림함]
 *
 * 원본(NotificationHistory.vue). 담당자는 다른 사용자의 수신 내역도 조회할 수 있다.
 */

const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);
const rows = ref<any[]>([]);
const userOptions = ref<{ label: string; value: number }[]>([]);

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

// 서버가 주는 필드는 receivedAt / message / url 이다(원본 NotificationHistory.vue 기준).
const columns = [
  { dataIndex: 'receivedAt', key: 'receivedAt', sorter: true, title: '수신 시간', width: 170 },
  { dataIndex: 'message', key: 'message', title: '내용' },
  { dataIndex: 'isRead', key: 'isRead', title: '읽음', width: 80 },
];

/** url 이 붙은 알림은 눌러서 그 화면으로 이동한다(원본과 동일). */
function openNotification(record: any) {
  if (record.url) router.push(record.url);
}

async function loadData() {
  loading.value = true;
  try {
    rows.value =
      (await getMyNotifications({
        endDate: filters.endDate,
        isRead: filters.isRead === '' ? undefined : filters.isRead === 'true',
        startDate: filters.startDate,
        userId: filters.userId,
      })) ?? [];
  } finally {
    loading.value = false;
  }
}

onMounted(async () => {
  await helpdesk.loadIdentity();

  if (helpdesk.isAdmin) {
    // 담당자+고객 통합 목록. 경로와 라벨/값 필드는 메타데이터(helpdesk_user)가 정한다.
    userOptions.value = await fetchBizOptions('helpdesk_user')
      .then((r) => r.options)
      .catch(() => []);
  }

  await loadData();
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Card class="mb-3" size="small">
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
        <Button :loading="loading" type="primary" @click="loadData">조회</Button>
      </Space>
    </Card>

    <Card :body-style="{ padding: 0 }" size="small">
      <Table
        :columns="columns"
        :data-source="rows"
        :loading="loading"
        row-key="id"
        size="small"
      >
        <template #title>
          <span class="text-sm">알림 목록 (총 {{ rows.length }}개)</span>
        </template>
        <template #emptyText>
          <Empty description="수신한 알림이 없습니다." />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'receivedAt'">
            {{ formatDateTime(record.receivedAt) }}
          </template>
          <template v-else-if="column.key === 'message'">
            <span
              :class="record.url ? 'cursor-pointer text-primary' : ''"
              @click="openNotification(record)"
            >
              {{ record.message }}
            </span>
          </template>
          <template v-else-if="column.key === 'isRead'">
            <Tag :color="record.isRead ? 'success' : 'default'">
              {{ record.isRead ? '읽음' : '안읽음' }}
            </Tag>
          </template>
        </template>
      </Table>
    </Card>
  </Page>
</template>
