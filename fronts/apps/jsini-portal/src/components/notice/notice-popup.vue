<script lang="ts" setup>
import type { NoticeApi } from '#/api/portal/notice';

import { computed, onMounted, ref, watch } from 'vue';

import { useAccessStore } from '@vben/stores';

import { Button, Checkbox, Modal, Tag } from 'ant-design-vue';

import { getPopupNotices, getPublicPopupNotices } from '#/api/portal/notice';

/**
 * [공지 팝업]
 *
 * 공지는 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
 *
 * 띄우는 시점이 두 가지다.
 *  - `is_public` 공지: 로그인하지 않아도 볼 수 있다. 화면이 뜨자마자 띄운다.
 *  - 그 외 공지: 로그인한 뒤에 띄운다.
 *
 * 그래서 로그인 상태를 지켜보다가, 로그인되면 전체 목록으로 한 번 더 받아 온다.
 *
 * '오늘 하루 보지 않기'는 브라우저에만 남긴다(localStorage).
 * 계정별로 남기려면 서버에 읽음 기록이 필요한데, 공지 성격상 그만한 무게가 아니다.
 */

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

function goPrev() {
  if (hasPrev.value) {
    index.value -= 1;
    dontShowToday.value = false;
  }
}

// 로그인하면 전체 공지로 한 번 더 받는다.
// 로그아웃하면 공개 공지만 남도록 다시 받는다.
watch(loggedIn, () => {
  load();
});

onMounted(load);
</script>

<template>
  <Modal
    v-model:open="open"
    :footer="null"
    :mask-closable="false"
    :title="null"
    :width="640"
    centered
  >
    <template v-if="current">
      <!-- 제목 -->
      <div class="mb-3 border-b border-border pb-3">
        <div class="mb-1 flex flex-wrap items-center gap-2">
          <Tag v-if="current.isPublic" color="blue">전체 공개</Tag>
          <span v-if="notices.length > 1" class="text-xs text-muted-foreground">
            {{ index + 1 }} / {{ notices.length }}
          </span>
        </div>
        <h3 class="m-0 text-lg font-semibold">{{ current.title }}</h3>
      </div>

      <!-- 본문 -->
      <!-- eslint-disable-next-line vue/no-v-html -->
      <div
        class="notice-body max-h-[50vh] overflow-y-auto text-sm"
        v-html="current.content || ''"
      ></div>

      <!-- 첨부파일 -->
      <div
        v-if="current.files?.length"
        class="mt-3 rounded border border-border p-2"
      >
        <div class="mb-1 text-xs font-medium text-muted-foreground">첨부파일</div>
        <ul class="m-0 list-none space-y-1 p-0">
          <li v-for="file in current.files" :key="file.fileId">
            <a
              class="text-sm text-primary hover:underline"
              :href="downloadUrl(file)"
              rel="noopener"
              target="_blank"
            >
              {{ file.fileName }}
            </a>
            <span class="ml-1 text-xs text-muted-foreground">
              {{ formatSize(file.fileSize) }}
            </span>
          </li>
        </ul>
      </div>

      <!-- 아래 줄 -->
      <div class="mt-4 flex items-center justify-between border-t border-border pt-3">
        <Checkbox v-model:checked="dontShowToday">오늘 하루 보지 않기</Checkbox>
        <div class="flex gap-2">
          <Button v-if="hasPrev" @click="goPrev">이전</Button>
          <Button type="primary" @click="closeCurrent">
            {{ hasNext ? '다음' : '닫기' }}
          </Button>
        </div>
      </div>
    </template>
  </Modal>
</template>

<style scoped>
.notice-body :deep(img) {
  max-width: 100%;
  height: auto;
}

.notice-body :deep(a) {
  color: hsl(var(--primary));
  text-decoration: underline;
}
</style>
