<script lang="ts" setup>
import { Form, Input, InputNumber } from 'ant-design-vue';
import { computed } from 'vue';
import DictSelect from '#/components/DictSelect.vue';

const props = defineProps({
  modelValue: {
    type: Object,
    required: true
  }
});

const emit = defineEmits(['update:modelValue']);

const model = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
});
</script>

<template>
  <div class="space-y-4">
    <div class="grid grid-cols-2 gap-4">
      <Form.Item label="고인명" required>
        <Input v-model:value="model.name" placeholder="고인 성명" />
      </Form.Item>
      <Form.Item label="성별">
        <DictSelect dict-code="SEX" v-model:value="model.gender" />
      </Form.Item>
    </div>

    <div class="grid grid-cols-2 gap-4">
      <Form.Item label="연령/연세" required>
        <InputNumber v-model:value="model.age" :min="0" style="width: 100%" />
      </Form.Item>
      <Form.Item label="주민등록번호">
        <Input v-model:value="model.ssn" placeholder="예: 451023-1******" />
      </Form.Item>
    </div>

    <div class="grid grid-cols-2 gap-4">
      <Form.Item label="종교">
        <DictSelect dict-code="RELIGION" v-model:value="model.religion" />
      </Form.Item>
      <Form.Item label="사망원인">
        <Input v-model:value="model.causeOfDeath" placeholder="사망 원인 기술" />
      </Form.Item>
    </div>

    <Form.Item label="장지 위치(장지)">
      <Input v-model:value="model.burialPlot" placeholder="예: 경기도 벽제 화장장 / 파주 메모리얼 파크" />
    </Form.Item>

    <Form.Item label="비고/특이사항">
      <Input.TextArea v-model:value="model.remark" placeholder="기타 행정 및 안내 비고" />
    </Form.Item>
  </div>
</template>
