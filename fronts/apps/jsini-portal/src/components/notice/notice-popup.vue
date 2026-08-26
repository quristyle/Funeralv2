<script lang="ts" setup>
import type { NoticeApi } from '#/api/portal/notice';

import { computed, onMounted, ref, watch } from 'vue';

import { IconifyIcon } from '@vben/icons';
import { useAccessStore } from '@vben/stores';

import { Button, Checkbox, Modal, Tag } from 'ant-design-vue';

import { getPopupNotices, getPublicPopupNotices } from '#/api/portal/notice';

/**
 * [공지 팝업]
 *
 * 공지는 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
 *
 * 쓰임이 둘이다.
 *
 * 1. `mode="auto"` (기본) — 앱 껍데기(`app.vue`)에 하나만 두고 스스로 띄운다.
 *    - `is_public` 공지: 로그인하지 않아도 볼 수 있다. 화면이 뜨자마자 띄운다.
 *    - 그 외 공지: 로그인한 뒤에 띄운다.
 *    그래서 로그인 상태를 지켜보다가, 로그인되면 전체 목록으로 한 번 더 받아 온다.
 *
 * 2. `mode="preview"` — 공지 관리 화면의 [미리보기]가 쓴다.
 *    스스로 조회하지 않고 `v-model:preview` 로 받은 공지 하나만 띄운다.
 *    **사용자에게 실제로 보이는 것과 같은 화면을 그대로 쓰는 것이 목적**이라
 *    별도의 미리보기 화면을 따로 만들지 않았다.
 *    미리보기는 흔적을 남기지 않는다 — '오늘 하루 보지 않기'가 없고 localStorage 도 건드리지 않는다.
 *
 * '오늘 하루 보지 않기'는 브라우저에만 남긴다(localStorage).
 * 계정별로 남기려면 서버에 읽음 기록이 필요한데, 공지 성격상 그만한 무게가 아니다.
 */

interface Props {
  /** `auto` 는 스스로 조회해 띄운다. `preview` 는 넘겨받은 공지만 띄운다. */
  mode?: 'auto' | 'preview';
  /** 미리보기로 띄울 공지. null 이면 닫힌다. `mode="preview"` 에서만 쓴다. */
  preview?: NoticeApi.Notice | null;
}

const props = withDefaults(defineProps<Props>(), {
  mode: 'auto',
  preview: null,
});

const emit = defineEmits<{
  (e: 'update:preview', value: null | NoticeApi.Notice): void;
}>();

const isPreview = computed(() => props.mode === 'preview');

const DISMISS_KEY = 'jsini-notice-dismissed';

const accessStore = useAccessStore();

const notices = ref<NoticeApi.Notice[]>([]);
/** 지금 보고 있는 공지 순번 */
const index = ref(0);
const open = ref(false);
/** '오늘 하루 보지 않기' 체크 상태 */
const dontShowToday = ref(false);

/** 로그인 여부. 이 값이 바뀌면 목록을 다시 받는다. */
const loggedIn = computed(() => Boolean(accessStore.accessToken));

const current = computed(() => notices.value[index.value]);
const hasPrev = computed(() => index.value > 0);
const hasNext = computed(() => index.value < notices.value.length - 1);

/** 오늘 안 보기로 해 둔 공지 아이디 */
function dismissedIds(): string[] {
  try {
    const raw = localStorage.getItem(DISMISS_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as { ids: string[]; until: string };
    // 날짜가 지났으면 없던 일로 한다.
    if (parsed.until !== todayKey()) return [];
    return parsed.ids ?? [];
  } catch {
    return [];
  }
}

function todayKey() {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

function rememberDismissed(id: string) {
  const ids = new Set([...dismissedIds(), id]);
  localStorage.setItem(
    DISMISS_KEY,
    JSON.stringify({ ids: [...ids], until: todayKey() }),
  );
}

/**
 * 가림막을 기본보다 진하게, 그리고 살짝 흐리게 만든다.
 *
 * 이 팝업은 흰 상자를 쓰지 않고 패널만 띄우기 때문에 가림막이 옅으면 경계가 흐려진다.
 * 특히 **어두운 테마**에서는 패널(어두운 카드색)과 가림막이 비슷해져
 * 팝업인지 화면 일부인지 구분이 안 된다.
 * 가림막을 진하게 깔고 뒤를 흐리면 두 테마 모두에서 패널이 또렷하게 떠오른다.
 *
 * 바깥에 놓인 닫기·'오늘 하루 보지 않기' 를 흰색으로 그리는 것도
 * 이 진한 가림막을 전제로 한다.
 */
const MASK_STYLE = {
  backdropFilter: 'blur(3px)',
  backgroundColor: 'rgba(0, 0, 0, 0.65)',
};

/** 게시일. 시작일이 있으면 그걸, 없으면 등록일을 보여 준다. */
function noticeDate(notice?: NoticeApi.Notice) {
  const raw = notice?.startAt || notice?.createdAt;
  if (!raw) return '';
  const d = new Date(raw);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}. ${pad(d.getMonth() + 1)}. ${pad(d.getDate())}`;
}

/** 특정 공지로 바로 넘어간다(아래 점 표시를 눌렀을 때). */
function goTo(target: number) {
  if (target < 0 || target >= notices.value.length) return;
  index.value = target;
  dontShowToday.value = false;
}

/** 파일 크기를 읽기 좋게 */
function formatSize(bytes: number) {
  if (!bytes) return '';
  const units = ['B', 'KB', 'MB', 'GB'];
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  return `${value.toFixed(unit === 0 ? 0 : 1)}${units[unit]}`;
}

function downloadUrl(file: NoticeApi.NoticeFile) {
  return file.downloadUrl || `/api/file/download/id/${file.fileId}`;
}

async function load() {
  const list = loggedIn.value
    ? await getPopupNotices().catch(() => [])
    : await getPublicPopupNotices();

  const skip = new Set(dismissedIds());
  const fresh = list.filter((n) => !skip.has(n.id));

  notices.value = fresh;
  index.value = 0;
  dontShowToday.value = false;
  open.value = fresh.length > 0;
}

/** 지금 공지를 닫고 다음으로 넘어간다. 마지막이면 팝업을 닫는다. */
function closeCurrent() {
  // 미리보기는 관리자가 확인만 하는 것이라 아무것도 기록하지 않는다.
  if (isPreview.value) {
    open.value = false;
    return;
  }

  const notice = current.value;
  if (notice && dontShowToday.value) {
    rememberDismissed(notice.id);
  }

  if (hasNext.value) {
    index.value += 1;
    dontShowToday.value = false;
  } else {
    open.value = false;
  }
}

/**
 * 위쪽 × 로 팝업을 닫는다.
 *
 * '다음' 은 공지를 하나씩 넘기지만 × 는 **더 보지 않겠다는 뜻**이므로 바로 닫는다.
 * 체크해 둔 '오늘 하루 보지 않기' 는 지금 보고 있던 공지에만 적용한다.
 */
function closeAll() {
  if (!isPreview.value && current.value && dontShowToday.value) {
    rememberDismissed(current.value.id);
  }
  open.value = false;
}

function goPrev() {
  if (hasPrev.value) {
    index.value -= 1;
    dontShowToday.value = false;
  }
}

// 로그인하면 전체 공지로 한 번 더 받는다.
// 로그아웃하면 공개 공지만 남도록 다시 받는다.
watch(loggedIn, () => {
  if (!isPreview.value) load();
});

// 미리보기 대상이 들어오면 그 공지 하나만 띄운다.
watch(
  () => props.preview,
  (notice) => {
    if (!isPreview.value) return;
    notices.value = notice ? [notice] : [];
    index.value = 0;
    dontShowToday.value = false;
    open.value = Boolean(notice);
  },
  { immediate: true },
);

// 닫히면(닫기 버튼·ESC·X) 부모의 v-model 도 비운다. 같은 공지를 다시 눌러도 열리게 하기 위해서다.
watch(open, (value) => {
  if (isPreview.value && !value && props.preview) emit('update:preview', null);
});

onMounted(() => {
  if (!isPreview.value) load();
});
</script>

<template>
  <!--
    공지 팝업은 다른 팝업과 디자인이 다르다.

    닫기(×)와 '오늘 하루 보지 않기' 를 **흰 패널 바깥**(위·아래)에 두고 그 자리는 투명하게 둔다.
    조작 장치가 내용과 섞이지 않아 공지 자체에 눈이 가게 하려는 것이다.

    그래서 ant 가 그려 주는 껍데기(닫기 버튼·헤더·푸터)를 쓰지 않고,
    모달 내용 영역을 투명하게 만든 뒤 그 안에 패널을 직접 그린다.

    [준수사항 3] 팝업은 헤더를 잡고 옮길 수 있어야 한다.
    ant 헤더가 없으므로 제목 줄에 `data-drag-handle` 을 붙였다
    (`plugins/draggable-modal.ts` 가 이 표시도 손잡이로 받는다).
  -->
  <Modal
    v-model:open="open"
    :closable="false"
    :footer="null"
    :mask-closable="false"
    :mask-style="MASK_STYLE"
    :title="null"
    :width="620"
    centered
    wrap-class-name="jsini-notice-modal"
  >
    <template v-if="current">
      <!-- 패널 위: 닫기 -->
      <div class="mb-2 flex justify-end">
        <button
          aria-label="공지 닫기"
          class="jsini-notice-close"
          title="닫기"
          type="button"
          @click="closeAll"
        >
          <IconifyIcon class="size-5" icon="lucide:x" />
        </button>
      </div>

      <!-- 흰 패널 -->
      <div class="jsini-notice-panel bg-card overflow-hidden rounded-lg">
        <!-- 제목 줄 = 드래그 손잡이 -->
        <div
          class="border-border flex items-start gap-2.5 border-b px-4 py-3"
          data-drag-handle
        >
          <span
            class="bg-primary/10 text-primary mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-full"
          >
            <IconifyIcon class="size-4" icon="lucide:megaphone" />
          </span>
          <div class="min-w-0 flex-1">
            <div class="flex flex-wrap items-center gap-1.5">
              <span class="truncate text-base font-semibold leading-tight">
                {{ current.title }}
              </span>
              <Tag v-if="isPreview" color="orange">미리보기</Tag>
              <Tag v-if="current.isPublic" color="blue">전체 공개</Tag>
            </div>
            <div class="text-muted-foreground mt-0.5 text-xs font-normal">
              <span v-if="noticeDate(current)">{{ noticeDate(current) }}</span>
              <span v-if="noticeDate(current) && notices.length > 1"> · </span>
              <span v-if="notices.length > 1">
                공지 {{ index + 1 }} / {{ notices.length }}
              </span>
            </div>
          </div>
        </div>

        <div class="px-4 py-3">
          <!--
            내용이 한 줄뿐인 공지도 있어 최소 높이를 준다 —
            없으면 팝업이 제목 줄만 한 크기로 쪼그라들어 공지처럼 보이지 않는다.
          -->
          <!-- eslint-disable-next-line vue/no-v-html -->
          <div
            class="notice-body max-h-[52vh] min-h-[140px] overflow-y-auto text-sm leading-relaxed"
            v-html="current.content || '<p class=&quot;empty&quot;>내용이 없습니다.</p>'"
          ></div>

          <!-- 첨부파일 -->
          <div v-if="current.files?.length" class="border-border mt-3 border-t pt-3">
            <div
              class="text-muted-foreground mb-1.5 flex items-center gap-1 text-xs font-medium"
            >
              <IconifyIcon class="size-3.5" icon="lucide:paperclip" />
              첨부파일 {{ current.files.length }}
            </div>
            <!--
              [첨부파일명은 끝까지 보여 준다]

              예전에는 `max-w-[220px] truncate` 로 잘라서 긴 이름이 `…` 로 가려졌다.
              실제 첨부에 42자짜리(`ChatGPT-Image-2026년-5월-10일-오후-02_09_01.jpg`)가 있어
              무엇을 내려받는 것인지 알 수 없었다. 확장자가 잘리면 더 나쁘다.

              자르는 대신 **줄바꿈**으로 바꿨다.
                · `max-w-full` — 칸이 팝업 폭을 넘지 않게 한다(가로 스크롤 방지).
                · `break-all`  — 파일명은 띄어쓰기가 없는 경우가 많다. `break-words` 는
                                 끊을 자리를 못 찾아 그대로 넘쳐 흐른다.
                · 아이콘·용량은 `shrink-0` 으로 고정하고, 용량은 줄바꿈에서 떼어 놓는다.

              이름이 짧으면 예전처럼 여러 개가 한 줄에 나란히 붙는다.
            -->
            <ul class="m-0 flex list-none flex-wrap gap-1.5 p-0">
              <li
                v-for="file in current.files"
                :key="file.fileId"
                class="min-w-0 max-w-full"
              >
                <a
                  class="border-border hover:border-primary hover:text-primary flex max-w-full items-start gap-1 rounded-md border px-2 py-1 text-xs transition-colors"
                  :href="downloadUrl(file)"
                  rel="noopener"
                  target="_blank"
                >
                  <IconifyIcon class="mt-0.5 size-3.5 shrink-0" icon="lucide:download" />
                  <span class="min-w-0 break-all">{{ file.fileName }}</span>
                  <span class="text-muted-foreground shrink-0 whitespace-nowrap">
                    {{ formatSize(file.fileSize) }}
                  </span>
                </a>
              </li>
            </ul>
          </div>
        </div>
      </div>

      <!-- 패널 아래: 오늘 하루 보지 않기 · 이동 -->
      <div class="jsini-notice-below mt-3 flex flex-wrap items-center gap-3">
        <Checkbox v-if="!isPreview" v-model:checked="dontShowToday">
          <span class="text-xs">오늘 하루 보지 않기</span>
        </Checkbox>
        <span v-else class="text-xs opacity-80">
          실제 사용자에게 보이는 그대로입니다.
        </span>

        <span class="flex-1"></span>

        <!-- 공지가 여럿이면 점으로 위치를 보여 주고, 눌러서 바로 넘어갈 수 있게 한다 -->
        <div v-if="notices.length > 1" class="flex items-center gap-1.5">
          <button
            v-for="(item, i) in notices"
            :key="item.id"
            :aria-label="`${i + 1}번째 공지`"
            :class="i === index ? 'is-current' : ''"
            :title="item.title"
            class="jsini-notice-dot"
            type="button"
            @click="goTo(i)"
          ></button>
        </div>

        <div class="flex gap-2">
          <Button v-if="hasPrev" size="small" @click="goPrev">이전</Button>
          <Button size="small" type="primary" @click="closeCurrent">
            {{ hasNext ? '다음' : '닫기' }}
          </Button>
        </div>
      </div>
    </template>
  </Modal>
</template>

<style scoped>
/*
  공지 팝업 전용 껍데기.
  ant 가 그리는 흰 상자를 투명하게 만들어, 안에서 그린 패널만 보이게 한다.
  그래야 닫기(×)와 '오늘 하루 보지 않기' 를 패널 바깥에 둘 수 있다.
*/
:global(.jsini-notice-modal .ant-modal-content) {
  padding: 0;
  background: transparent;
  box-shadow: none;
}

/* 패널 위에 뜨는 닫기 — 어두운 가림막 위라 흰색으로 그린다 */
.jsini-notice-close {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
  color: rgb(255 255 255 / 85%);
  cursor: pointer;
  background: rgb(255 255 255 / 12%);
  border: 1px solid rgb(255 255 255 / 25%);
  border-radius: 9999px;
  transition:
    background-color 0.15s,
    color 0.15s;
}

.jsini-notice-close:hover {
  color: #fff;
  background: rgb(255 255 255 / 25%);
}

/* 패널 아래 줄 — 같은 이유로 흰색 계열 */
.jsini-notice-below {
  color: rgb(255 255 255 / 85%);
}

.jsini-notice-below :deep(.ant-checkbox-wrapper) {
  color: rgb(255 255 255 / 85%);
}

.jsini-notice-dot {
  width: 0.375rem;
  height: 0.375rem;
  background: rgb(255 255 255 / 40%);
  border-radius: 9999px;
  transition: all 0.15s;
}

.jsini-notice-dot:hover {
  background: rgb(255 255 255 / 70%);
}

.jsini-notice-dot.is-current {
  width: 1rem;
  background: #fff;
}

/* 밝은 가림막을 쓰는 환경(밝은 테마에서 mask 가 옅은 경우)을 위한 최소한의 대비 */
.jsini-notice-close,
.jsini-notice-below {
  text-shadow: 0 1px 2px rgb(0 0 0 / 35%);
}

.notice-body :deep(img) {
  max-width: 100%;
  height: auto;
  border-radius: 0.25rem;
}

.notice-body :deep(a) {
  color: hsl(var(--primary));
  text-decoration: underline;
}

.notice-body :deep(p) {
  margin: 0 0 0.5rem;
}

.notice-body :deep(p:last-child) {
  margin-bottom: 0;
}

.notice-body :deep(h2) {
  margin: 0.5rem 0;
  font-size: 1.05rem;
  font-weight: 600;
}

.notice-body :deep(h3) {
  margin: 0.5rem 0;
  font-size: 0.95rem;
  font-weight: 600;
}

.notice-body :deep(ul),
.notice-body :deep(ol) {
  padding-left: 1.25rem;
  margin: 0 0 0.5rem;
  list-style: revert;
}

.notice-body :deep(ul[data-type='taskList']) {
  padding-left: 0.25rem;
  list-style: none;
}

.notice-body :deep(ul[data-type='taskList'] li) {
  display: flex;
  gap: 0.5rem;
  align-items: flex-start;
}

.notice-body :deep(blockquote) {
  padding-left: 0.75rem;
  margin: 0 0 0.5rem;
  color: hsl(var(--muted-foreground));
  border-left: 3px solid hsl(var(--border));
}

.notice-body :deep(pre) {
  padding: 0.5rem 0.75rem;
  margin: 0 0 0.5rem;
  overflow-x: auto;
  font-family: ui-monospace, monospace;
  font-size: 0.8125rem;
  background-color: hsl(var(--muted));
  border-radius: 0.25rem;
}

.notice-body :deep(table) {
  width: 100%;
  margin: 0 0 0.5rem;
  border-collapse: collapse;
}

.notice-body :deep(table td),
.notice-body :deep(table th) {
  padding: 0.25rem 0.5rem;
  border: 1px solid hsl(var(--border));
}

.notice-body :deep(table th) {
  font-weight: 600;
  text-align: left;
  background-color: hsl(var(--muted));
}

.notice-body :deep(hr) {
  margin: 0.75rem 0;
  border-top: 1px solid hsl(var(--border));
}

/* 내용이 비었을 때 */
.notice-body :deep(p.empty) {
  color: hsl(var(--muted-foreground));
}
</style>
