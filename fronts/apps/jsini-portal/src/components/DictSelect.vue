<script lang="ts" setup>
import { computed, onMounted, ref, watch } from 'vue';
import { Select } from 'ant-design-vue';
import { getCommonCodes } from '#/api/portal/system/common-code';
import { $t } from '#/locales';

interface Props {
  dictCode: string;
  value?: any;
  modelValue?: any; // Vben Form 커스텀 컴포넌트 모델 바인딩 수용
  showAll?: boolean; // 검색 콤보박스에 '전체' 표현용 옵션
}

const props = defineProps<Props>();
const emit = defineEmits(['update:value', 'update:modelValue', 'change']);

const options = ref<{ label: string; value: any }[]>([]);
const loading = ref(false);

const selectValue = computed(() => {
  return props.value !== undefined ? props.value : props.modelValue;
});

async function loadOptions() {
  if (!props.dictCode) return;
  loading.value = true;
  try {
    const res = await getCommonCodes(props.dictCode);
    const raw = (res as any)?.result ?? res;
    const list: any[] = Array.isArray(raw) ? raw : [];
    
    const mappedList = list.map((item: any) => ({
      label: item.i18nKey ? $t(item.i18nKey) : item.codeName,
      value: item.codeValue,
    }));

    if (props.showAll) {
      options.value = [{ label: '전체', value: '' }, ...mappedList];
    } else {
      options.value = mappedList;
    }
  } catch (error) {
    console.error(`공통코드 [${props.dictCode}] 로드 실패:`, error);
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.dictCode,
  () => {
    loadOptions();
  },
);

onMounted(() => {
  loadOptions();
});

function onChange(val: any) {
  emit('update:value', val);
  emit('update:modelValue', val);
  emit('change', val);
}
</script>

<template>
  <Select
    :loading="loading"
    :options="options"
    :value="selectValue"
    v-bind="$attrs"
    class="w-full"
    style="min-width: 80px; width: 100%; max-width: 100%"
    @change="onChange"
  />
</template>

<style scoped>
/* DictSelect 컴포넌트의 가로 찌그러짐 방지를 위해 최소 너비 80px 강제 적용 */
:deep(.ant-select) {
  max-width: 100%;
  min-width: 80px !important;
}

/* 모바일에서는 최소 너비를 풀어 화면 폭 안에서 줄어들 수 있게 한다 */
@media (max-width: 767px) {
  :deep(.ant-select) {
    min-width: 0 !important;
  }
}
</style>
