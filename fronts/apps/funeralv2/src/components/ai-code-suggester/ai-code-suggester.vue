<script lang="ts" setup>
import { ref, watch } from 'vue';
import { useDebounceFn } from '@vueuse/core';
import { Tag, Spin } from 'ant-design-vue';
import { suggestCommonCodeByAI } from '#/api/system/common-code';

defineOptions({
  name: 'AiCodeSuggester',
});

interface Props {
  /** AI 추천 대상이 되는 입력 텍스트 (예: 한글명) */
  inputText: string;
  /** AI 추천 API 함수 (생략 시 기본 공통 코드 AI 추천 API 사용) */
  suggestApi?: (text: string) => Promise<string>;
  /** 디바운스 딜레이 시간 (ms) */
  debounceMs?: number;
}

const props = withDefaults(defineProps<Props>(), {
  suggestApi: suggestCommonCodeByAI,
  debounceMs: 500,
});

const emit = defineEmits<{
  (e: 'select', code: string): void;
}>();

const suggestedCode = ref('');
const isSuggesting = ref(false);

// AI API 호출 함수 (디바운스 적용)
const fetchSuggestion = useDebounceFn(async (text: string) => {
  const queryText = text?.trim();
  if (!queryText) {
    suggestedCode.value = '';
    return;
  }

  isSuggesting.value = true;
  try {
    const code = await props.suggestApi(queryText);
    suggestedCode.value = code;
  } catch (error) {
    console.error('[AiCodeSuggester] AI 추천 실패:', error);
    suggestedCode.value = '';
  } finally {
    isSuggesting.value = false;
  }
}, props.debounceMs);

// 입력 텍스트 실시간 감시
watch(
  () => props.inputText,
  (newVal) => {
    fetchSuggestion(newVal);
  },
  { immediate: true }
);

function handleSelect() {
  if (suggestedCode.value) {
    emit('select', suggestedCode.value);
  }
}
</script>

<template>
  <div class="mx-4 mt-2 px-1 text-sm" style="min-height: 24px;">
    <Spin v-if="isSuggesting" size="small" />
    <div v-else-if="suggestedCode" class="flex items-center gap-2">
      <span class="text-gray-500">💡 AI 추천 코드:</span>
      <Tag color="blue" class="cursor-pointer hover:opacity-80" @click="handleSelect">
        {{ suggestedCode }}
      </Tag>
      <span class="text-xs text-gray-400">(클릭하여 적용)</span>
    </div>
  </div>
</template>

<style scoped>
/* 공통 AI 추천 텍스트/뱃지 영역에 대한 스타일이 필요할 경우 여기에 정의 */
</style>
