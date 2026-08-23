<script setup lang="ts">
/**
 * 공통코드 드롭다운 — 이식 전 `QuriDropDown`.
 *
 * 코드 ID 만 주면 `sp_projCommon` 을 읽어 목록을 채운다.
 * 화면들이 DB·프로젝트·구분값을 고를 때 거의 다 이걸 쓴다.
 *
 * `change` 이벤트로는 코드만이 아니라 항목 전체를 넘긴다.
 * 개발 도구 화면들이 `others.db_nick` / `others.db_schema` 같은 부가 컬럼을
 * 프로시저 파라미터로 다시 실어 보내야 하기 때문이다.
 */
import type { CommonCodeItem } from '#/api/projmng';

import { computed, onMounted, ref, watch } from 'vue';

import { Select } from 'ant-design-vue';

import { getCommon } from '#/api/projmng';

interface Props {
  /** 공통코드 ID — 예: `db`, `CODE_TYPE`, `proj` */
  codeId: string;
  /** 코드 조회 시 함께 넘기는 보조 키 (`etc0`) */
  codeKey?: string;
  modelValue?: string;
  placeholder?: string;
  /** 목록을 읽은 뒤 첫 항목을 자동으로 고른다 */
  autoSelectFirst?: boolean;
  /** 맨 앞에 "전체"(빈 코드) 항목을 넣는다 — 이식 전 `IsAll` */
  showAll?: boolean;
  /** `codeKey` 가 비어 있으면 조회하지 않는다 — 이식 전 `IsEtcFix`. 상위 선택에 딸린 코드에 쓴다 */
  etcFix?: boolean;
  allowClear?: boolean;
  width?: number | string;
  disabled?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  codeKey: '',
  modelValue: '',
  placeholder: '선택',
  autoSelectFirst: true,
  showAll: false,
  etcFix: false,
  allowClear: false,
  width: 180,
  disabled: false,
});

const emit = defineEmits<{
  (e: 'change', item: null | CommonCodeItem): void;
  (e: 'loaded', items: CommonCodeItem[]): void;
  (e: 'update:modelValue', value: string): void;
}>();

const items = ref<CommonCodeItem[]>([]);
const loading = ref(false);

const options = computed(() =>
  items.value.map((it) => ({
    label: it.name || it.code,
    value: it.code,
    title: it.desc,
  })),
);

const styleWidth = computed(() =>
  typeof props.width === 'number' ? `${props.width}px` : props.width,
);

function itemOf(code: string) {
  return items.value.find((it) => it.code === code) ?? null;
}

async function load() {
  // 상위 선택이 아직 정해지지 않았으면 조회하지 않는다.
  // 조회해 봐야 빈 목록이고, 첫 항목 자동 선택이 엉뚱한 값을 집게 된다.
  if (props.etcFix && !props.codeKey) {
    items.value = [];
    emit('loaded', items.value);
    return;
  }

  loading.value = true;
  try {
    const loaded = await getCommon(props.codeId, props.codeKey);
    items.value = props.showAll
      ? [{ code: '', name: '전체', desc: '전체', others: {} }, ...loaded]
      : loaded;
    emit('loaded', items.value);

    // 값이 비어 있고 목록이 있으면 첫 항목을 고른다.
    // 이식 전에도 대부분의 화면이 이 동작에 의존해 최초 조회를 시작했다.
    if (
      props.autoSelectFirst &&
      !props.modelValue &&
      items.value.length > 0 &&
      items.value[0]
    ) {
      const first = items.value[0];
      emit('update:modelValue', first.code);
      emit('change', first);
    }
  } finally {
    loading.value = false;
  }
}

function onChange(value: any) {
  const code = (value ?? '') as string;
  emit('update:modelValue', code);
  emit('change', itemOf(code));
}

onMounted(load);
watch(() => [props.codeId, props.codeKey], load);
</script>

<template>
  <Select
    :value="showAll ? (modelValue ?? '') : modelValue || undefined"
    :options="options"
    :loading="loading"
    :placeholder="placeholder"
    :allow-clear="allowClear"
    :disabled="disabled"
    :style="{ width: styleWidth }"
    size="small"
    show-search
    :filter-option="
      (input: string, option: any) =>
        String(option?.label ?? '')
          .toLowerCase()
          .includes(input.toLowerCase())
    "
    @change="onChange"
  />
</template>
