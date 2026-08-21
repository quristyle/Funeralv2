<script lang="ts" setup>
import { onMounted, reactive, ref } from 'vue';

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

import { getAllUsers, getMyNotifications } from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import { formatDateTime } from '../shared/constants';

/**
 * [내 알림함]
 *
 * 원본(NotificationHistory.vue). 담당자는 다른 사용자의 수신 내역도 조회할 수 있다.
 */

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

const columns = [
  { dataIndex: 'createdAt', key: 'createdAt', title: '수신시각', width: 160 },
  { dataIndex: 'title', key: 'title', title: '제목', ellipsis: true },
  { dataIndex: 'body', key: 'body', title: '내용', ellipsis: true },
  { dataIndex: 'isRead', key: 'isRead', title: '읽음', width: 80 },
];

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
    const users = await getAllUsers().catch(() => []);
    userOptions.value = (users ?? []).map((u: any) => ({
      label: u.userName,
      value: u.userId,
    }));
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
        <template #emptyText>
          <Empty description="수신한 알림이 없습니다." />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'createdAt'">
            {{ formatDateTime(record.createdAt) }}
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
