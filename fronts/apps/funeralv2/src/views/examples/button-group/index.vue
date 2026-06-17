<script lang="ts" setup>
import type { Recordable } from '@vben/types';

import { reactive, ref } from 'vue';

import {
  Page,
  VbenButton,
  VbenButtonGroup,
  VbenCheckButtonGroup,
} from '@vben/common-ui';
import { LoaderCircle, Square, SquareCheckBig } from '@vben/icons';

import { Button, Card, message } from 'ant-design-vue';

import { useVbenForm } from '#/adapter/form';

const radioValue = ref<string | undefined>('a');
const checkValue = ref(['a', 'b']);

const options = [
  { label: '옵션1', value: 'a' },
  { label: '옵션2', value: 'b', num: 999 },
  { label: '옵션3', value: 'c' },
  { label: '옵션4', value: 'd' },
  { label: '옵션5', value: 'e' },
  { label: '옵션6', value: 'f' },
];

function resetValues() {
  radioValue.value = undefined;
  checkValue.value = [];
}

function beforeChange(v: any, isChecked: boolean) {
  return new Promise((resolve) => {
    message.loading({
      content: `${v}을(를) ${isChecked ? '선택' : '선택 해제'} 중...`,
      duration: 0,
      key: 'beforeChange',
    });
    setTimeout(() => {
      message.success({ content: `${v} 설정 완료`, key: 'beforeChange' });
      resolve(true);
    }, 2000);
  });
}

const compProps = reactive({
  beforeChange: undefined,
  disabled: false,
  gap: 0,
  showIcon: true,
  size: 'middle',
  allowClear: false,
} as Recordable<any>);

const [Form] = useVbenForm({
  handleValuesChange(values) {
    Object.keys(values).forEach((k) => {
      if (k === 'beforeChange') {
        compProps[k] = values[k] ? beforeChange : undefined;
      } else {
        compProps[k] = values[k];
      }
    });
  },
  commonConfig: {
    labelWidth: 150,
  },
  schema: [
    {
      component: 'RadioGroup',
      componentProps: {
        options: [
          { label: '대', value: 'large' },
          { label: '중', value: 'middle' },
          { label: '소', value: 'small' },
        ],
      },
      defaultValue: compProps.size,
      fieldName: 'size',
      label: '크기',
    },
    {
      component: 'RadioGroup',
      componentProps: {
        options: [
          { label: '없음', value: 0 },
          { label: '소', value: 5 },
          { label: '중', value: 15 },
          { label: '대', value: 30 },
        ],
      },
      defaultValue: compProps.gap,
      fieldName: 'gap',
      label: '간격',
    },
    {
      component: 'Switch',
      defaultValue: compProps.showIcon,
      fieldName: 'showIcon',
      label: '아이콘 표시',
    },
    {
      component: 'Switch',
      defaultValue: compProps.disabled,
      fieldName: 'disabled',
      label: '비활성화',
    },
    {
      component: 'Switch',
      defaultValue: false,
      fieldName: 'beforeChange',
      label: '사전 콜백',
    },
    {
      component: 'Switch',
      defaultValue: false,
      fieldName: 'allowClear',
      label: '지우기 허용',
      help: '단일 선택 시 선택 취소 허용 여부 (값은 undefined)',
    },
    {
      component: 'InputNumber',
      defaultValue: 0,
      fieldName: 'maxCount',
      label: '최대 선택 수',
      help: '다중 선택 시 유효하며, 0은 제한 없음을 의미합니다.',
    },
  ],
  showDefaultActions: false,
  submitOnChange: true,
});

function onBtnClick(value: any) {
  const opt = options.find((o) => o.value === value);
  if (opt) {
    message.success(`버튼 ${opt.label}을(를) 클릭했습니다. value = ${value}`);
  }
}
</script>
<template>
  <Page
    title="VbenButtonGroup 버튼 그룹"
    description="VbenButtonGroup은 버튼 세트를 감싸서 전체적인 스타일을 조정하는 버튼 컨테이너입니다. VbenCheckButtonGroup은 폼 컴포넌트로 사용되어 단일 또는 다중 선택 기능을 제공합니다."
  >
    <Card title="기본 사용법">
      <template #extra>
        <Button type="primary" @click="resetValues">값 비우기</Button>
      </template>
      <p class="mt-4">버튼 그룹:</p>
      <div class="mt-2 flex flex-col gap-2">
        <VbenButtonGroup v-bind="compProps" border>
          <VbenButton
            v-for="btn in options"
            :key="btn.value"
            variant="link"
            @click="onBtnClick(btn.value)"
          >
            {{ btn.label }}
          </VbenButton>
        </VbenButtonGroup>
        <VbenButtonGroup v-bind="compProps" border>
          <VbenButton
            v-for="btn in options"
            :key="btn.value"
            variant="outline"
            @click="onBtnClick(btn.value)"
          >
            {{ btn.label }}
          </VbenButton>
        </VbenButtonGroup>
      </div>
      <p class="mt-4">단일 선택: {{ radioValue }}</p>
      <div class="mt-2 flex flex-col gap-2">
        <VbenCheckButtonGroup
          v-model="radioValue"
          :options="options"
          v-bind="compProps"
        />
      </div>
      <p class="mt-4">단일 선택 슬롯: {{ radioValue }}</p>
      <div class="mt-2 flex flex-col gap-2">
        <VbenCheckButtonGroup
          v-model="radioValue"
          :options="options"
          v-bind="compProps"
        >
          <template #option="{ label, value, data }">
            <div class="flex items-center">
              <span>{{ label }}</span>
              <span class="ml-2 text-gray-400">{{ value }}</span>
              <span v-if="data.num" class="white ml-2">{{ data.num }}</span>
            </div>
          </template>
        </VbenCheckButtonGroup>
      </div>
      <p class="mt-4">다중 선택{{ checkValue }}</p>
      <div class="mt-2 flex flex-col gap-2">
        <VbenCheckButtonGroup
          v-model="checkValue"
          multiple
          :options="options"
          v-bind="compProps"
        />
      </div>
      <p class="mt-4">사용자 정의 아이콘{{ checkValue }}</p>
      <div class="mt-2 flex flex-col gap-2">
        <VbenCheckButtonGroup
          v-model="checkValue"
          multiple
          :options="options"
          v-bind="compProps"
        >
          <template #icon="{ loading, checked }">
            <LoaderCircle class="animate-spin" v-if="loading" />
            <SquareCheckBig v-else-if="checked" />
            <Square v-else />
          </template>
        </VbenCheckButtonGroup>
      </div>
    </Card>

    <Card title="설정" class="mt-4">
      <Form />
    </Card>
  </Page>
</template>
