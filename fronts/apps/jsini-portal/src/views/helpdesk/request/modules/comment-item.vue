<script lang="ts" setup>
import { computed, ref } from 'vue';

import { Avatar, Button, Popconfirm } from 'ant-design-vue';

import { useHelpdeskStore } from '#/store/helpdesk';

import { formatDateTime } from '../../shared/constants';
import CommentForm from './comment-form.vue';

/** 트리로 조립된 댓글 노드 */
export interface CommentNode {
  author?: { photo?: string; userName?: string };
  authorId?: number;
  authorType?: string;
  authortype?: string;
  children: CommentNode[];
  commentText?: string;
  createdAt?: string;
  id: number;
  requestId: number;
}

const props = defineProps<{ comment: CommentNode }>();

const emit = defineEmits<{
  deleteComment: [commentId: number];
  submitReply: [payload: { parentId: number; text: string }];
}>();

const helpdesk = useHelpdeskStore();
const showReplyForm = ref(false);

/**
 * 본문의 상대경로 이미지에 폴백을 달아준다.
 * 첨부 이미지가 예전 헬프데스크 도메인에 남아 있어 상대경로만으로는 깨진다.
 */
const renderedText = computed(() => {
  const html = props.comment.commentText || '';
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

/** 내가 쓴 댓글만 삭제할 수 있다. */
const canDelete = computed(() => {
  const authorType = props.comment.authorType ?? props.comment.authortype;
  return (
    helpdesk.helpdeskUserId !== undefined &&
    Number(props.comment.authorId) === Number(helpdesk.helpdeskUserId) &&
    authorType === helpdesk.identity?.loginType
  );
});

const authorInitial = computed(() =>
  (props.comment.author?.userName ?? '?').charAt(0).toUpperCase(),
);

function onReply(text: string) {
  emit('submitReply', { parentId: props.comment.id, text });
  showReplyForm.value = false;
}

/** 댓글로 바로 가는 링크를 클립보드에 복사한다. */
async function copyLink() {
  const url = `${window.location.origin}/helpdesk/request/detail/${props.comment.requestId}#comment-${props.comment.id}`;
  await navigator.clipboard?.writeText(url);
}
</script>

<template>
  <div :id="`comment-${comment.id}`" class="border-b border-border py-3">
    <div class="flex items-start gap-3">
      <Avatar v-if="comment.author?.photo" :src="comment.author.photo" />
      <Avatar v-else>{{ authorInitial }}</Avatar>

      <div class="min-w-0 flex-1">
        <div class="mb-1 flex items-center gap-2 text-xs text-muted-foreground">
          <span class="font-medium text-foreground">
            {{ comment.author?.userName }}
          </span>
          <span>{{ formatDateTime(comment.createdAt) }}</span>
        </div>

        <!-- 원본과 동일하게 서식이 담긴 HTML 본문을 렌더링한다 -->
        <!-- eslint-disable-next-line vue/no-v-html -->
        <div class="hd-comment-body text-sm" v-html="renderedText"></div>

        <div class="mt-1 flex items-center gap-1">
          <Button size="small" type="link" @click="showReplyForm = !showReplyForm">
            답글
          </Button>
          <Button size="small" type="link" @click="copyLink">링크 복사</Button>
          <Popconfirm
            v-perm:delete
            v-if="canDelete"
            cancel-text="취소"
            ok-text="삭제"
            title="이 댓글을 삭제할까요?"
            @confirm="emit('deleteComment', comment.id)"
          >
            <Button danger size="small" type="link">삭제</Button>
          </Popconfirm>
        </div>

        <div v-if="showReplyForm" class="mt-2">
          <CommentForm submit-label="답글 등록" @submit="onReply" />
        </div>
      </div>
    </div>

    <div v-if="comment.children?.length" class="ml-6 border-l border-border pl-4">
      <CommentItem
        v-for="child in comment.children"
        :key="child.id"
        :comment="child"
        @delete-comment="(id) => emit('deleteComment', id)"
        @submit-reply="(payload) => emit('submitReply', payload)"
      />
    </div>
  </div>
</template>

<style scoped>
.hd-comment-body :deep(img) {
  max-width: 100%;
  height: auto;
}
</style>
