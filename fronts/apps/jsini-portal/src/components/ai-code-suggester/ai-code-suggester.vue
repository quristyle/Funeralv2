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
  suggestApi?: (text: string, natural?: boolean) => Promise<unknown>;
  /** 디바운스 딜레이 시간 (ms) */
  debounceMs?: number;
  /** 자연스러운 영문 추천 제공 여부 */
  natural?: boolean;
  /** 추천 정보 라벨명 */
  label?: string;
}

const props = withDefaults(defineProps<Props>(), {
  suggestApi: suggestCommonCodeByAI,
  debounceMs: 500,
  natural: false,
  label: '',
});

const emit = defineEmits<{
  (e: 'select', code: string): void;
}>();

const suggestedCode = ref('');
const isSuggesting = ref(false);

/**
 * API 응답 구조가 중첩되어 오더라도 최하위 result 배열의 첫 번째 요소를 안전하게 탐색하여 추출하는 헬퍼 함수
 */
function extractCodeText(response: unknown): string {
  if (!response) return '';
  
  // JSON 형식의 문자열인 경우 먼저 객체로 파싱 시도
  let dataObj: unknown = response;
  if (typeof response === 'string' && (response.trim().startsWith('{') || response.trim().startsWith('['))) {
    try {
      dataObj = JSON.parse(response);
    } catch {
      // 파싱 실패 시 원본 문자열 유지
    }
  }
  
  // 1. response 자체가 string 인 경우
  if (typeof dataObj === 'string') {
    return dataObj;
  }
  
  // 2. response.data 에 실제 데이터가 들어있는 경우 (AxiosResponse 또는 감싸진 ApiResponse 구조)
  let target: unknown = dataObj;
  if (dataObj && typeof dataObj === 'object') {
    const record = dataObj as Record<string, unknown>;
    if (record.data !== undefined && record.data !== null) {
      target = record.data;
    }
  }
  
  // 만약 target도 JSON 문자열인 경우 한 번 더 파싱 시도
  if (typeof target === 'string' && (target.trim().startsWith('{') || target.trim().startsWith('['))) {
    try {
      target = JSON.parse(target);
    } catch {
      // 실패 시 유지
    }
  }
  
  // 3. target.result 가 배열인 경우
  if (target && typeof target === 'object') {
    const record = target as Record<string, unknown>;
    if ('result' in record && Array.isArray(record.result)) {
      const firstVal = record.result[0];
      return typeof firstVal === 'string' ? firstVal : (firstVal !== undefined && firstVal !== null ? String(firstVal) : '');
    }
  }
  
  // 4. target 자체가 배열인 경우
  if (Array.isArray(target)) {
    const firstVal = target[0];
    return typeof firstVal === 'string' ? firstVal : (firstVal !== undefined && firstVal !== null ? String(firstVal) : '');
  }
  
  // 5. target 이 string 인 경우
  if (typeof target === 'string') {
    return target;
  }

  // 6. 직렬화된 JSON 문자열 형태인 경우 역직렬화 후 재시도
  try {
    if (target && typeof target === 'object') {
      const rawStr = JSON.stringify(target);
      if (rawStr.includes('result')) {
        const parsed = (typeof target === 'string' ? JSON.parse(target) : target) as Record<string, unknown>;
        if (parsed && typeof parsed === 'object' && 'result' in parsed && Array.isArray(parsed.result)) {
          const firstVal = parsed.result[0];
          return typeof firstVal === 'string' ? firstVal : (firstVal !== undefined && firstVal !== null ? String(firstVal) : '');
        }
      }
    }
  } catch {}
  
  return '';
}

// AI API 호출 함수 (디바운스 적용)
const fetchSuggestion = useDebounceFn(async (text: string) => {
  const queryText = text?.trim();
  if (!queryText) {
    suggestedCode.value = '';
    return;
  }

  isSuggesting.value = true;
  try {
    const response = await props.suggestApi(queryText, props.natural);
    const code = extractCodeText(response);
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
      <span class="text-gray-500">💡 {{ props.label || (props.natural ? 'AI 추천 영문' : 'AI 추천 코드') }}:</span>
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
