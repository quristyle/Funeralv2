<script lang="ts" setup>
import { computed } from 'vue';
import { Button, Form, Input, InputNumber, Select, DatePicker } from 'ant-design-vue';
import { Plus, Trash2 } from '@vben/icons';
import dayjs from 'dayjs';
import DictSelect from '#/components/DictSelect.vue';

const props = defineProps({
  modelValue: {
    type: Array,
    required: true
  }
});

const emit = defineEmits(['update:modelValue']);

const facilities = computed({
  get: () => props.modelValue as any[],
  set: (val) => emit('update:modelValue', val)
});

function addFacility() {
  facilities.value = [
    ...facilities.value,
    {
      id: '',
      facilityType: null,
      startTime: '',
      endTime: '',
      useHours: 0,
      unitPrice: 50000,
      totalPrice: 0,
      remark: ''
    }
  ];
}

function removeFacility(index: number) {
  const list = [...facilities.value];
  list.splice(index, 1);
  facilities.value = list;
}

function onTimeChange(index: number) {
  const item = facilities.value[index];
  if (!item) return;

  if (item.startTime && item.endTime) {
    const start = dayjs(item.startTime);
    const end = dayjs(item.endTime);
    const diffHours = Math.max(0, parseFloat((end.diff(start, 'minute') / 60).toFixed(1)));
    item.useHours = diffHours;
    item.totalPrice = Math.round(diffHours * item.unitPrice);
  }
}

function onPriceOrHoursChange(index: number) {
  const item = facilities.value[index];
  if (item) {
    item.totalPrice = Math.round(item.useHours * item.unitPrice);
  }
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex justify-between items-center mb-2">
      <span class="text-xs text-gray-500">* 사용 시작일시와 종료일시 입력 시 자동 사용시간 및 총 요금이 집계됩니다.</span>
      <Button type="dashed" size="small" @click="addFacility">
        <Plus class="size-4 mr-1 inline-block" /> 시설 사용 추가
      </Button>
    </div>

    <div v-if="facilities.length === 0" class="text-center py-6 text-gray-400  rounded border border-dashed">
      등록된 시설 이용 내역이 없습니다. 안치실/염습실 등의 내역이 필요하면 추가하십시오.
    </div>

    <div v-else class="space-y-3">
      <div
        v-for="(f, index) in facilities"
        :key="index"
        class="p-4  rounded border border-gray-150 relative space-y-3"
      >
        <div class="absolute top-2 right-2 z-10">
          <Button type="text" size="small" danger @click="removeFacility(index)">
            <Trash2 class="size-4" />
          </Button>
        </div>

        <div class="grid grid-cols-3 gap-4 pt-4">
          <Form.Item label="시설 유형" required>
            <DictSelect dict-code="ROOM_TYPE" v-model:value="f.facilityType" placeholder="시설 유형을 선택해주세요" />
          </Form.Item>
          <Form.Item label="사용 시작일시">
            <DatePicker
              v-model:value="f.startTime"
              show-time
              format="YYYY-MM-DD HH:mm:ss"
              value-format="YYYY-MM-DD HH:mm:ss"
              style="width: 100%"
              @change="() => onTimeChange(index)"
            />
          </Form.Item>
          <Form.Item label="사용 종료일시">
            <DatePicker
              v-model:value="f.endTime"
              show-time
              format="YYYY-MM-DD HH:mm:ss"
              value-format="YYYY-MM-DD HH:mm:ss"
              style="width: 100%"
              @change="() => onTimeChange(index)"
            />
          </Form.Item>
        </div>

        <div class="grid grid-cols-4 gap-4">
          <Form.Item label="사용시간 (시간)" required>
            <InputNumber
              v-model:value="f.useHours"
              :min="0"
              :step="0.5"
              style="width: 100%"
              @change="() => onPriceOrHoursChange(index)"
            />
          </Form.Item>
          <Form.Item label="시간당 단가 (원)" required>
            <InputNumber
              v-model:value="f.unitPrice"
              :min="0"
              :formatter="value => `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')"
              :parser="value => value ? value.replace(/\$\s?|(,*)/g, '') : 0"
              style="width: 100%"
              @change="() => onPriceOrHoursChange(index)"
            />
          </Form.Item>
          <Form.Item label="이용금액 (원)">
            <InputNumber
              v-model:value="f.totalPrice"
              readonly
              :formatter="value => `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')"
              style="width: 100%"
              class="bg-gray-100"
            />
          </Form.Item>
          <Form.Item label="이용 비고">
            <Input v-model:value="f.remark" placeholder="할인 적용 사유 등" />
          </Form.Item>
        </div>
      </div>
    </div>
  </div>
</template>
