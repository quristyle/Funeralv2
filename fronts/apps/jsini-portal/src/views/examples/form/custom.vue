<script lang="ts" setup>
import { h, markRaw } from 'vue';

import { Page } from '@vben/common-ui';

import { Card, Input, message } from 'ant-design-vue';

import { useVbenForm, z } from '#/adapter/form';

import TwoFields from './modules/two-fields.vue';

const [Form] = useVbenForm({
  // 모든 폼 항목 공용, 폼 내에서 개별적으로 덮어쓸 수 있음
  commonConfig: {
    // 모든 폼 항목
    componentProps: {
      class: 'w-full',
    },
    labelClass: 'w-2/6',
  },
  fieldMappingTime: [['field4', ['phoneType', 'phoneNumber'], null]],
  // 제출 함수
  handleSubmit: onSubmit,
  // 수직 레이아웃, 레이블과 입력이 다른 줄에 위치, 값은 vertical
  // 수평 레이아웃, 레이블과 입력이 같은 줄에 위치
  layout: 'horizontal',
  schema: [
    {
      // 컴포넌트는 #/adapter.ts 내에 등록되어야 하며 타입이 추가되어야 함
      component: 'Input',
      fieldName: 'field',
      label: '사용자 정의 접미사',
      suffix: () => h('span', { class: 'text-red-600' }, '원'),
    },
    {
      component: 'Input',
      fieldName: 'field1',
      label: '사용자 정의 컴포넌트 slot',
      renderComponentContent: () => ({
        prefix: () => 'prefix',
        suffix: () => 'suffix',
      }),
    },
    {
      component: h(Input, { placeholder: 'Field2를 입력하세요' }),
      fieldName: 'field2',
      label: '사용자 정의 컴포넌트',
      modelPropName: 'value',
      rules: 'required',
    },
    {
      component: 'Input',
      fieldName: 'field3',
      label: '사용자 정의 컴포넌트(slot)',
      rules: 'required',
    },
    {
      component: markRaw(TwoFields),
      defaultValue: [undefined, ''],
      fieldName: 'field4',
      formItemClass: 'col-span-1',
      label: '조합 필드',
      rules: z
        .array(z.string().optional())
        .length(2, '유형을 선택하고 휴대폰 번호를 입력하세요')
        .refine((v) => !!v[0], {
          message: '유형을 선택하세요',
        })
        .refine((v) => !!v[1] && v[1] !== '', {
          message: '　　　　　　　휴대폰 번호를 입력하세요',
        })
        .refine((v) => v[1]?.match(/^1[3-9]\d{9}$/), {
          // 전각 공백을 사용하여 오류 메시지를 휴대폰 번호 입력란 아래로 배치
          message: '　　　　　　　번호 형식이 올바르지 않습니다',
        }),
    },
  ],
  // 중간 화면에서는 한 줄에 2개, 작은 화면에서는 1개 표시
  wrapperClass: 'grid-cols-1 md:grid-cols-2',
});

function onSubmit(values: Record<string, any>) {
  message.success({
    content: `form values: ${JSON.stringify(values)}`,
  });
}
</script>

<template>
  <Page description="폼 컴포넌트 사용자 정의 예시" title="폼 컴포넌트">
    <Card title="기본 예시">
      <Form>
        <template #field3="slotProps">
          <Input placeholder="입력하세요" v-bind="slotProps" />
        </template>
      </Form>
    </Card>
  </Page>
</template>
