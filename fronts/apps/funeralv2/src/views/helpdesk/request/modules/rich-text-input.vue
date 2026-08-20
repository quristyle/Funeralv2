<script lang="ts" setup>
import { onBeforeUnmount, onMounted, ref, watch } from 'vue';

/**
 * 가벼운 서식 입력 필드.
 *
 * 원본(JinReception)은 댓글·본문 작성에 tiptap 을 썼고, 붙여넣기로 이미지를 바로 끼워 넣는
 * 기능이 실제로 쓰이고 있었다. 그 기능을 살리되 에디터 라이브러리를 새로 들이지 않으려고
 * contenteditable 로 최소한만 구현했다. 저장 값은 원본과 같은 HTML 문자열이다.
 */
const props = withDefaults(
  defineProps<{
    /** 입력 영역 최소 높이(px) */
    minHeight?: number;
    /** 비어 있을 때 표시할 안내 문구 */
    placeholder?: string;
  }>(),
  { minHeight: 90, placeholder: '내용을 입력하세요' },
);

const modelValue = defineModel<string>({ default: '' });

const editorRef = ref<HTMLDivElement | null>(null);
const isEmpty = ref(true);

/** 편집 영역의 현재 HTML 을 모델에 반영한다. */
function syncFromDom() {
  const html = editorRef.value?.innerHTML ?? '';
  isEmpty.value = (editorRef.value?.textContent ?? '').trim() === '' &&
    !html.includes('<img');
  modelValue.value = isEmpty.value ? '' : html;
}

/**
 * 이미지 붙여넣기를 base64 로 삽입한다.
 * 원본과 동일하게 클립보드의 이미지 항목을 가로채 <img> 로 넣는다.
 */
function onPaste(event: ClipboardEvent) {
  const items = event.clipboardData?.items;
  if (!items) return;

  for (const item of items) {
    if (!item.type.startsWith('image/')) continue;

    const file = item.getAsFile();
    if (!file) continue;

    event.preventDefault();
    const reader = new FileReader();
    reader.addEventListener('load', (e) => {
      const base64 = e.target?.result;
      if (typeof base64 !== 'string') return;
      document.execCommand(
        'insertHTML',
        false,
        `<img src="${base64}" style="max-width:100%" />`,
      );
      syncFromDom();
    });
    reader.readAsDataURL(file);
    return;
  }
}

/** 서식 없는 순수 텍스트로 붙여넣도록 강제하는 경우(이미지가 아닌 항목) */
function onPasteText(event: ClipboardEvent) {
  const items = event.clipboardData?.items;
  const hasImage = items
    ? [...items].some((i) => i.type.startsWith('image/'))
    : false;
  if (hasImage) return;

  const text = event.clipboardData?.getData('text/plain');
  if (text === undefined) return;
  event.preventDefault();
  document.execCommand('insertText', false, text);
  syncFromDom();
}

function handlePaste(event: ClipboardEvent) {
  onPaste(event);
  onPasteText(event);
}

/** 외부에서 값을 비우거나 채운 경우 편집 영역에 반영한다. */
watch(modelValue, (value) => {
  if (!editorRef.value) return;
  if ((value || '') === editorRef.value.innerHTML) return;
  editorRef.value.innerHTML = value || '';
  isEmpty.value = !value;
});

onMounted(() => {
  if (editorRef.value && modelValue.value) {
    editorRef.value.innerHTML = modelValue.value;
    isEmpty.value = false;
  }
  editorRef.value?.addEventListener('paste', handlePaste);
});

onBeforeUnmount(() => {
  editorRef.value?.removeEventListener('paste', handlePaste);
});

/** 입력 내용을 비운다. 부모(댓글 폼)가 등록 후 호출한다. */
function clear() {
  if (editorRef.value) editorRef.value.innerHTML = '';
  modelValue.value = '';
  isEmpty.value = true;
}

defineExpose({ clear });
</script>

<template>
  <div class="relative">
    <div
      ref="editorRef"
      class="hd-rich-input w-full overflow-auto rounded-md border border-border bg-background px-3 py-2 text-sm outline-none focus:border-primary"
      :style="{ minHeight: `${props.minHeight}px` }"
      contenteditable="true"
      role="textbox"
      tabindex="0"
      @blur="syncFromDom"
      @input="syncFromDom"
    ></div>
    <span
      v-if="isEmpty"
      class="pointer-events-none absolute left-3 top-2 text-sm text-muted-foreground"
    >
      {{ props.placeholder }}
    </span>
  </div>
</template>

<style scoped>
.hd-rich-input :deep(img) {
  max-width: 100%;
  height: auto;
}
</style>
