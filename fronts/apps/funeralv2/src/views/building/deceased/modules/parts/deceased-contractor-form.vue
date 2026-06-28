<script lang="ts" setup>
import { Form, Input } from 'ant-design-vue';
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
      <Form.Item label="계약자 성명" required>
        <Input v-model:value="model.name" placeholder="계약 당사자 성명" />
      </Form.Item>
      <Form.Item label="연락처(휴대폰)" required>
        <Input v-model:value="model.contact" placeholder="연락처 번호 입력" />
      </Form.Item>
    </div>

    <div class="grid grid-cols-2 gap-4">
      <Form.Item label="고인과의 관계">
        <DictSelect dict-code="FAM_TYPE" v-model:value="model.relation" />
      </Form.Item>
      <Form.Item label="계약자 주소">
        <Input v-model:value="model.address" placeholder="계약자 주소 입력" />
      </Form.Item>
    </div>

    <Form.Item label="비고/특이사항">
      <Input.TextArea v-model:value="model.remark" placeholder="계약 조건 및 정산 지연 특이사항 기술" />
    </Form.Item>
  </div>
</template>
