<script lang="ts" setup>
import { DatePicker } from 'ant-design-vue';
import { onMounted, nextTick, computed, useAttrs } from 'vue';
import dayjs, { type Dayjs } from 'dayjs';

const props = defineProps({
  value: {
    type: [Object, String] as any,
    default: null
  },
  offsetDays: {
    type: Number,
    default: 0
  },
  showTime: {
    type: Boolean,
    default: true
  },
  format: {
    type: String,
    default: 'YYYY-MM-DD HH:mm:ss'
  },
  valueFormat: {
    type: String,
    default: ''
  }
});

const emit = defineEmits<{
  (e: 'update:value', value: any): void;
}>();

const attrs = useAttrs();

// DatePicker 컴포넌트는 항상 Dayjs 인스턴스 혹은 null만 전달받아야 함 (date.locale 에러 방지)
const innerValue = computed(() => {
  if (!props.value) return null;
  if (typeof props.value === 'string') {
    // 공백 문자나 빈 문자열 검사
    if (!props.value.trim()) return null;
    const parsed = dayjs(props.value);
    return parsed.isValid() ? parsed : null;
  }
  return props.value;
});

function emitDefault() {
  let defaultDate = dayjs();
  if (props.offsetDays !== 0) {
    defaultDate = defaultDate.add(props.offsetDays, 'day');
  }
  
  // value-format 속성이 존재하면 포맷팅된 문자열로 반환, 없으면 Dayjs 인스턴스 반환
  const valFormat = (attrs['value-format'] || attrs['valueFormat'] || props.valueFormat) as string;
  if (valFormat) {
    emit('update:value', defaultDate.format(valFormat));
  } else {
    emit('update:value', defaultDate);
  }
}

function handleUpdate(val: Dayjs | null) {
  if (!val) {
    emit('update:value', null);
    return;
  }
  
  const valFormat = (attrs['value-format'] || attrs['valueFormat'] || props.valueFormat) as string;
  if (valFormat) {
    emit('update:value', val.format(valFormat));
  } else {
    emit('update:value', val);
  }
}

onMounted(() => {
  nextTick(() => {
    if (!props.value) {
      emitDefault();
    }
  });
});
</script>

<template>
  <DatePicker
    v-bind="$attrs"
    :value="innerValue"
    @update:value="handleUpdate"
    :show-time="showTime"
    :format="format"
    style="width: 100%"
  />
</template>
