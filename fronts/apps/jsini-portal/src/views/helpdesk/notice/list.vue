<script lang="ts" setup>
import type { Notice } from '#/api/helpdesk';

import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Empty,
  Input,
  message,
  Popconfirm,
  Space,
  Table,
} from 'ant-design-vue';

import { deleteNotice, getNotices } from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import { formatDateTime } from '../shared/constants';

/** [공지 목록] 원본 NoticeList.vue */

const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);
const rows = ref<Notice[]>([]);
const keyword = ref('');

const columns = [
  { dataIndex: 'id', key: 'id', title: 'ID', width: 70 },
  { dataIndex: 'title', key: 'title', title: '제목' },
  { dataIndex: 'createdAt', key: 'createdAt', title: '등록일', width: 160 },
  { key: 'action', title: '', width: 120 },
];

const filteredRows = computed(() => {
  const kw = keyword.value.trim().toLowerCase();
  if (!kw) return rows.value;
  return rows.value.filter((n) =>
    `${n.title} ${n.content ?? ''}`.toLowerCase().includes(kw),
  );
});

async function loadData() {
  loading.value = true;
  try {
    rows.value = (await getNotices()) ?? [];
  } finally {
    loading.value = false;
  }
}

async function onDelete(row: Notice) {
  await deleteNotice(row.id);
  message.success('공지를 삭제했습니다.');
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

    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Input
          v-model:value="keyword"
          allow-clear
          placeholder="제목 + 내용"
          style="width: 240px"
        />
        <Button
          v-if="helpdesk.isAdmin"
          type="primary"
          @click="router.push('/helpdesk/notice/form/new')"
        >
          공지 작성
        </Button>
      </div>
    </Card>

    <Card :body-style="{ padding: 0 }" size="small">
      <Table
        :columns="columns"
        :custom-row="
          (record: any) => ({
            onClick: () => router.push(`/helpdesk/notice/view/${record.id}`),
            style: 'cursor: pointer',
          })
        "
        :data-source="filteredRows"
        :loading="loading"
        row-key="id"
        size="small"
      >
        <template #emptyText>
          <Empty description="등록된 공지가 없습니다." />
        </template>

        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'createdAt'">
            {{ formatDateTime(record.createdAt) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <Space v-if="helpdesk.isAdmin" @click.stop>
              <Button
                size="small"
                type="link"
                @click="router.push(`/helpdesk/notice/form/${record.id}`)"
              >
                수정
              </Button>
              <Popconfirm
                cancel-text="취소"
                ok-text="삭제"
                title="공지를 삭제할까요?"
                @confirm="onDelete(record as Notice)"
              >
                <Button danger size="small" type="link">삭제</Button>
              </Popconfirm>
            </Space>
          </template>
        </template>
      </Table>
    </Card>
  </Page>
</template>
