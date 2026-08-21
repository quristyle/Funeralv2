<script lang="ts" setup>
import { onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  DatePicker,
  Empty,
  Input,
  Space,
  Table,
} from 'ant-design-vue';

import { getMyComments } from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import { formatDateTime } from '../shared/constants';

/** [내 댓글] 내가 쓴 댓글을 기간·키워드로 찾아 원 요청으로 이동한다. */

const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);
const rows = ref<any[]>([]);

const filters = reactive<{
  endDate: string | undefined;
  keyword: string;
  startDate: string | undefined;
}>({ endDate: undefined, keyword: '', startDate: undefined });

const columns = [
  { dataIndex: 'requestTitle', key: 'requestTitle', title: '요청', ellipsis: true },
  { dataIndex: 'commentText', key: 'commentText', title: '댓글', ellipsis: true },
  { dataIndex: 'createdAt', key: 'createdAt', title: '작성일', width: 160 },
];

async function loadData() {
  loading.value = true;
  try {
    rows.value =
      (await getMyComments({
        endDate: filters.endDate,
        keyword: filters.keyword.trim() || undefined,
        startDate: filters.startDate,
      })) ?? [];
  } finally {
    loading.value = false;
  }
}

/** 댓글 본문에서 태그를 걷어내 목록에 한 줄로 보여준다. */
function plainText(html?: string) {
  if (!html) return '';
  return html.replaceAll(/<[^>]+>/g, ' ').replaceAll(/\s+/g, ' ').trim();
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
      <Space wrap>
        <Input
          v-model:value="filters.keyword"
          allow-clear
          placeholder="댓글 내용"
          style="width: 220px"
          @press-enter="loadData"
        />
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
        <Button :loading="loading" type="primary" @click="loadData">조회</Button>
      </Space>
    </Card>

    <Card :body-style="{ padding: 0 }" size="small">
      <Table
        :columns="columns"
        :custom-row="
          (record: any) => ({
            onClick: () =>
              router.push(
                `/helpdesk/request/detail/${record.requestId}#comment-${record.id}`,
              ),
            style: 'cursor: pointer',
          })
        "
        :data-source="rows"
        :loading="loading"
        row-key="id"
        size="small"
      >
        <template #emptyText>
          <Empty description="작성한 댓글이 없습니다." />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'commentText'">
            {{ plainText(record.commentText) }}
          </template>
          <template v-else-if="column.key === 'createdAt'">
            {{ formatDateTime(record.createdAt) }}
          </template>
        </template>
      </Table>
    </Card>
  </Page>
</template>
