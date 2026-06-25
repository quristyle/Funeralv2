<script lang="ts" setup>
import { computed, onMounted, ref, watch } from 'vue';
import { Select } from 'ant-design-vue';
import { getCommonCodes } from '#/api/system/common-code';
import { $t } from '#/locales';

interface Props {
  dictCode: string;
  value?: any;
  modelValue?: any; // Vben Form 커스텀 컴포넌트 모델 바인딩 수용
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
    // requestClient는 HTTP 응답의 최상위 data 필드를 언래핑하여 { result: [...] } 형태로 반환
    // result 필드를 우선 추출하고, 없으면 res 자체를 배열로 사용 (fallback)
    const raw = (res as any)?.result ?? res;
    const list: any[] = Array.isArray(raw) ? raw : [];
    options.value = list.map((item: any) => ({
      label: item.i18nKey ? $t(item.i18nKey) : item.codeName,
      value: item.codeValue,
    }));
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
    @change="onChange"
  />
</template>
