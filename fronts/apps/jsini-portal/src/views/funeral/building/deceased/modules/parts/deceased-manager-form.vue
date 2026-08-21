<script lang="ts" setup>
import { Form, Input } from 'ant-design-vue';
import { computed } from 'vue';

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
      <Form.Item label="담당 장례지도사">
        <Input v-model:value="model.directorName" placeholder="장례지도사 성명" />
      </Form.Item>
      <Form.Item label="지도사 연락처">
        <Input v-model:value="model.directorContact" placeholder="연락처 입력" />
      </Form.Item>
    </div>

    <div class="grid grid-cols-3 gap-4">
      <Form.Item label="연동 상조회사">
        <Input v-model:value="model.mutualAidCompany" placeholder="예: 보람상조, 프리드라이프 등" />
      </Form.Item>
      <Form.Item label="내부 담당 임직원">
        <Input v-model:value="model.staffName" placeholder="당사 담당 직원명" />
      </Form.Item>
      <Form.Item label="담당 직원 연락처">
        <Input v-model:value="model.staffContact" placeholder="담당자 연락처" />
      </Form.Item>
    </div>
  </div>
</template>
