<script lang="ts" setup>
/**
 * 업무 데이터 셀렉트.
 *
 * `type` 하나만 주면 나머지(어느 MSA 의 어느 API 를 어떻게 부르는지)는
 * DB 메타데이터(`scom.biz_select_configs`)가 정한다. 포털·장례식장뿐 아니라
 * 헬프데스크·프로젝트관리도 같은 통로를 쓴다 — `#/api/biz-select` 참고.
 */
import type { BizOption } from '#/api/biz-select';

import { computed, ref, watch } from 'vue';

import { Select } from 'ant-design-vue';

import { fetchBizOptions } from '#/api/biz-select';

interface Props {
  type: string;
  params?: Record<string, any>;
  value?: any;
  modelValue?: any; // Vben Form 커스텀 컴포넌트 모델 바인딩 수용
  autoSelectFirst?: boolean;
  showAll?: boolean;
  /** '전체' 항목의 값. 기본은 빈 문자열이지만 숫자 ID 목록에서는 null 을 쓰기도 한다. */
  allValue?: any;
  /**
   * 이 파라미터들이 채워지기 전에는 조회하지 않는다.
   * 상위 선택에 딸린 셀렉트(회사→부서, 건물→층)가 빈 목록을 받아
   * 엉뚱한 첫 항목을 자동 선택하는 것을 막는다.
   */
  requiredParams?: string[];
}

const props = withDefaults(defineProps<Props>(), {
  allValue: '',
});
const emit = defineEmits(['update:value', 'update:modelValue', 'change', 'loaded']);

const options = ref<BizOption[]>([]);
const items = ref<any[]>([]);
const loading = ref(false);

const selectValue = computed(() =>
  props.value === undefined ? props.modelValue : props.value,
);

/**
 * 상위 선택을 기다려야 하는가.
 *
 * `requiredParams` 로 명시한 것 외에, 예전부터 이름으로 걸려 있던 세 쌍
 * (부서·건물의 companyId, 층의 buildingId)도 계속 지킨다. 그 화면들이
 * 프로퍼티를 따로 주지 않고 이 동작에 기대고 있다.
 */
const LEGACY_REQUIRED: Record<string, string> = {
  dept: 'companyId',
  building: 'companyId',
  floor: 'buildingId',
};

function shouldWait() {
  const params = props.params;
  if (!params) return false;

  const keys = [...(props.requiredParams ?? [])];
  const legacy = LEGACY_REQUIRED[props.type];
  if (legacy) keys.push(legacy);

  // 화면이 아예 넘기지 않은 키는 조건이 아니다. 넘겼는데 비어 있을 때만 기다린다.
  return keys.some(
    (key) => key in params && (params[key] === '' || params[key] === null || params[key] === undefined),
  );
}

/** 원본 행 하나를 값으로 되찾는다. `change` 로 함께 넘겨 준다. */
function itemOf(value: any) {
  const index = options.value.findIndex((o) => o.value === value);
  return index === -1 ? null : (items.value[props.showAll ? index - 1 : index] ?? null);
}

async function loadOptions() {
  if (!props.type) return;

  if (shouldWait()) {
    options.value = [];
    items.value = [];
    return;
  }

  loading.value = true;
  try {
    const result = await fetchBizOptions(props.type, props.params);
    items.value = result.items;
    options.value = props.showAll
      ? [{ label: '전체', value: props.allValue }, ...result.options]
      : result.options;

    emit('loaded', items.value);

    // value 와 modelValue 모두 비어 있을 때만 첫 항목을 자동 선택한다.
    const isEmpty = (v: any) => v === undefined || v === null || v === '';
    if (
      props.autoSelectFirst &&
      options.value.length > 0 &&
      isEmpty(props.value) &&
      isEmpty(props.modelValue)
    ) {
      const first = options.value[0]?.value;
      emit('update:value', first);
      emit('update:modelValue', first);
      emit('change', first, itemOf(first));
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
  emit('change', val, itemOf(val));
}

defineExpose({ reload: () => loadOptions(), items, options });
</script>

<template>
  <Select
    :loading="loading"
    :options="options"
    :value="selectValue"
    v-bind="$attrs"
    class="w-full"
    style="min-width: 100px; width: 100%"
    @change="onChange"
  />
</template>

<style scoped>
/* BizSelect 컴포넌트의 가로 찌그러짐 방지를 위해 최소 너비 100px 강제 적용 */
:deep(.ant-select) {
  min-width: 100px !important;
}
</style>
