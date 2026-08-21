<script lang="ts" setup>
import { Form, Input, RangePicker, Button } from 'ant-design-vue';
import BizSelect from '#/components/BizSelect.vue';

const props = defineProps<{
  modelValue: {
    companyId: string;
    buildingId: string;
    floorId: string;
    name: string;
  };
  roomEnterDates: any;
  funeralDates: any;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', val: typeof props.modelValue): void;
  (e: 'update:roomEnterDates', val: any): void;
  (e: 'update:funeralDates', val: any): void;
  (e: 'search'): void;
  (e: 'reset'): void;
}>();

function onSearch() {
  emit('search');
}

function onReset() {
  emit('reset');
}
</script>

<style scoped>
.ant-form-item{
  margin-bottom: 0rem !important;
}
</style>


<template>
  <div class="mb-4 bg-card p-4 rounded-lg shadow-sm border border-border">
    <Form layout="horizontal" class="flex justify-end  space-y-1">
      <div class="flex grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 pr-2">
        <!-- 회사 필터 -->
        <Form.Item 
          label="회사" 
          class="mb-0 flex items-center" 
          :label-col="{ style: 'width: 55px; text-align: right; margin-right: 8px;' }" 
          :wrapper-col="{ style: 'flex: 1' }"
        >
          <BizSelect
            :value="modelValue.companyId"
            type="company"
            placeholder="회사 전체"
            show-all
            @update:value="(val) => emit('update:modelValue', { ...modelValue, companyId: val as string })"
            @change="onSearch"
          />
        </Form.Item>

        <!-- 건물 필터 -->
        <Form.Item 
          label="건물" 
          class="mb-0 flex items-center" 
          :label-col="{ style: 'width: 55px; text-align: right; margin-right: 8px;' }" 
          :wrapper-col="{ style: 'flex: 1' }"
        >
          <BizSelect
            :value="modelValue.buildingId"
            type="building"
            :params="{ companyId: modelValue.companyId }"
            placeholder="건물 전체"
            show-all
            @update:value="(val) => emit('update:modelValue', { ...modelValue, buildingId: val as string })"
            @change="onSearch"
          />
        </Form.Item>

        <!-- 층 필터 -->
        <Form.Item 
          label="층" 
          class="mb-0 flex items-center" 
          :label-col="{ style: 'width: 55px; text-align: right; margin-right: 8px;' }" 
          :wrapper-col="{ style: 'flex: 1' }"
        >
          <BizSelect
            :value="modelValue.floorId"
            type="floor"
            :params="{ buildingId: modelValue.buildingId }"
            placeholder="층 전체"
            show-all
            @update:value="(val) => emit('update:modelValue', { ...modelValue, floorId: val as string })"
            @change="onSearch"
          />
        </Form.Item>

        <!-- 고인명 -->
        <Form.Item 
          label="고인" 
          class="mb-0 flex items-center" 
          :label-col="{ style: 'width: 55px; text-align: right; margin-right: 8px;' }" 
          :wrapper-col="{ style: 'flex: 1' }"
        >
          <Input 
            :value="modelValue.name" 
            placeholder="고인명 입력" 
            allow-clear 
            @input="(e) => emit('update:modelValue', { ...modelValue, name: (e.target as HTMLInputElement).value })"
            @press-enter="onSearch" 
          />
        </Form.Item>

        <!-- 입실 기간 -->
        <Form.Item 
          label="입실" 
          class="mb-0 flex items-center" 
          :label-col="{ style: 'width: 55px; text-align: right; margin-right: 8px;' }" 
          :wrapper-col="{ style: 'flex: 1' }"
        >
          <RangePicker 
            :value="roomEnterDates" 
            class="w-full" 
            @change="(val) => emit('update:roomEnterDates', val)"
          />
        </Form.Item>

        <!-- 발인 기간 -->
        <Form.Item 
          label="발인" 
          class="mb-0 flex items-center" 
          :label-col="{ style: 'width: 55px; text-align: right; margin-right: 8px;' }" 
          :wrapper-col="{ style: 'flex: 1' }"
        >
          <RangePicker 
            :value="funeralDates" 
            class="w-full" 
            @change="(val) => emit('update:funeralDates', val)"
          />
        </Form.Item>
      </div>

      <div class="flex justify-end gap-2 pr-2">
        <Button @click="onReset">초기화</Button>
        <Button type="primary" @click="onSearch">검색</Button>
      </div>
    </Form>
  </div>
</template>
