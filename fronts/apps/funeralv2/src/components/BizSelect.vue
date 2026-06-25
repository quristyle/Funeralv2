<script lang="ts" setup>
import { computed, ref, watch } from 'vue';
import { Select } from 'ant-design-vue';
import { useBizSelectStore } from '#/store/biz-select-config';
import { requestClient } from '#/api/request';

interface Props {
  type: string;
  params?: Record<string, any>;
  value?: any;
  modelValue?: any; // Vben Form 커스텀 컴포넌트 모델 바인딩 수용
  autoSelectFirst?: boolean;
  showAll?: boolean;
}

const props = defineProps<Props>();
const emit = defineEmits(['update:value', 'update:modelValue', 'change']);

const bizSelectStore = useBizSelectStore();

const options = ref<{ label: string; value: any }[]>([]);
const loading = ref(false);

const selectValue = computed(() => {
  return props.value !== undefined ? props.value : props.modelValue;
});

function getValueByPath(obj: any, path: string | null | undefined): any {
  if (!path) return obj;
  return path.split('.').reduce((acc, part) => {
    return acc && acc[part] !== undefined ? acc[part] : undefined;
  }, obj);
}

function flattenDepts(depts: any[]): any[] {
  const result: any[] = [];
  function recurse(list: any[]) {
    if (!Array.isArray(list)) return;
    for (const item of list) {
      result.push(item);
      if (item.children && item.children.length > 0) {
        recurse(item.children);
      }
    }
  }
  recurse(depts);
  return result;
}

async function loadOptions() {
  if (!props.type) return;

  // 부서·건물 조회: companyId가 지정되었으나 값이 비어있으면 API 요청 대기
  if ((props.type === 'dept' || props.type === 'building') && props.params && 'companyId' in props.params && !props.params.companyId) {
    options.value = [];
    return;
  }

  // 층 조회: buildingId가 지정되었으나 값이 비어있으면 API 요청 대기
  if (props.type === 'floor' && props.params && 'buildingId' in props.params && !props.params.buildingId) {
    options.value = [];
    return;
  }

  loading.value = true;
  try {
    const config = await bizSelectStore.getConfigByType(props.type);
    if (!config) {
      console.warn(`[BizSelect] No metadata config found for type: ${props.type}`);
      options.value = [];
      return;
    }

    const method = (config.httpMethod || 'GET').toUpperCase();
    let res: any;

    if (method === 'GET') {
      res = await requestClient.get<any>(config.apiUrl, {
        params: props.params,
      });
    } else {
      res = await requestClient.post<any>(config.apiUrl, props.params);
    }
    
    // resultPath가 없고 백엔드 응답이 ApiResponse 공통 규격인 경우 result를 기본값으로 탐색 (Fallback)
    let path = config.resultPath;
    if (!path && res && res.result !== undefined) {
      path = 'result';
    }

    let rawList = getValueByPath(res, path);
    if (!rawList) {
      rawList = [];
    }
    
    let list = Array.isArray(rawList) ? rawList : [];

    if (config.processorType === 'FLATTEN') {
      list = flattenDepts(list);
    }

    const labelField = config.labelField || 'name';
    const valueField = config.valueField || 'id';

    const mappedList = list.map((item: any) => ({
      label: item[labelField] ?? '',
      value: item[valueField],
    }));

    if (props.showAll) {
      options.value = [{ label: '전체', value: '' }, ...mappedList];
    } else {
      options.value = mappedList;
    }

    // value와 modelValue 모두 비어있거나 빈 문자열일 때만 자동 선택 처리
    const isValEmpty = props.value === undefined || props.value === null || props.value === '';
    const isModelValEmpty = props.modelValue === undefined || props.modelValue === null || props.modelValue === '';
    if (props.autoSelectFirst && options.value.length > 0 && isValEmpty && isModelValEmpty) {
      const firstVal = options.value[0]?.value;
      emit('update:value', firstVal);
      emit('update:modelValue', firstVal);
      emit('change', firstVal);
    }
  } catch (error) {
    console.error(`비즈니스 데이터 [${props.type}] 로드 실패:`, error);
  } finally {
    loading.value = false;
  }
}

watch(
  [() => props.type, () => props.params],
  (newValues, oldValues) => {
    if (oldValues && JSON.stringify(newValues) === JSON.stringify(oldValues)) {
      return;
    }
    loadOptions();
  },
  { deep: true, immediate: true },
);

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
