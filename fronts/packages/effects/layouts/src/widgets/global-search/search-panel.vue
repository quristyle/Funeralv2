<script setup lang="ts">
import type { MenuRecordRaw } from '@vben/types';

import { nextTick, onMounted, ref, shallowRef, watch } from 'vue';
import { useRouter } from 'vue-router';

import { SearchX, X } from '@vben/icons';
import { $t } from '@vben/locales';
import { mapTree, traverseTreeValues, uniqueByField } from '@vben/utils';

import { VbenIcon, VbenScrollbar } from '@vben-core/shadcn-ui';
import { isHttpUrl } from '@vben-core/shared/utils';

import { onKeyStroke, useLocalStorage, useThrottleFn } from '@vueuse/core';

defineOptions({
  name: 'SearchPanel',
});

const props = withDefaults(
  defineProps<{ keyword?: string; menus?: MenuRecordRaw[] }>(),
  {
    keyword: '',
    menus: () => [],
  },
);
const emit = defineEmits<{ close: [] }>();

const router = useRouter();
const searchHistory = useLocalStorage<MenuRecordRaw[]>(
  `__search-history-${location.hostname}__`,
  [],
);
const activeIndex = ref(-1);
const searchItems = shallowRef<MenuRecordRaw[]>([]);
const searchResults = ref<MenuRecordRaw[]>([]);

const handleSearch = useThrottleFn(search, 200);

// 검색 함수: 검색 키워드에 따라 일치하는 메뉴 항목을 찾음
function search(searchKey: string) {
  // 검색 키워드 앞뒤 공백 제거
  searchKey = searchKey.trim();

  // 검색 키워드가 비어 있으면 검색 결과 초기화 후 반환
  if (!searchKey) {
    searchResults.value = [];
    return;
  }

  // 검색 키워드로 정규식 생성
  const reg = createSearchReg(searchKey);

  // 결과 배열 초기화
  const results: MenuRecordRaw[] = [];

  // 검색 항목 순회
  traverseTreeValues(searchItems.value, (item) => {
    // 메뉴 항목 이름이 정규식과 일치하면 결과 배열에 추가
    if (reg.test(item.name?.toLowerCase())) {
      results.push(item);
    }
  });

  // 검색 결과 업데이트
  searchResults.value = results;

  // 검색 결과가 있으면 인덱스를 0으로 설정
  if (results.length > 0) {
    activeIndex.value = 0;
  }
}

// 키보드 상하 키로 보이지 않는 위치로 이동할 때 스크롤바 자동 이동
function scrollIntoView() {
  const element = document.querySelector(
    `[data-search-item="${activeIndex.value}"]`,
  );

  if (element) {
    element.scrollIntoView({ block: 'nearest' });
  }
}

// 엔터 키보드 이벤트
async function handleEnter() {
  if (searchResults.value.length === 0) {
    return;
  }
  const result = searchResults.value;
  const index = activeIndex.value;
  if (result.length === 0 || index < 0) {
    return;
  }
  const to = result[index];
  if (to) {
    searchHistory.value = uniqueByField([...searchHistory.value, to], 'path');
    handleClose();
    await nextTick();
    if (isHttpUrl(to.path)) {
      window.open(to.path, '_blank');
    } else {
      router.push({ path: to.path, replace: true });
    }
  }
}

// 위쪽 화살표 키
function handleUp() {
  if (searchResults.value.length === 0) {
    return;
  }
  activeIndex.value--;
  if (activeIndex.value < 0) {
    activeIndex.value = searchResults.value.length - 1;
  }
  scrollIntoView();
}

// 아래쪽 화살표 키
function handleDown() {
  if (searchResults.value.length === 0) {
    return;
  }
  activeIndex.value++;
  if (activeIndex.value > searchResults.value.length - 1) {
    activeIndex.value = 0;
  }
  scrollIntoView();
}

// 검색 모달 닫기
function handleClose() {
  searchResults.value = [];
  emit('close');
}

// 마우스가 특정 라인으로 이동할 때 활성화
function handleMouseenter(e: MouseEvent) {
  const index = (e.target as HTMLElement)?.dataset.index;
  activeIndex.value = Number(index);
}

function removeItem(index: number) {
  if (props.keyword) {
    searchResults.value.splice(index, 1);
  } else {
    searchHistory.value.splice(index, 1);
  }
  activeIndex.value = Math.max(activeIndex.value - 1, 0);
  scrollIntoView();
}

// 이스케이프가 필요한 모든 특수 문자 저장
const code = new Set([
  '$',
  '(',
  ')',
  '*',
  '+',
  '.',
  '?',
  '[',
  '\\',
  ']',
  '^',
  '{',
  '|',
  '}',
]);

// 변환 함수: 특수 문자 이스케이프
function transform(c: string) {
  // 문자가 특수 문자 목록에 있으면 이스케이프된 문자 반환
  // 그렇지 않으면 문자 그대로 반환
  return code.has(c) ? `\\${c}` : c;
}

// 검색 정규식 생성
function createSearchReg(key: string) {
  // 입력 문자열을 개별 문자로 분리
  // 각 문자 이스케이프
  // 모든 문자를 '.*'로 연결하여 정규식 생성
  const keys = [...key].map((item) => transform(item)).join('.*');
  // 생성된 정규식 반환
  return new RegExp(`.*${keys}.*`);
}

watch(
  () => props.keyword,
  (val) => {
    if (val) {
      handleSearch(val);
    } else {
      searchResults.value = searchHistory.value;
    }
  },
);

onMounted(() => {
  searchItems.value = mapTree(props.menus, (item) => {
    return {
      ...item,
      name: $t(item?.name),
    };
  });
  if (searchHistory.value.length > 0) {
    searchResults.value = searchHistory.value;
  }
  // 엔터 검색
  onKeyStroke('Enter', handleEnter);
  // 키보드 화살표 키 감시
  onKeyStroke('ArrowUp', handleUp);
  onKeyStroke('ArrowDown', handleDown);
  // ESC 키로 닫기
  onKeyStroke('Escape', handleClose);
});
</script>

<template>
  <VbenScrollbar>
    <div class="flex! h-full justify-center px-2 sm:max-h-112.5">
      <!-- 검색 결과 없음 -->
      <div
        v-if="keyword && searchResults.length === 0"
        class="text-center text-muted-foreground"
      >
        <SearchX class="mx-auto mt-4 size-12" />
        <p class="mt-6 mb-10 text-xs">
          {{ $t('ui.widgets.search.noResults') }}
          <span class="text-sm font-medium text-foreground">
            "{{ keyword }}"
          </span>
        </p>
      </div>
      <!-- 최근 검색 기록 및 검색 결과 없음 -->
      <div
        v-if="!keyword && searchResults.length === 0"
        class="text-center text-muted-foreground"
      >
        <p class="my-10 text-xs">
          {{ $t('ui.widgets.search.noRecent') }}
        </p>
      </div>

      <ul v-show="searchResults.length > 0" class="w-full">
        <li
          v-if="searchHistory.length > 0 && !keyword"
          class="mb-2 text-xs text-muted-foreground"
        >
          {{ $t('ui.widgets.search.recent') }}
        </li>
        <li
          v-for="(item, index) in uniqueByField(searchResults, 'path')"
          :key="item.path"
          :class="
            activeIndex === index
              ? 'active bg-primary text-primary-foreground'
              : ''
          "
          :data-index="index"
          :data-search-item="index"
          class="group mb-3 flex-center w-full cursor-pointer rounded-lg bg-accent p-4"
          @click="handleEnter"
          @mouseenter="handleMouseenter"
        >
          <VbenIcon :icon="item.icon" class="mr-2 size-5 shrink-0" fallback />

          <span class="flex-1">{{ item.name }}</span>
          <div
            class="flex-center rounded-full p-1 hover:scale-110 hover:text-primary-foreground dark:hover:bg-accent"
            @click.stop="removeItem(index)"
          >
            <X class="size-4" />
          </div>
        </li>
      </ul>
    </div>
  </VbenScrollbar>
</template>
