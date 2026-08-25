<script lang="ts" setup>
import type { QnaApi } from '#/api/portal/qna';

import { computed, ref } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { Button, Popconfirm, Switch, Tag, Tooltip } from 'ant-design-vue';

import { RichEditor } from '#/components/rich-editor';

import AuthorAvatar from './author-avatar.vue';

/**
 * [Q&A 글 한 개]
 *
 * 질문과 답글의 모양이 같고 깊이 제한이 없어서 **자기 자신을 다시 부른다.**
 * 답글의 답글의 답글도 이 부품 하나로 그려진다.
 *
 * 자식으로 내려갈수록 emit 을 몇 번이나 다시 올려 보내야 하므로,
 * 동작은 이벤트가 아니라 `handlers` 로 받는다 — 재귀 깊이와 무관하게 같은 함수를 쓴다.
 */

interface Handlers {
  /** 답글 폼을 열거나 닫는다. */
  toggleReply: (post: QnaApi.Post) => void;
  onDelete: (post: QnaApi.Post) => Promise<void> | void;
  onEdit: (post: QnaApi.Post) => void;
  /** 답글 저장. 저장이 끝나면 참을 돌려준다(폼을 닫는 데 쓴다). */
  onSubmitReply: (post: QnaApi.Post, content: string) => Promise<boolean>;
  /** 공개 여부 변경 (관리자) */
  onToggleVisibility: (post: QnaApi.Post, isPublic: boolean) => Promise<void>;
}

const props = defineProps<{
  /** 남의 글에 답하고 공개 여부를 정할 수 있는지 */
  canManage: boolean;
  /** 글을 쓸 수 있는지 */
  canWrite: boolean;
  handlers: Handlers;
  post: QnaApi.Post;
  /** 지금 답글 폼이 열려 있는 글 아이디 */
  replyingTo: null | string;
}>();

const draft = ref('');
const submitting = ref(false);

const isReplying = computed(() => props.replyingTo === props.post.id);

/**
 * 들여쓰기는 6단까지만 준다.
 * 답글이 깊어질수록 본문이 좁아져서, 그보다 깊어지면 읽기가 더 어려워진다.
 */
const indent = computed(() => `${Math.min(props.post.depth, 6) * 20}px`);

function formatDateTime(value?: null | string) {
  if (!value) return '';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

async function submitReply() {
  submitting.value = true;
  try {
    const ok = await props.handlers.onSubmitReply(props.post, draft.value);
    if (ok) draft.value = '';
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <div
    class="border-l-2 py-2"
    :class="post.isAnswer ? 'border-green-500/60' : 'border-border'"
    :style="{ marginLeft: indent }"
  >
    <div class="flex gap-2 pl-3">
      <!--
        아바타. 글마다 왼쪽에 두면 누가 쓴 글인지 훑어보기만 해도 구분된다 —
        답글이 깊어질수록 이름만으로는 눈에 잘 안 들어온다.
      -->
      <AuthorAvatar
        class="mt-0.5 shrink-0"
        :name="post.authorName || post.authorId"
        :photo="post.authorAvatar"
        :size="28"
      />

      <div class="min-w-0 flex-1">
        <!-- 머리글: 누가 · 언제 · 어떤 글인지 -->
        <div class="mb-1 flex flex-wrap items-center gap-2 text-xs">
          <span class="text-foreground font-medium">
            {{ post.authorName || post.authorId || '알 수 없음' }}
          </span>
          <span class="text-muted-foreground">
            {{ formatDateTime(post.createdAt) }}
          </span>

          <Tag v-if="post.isAnswer" color="green">답변</Tag>
          <Tag v-if="post.isMine" color="blue">내 글</Tag>
          <!--
            비공개는 작성자 본인과 관리자에게만 보인다. 그 사람들에게는
            "왜 남에게 안 보이는지" 를 알려 주는 표시가 된다.
          -->
          <Tag v-if="!post.isPublic" color="orange">비공개</Tag>

          <span class="flex-1"></span>

          <!-- 공개 여부는 관리자만 바꾼다. -->
          <Tooltip
            v-if="canManage"
            title="켜면 모든 사용자에게 보입니다. 이 글의 답글은 각각 따로 정합니다."
          >
            <Switch
              :checked="post.isPublic"
              checked-children="공개"
              size="small"
              un-checked-children="비공개"
              @change="handlers.onToggleVisibility(post, !post.isPublic)"
            />
          </Tooltip>
        </div>

        <!-- 본문 -->
        <!-- eslint-disable-next-line vue/no-v-html -->
        <div class="qna-body text-sm" v-html="post.content"></div>

        <!-- 동작 -->
        <div class="mt-1 flex items-center gap-1">
          <Button
            v-if="canWrite"
            class="!px-1"
            size="small"
            type="link"
            @click="handlers.toggleReply(post)"
          >
            <template #icon>
              <IconifyIcon class="mr-1 size-3.5" icon="lucide:reply" />
            </template>
            {{ isReplying ? '취소' : '답글' }}
          </Button>

          <Button
            v-if="post.canEdit"
            class="!px-1"
            size="small"
            type="link"
            @click="handlers.onEdit(post)"
          >
            <template #icon>
              <IconifyIcon class="mr-1 size-3.5" icon="lucide:edit" />
            </template>
            수정
          </Button>

          <Popconfirm
            v-if="post.canEdit"
            cancel-text="취소"
            ok-text="삭제"
            title="이 글과 달린 답글까지 함께 지워집니다. 삭제할까요?"
            @confirm="handlers.onDelete(post)"
          >
            <Button class="!px-1" danger size="small" type="link">
              <template #icon>
                <IconifyIcon class="mr-1 size-3.5" icon="lucide:trash-2" />
              </template>
              삭제
            </Button>
          </Popconfirm>
        </div>

        <!-- 답글 쓰기 -->
        <div v-if="isReplying" class="border-border mt-2 rounded border p-2">
          <!-- 영상 넣기는 관리자만. 서버가 관리자 본문에만 iframe 을 남긴다. -->
          <RichEditor
            v-model="draft"
            :allow-video="canManage"
            :min-height="90"
            biz-type="qna"
            placeholder="답글을 입력하세요. 이미지는 붙여넣기로 바로 넣을 수 있습니다."
            toolbar="compact"
          />
          <div class="mt-2 flex items-center justify-end gap-2">
            <span
              v-if="!canManage"
              class="text-muted-foreground mr-auto text-xs"
            >
              올린 글은 관리자가 공개하기 전까지 본인과 관리자에게만 보입니다.
            </span>
            <Button size="small" @click="handlers.toggleReply(post)">
              취소
            </Button>
            <Button
              :loading="submitting"
              size="small"
              type="primary"
              @click="submitReply"
            >
              답글 등록
            </Button>
          </div>
        </div>
      </div>
    </div>

    <!--
      답글. 같은 부품을 다시 부른다 — 깊이 제한이 없다.
      SFC 는 파일명(qna-post.vue → QnaPost)으로 자기 자신을 부를 수 있다.
    -->
    <QnaPost
      v-for="child in post.children"
      :key="child.id"
      :can-manage="canManage"
      :can-write="canWrite"
      :handlers="handlers"
      :post="child"
      :replying-to="replyingTo"
    />
  </div>
</template>

<style scoped>
/*
  본문은 편집기(tiptap)가 만든 HTML 이다. Tailwind 초기화가 지운 기본 여백만 되살린다.
*/
.qna-body :deep(p) {
  margin: 0 0 0.4em;
}

.qna-body :deep(p:last-child) {
  margin-bottom: 0;
}

.qna-body :deep(ul),
.qna-body :deep(ol) {
  margin: 0 0 0.4em;
  padding-left: 1.5em;
  list-style: revert;
}

.qna-body :deep(img) {
  max-width: 100%;
  height: auto;
  border-radius: 4px;
}

/* 영상. 답글은 들여쓰기 때문에 폭이 더 좁다 — 넘치지 않게 눌러 둔다. */
.qna-body :deep(iframe) {
  display: block;
  max-width: 100%;
  margin: 0.4em 0;
  border: 0;
}

.qna-body :deep(a) {
  color: hsl(var(--primary));
  text-decoration: underline;
}

.qna-body :deep(blockquote) {
  margin: 0 0 0.4em;
  padding-left: 0.75em;
  border-left: 3px solid hsl(var(--border));
  color: hsl(var(--muted-foreground));
}

.qna-body :deep(pre) {
  padding: 0.5em 0.75em;
  overflow-x: auto;
  background: hsl(var(--muted));
  border-radius: 4px;
}

.qna-body :deep(table) {
  border-collapse: collapse;
}

.qna-body :deep(td),
.qna-body :deep(th) {
  padding: 0.25em 0.5em;
  border: 1px solid hsl(var(--border));
}
</style>
