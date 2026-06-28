<script lang="ts" setup>
import { computed } from 'vue';
import { Button, Form, Input, Checkbox } from 'ant-design-vue';
import { Plus, Trash2 } from '@vben/icons';
import DictSelect from '#/components/DictSelect.vue';

const props = defineProps({
  modelValue: {
    type: Array,
    required: true
  }
});

const emit = defineEmits(['update:modelValue']);

const mourners = computed({
  get: () => props.modelValue as any[],
  set: (val) => emit('update:modelValue', val)
});

function addMourner() {
  mourners.value = [
    ...mourners.value,
    {
      id: '',
      name: '',
      relation: '',
      contact: '',
      email: '',
      address: '',
      isChief: false,
      sortOrder: mourners.value.length + 1
    }
  ];
}

function removeMourner(index: number) {
  const list = [...mourners.value];
  list.splice(index, 1);
  // 순서 정렬 재할당
  list.forEach((m, idx) => {
    m.sortOrder = idx + 1;
  });
  mourners.value = list;
}

function onChiefChange(index: number, val: any) {
  if (val.target.checked) {
    // 대표상주는 한 명만 가능하므로 다른 상주들의 대표 설정을 해제
    mourners.value = mourners.value.map((m, idx) => ({
      ...m,
      isChief: idx === index
    }));
  }
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex justify-between items-center mb-2">
      <span class="text-xs text-gray-500">* 대표상주는 장례 전반의 행정 동의 및 정산 알림을 수신합니다.</span>
      <Button type="dashed" size="small" @click="addMourner">
        <Plus class="size-4 mr-1 inline-block" /> 상주 추가
      </Button>
    </div>

    <div v-if="mourners.length === 0" class="text-center py-6 text-gray-400  rounded border border-dashed">
      등록된 상주 정보가 없습니다. 상주 추가를 클릭하십시오.
    </div>

    <div v-else class="space-y-3">
      <div
        v-for="(m, index) in mourners"
        :key="index"
        class="p-4  rounded border border-gray-150 relative space-y-3"
      >
        <div class="absolute top-2 right-2 flex items-center gap-2 z-10">
          <Checkbox v-model:checked="m.isChief" @change="(val) => onChiefChange(index, val)">대표상주</Checkbox>
          <Button type="text" size="small" danger @click="removeMourner(index)">
            <Trash2 class="size-4" />
          </Button>
        </div>

        <div class="grid grid-cols-3 gap-4 pt-4">
          <Form.Item label="상주명" required>
            <Input v-model:value="m.name" placeholder="성명" />
          </Form.Item>
          <Form.Item label="고인과의 관계" required>
            <DictSelect dict-code="FAM_TYPE" v-model:value="m.relation" />
          </Form.Item>
          <Form.Item label="연락처" required>
            <Input v-model:value="m.contact" placeholder="휴대폰 번호" />
          </Form.Item>
        </div>

        <div class="grid grid-cols-2 gap-4">
          <Form.Item label="이메일">
            <Input v-model:value="m.email" placeholder="이메일 주소" />
          </Form.Item>
          <Form.Item label="주소">
            <Input v-model:value="m.address" placeholder="거주지 주소" />
          </Form.Item>
        </div>
      </div>
    </div>
  </div>
</template>
