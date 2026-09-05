<script lang="ts" setup>
/**
 * 업무 데이터 셀렉트.
 *
 * `type` 하나만 주면 나머지(어느 MSA 의 어느 API 를 어떻게 부르는지)는
 * DB 메타데이터(`scom.biz_select_configs`)가 정한다. 포털·장례식장뿐 아니라
 * 헬프데스크·프로젝트관리도 같은 통로를 쓴다 — `#/api/biz-select` 참고.
 */
import type { BizOption } from '#/api/biz-select';

import { computed, ref, useAttrs, watch } from 'vue';

import { cn } from '@vben/utils';

import { Select } from 'ant-design-vue';

import { fetchBizOptions } from '#/api/biz-select';

/**
 * 속성을 **직접** 넘긴다 (`inheritAttrs: false`).
 *
 * 예전에는 `v-bind="$attrs"` 와 자동 상속이 겹쳐 클래스가 두 번 붙었고
 * (`w-64 w-full w-64`), 거기에 인라인 `style="width:100%"` 까지 있어서
 * **화면이 준 폭이 통째로 죽었다.** `class="w-64"` 를 넘기는 자리가 11곳인데
 * 한 곳도 먹지 않았다.
 *
 * 폭이 죽으면 셀렉트가 '부모의 100%' 가 되고, 부모(내용에 맞춰 크는 flex 상자)는
 * 백분율을 크기 계산에서 `auto` 로 보므로 줄이 좁게 잡힌다. 그러면 옆의 글자
 * 라벨이 min-content(한글은 **한 글자**)까지 눌려 **세로로 눕는다.**
 */
defineOptions({ inheritAttrs: false });

const attrs = useAttrs();

/**
 * 뿌리에 붙일 클래스. `cn` 은 tailwind-merge 라 **화면이 준 `w-*` 가 기본값
 * `w-full` 을 밀어낸다** — 같은 갈래의 유틸리티는 뒤엣것만 남는다.
 */
const rootClass = computed(() => cn('jsini-bizselect w-full', attrs.class as any));

/** 클래스는 위에서 합쳤으므로 나머지만 넘긴다(스타일·이벤트·antd 속성). */
const restAttrs = computed(() => {
  const { class: _class, ...rest } = attrs as Record<string, any>;
  return rest;
});

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
    :class="rootClass"
    :loading="loading"
    :options="options"
    :value="selectValue"
    v-bind="restAttrs"
    @change="onChange"
  />
</template>

<style scoped>
/*
  가로로 찌그러지는 것을 막는 최소 너비.

  **뿌리 자신을 가리킨다**(`:deep` 이 아니다). 예전에는 `:deep(.ant-select)` 라
  적혀 있었는데 그것은 `[data-v-x] .ant-select` 로 풀려 **자손**을 찾는다.
  antd 셀렉트 안에 또 다른 `.ant-select` 는 없으므로 이 규칙들은 한 번도
  적용된 적이 없다 — 실제로 듣던 것은 인라인 `style` 이었다.
  (그래서 아래 모바일 해제도 여태 안 먹었다. 이제 먹는다.)

  폭(`width`)은 여기서 정하지 않는다. 그것은 클래스가 맡는다 —
  기본 `w-full`, 화면이 `w-64` 등을 주면 그것이 이긴다(`rootClass`).
*/
.jsini-bizselect {
  min-width: 100px;
  max-width: 100%;
}

/* 모바일에서는 최소 너비를 풀어 화면 폭 안에서 줄어들 수 있게 한다 */
@media (max-width: 767px) {
  .jsini-bizselect {
    min-width: 0;
  }
}
</style>
