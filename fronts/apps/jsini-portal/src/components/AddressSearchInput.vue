<script lang="ts" setup>
import { ref } from 'vue';
import { Input, Button, Modal } from 'ant-design-vue';
import { VueDaumPostcode } from 'vue-daum-postcode';

const props = defineProps({
  modelValue: {
    type: String,
    default: '',
  },
  placeholder: {
    type: String,
    default: '우편번호',
  },
  disabled: {
    type: Boolean,
    default: false,
  }
});

const emit = defineEmits(['update:modelValue', 'change', 'selected']);

const isModalOpen = ref(false);

function openPostcode() {
  if (props.disabled) return;
  isModalOpen.value = true;
}

function handleComplete(data: any) {
  isModalOpen.value = false;
  if (!data) return;

  // 우편번호
  const zonecode = data.zonecode;
  // 기본주소 (도로명주소 우선, 없을 경우 지번주소)
  let address = data.address;
  if (data.addressType === 'R') {
    if (data.bname !== '' && /[동|로|가]$/g.test(data.bname)) {
      address += ` (${data.bname})`;
    }
  }
  
  emit('update:modelValue', zonecode);
  emit('change', zonecode);
  emit('selected', { zipCode: zonecode, address });
}
</script>

<template>
  <div class="flex gap-2 w-full">
    <Input
      :value="props.modelValue"
      :placeholder="props.placeholder"
      :disabled="true"
      class="flex-1"
    />
    <Button
      type="primary"
      :disabled="props.disabled"
      @click="openPostcode"
    >
      우편번호 찾기
    </Button>

    <Modal
      v-model:open="isModalOpen"
      title="우편번호 검색"
      :footer="null"
      destroy-on-close
      width="600px"
    >
      <div class="py-4">
        <VueDaumPostcode @complete="handleComplete" />
      </div>
    </Modal>
  </div>
</template>
