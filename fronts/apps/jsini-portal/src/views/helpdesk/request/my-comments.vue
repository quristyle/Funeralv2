<script lang="ts" setup>
import { onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Col,
  DatePicker,
  Empty,
  Input,
  Row,
  Select,
  Space,
  Spin,
  Tag,
} from 'ant-design-vue';

import { getAllUsers, getMyComments } from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import { formatDateTime } from '../shared/constants';

/**
 * [내 댓글]
 *
 * 원본(JinReception etc/MyComments.vue, `/my-comments`).
 *
 * 원본은 표가 아니라 카드 격자였다. 카드마다 작성자·작성일·대댓글 여부·답글 수와
 * 원 요청의 제목·상태를 함께 보여주고, 카드를 누르면 그 댓글 위치로 이동한다.
 * 관리자는 작성자를 골라 남의 댓글도 볼 수 있다.
 */

const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);
const comments = ref<any[]>([]);
const userOptions = ref<{ label: string; value: number }[]>([]);

const filters = reactive<{
  endDate: string | undefined;
  keyword: string;
  startDate: string | undefined;
  userId: number | undefined;
}>({
  endDate: undefined,
  keyword: '',
  startDate: undefined,
  userId: undefined,
});

/** 댓글 본문의 상대경로 이미지에 예전 도메인 폴백을 달아준다. */
function processHtml(html?: string) {
  if (!html) return '';
  return html.replaceAll(
    /<img([^>]+)src=["']([^"']+)["']([^>]*)>/gi,
    (match, before, src, after) => {
      if (String(src).startsWith('http') || String(src).startsWith('data:')) {
        return match;
      }
      return `<img${before}src="${src}" onerror="this.onerror=null;this.src='https://help.jin114.co.kr${src}';"${after}>`;
    },
  );
}

async function loadComments() {
  loading.value = true;
  try {
    comments.value =
      (await getMyComments({
        endDate: filters.endDate,
        keyword: filters.keyword.trim() || undefined,
        startDate: filters.startDate,
        userId: filters.userId,
      } as any)) ?? [];
  } finally {
    loading.value = false;
  }
}

/**
 * 카드를 누르면 원 요청의 해당 댓글로 이동한다.
 * 본문 안의 링크나 이미지를 눌렀을 때는 이동하지 않는다(원본과 동일).
 */
function navigateToRequest(item: any, event: MouseEvent) {
  const target = event.target as HTMLElement;
  if (target.closest('a, img')) return;

  router.push(
    `/helpdesk/request/detail/${item.requestId}#comment-${item.id}`,
  );
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

  await loadComments();
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <!-- 검색 -->
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <Space wrap>
          <Input
            v-model:value="filters.keyword"
            allow-clear
            placeholder="댓글 내용 검색"
            style="width: 220px"
            @press-enter="loadComments"
          />
          <Select
            v-if="helpdesk.isAdmin"
            v-model:value="filters.userId"
            :options="userOptions"
            allow-clear
            option-filter-prop="label"
            placeholder="작성자"
            show-search
            style="width: 170px"
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
        </Space>
        <Button :loading="loading" type="primary" @click="loadComments">
          검색
        </Button>
      </div>
    </Card>

    <!-- 카드 격자 -->
    <Spin :spinning="loading">
      <Empty
        v-if="comments.length === 0 && !loading"
        description="작성한 댓글이 없습니다."
      />

      <Row v-else :gutter="[12, 12]">
        <Col
          v-for="item in comments"
          :key="item.id"
          :lg="8"
          :md="12"
          :xl="6"
          :xs="24"
        >
          <button
            class="h-full w-full cursor-pointer rounded border border-border p-3 text-left transition-shadow hover:shadow-md"
            type="button"
            @click="navigateToRequest(item, $event)"
          >
            <!-- 배지 줄 -->
            <Space class="mb-2" wrap :size="4">
              <Tag v-if="item.parentCommentId" color="purple">대댓글</Tag>
              <Tag>{{ item.authorName }}</Tag>
              <Tag>{{ formatDateTime(item.createdAt) }}</Tag>
              <Tag v-if="item.replyCount > 0" color="blue">
                답글 {{ item.replyCount }}
              </Tag>
            </Space>

            <!-- 원 요청 -->
            <div
              class="mb-2 truncate text-xs font-semibold text-primary"
              :title="item.requestTitle"
            >
              {{ item.requestTitle }}
              <span class="ml-1 font-normal text-muted-foreground">
                [{{ item.requestStatus }}]
              </span>
            </div>

            <!-- 댓글 본문 -->
            <!-- eslint-disable-next-line vue/no-v-html -->
            <div
              class="hd-comment-card max-h-40 overflow-y-auto text-sm"
              v-html="processHtml(item.commentText)"
            ></div>
          </button>
        </Col>
      </Row>
    </Spin>
  </Page>
</template>

<style scoped>
.hd-comment-card :deep(img) {
  max-width: 100%;
  height: auto;
}
</style>
