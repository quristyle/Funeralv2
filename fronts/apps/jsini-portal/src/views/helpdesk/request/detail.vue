<script lang="ts" setup>
import type { ImprovementRequest } from '#/api/helpdesk';

import { computed, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Descriptions,
  DescriptionsItem,
  message,
  Modal,
  Space,
  Spin,
  Tag,
} from 'ant-design-vue';

import {
  acceptRequest,
  deleteRequest,
  getRequest,
  resetRequest,
} from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import {
  formatDateTime,
  REQUEST_STATUSES,
  statusMeta,
  typeLabel,
} from '../shared/constants';
import CommentList from './modules/comment-list.vue';

/**
 * [요청 상세]
 *
 * 원본(RequestDetail.vue)의 동작을 그대로 옮겼다.
 * - 고객은 자기 회사 요청만 열람할 수 있다.
 * - 완료/반려/협의/논의/초기화는 접수 담당자 본인만 바꿀 수 있다.
 */

const route = useRoute();
const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);
const request = ref<ImprovementRequest | null>(null);
const commentCount = ref(0);

const requestId = computed(() => Number(route.params.id));

/** 상대경로 이미지에 예전 도메인 폴백을 달아 본문을 렌더링한다. */
const renderedDescription = computed(() => {
  const html = request.value?.description || '';
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
});

/** 접수 담당자 본인인지. 상태 변경 권한의 기준이 된다. */
const isAssignee = computed(
  () =>
    helpdesk.helpdeskUserId !== undefined &&
    Number(request.value?.adminId) === Number(helpdesk.helpdeskUserId),
);

/** 담당자만 바꿀 수 있는 상태들 */
const ASSIGNEE_ONLY = new Set([
  'Completed',
  'Consultation',
  'Negotiation',
  'Rejected',
]);

/** 상태 변경 버튼 목록 — 삭제는 따로 다룬다. */
const statusActions = computed(() =>
  REQUEST_STATUSES.filter((s) => s.value !== 'Delete'),
);

async function load() {
  if (!requestId.value) return;

  loading.value = true;
  try {
    const data = await getRequest(requestId.value);

    // 고객은 본인 회사 요청만 볼 수 있다.
    if (
      !helpdesk.isAdmin &&
      Number(data?.customer?.companyId) !== Number(helpdesk.companyId)
    ) {
      message.error('이 요청을 볼 권한이 없습니다.');
      router.push('/helpdesk/request/list');
      return;
    }

    request.value = data ?? null;
  } finally {
    loading.value = false;
  }
}

/** 상태를 변경한다. 담당자 전용 상태는 본인 여부를 먼저 확인한다. */
function changeStatus(statusValue: string, statusLabel: string, code: number) {
  if (ASSIGNEE_ONLY.has(statusValue) && !isAssignee.value) {
    message.warning('접수 담당자만 변경할 수 있습니다.');
    return;
  }

  Modal.confirm({
    cancelText: '취소',
    content: `'${statusLabel}'(으)로 변경하시겠습니까?`,
    okText: '변경',
    async onOk() {
      await acceptRequest(requestId.value, statusValue as any, code as any);
      message.success(`상태를 '${statusLabel}'(으)로 변경했습니다.`);
      await load();
    },
    title: '상태 변경',
  });
}

/** 접수를 취소하고 상태를 되돌린다. */
function onReset() {
  if (!isAssignee.value) {
    message.warning('접수 담당자만 초기화할 수 있습니다.');
    return;
  }

  Modal.confirm({
    cancelText: '취소',
    content: '접수를 취소하고 상태를 대기로 되돌립니다.',
    okText: '초기화',
    async onOk() {
      await resetRequest(requestId.value, helpdesk.helpdeskUserId);
      message.success('상태를 초기화했습니다.');
      await load();
    },
    title: '접수 취소',
  });
}

function onDelete() {
  Modal.confirm({
    cancelText: '취소',
    content: '삭제한 요청은 목록에서 사라집니다.',
    okText: '삭제',
    okType: 'danger',
    async onOk() {
      await deleteRequest(requestId.value);
      message.success('요청을 삭제했습니다.');
      router.push('/helpdesk/request/manage');
    },
    title: '요청 삭제',
  });
}

watch(requestId, load);

onMounted(async () => {
  await helpdesk.loadIdentity();
  await load();
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Spin :spinning="loading">
      <Card v-if="request" size="small">
        <template #title>
          <div class="flex items-center gap-2">
            <Tag :color="statusMeta(request.status).color">
              {{ request.statusName || statusMeta(request.status).label }}
            </Tag>
            <span class="text-base font-semibold">{{ request.title }}</span>
          </div>
        </template>

        <template #extra>
          <Space>
            <Button size="small" @click="router.back()">목록</Button>
            <Button
              size="small"
              type="primary"
              @click="router.push(`/helpdesk/request/edit/${request.id}`)"
            >
              수정
            </Button>
          </Space>
        </template>

        <Descriptions
          :column="{ md: 3, sm: 2, xs: 1 }"
          bordered
          size="small"
        >
          <DescriptionsItem label="작성자">
            {{ request.customer?.userName }}
          </DescriptionsItem>
          <DescriptionsItem label="회사">
            {{ request.customer?.company?.name || request.company?.name }}
          </DescriptionsItem>
          <DescriptionsItem label="유형">
            {{ typeLabel(request.ipType) }}
          </DescriptionsItem>
          <DescriptionsItem label="접수자">
            {{ request.admin?.userName || '-' }}
          </DescriptionsItem>
          <DescriptionsItem label="작성일">
            {{ formatDateTime(request.createdAt) }}
          </DescriptionsItem>
          <DescriptionsItem label="완료일">
            {{ formatDateTime(request.completededAt) || '-' }}
          </DescriptionsItem>
        </Descriptions>

        <!-- 원본과 동일하게 서식이 담긴 HTML 본문을 렌더링한다 -->
        <!-- eslint-disable-next-line vue/no-v-html -->
        <div
          class="hd-request-body mt-4 rounded-md border border-border p-4 text-sm"
          v-html="renderedDescription"
        ></div>

        <div v-if="helpdesk.isAdmin" class="mt-4">
          <div class="mb-2 text-xs text-muted-foreground">상태 변경</div>
          <Space wrap>
            <Button
              v-for="s in statusActions"
              :key="s.value"
              :disabled="request.status === s.value"
              size="small"
              @click="changeStatus(s.value, s.label, s.code)"
            >
              {{ s.label }}
            </Button>
            <Button size="small" @click="onReset">접수 취소</Button>
            <Button danger size="small" @click="onDelete">삭제</Button>
          </Space>
        </div>
      </Card>

      <Card
        v-if="request"
        class="mt-3"
        size="small"
        :title="`댓글 ${commentCount}`"
      >
        <CommentList
          :request-id="request.id"
          @loaded="(count) => (commentCount = count)"
        />
      </Card>
    </Spin>
  </Page>
</template>

<style scoped>
.hd-request-body :deep(img) {
  max-width: 100%;
  height: auto;
}
</style>
