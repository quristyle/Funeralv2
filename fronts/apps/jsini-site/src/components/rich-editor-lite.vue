<script setup lang="ts">
/**
 * 문의 폼용 경량 리치 에디터.
 *
 * 이 앱은 @vben 의존이 0 이라 포털의 RichEditor(tiptap)를 못 쓴다.
 * 문의 본문 꾸미기에 필요한 만큼만 — 굵게 · 기울임 · 밑줄 · 취소선 · 글자색 ·
 * 목록 · 인용 — contenteditable 로 만든다. 서버(InquiryHtmlSanitizer)가
 * 같은 허용 목록으로 다시 거르므로 여기서 못 막은 것도 저장 전에 걸러진다.
 *
 * v-model 은 HTML 문자열이다. 비었는지 판단은 부모가 plainText 로 한다.
 */
import { onBeforeUnmount, onMounted, ref, watch } from 'vue';

const model = defineModel<string>({ default: '' });

defineProps<{ placeholder?: string }>();

const editorRef = ref<HTMLElement | null>(null);
const focused = ref(false);

/**
 * 마지막으로 에디터 안에 있던 선택 영역.
 *
 * 툴바 버튼을 누르는 순간 브라우저에 따라 선택이 풀리거나 포커스가
 * 에디터 밖에 있을 수 있다 — 그 상태로 execCommand 를 부르면 아무 일도
 * 일어나지 않는다("블록 지정 후 툴바가 안 먹는" 증상). 그래서 에디터 안의
 * 선택을 계속 기억해 두고, 버튼 실행 직전에 복원한다.
 */
let savedRange: null | Range = null;

function saveSelection() {
  const sel = window.getSelection();
  if (!sel || sel.rangeCount === 0 || !editorRef.value) return;
  const range = sel.getRangeAt(0);
  if (editorRef.value.contains(range.commonAncestorContainer)) {
    savedRange = range.cloneRange();
  }
}

function restoreSelection() {
  if (!savedRange || !editorRef.value) return;
  if (!editorRef.value.contains(savedRange.commonAncestorContainer)) return;
  const sel = window.getSelection();
  if (!sel) return;
  sel.removeAllRanges();
  sel.addRange(savedRange);
}

/** 도구줄 버튼 정의 — execCommand 이름과 표시를 함께 둔다 */
const buttons = [
  { cmd: 'bold', label: 'B', title: '굵게', cls: 'font-bold' },
  { cmd: 'italic', label: 'I', title: '기울임', cls: 'italic' },
  { cmd: 'underline', label: 'U', title: '밑줄', cls: 'underline' },
  { cmd: 'strikeThrough', label: 'S', title: '취소선', cls: 'line-through' },
  { cmd: 'insertUnorderedList', label: '•', title: '글머리 목록', cls: '' },
  { cmd: 'insertOrderedList', label: '1.', title: '번호 목록', cls: '' },
] as const;

/** 글자색 팔레트 — 사이트 잉크 톤과 강조 몇 가지만 */
const colors = ['#1a1a1a', '#b91c1c', '#1d4ed8', '#047857', '#b45309'];

/** 글씨 크기 — execCommand fontSize 의 1~7 단계 중 쓸 만한 넷만 */
const fontSizes = [
  { value: '2', label: '작게' },
  { value: '3', label: '보통' },
  { value: '5', label: '크게' },
  { value: '7', label: '아주 크게' },
];

function onFontSize(e: Event) {
  const el = e.target as HTMLSelectElement;
  if (el.value) exec('fontSize', el.value);
  // 크기는 선택 영역마다 다를 수 있어 셀렉트에 상태를 남기지 않는다
  el.value = '';
}

function exec(cmd: string, value?: string) {
  editorRef.value?.focus();
  // 포커스 이동으로 선택이 풀렸으면 기억해 둔 선택을 되살린다 —
  // 이것이 없으면 블록을 지정하고 버튼을 눌러도 빈 선택에 적용돼 아무 일도 없다.
  restoreSelection();
  document.execCommand(cmd, false, value);
  saveSelection();
  sync();
}

function sync() {
  const html = editorRef.value?.innerHTML ?? '';
  // 내용 없이 빈 태그만 남으면 빈 값으로 취급한다
  model.value = editorRef.value?.textContent?.trim() ? html : '';
}

// 밖에서 값이 리셋될 때(전송 후 등)만 DOM 에 반영한다 —
// 입력 중에 innerHTML 을 다시 쓰면 커서가 튄다.
watch(model, (v) => {
  if (!editorRef.value) return;
  if (editorRef.value.innerHTML !== v && document.activeElement !== editorRef.value) {
    editorRef.value.innerHTML = v || '';
  }
});

onMounted(() => {
  if (editorRef.value && model.value) {
    editorRef.value.innerHTML = model.value;
  }
  // 드래그가 에디터 밖에서 끝나면 에디터의 mouseup 이 오지 않는다 —
  // 문서 단위 selectionchange 로 에디터 안의 선택을 놓치지 않고 기억한다.
  document.addEventListener('selectionchange', saveSelection);
});

onBeforeUnmount(() => {
  document.removeEventListener('selectionchange', saveSelection);
});
</script>

<template>
  <div class="border border-mist focus-within:border-ink">
    <!-- 도구줄 -->
    <div class="flex flex-wrap items-center gap-1 border-b border-mist px-2 py-1.5">
      <button
        v-for="b in buttons"
        :key="b.cmd"
        type="button"
        :title="b.title"
        class="flex h-7 w-7 items-center justify-center text-xs text-graphite hover:bg-paper hover:text-ink"
        :class="b.cls"
        @mousedown.prevent="exec(b.cmd)"
      >
        {{ b.label }}
      </button>

      <span class="mx-1 h-4 w-px bg-mist" />

      <button
        v-for="c in colors"
        :key="c"
        type="button"
        title="글자색"
        class="h-4 w-4 border border-mist"
        :style="{ backgroundColor: c }"
        @mousedown.prevent="exec('foreColor', c)"
      />

      <span class="mx-1 h-4 w-px bg-mist" />

      <!--
        크기 셀렉트는 mousedown 을 막지 않는다 — 막으면 목록이 안 열린다.
        여는 순간 에디터가 포커스를 잃지만, 기억해 둔 선택(savedRange)을
        exec() 가 복원하므로 블록에 그대로 적용된다.
      -->
      <select
        class="h-7 border border-mist bg-transparent px-1 text-xs text-graphite outline-none hover:border-ink"
        title="글씨 크기"
        value=""
        @change="onFontSize"
      >
        <option disabled value="">크기</option>
        <option v-for="s in fontSizes" :key="s.value" :value="s.value">{{ s.label }}</option>
      </select>

      <span class="mx-1 h-4 w-px bg-mist" />

      <button
        type="button"
        title="서식지우기"
        class="h-7 px-2 text-xs text-steel hover:bg-paper hover:text-ink"
        @mousedown.prevent="exec('removeFormat')"
      >
        서식지우기
      </button>
    </div>

    <!-- 본문 -->
    <div class="relative">
      <div
        ref="editorRef"
        contenteditable="true"
        class="min-h-[180px] px-4 py-3 text-sm leading-relaxed outline-none [&_ol]:list-decimal [&_ol]:pl-5 [&_ul]:list-disc [&_ul]:pl-5"
        @blur="focused = false; saveSelection(); sync()"
        @focus="focused = true"
        @input="sync"
        @keyup="saveSelection"
        @mouseup="saveSelection"
      />
      <p
        v-if="!model && !focused"
        class="pointer-events-none absolute left-4 top-3 text-sm text-mist"
      >
        {{ placeholder }}
      </p>
    </div>
  </div>
</template>
