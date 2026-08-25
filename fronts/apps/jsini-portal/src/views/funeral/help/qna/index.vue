<script lang="ts" setup>
import type { QnaApi } from '#/api/portal/qna';

import { computed, onMounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import {
  Button,
  Card,
  Checkbox,
  Collapse,
  CollapsePanel,
  Empty,
  Form,
  FormItem,
  Input,
  message,
  Modal,
  Pagination,
  Segmented,
  Space,
  Spin,
  Tag,
} from 'ant-design-vue';

import {
  createQnaPost,
  deleteQnaPost,
  getQnaList,
  getQnaThread,
  setQnaVisibility,
  updateQnaPost,
} from '#/api/portal/qna';
import { RichEditor } from '#/components/rich-editor';

import AuthorAvatar from './modules/author-avatar.vue';
import QnaPost from './modules/qna-post.vue';

/**
 * [Q&A]
 *
 * 사용자가 질문하고 관리자가 답한다. 답글에 다시 질문할 수 있고,
 * 그 답글에 또 답글을 달 수 있다 — 깊이 제한이 없다.
 *
 * [무엇이 보이나]
 *   관리자   전부
 *   그 외    관리자가 공개한 글 + 자기가 쓴 글
 *
 * 답글도 같은 규칙이다. 부모가 안 보이면 그 아래는 통째로 안 보인다 —
 * 무엇에 대한 답인지 알 수 없는 답글만 떠 있으면 오히려 혼란스럽기 때문이다.
 *
 * **거르는 일은 전부 서버가 한다.** 화면은 받은 것을 그대로 그린다.
 * 버튼을 켜고 끄는 판단도 서버가 준 값(`canManage` · `canWrite` · `canEdit`)을 쓴다.
 * 화면이 권한 스토어만 보고 판단하면 권한이 늦게 도착했을 때 서버와 어긋난다.
 *
 * [화면 구성]
 * 세로 스크롤을 만들지 않으려고(준수사항 4) 조회 줄과 쪽 넘기기는 고정하고
 * 목록만 안에서 스크롤한다. 질문을 누르면 그 스레드가 펼쳐진다.
 */

const FILTER_ALL = 'all';

const loading = ref(false);
const saving = ref(false);

const items = ref<QnaApi.Post[]>([]);
const canManage = ref(false);
const canWrite = ref(false);

const keyword = ref('');
const filter = ref(FILTER_ALL);
const page = ref(1);
const pageSize = ref(10);
const total = ref(0);

/** 펼쳐 둔 질문 */
const openKeys = ref<string[]>([]);
/** 답글 폼이 열려 있는 글. 한 번에 하나만 연다. */
const replyingTo = ref<null | string>(null);

/** 질문 등록 · 글 수정 창 */
const modalOpen = ref(false);
/** 수정 중인 글. null 이면 새 질문이다. */
const editing = ref<null | QnaApi.Post>(null);

const form = reactive<{ content: string; isPublic: boolean; title: string }>({
  content: '',
  isPublic: true,
  title: '',
});

/** 공개 대기는 관리자에게만 뜻이 있다. */
const filterOptions = computed(() => {
  const options = [
    { label: '전체', value: FILTER_ALL },
    { label: '내 질문', value: 'mine' },
    { label: '답변 없음', value: 'unanswered' },
  ];
  if (canManage.value) {
    options.push({ label: '공개 대기', value: 'pending' });
  }
  return options;
});

function formatDateTime(value?: null | string) {
  if (!value) return '';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/**
 * 알맹이가 있는 본문인지.
 *
 * 편집기는 아무것도 안 써도 `<p></p>` 를 보낸다. 길이만 보면 '내용 있음' 이 된다.
 * 이미지만 붙여넣은 본문은 글자가 없어도 내용이 있는 것이다.
 * (서버도 같은 판단을 한다 — 여기서 미리 걸러 헛걸음을 줄인다.)
 */
function hasContent(html: string) {
  if (!html) return false;
  if (/<img\b/i.test(html)) return true;
  return (
    html
      .replaceAll(/<[^>]*>/g, '')
      .replaceAll('&nbsp;', ' ')
      .trim().length > 0
  );
}

async function loadData() {
  loading.value = true;
  try {
    const res = await getQnaList({
      filter: filter.value === FILTER_ALL ? undefined : filter.value,
      keyword: keyword.value.trim() || undefined,
      page: page.value,
      pageSize: pageSize.value,
    });

    items.value = res.items ?? [];
    canManage.value = res.canManage;
    canWrite.value = res.canWrite;
    total.value = res.total ?? 0;
  } finally {
    loading.value = false;
  }
}

/** 조건이 바뀌면 첫 쪽부터 다시 본다. */
async function search() {
  page.value = 1;
  replyingTo.value = null;
  await loadData();
}

/**
 * 스레드 하나만 다시 받아 그 자리에 갈아 끼운다.
 * 답글을 달 때마다 목록 전체를 다시 받으면 펼쳐 둔 것이 접히고 스크롤이 튄다.
 */
async function refreshThread(rootId: string) {
  const thread = await getQnaThread(rootId);
  const index = items.value.findIndex((item) => item.id === rootId);

  if (!thread) {
    // 지워졌다. 목록에서도 뺀다.
    if (index !== -1) items.value.splice(index, 1);
    return;
  }

  if (index === -1) await loadData();
  else items.value.splice(index, 1, thread);
}

// ── 질문 등록 · 수정 ────────────────────────────────────────

function openAsk() {
  editing.value = null;
  Object.assign(form, {
    content: '',
    // 관리자는 올리면서 바로 공개할 수 있다. 일반 사용자의 값은 서버가 무시한다.
    isPublic: true,
    title: '',
  });
  modalOpen.value = true;
}

function openEdit(post: QnaApi.Post) {
  editing.value = post;
  Object.assign(form, {
    content: post.content,
    isPublic: post.isPublic,
    title: post.title ?? '',
  });
  modalOpen.value = true;
}

async function onSave() {
  const isRoot = !editing.value || editing.value.parentId == null;

  if (isRoot && !form.title.trim()) {
    message.warning('제목을 입력하세요.');
    return;
  }
  if (!hasContent(form.content)) {
    message.warning('내용을 입력하세요.');
    return;
  }

  saving.value = true;
  try {
    if (editing.value) {
      await updateQnaPost(editing.value.id, {
        content: form.content,
        // 공개 여부는 관리자가 보낸 값만 서버가 반영한다.
        isPublic: canManage.value ? form.isPublic : undefined,
        title: isRoot ? form.title.trim() : undefined,
      });
      message.success('글을 수정했습니다.');
      modalOpen.value = false;
      await refreshThread(editing.value.rootId);
    } else {
      await createQnaPost({
        content: form.content,
        isPublic: canManage.value ? form.isPublic : undefined,
        title: form.title.trim(),
      });
      message.success(
        canManage.value
          ? '질문을 등록했습니다.'
          : '질문을 등록했습니다. 관리자가 확인한 뒤 답변을 남깁니다.',
      );
      modalOpen.value = false;
      await search();
    }
  } finally {
    saving.value = false;
  }
}

// ── 글 하나에 대한 동작. 재귀 부품에 그대로 넘긴다. ─────────

const handlers = {
  toggleReply(post: QnaApi.Post) {
    replyingTo.value = replyingTo.value === post.id ? null : post.id;
  },

  onEdit(post: QnaApi.Post) {
    openEdit(post);
  },

  async onDelete(post: QnaApi.Post) {
    await deleteQnaPost(post.id);
    message.success('글을 삭제했습니다.');

    // 질문(뿌리)을 지웠으면 스레드가 사라진다. 목록을 다시 받는다.
    if (post.parentId == null) await loadData();
    else await refreshThread(post.rootId);
  },

  async onSubmitReply(post: QnaApi.Post, content: string) {
    if (!hasContent(content)) {
      message.warning('내용을 입력하세요.');
      return false;
    }

    await createQnaPost({ content, parentId: post.id });
    message.success(
      canManage.value
        ? '답변을 등록했습니다.'
        : '답글을 등록했습니다. 관리자가 공개하기 전까지 본인과 관리자에게만 보입니다.',
    );

    replyingTo.value = null;
    await refreshThread(post.rootId);
    return true;
  },

  async onToggleVisibility(post: QnaApi.Post, isPublic: boolean) {
    await setQnaVisibility(post.id, isPublic);
    message.success(isPublic ? '공개로 바꿨습니다.' : '비공개로 바꿨습니다.');
    await refreshThread(post.rootId);
  },
};

/** 스레드를 통째로 공개·비공개로 바꾼다. 답글이 여러 개일 때 한 번에 처리한다. */
async function toggleThread(root: QnaApi.Post, isPublic: boolean) {
  await setQnaVisibility(root.id, isPublic, true);
  message.success(
    isPublic
      ? '질문과 답글을 모두 공개했습니다.'
      : '질문과 답글을 모두 비공개로 바꿨습니다.',
  );
  await refreshThread(root.id);
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height>
    <div class="flex h-full flex-col gap-3">
      <!-- 조회 줄 -->
      <Card class="shrink-0" size="small">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <Space wrap>
            <Input
              v-model:value="keyword"
              allow-clear
              placeholder="제목 + 내용 검색"
              style="width: 220px"
              @press-enter="search"
            />
            <Button :loading="loading" @click="search">조회</Button>
            <Segmented
              v-model:value="filter"
              :options="filterOptions"
              @change="search"
            />
          </Space>

          <Button v-if="canWrite" type="primary" @click="openAsk">
            질문하기
          </Button>
        </div>
      </Card>

      <!-- 목록 -->
      <Card
        :body-style="{ padding: 0, height: '100%', overflow: 'hidden' }"
        class="min-h-0 flex-1"
        size="small"
      >
        <!--
          Spin 으로 감싸지 않는다. antd 가 안쪽에 감싸개를 하나 더 만들어서
          높이 사슬(h-full)이 그 자리에서 끊긴다. 대신 겹쳐 띄운다.
        -->
        <div class="relative h-full">
          <div
            v-if="loading"
            class="absolute inset-0 z-10 flex items-center justify-center bg-background/60"
          >
            <Spin />
          </div>

          <div class="h-full overflow-auto p-3">
            <Empty
              v-if="items.length === 0"
              class="py-16"
              :description="
                keyword || filter !== 'all'
                  ? '조건에 맞는 질문이 없습니다.'
                  : '등록된 질문이 없습니다.'
              "
            />

            <Collapse v-model:active-key="openKeys" ghost>
              <CollapsePanel
                v-for="root in items"
                :key="root.id"
                class="rounded border border-border !mb-2"
              >
                <template #header>
                  <!-- 질문한 사람의 아바타를 제목 왼쪽에 둔다. 목록을 훑을 때
                       누가 올린 질문인지 이름을 읽기 전에 눈에 들어온다. -->
                  <div class="flex min-w-0 items-start gap-2">
                    <AuthorAvatar
                      class="mt-0.5 shrink-0"
                      :name="root.authorName || root.authorId"
                      :photo="root.authorAvatar"
                      :size="32"
                    />

                    <div class="flex min-w-0 flex-1 flex-col gap-1">
                    <div class="flex min-w-0 items-center gap-2">
                      <span class="text-primary shrink-0 font-bold">Q</span>
                      <span class="min-w-0 flex-1 truncate font-medium">
                        {{ root.title }}
                      </span>
                    </div>
                    <div
                      class="text-muted-foreground flex flex-wrap items-center gap-2 text-xs"
                    >
                      <span>{{ root.authorName || root.authorId }}</span>
                      <span>{{ formatDateTime(root.createdAt) }}</span>

                      <Tag v-if="root.isAnswered" color="green">답변 완료</Tag>
                      <Tag v-else color="default">답변 대기</Tag>

                      <Tag v-if="root.isMine" color="blue">내 질문</Tag>
                      <Tag v-if="!root.isPublic" color="orange">비공개</Tag>

                      <span v-if="root.replyCount" class="flex items-center gap-1">
                        <IconifyIcon
                          class="size-3.5"
                          icon="lucide:message-square"
                        />
                        {{ root.replyCount }}
                      </span>
                    </div>
                    </div>
                  </div>
                </template>

                <!--
                  스레드 전체 공개. 답글이 여러 개일 때 하나씩 켜지 않아도 되게 둔다.
                  글 하나씩 켜고 끄는 스위치는 각 글 옆에 있다.
                -->
                <template v-if="canManage" #extra>
                  <div class="flex items-center gap-1" @click.stop>
                    <Button
                      size="small"
                      type="link"
                      @click="toggleThread(root, !root.isPublic)"
                    >
                      {{ root.isPublic ? '스레드 전체 비공개' : '스레드 전체 공개' }}
                    </Button>
                  </div>
                </template>

                <!-- 질문과 그 아래 답글 전부. 부품이 자기를 다시 불러 이어 그린다. -->
                <QnaPost
                  :can-manage="canManage"
                  :can-write="canWrite"
                  :handlers="handlers"
                  :post="root"
                  :replying-to="replyingTo"
                />
              </CollapsePanel>
            </Collapse>
          </div>
        </div>
      </Card>

      <!-- 쪽 넘기기. 목록이 길어져도 화면 밖으로 밀려나지 않게 아래에 고정한다. -->
      <div v-if="total > pageSize" class="shrink-0 text-right">
        <Pagination
          v-model:current="page"
          v-model:page-size="pageSize"
          :page-size-options="['10', '20', '50']"
          :total="total"
          show-size-changer
          size="small"
          @change="loadData"
        />
      </div>
    </div>

    <!-- 질문 등록 · 글 수정 -->
    <Modal
      v-model:open="modalOpen"
      :confirm-loading="saving"
      :title="editing ? '글 수정' : '질문하기'"
      :width="720"
      cancel-text="취소"
      ok-text="저장"
      @ok="onSave"
    >
      <Form layout="vertical">
        <!-- 제목은 질문(뿌리)만 갖는다. 답글을 고칠 때는 보이지 않는다. -->
        <FormItem
          v-if="!editing || editing.parentId == null"
          label="제목"
          required
        >
          <Input v-model:value="form.title" :maxlength="200" />
        </FormItem>

        <FormItem
          extra="이미지는 붙여넣기·드래그로 바로 넣을 수 있습니다. 넣는 즉시 파일로 보관되고 본문에는 경로만 들어갑니다."
          label="내용"
          required
        >
          <!--
            영상 넣기와 HTML 직접 입력은 관리자에게만 준다.
            서버가 관리자 본문에만 `<iframe>` 을 남기므로, 일반 사용자에게 버튼을
            보여 주면 넣었는데 저장하면 사라지는 화면이 된다.
          -->
          <RichEditor
            v-model="form.content"
            :allow-video="canManage"
            :html-source="canManage"
            :min-height="220"
            biz-type="qna"
            placeholder="궁금한 점을 적어 주세요."
          />
        </FormItem>

        <!-- 공개 여부는 관리자만 정한다. -->
        <FormItem v-if="canManage">
          <Checkbox v-model:checked="form.isPublic">
            공개 (끄면 작성자 본인과 관리자에게만 보입니다)
          </Checkbox>
        </FormItem>
        <div v-else class="text-xs text-muted-foreground">
          올린 글은 관리자가 공개하기 전까지 본인과 관리자에게만 보입니다.
        </div>
      </Form>
    </Modal>
  </Page>
</template>
