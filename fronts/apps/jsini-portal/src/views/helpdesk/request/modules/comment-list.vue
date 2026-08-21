<script lang="ts" setup>
import type { CommentNode } from './comment-item.vue';

import { computed, ref, watch } from 'vue';

import { Empty, message, Spin } from 'ant-design-vue';

import {
  createComment,
  deleteComment as deleteCommentApi,
  getRequestComments,
} from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import CommentForm from './comment-form.vue';
import CommentItem from './comment-item.vue';

/**
 * 요청 상세 하단의 댓글 영역.
 * 서버는 평평한 목록을 주므로 parentCommentId 로 트리를 만들어 표시한다.
 */
const props = defineProps<{ requestId: number }>();

const emit = defineEmits<{ loaded: [count: number] }>();

const helpdesk = useHelpdeskStore();
const comments = ref<any[]>([]);
const loading = ref(false);

/** 평평한 목록 → 부모-자식 트리 */
const commentTree = computed<CommentNode[]>(() => {
  const map = new Map<number, CommentNode>();
  const roots: CommentNode[] = [];

  comments.value.forEach((c) => {
    map.set(c.id, { ...c, children: [] });
  });

  comments.value.forEach((c) => {
    const node = map.get(c.id)!;
    const parent = c.parentCommentId ? map.get(c.parentCommentId) : undefined;
    if (parent) {
      parent.children.push(node);
    } else {
      roots.push(node);
    }
  });

  const byDate = (a: CommentNode, b: CommentNode) =>
    new Date(a.createdAt ?? 0).getTime() - new Date(b.createdAt ?? 0).getTime();

  roots.sort(byDate);
  map.forEach((node) => node.children.sort(byDate));
  return roots;
});

async function fetchComments() {
  if (!props.requestId) return;

  loading.value = true;
  try {
    comments.value = (await getRequestComments(props.requestId)) ?? [];
    emit('loaded', comments.value.length);
  } catch {
    comments.value = [];
  } finally {
    loading.value = false;
  }
}

async function submitComment(text: string, parentId: null | number = null) {
  if (!helpdesk.helpdeskUserId) {
    message.warning('연결된 헬프데스크 계정이 없어 댓글을 쓸 수 없습니다.');
    return;
  }

  // 서버는 작성자를 클레임에서도 읽지만, 원본과 동일하게 본문에도 실어 보낸다.
  await createComment({
    authorId: helpdesk.helpdeskUserId,
    authortype: helpdesk.identity?.loginType,
    commentText: text,
    parentCommentId: parentId,
    requestId: props.requestId,
  } as any);

  message.success('댓글을 등록했습니다.');
  await fetchComments();
}

async function removeComment(commentId: number) {
  await deleteCommentApi(commentId);
  message.success('댓글을 삭제했습니다.');
  await fetchComments();
}

watch(() => props.requestId, fetchComments, { immediate: true });

defineExpose({ fetchComments });
</script>

<template>
  <div>
    <CommentForm @submit="(text) => submitComment(text, null)" />

    <Spin :spinning="loading">
      <div class="mt-4">
        <template v-if="commentTree.length > 0">
          <CommentItem
            v-for="comment in commentTree"
            :key="comment.id"
            :comment="comment"
            @delete-comment="removeComment"
            @submit-reply="({ parentId, text }) => submitComment(text, parentId)"
          />
        </template>
        <Empty v-else description="아직 댓글이 없습니다." />
      </div>
    </Spin>
  </div>
</template>
