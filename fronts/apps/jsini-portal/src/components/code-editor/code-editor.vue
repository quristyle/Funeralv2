<script setup lang="ts">
/**
 * 코드 편집기 — 이식 전 `QuriCodeEditor`(BlazorMonaco).
 *
 * 원본이 Monaco 를 쓰던 화면이므로 이식본도 Monaco 를 쓴다.
 * (이식 직후에는 의존성 없이 만든 textarea 편집기였다. `monaco-editor` 를 앱 의존성으로
 * 넣으면서 원본과 같은 편집 경험으로 되돌렸다 — 구문 강조, 접기, 찾기·바꾸기, 다중 커서.)
 *
 * 쓰는 쪽 인터페이스(`v-model`, `language`, `readonly`, `height`, `placeholder`)는
 * 그대로 유지한다. 화면 8곳이 이 부품을 쓰고 있다.
 *
 * 워커 배선과 언어 이름 정리는 `monaco-setup.ts` 에 모아 두었다.
 */
import type * as MonacoNs from 'monaco-editor';

import { computed, onBeforeUnmount, onMounted, ref, shallowRef, watch } from 'vue';

import { usePreferences } from '@vben/preferences';

import { setupMonaco, toMonacoLanguage } from './monaco-setup';

interface Props {
  modelValue?: string;
  /** 구문 종류 (`pgsql`, `json`, `csharp` …) */
  language?: string;
  readonly?: boolean;
  height?: number | string;
  placeholder?: string;
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: '',
  language: 'pgsql',
  readonly: false,
  height: 240,
  placeholder: '',
});

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void;
}>();

const { isDark } = usePreferences();

const containerRef = ref<HTMLDivElement>();
const editor = shallowRef<MonacoNs.editor.IStandaloneCodeEditor>();
/** 편집기가 스스로 낸 변경인지 표시한다. 되먹임으로 커서가 튀는 것을 막는다. */
let applyingExternalValue = false;

const styleHeight = computed(() =>
  typeof props.height === 'number' ? `${props.height}px` : props.height,
);

const text = computed(() => props.modelValue ?? '');
const lineCount = computed(() => text.value.split('\n').length);

onMounted(() => {
  const monaco = setupMonaco();
  if (!containerRef.value) return;

  editor.value = monaco.editor.create(containerRef.value, {
    value: props.modelValue ?? '',
    language: toMonacoLanguage(props.language),
    theme: isDark.value ? 'vs-dark' : 'vs',
    readOnly: props.readonly,
    automaticLayout: true,
    fontSize: 12,
    lineNumbers: 'on',
    minimap: { enabled: false },
    scrollBeyondLastLine: false,
    tabSize: 2,
    wordWrap: 'off',
    // 원본(BlazorMonaco)과 같은 자잘한 설정
    renderLineHighlight: 'line',
    smoothScrolling: true,
  });

  editor.value.onDidChangeModelContent(() => {
    if (applyingExternalValue) return;
    emit('update:modelValue', editor.value?.getValue() ?? '');
  });
});

onBeforeUnmount(() => {
  editor.value?.getModel()?.dispose();
  editor.value?.dispose();
  editor.value = undefined;
});

// 바깥에서 값이 바뀐 경우에만 편집기에 밀어 넣는다.
watch(
  () => props.modelValue,
  (next) => {
    const current = editor.value?.getValue();
    if (!editor.value || current === (next ?? '')) return;

    applyingExternalValue = true;
    editor.value.setValue(next ?? '');
    applyingExternalValue = false;
  },
);

watch(
  () => props.language,
  (next) => {
    const model = editor.value?.getModel();
    if (!model) return;
    setupMonaco().editor.setModelLanguage(model, toMonacoLanguage(next));
  },
);

watch(
  () => props.readonly,
  (next) => editor.value?.updateOptions({ readOnly: next }),
);

watch(isDark, (dark) => {
  setupMonaco().editor.setTheme(dark ? 'vs-dark' : 'vs');
});

defineExpose({
  focus: () => editor.value?.focus(),
  /** 편집기 인스턴스. 원본처럼 세밀한 조작이 필요한 화면을 위해 열어 둔다. */
  getEditor: () => editor.value,
});
</script>

<template>
  <!-- 높이는 바깥 상자가 받고 편집기가 그 안을 채운다.
       Monaco 는 textarea 와 달리 내용에 따른 고유 높이가 없어서, 안쪽에 height:100% 를 주면
       높이가 정해지지 않은 부모 안에서 0 으로 접힌다. 화면 여럿이 height="100%" 로 쓰므로
       바깥에 높이를 걸고 안쪽은 flex-1 로 채우는 형태여야 한다. -->
  <div
    class="border-border flex min-h-40 flex-col overflow-hidden rounded-md border"
    :style="{ height: styleHeight }"
  >
    <div class="relative min-h-0 flex-1">
      <!--
        편집기 자리는 `absolute inset-0` 으로 잡는다.
        `h-full`(height:100%) 로 두면 부모 높이가 flex 로 계산된 값이라 백분율이 풀리지 않고,
        내용 높이(= 편집기 높이)로 되돌아가 서로를 참조하며 5px 로 접힌다.
      -->
      <div ref="containerRef" class="absolute inset-0"></div>

      <!-- Monaco 에는 placeholder 가 없다. 비어 있을 때만 겹쳐 보여 준다. -->
      <div
        v-if="placeholder && !text"
        class="text-muted-foreground pointer-events-none absolute left-14 top-1 font-mono text-xs"
      >
        {{ placeholder }}
      </div>
    </div>

    <div
      class="border-border bg-card text-muted-foreground flex shrink-0 items-center gap-3 border-t px-2 py-0.5 text-[11px]"
    >
      <span>{{ language }}</span>
      <span v-if="readonly">읽기 전용</span>
      <span class="flex-1"></span>
      <span>{{ lineCount }} 줄 · {{ text.length }} 자</span>
    </div>
  </div>
</template>
