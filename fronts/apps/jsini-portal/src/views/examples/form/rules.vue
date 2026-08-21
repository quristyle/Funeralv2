<script lang="ts" setup>
import { Page } from '@vben/common-ui';

import { Button, Card, message } from 'ant-design-vue';

import { useVbenForm, z } from '#/adapter/form';

const [Form, formApi] = useVbenForm({
  // 모든 폼 항목에서 공유되며, 개별 항목에서 재정의 가능
  commonConfig: {
    // 모든 폼 항목
    componentProps: {
      class: 'w-full',
    },
  },
  // 제출 함수
  handleSubmit: onSubmit,
  // 수직 레이아웃, label과 input이 다른 줄에 위치, 값은 vertical
  // 수평 레이아웃, label과 input이 같은 줄에 위치
  layout: 'horizontal',
  schema: [
    {
      // 컴포넌트는 #/adapter.ts 내에 등록되어야 하며 타입을 포함해야 함
      component: 'Input',
      // 컴포넌트 파라미터
      componentProps: {
        placeholder: '입력하세요',
      },
      // 필드명
      fieldName: 'field1',
      // 화면에 표시될 label
      label: '필드1',
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '입력하세요',
      },
      defaultValue: '기본값',
      fieldName: 'field2',
      label: '기본값(필수)',
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '입력하세요',
      },
      fieldName: 'field3',
      label: '기본값(선택)',
      rules: z.string().default('기본값').optional(),
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '입력하세요',
      },
      fieldName: 'field31',
      label: '사용자 정의 메시지',
      rules: z.string().min(1, { message: '최소 1자 이상 입력하세요' }),
    },
    {
      component: 'Input',
      // 컴포넌트 파라미터
      componentProps: {
        placeholder: '입력하세요',
      },
      // 필드명
      fieldName: 'field4',
      // 화면에 표시될 label
      label: '이메일',
      rules: z.string().email('올바른 이메일 주소를 입력하세요'),
    },
    {
      component: 'InputNumber',
      componentProps: {
        placeholder: '입력하세요',
      },
      fieldName: 'number',
      label: '숫자',
      rules: 'required',
    },
    {
      component: 'Select',
      componentProps: {
        allowClear: true,
        filterOption: true,
        options: [
          {
            label: '옵션1',
            value: '1',
          },
          {
            label: '옵션2',
            value: '2',
          },
        ],
        placeholder: '선택하세요',
        showSearch: true,
      },
      defaultValue: undefined,
      fieldName: 'options',
      label: '셀렉트박스',
      rules: 'selectRequired',
    },
    {
      component: 'RadioGroup',
      componentProps: {
        options: [
          {
            label: '옵션1',
            value: '1',
          },
          {
            label: '옵션2',
            value: '2',
          },
        ],
      },
      fieldName: 'radioGroup',
      label: '라디오 그룹',
      rules: 'selectRequired',
    },
    {
      component: 'CheckboxGroup',
      componentProps: {
        name: 'cname',
        options: [
          {
            label: '옵션1',
            value: '1',
          },
          {
            label: '옵션2',
            value: '2',
          },
        ],
      },
      fieldName: 'checkboxGroup',
      label: '체크박스 그룹',
      rules: 'selectRequired',
    },
    {
      component: 'Checkbox',
      fieldName: 'checkbox',
      label: '',
      renderComponentContent: () => {
        return {
          default: () => ['읽었으며 동의합니다'],
        };
      },
      rules: z.boolean().refine((value) => value, {
        message: '체크해 주세요',
      }),
    },
    {
      component: 'DatePicker',
      defaultValue: undefined,
      fieldName: 'datePicker',
      label: '날짜 선택',
      rules: 'selectRequired',
    },
    {
      component: 'RangePicker',
      defaultValue: undefined,
      fieldName: 'rangePicker',
      label: '범위 선택',
      rules: 'selectRequired',
    },
    {
      component: 'InputPassword',
      componentProps: {
        placeholder: '입력하세요',
      },
      fieldName: 'password',
      label: '비밀번호',
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '입력하세요',
      },
      fieldName: 'input-blur',
      formFieldProps: {
        validateOnChange: false,
        validateOnModelUpdate: false,
      },
      help: 'blur 시에만 유효성 검사가 트리거됩니다',
      label: 'blur 트리거',
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '입력하세요',
      },
      fieldName: 'input-async',
      label: '비동기 유효성 검사',
      rules: z
        .string()
        .min(3, '사용자 이름은 최소 3자 이상이어야 합니다')
        .refine(
          async (username) => {
            // 비동기 함수라고 가정하고 사용자 이름 중복 여부를 확인하는 시뮬레이션
            const checkUsernameExists = async (
              username: string,
            ): Promise<boolean> => {
              await new Promise((resolve) => setTimeout(resolve, 1000));
              return username === 'existingUser';
            };
            const exists = await checkUsernameExists(username);
            return !exists;
          },
          {
            message: '이미 존재하는 사용자 이름입니다',
          },
        ),
    },
  ],
  // 대화면 한 줄에 3개, 중화면 2개, 소화면 1개 표시
  wrapperClass: 'grid-cols-1 md:grid-cols-2 lg:grid-cols-3',
});

function onSubmit(values: Record<string, any>) {
  message.success({
    content: `form values: ${JSON.stringify(values)}`,
  });
}
</script>

<template>
  <Page description="폼 유효성 검사 예제" title="폼 컴포넌트">
    <Card title="기본 컴포넌트 유효성 검사 예제">
      <template #extra>
        <Button @click="() => formApi.validate()">폼 유효성 검사</Button>
        <Button class="mx-2" @click="() => formApi.resetValidate()">
          유효성 검사 정보 초기화
        </Button>
      </template>
      <Form />
    </Card>
  </Page>
</template>

