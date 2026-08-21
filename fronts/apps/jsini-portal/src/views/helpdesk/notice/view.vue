<script lang="ts" setup>
import type { Notice } from '#/api/helpdesk';

import { computed, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import { Button, Card, Empty, Space, Spin } from 'ant-design-vue';

import { getNotice } from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import { formatDateTime } from '../shared/constants';

/**
 * [공지 보기]
 *
 * 원본(NoticeView.vue)은 빈 스텁이었다. 목록에서 열람할 수 있도록 구현했다.
 */

const route = useRoute();
const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);
const notice = ref<Notice | null>(null);

const noticeId = computed(() => Number(route.params.id));

async function load() {
  if (!noticeId.value) return;

  loading.value = true;
  try {
    notice.value = (await getNotice(noticeId.value)) ?? null;
  } finally {
    loading.value = false;
  }
}

watch(noticeId, load);

onMounted(async () => {
  await helpdesk.loadIdentity();
  await load();
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Spin :spinning="loading">
      <Card v-if="notice" size="small" :title="notice.title">
        <template #extra>
          <Space>
            <Button size="small" @click="router.push('/helpdesk/notice/list')">
              목록
            </Button>
            <Button
              v-if="helpdesk.isAdmin"
              size="small"
              type="primary"
              @click="router.push(`/helpdesk/notice/form/${notice.id}`)"
            >
              수정
            </Button>
          </Space>
        </template>

        <div class="mb-3 text-xs text-muted-foreground">
          {{ formatDateTime(notice.createdAt) }}
        </div>

        <!-- 서식이 담긴 HTML 본문 -->
        <!-- eslint-disable-next-line vue/no-v-html -->
        <div class="hd-notice-body text-sm" v-html="notice.content ?? ''"></div>
      </Card>

      <Empty v-else-if="!loading" description="공지를 찾을 수 없습니다." />
    </Spin>
  </Page>
</template>

<style scoped>
.hd-notice-body :deep(img) {
  max-width: 100%;
  height: auto;
}
</style>
