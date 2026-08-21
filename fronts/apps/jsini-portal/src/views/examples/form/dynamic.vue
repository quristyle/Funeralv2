<script lang="ts" setup>
import { Page } from '@vben/common-ui';

import { Button, Card, message } from 'ant-design-vue';

import { useVbenForm } from '#/adapter/form';

const [Form, formApi] = useVbenForm({
  // 제출 함수
  handleSubmit: onSubmit,
  schema: [
    {
      component: 'Input',
      defaultValue: 'hidden value',
      dependencies: {
        show: false,
        // 아무 필드나 변경될 때 트리거됨
        triggerFields: ['field1Switch'],
      },
      fieldName: 'hiddenField',
      label: '숨겨진 필드',
    },
    {
      component: 'Switch',
      defaultValue: true,
      fieldName: 'field1Switch',
      help: 'DOM을 통한 파괴 제어',
      label: '필드1 표시',
    },
    {
      component: 'Switch',
      defaultValue: true,
      fieldName: 'field2Switch',
      help: 'CSS를 통한 숨김 제어',
      label: '필드2 표시',
    },
    {
      component: 'Switch',
      fieldName: 'field3Switch',
      label: '필드3 비활성화',
    },
    {
      component: 'Switch',
      fieldName: 'field4Switch',
      label: '필드4 필수',
    },
    {
      component: 'Input',
      dependencies: {
        if(values) {
          return !!values.field1Switch;
        },
        // 지정된 필드가 변경될 때만 트리거됨
        triggerFields: ['field1Switch'],
      },
      // 필드명
      fieldName: 'field1',
      // 화면에 표시될 label
      label: '필드1',
    },
    {
      component: 'Input',
      dependencies: {
        show(values) {
          return !!values.field2Switch;
        },
        triggerFields: ['field2Switch'],
      },
      fieldName: 'field2',
      label: '필드2',
    },
    {
      component: 'Input',
      dependencies: {
        disabled(values) {
          return !!values.field3Switch;
        },
        triggerFields: ['field3Switch'],
      },
      fieldName: 'field3',
      label: '필드3',
    },
    {
      component: 'Input',
      dependencies: {
        required(values) {
          return !!values.field4Switch;
        },
        triggerFields: ['field4Switch'],
      },
      fieldName: 'field4',
      label: '필드4',
    },
    {
      component: 'Input',
      dependencies: {
        rules(values) {
          if (values.field1 === '123') {
            return 'required';
          }
          return null;
        },
        triggerFields: ['field1'],
      },
      fieldName: 'field5',
      help: '필드1의 값이 `123`일 때 필수 입력',
      label: '동적 규칙',
    },
    {
      component: 'Select',
      componentProps: {
        allowClear: true,
        class: 'w-full',
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
      dependencies: {
        componentProps(values) {
          if (values.field2 === '123') {
            return {
              options: [
                {
                  label: '옵션1',
                  value: '1',
                },
                {
                  label: '옵션2',
                  value: '2',
                },
                {
                  label: '옵션3',
                  value: '3',
                },
              ],
            };
          }
          return {};
        },
        triggerFields: ['field2'],
      },
      fieldName: 'field6',
      help: '필드2의 값이 `123`일 때 셀렉트박스 옵션 변경',
      label: '동적 설정',
    },
    {
      component: 'Input',
      fieldName: 'field7',
      label: '필드7',
    },
  ],
  // 대화면 한 줄에 3개, 중화면 2개, 소화면 1개 표시
  wrapperClass: 'grid-cols-1 md:grid-cols-3 lg:grid-cols-4',
});

const [SyncForm] = useVbenForm({
  handleSubmit: onSubmit,
  schema: [
    {
      component: 'Input',
      // 필드명
      fieldName: 'field1',
      // 화면에 표시될 label
      label: '필드1',
    },
    {
      component: 'Input',
      componentProps: {
        disabled: true,
      },
      dependencies: {
        trigger(values, form) {
          form.setFieldValue('field2', values.field1);
        },
        // 지정된 필드가 변경될 때만 트리거됨
        triggerFields: ['field1'],
      },
      // 필드명
      fieldName: 'field2',
      // 화면에 표시될 label
      label: '필드2',
    },
  ],
  // 대화면 한 줄에 3개, 중화면 2개, 소화면 1개 표시
  wrapperClass: 'grid-cols-1 md:grid-cols-3 lg:grid-cols-4',
});

function onSubmit(values: Record<string, any>) {
  message.success({
    content: `form values: ${JSON.stringify(values)}`,
  });
}

function handleDelete() {
  formApi.setState((prev) => {
    return {
      schema: prev.schema?.filter((item) => item.fieldName !== 'field7'),
    };
  });
}

function handleAdd() {
  formApi.setState((prev) => {
    return {
      schema: [
        ...(prev?.schema ?? []),
        {
          component: 'Input',
          fieldName: `field${Date.now()}`,
          label: '필드+',
        },
      ],
    };
  });
}

function handleUpdate() {
  formApi.setState((prev) => {
    return {
      schema: prev.schema?.map((item) => {
        if (item.fieldName === 'field3') {
          return {
            ...item,
            label: '필드3-수정',
          };
        }
        return item;
      }),
    };
  });
}
</script>

<template>
  <Page
    description="폼 컴포넌트 동적 연동 예제입니다. 일반적인 시나리오를 포함하고 있습니다. 추가, 삭제, 수정은 본질적으로 schema를 수정하는 것이며, `setState`를 통해 동적으로 schema를 수정할 수도 있습니다."
    title="폼 컴포넌트"
  >
    <Card title="폼 동적 연동 예제">
      <template #extra>
        <Button class="mr-2" @click="handleUpdate">필드3 수정</Button>
        <Button class="mr-2" @click="handleDelete">필드7 삭제</Button>
        <Button @click="handleAdd">필드 추가</Button>
      </template>
      <Form />
    </Card>

    <Card class="mt-5" title="필드 동기화, 필드1 데이터와 필드2 데이터 동기화">
      <SyncForm />
    </Card>
  </Page>
</template>

