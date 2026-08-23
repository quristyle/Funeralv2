<script lang="ts" setup>
import { Input, Select } from 'ant-design-vue';

const emit = defineEmits(['blur', 'change']);

const modelValue = defineModel<[string, string]>({
  default: () => ['', ''] as [string, string],
});

function onChange() {
  emit('change', modelValue.value);
}
</script>
<template>
  <div class="flex w-full gap-1">
    <Select
      v-model:value="modelValue[0]"
      class="w-20"
      placeholder="유형"
      allow-clear
      :class="{ 'valid-success': !!modelValue[0] }"
      :options="[
        { label: '개인', value: 'personal' },
        { label: '업무', value: 'work' },
        { label: '비공개', value: 'private' },
      ]"
      @blur="emit('blur')"
      @change="onChange"
    />
    <Input
      placeholder="11자리 휴대폰 번호를 입력하세요"
      class="flex-1"
      allow-clear
      :class="{ 'valid-success': modelValue[1]?.match(/^1[3-9]\d{9}$/) }"
      v-model:value="modelValue[1]"
      :maxlength="11"
      type="tel"
      @blur="emit('blur')"
      @change="onChange"
    />
  </div>
</template>
