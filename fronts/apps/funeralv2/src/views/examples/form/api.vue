<script lang="ts" setup>
import type { RefSelectProps } from 'ant-design-vue/es/select';

import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button, Card, message, Space } from 'ant-design-vue';

import { useVbenForm } from '#/adapter/form';

const isReverseActionButtons = ref(false);

const [BaseForm, formApi] = useVbenForm({
  // 작업 버튼 위치 반전
  actionButtonsReverse: isReverseActionButtons.value,
  // 모든 폼 항목에서 공유되며, 개별 항목에서 재정의 가능
  commonConfig: {
    // 모든 폼 항목
    componentProps: {
      class: 'w-full',
    },
  },
  // tailwindcss grid 레이아웃 사용
  // 제출 함수
  handleSubmit: onSubmit,
  // 수직 레이아웃, label과 input이 다른 줄에 위치, 값은 vertical
  layout: 'horizontal',
  // 수평 레이아웃, label과 input이 같은 줄에 위치
  schema: [
    {
      // 컴포넌트는 #/adapter.ts 내에 등록되어야 하며 타입을 포함해야 함
      component: 'Input',
      // 컴포넌트 파라미터
      componentProps: {
        placeholder: '사용자 이름을 입력하세요',
      },
      // 필드명
      fieldName: 'field1',
      // 화면에 표시될 label
      label: 'field1',
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '입력하세요',
      },
      fieldName: 'field2',
      label: 'field2',
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
      fieldName: 'fieldOptions',
      label: '셀렉트박스',
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

function handleClick(
  action:
    | 'batchAddSchema'
    | 'batchDeleteSchema'
    | 'componentRef'
    | 'disabled'
    | 'hiddenAction'
    | 'hiddenResetButton'
    | 'hiddenSubmitButton'
    | 'labelWidth'
    | 'resetDisabled'
    | 'resetLabelWidth'
    | 'reverseActionButtons'
    | 'showAction'
    | 'showResetButton'
    | 'showSubmitButton'
    | 'updateActionAlign'
    | 'updateResetButton'
    | 'updateSchema'
    | 'updateSubmitButton',
) {
  switch (action) {
    case 'batchAddSchema': {
      formApi.setState((prev) => {
        const currentSchema = prev?.schema ?? [];
        const newSchema = [];
        for (let i = 0; i < 3; i++) {
          newSchema.push({
            component: 'Input',
            componentProps: {
              placeholder: '입력하세요',
            },
            fieldName: `field${i}${Date.now()}`,
            label: `field+`,
          });
        }
        return {
          schema: [...currentSchema, ...newSchema],
        };
      });
      break;
    }

    case 'batchDeleteSchema': {
      formApi.setState((prev) => {
        const currentSchema = prev?.schema ?? [];
        return {
          schema: currentSchema.slice(0, -3),
        };
      });
      break;
    }
    case 'componentRef': {
      // 셀렉트박스 컴포넌트 인스턴스를 가져와서 focus 메서드 호출
      formApi.getFieldComponentRef<RefSelectProps>('fieldOptions')?.focus?.();
      break;
    }
    case 'disabled': {
      formApi.setState({ commonConfig: { disabled: true } });
      break;
    }
    case 'hiddenAction': {
      formApi.setState({ showDefaultActions: false });
      break;
    }
    case 'hiddenResetButton': {
      formApi.setState({ resetButtonOptions: { show: false } });
      break;
    }
    case 'hiddenSubmitButton': {
      formApi.setState({ submitButtonOptions: { show: false } });
      break;
    }
    case 'labelWidth': {
      formApi.setState({
        commonConfig: {
          labelWidth: 150,
        },
      });
      break;
    }
    case 'resetDisabled': {
      formApi.setState({ commonConfig: { disabled: false } });
      break;
    }
    case 'resetLabelWidth': {
      formApi.setState({
        commonConfig: {
          labelWidth: 100,
        },
      });
      break;
    }
    case 'reverseActionButtons': {
      isReverseActionButtons.value = !isReverseActionButtons.value;
      formApi.setState({ actionButtonsReverse: isReverseActionButtons.value });
      break;
    }
    case 'showAction': {
      formApi.setState({ showDefaultActions: true });
      break;
    }
    case 'showResetButton': {
      formApi.setState({ resetButtonOptions: { show: true } });
      break;
    }
    case 'showSubmitButton': {
      formApi.setState({ submitButtonOptions: { show: true } });
      break;
    }

    case 'updateActionAlign': {
      formApi.setState({
        // 클래스 직접 조정 가능
        actionWrapperClass: 'text-center',
      });
      break;
    }
    case 'updateResetButton': {
      formApi.setState({
        resetButtonOptions: { disabled: true },
      });
      break;
    }
    case 'updateSchema': {
      formApi.updateSchema([
        {
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
              {
                label: '옵션3',
                value: '3',
              },
            ],
          },
          fieldName: 'fieldOptions',
        },
      ]);
      message.success('`fieldOptions` 필드의 셀렉트박스 옵션이 성공적으로 업데이트되었습니다.');
      break;
    }
    case 'updateSubmitButton': {
      formApi.setState({
        submitButtonOptions: { loading: true },
      });
      break;
    }
  }
}
</script>

<template>
  <Page description="폼 컴포넌트 API 조작 예제입니다." title="폼 컴포넌트">
    <Space class="mb-5 flex-wrap">
      <Button @click="handleClick('updateSchema')">updateSchema</Button>
      <Button @click="handleClick('labelWidth')">labelWidth 변경</Button>
      <Button @click="handleClick('resetLabelWidth')">labelWidth 복원</Button>
      <Button @click="handleClick('disabled')">폼 비활성화</Button>
      <Button @click="handleClick('resetDisabled')">비활성화 해제</Button>
      <Button @click="handleClick('reverseActionButtons')">
        작업 버튼 위치 반전
      </Button>
      <Button @click="handleClick('hiddenAction')">작업 버튼 숨기기</Button>
      <Button @click="handleClick('showAction')">작업 버튼 표시</Button>
      <Button @click="handleClick('hiddenResetButton')">초기화 버튼 숨기기</Button>
      <Button @click="handleClick('showResetButton')">초기화 버튼 표시</Button>
      <Button @click="handleClick('hiddenSubmitButton')">제출 버튼 숨기기</Button>
      <Button @click="handleClick('showSubmitButton')">제출 버튼 표시</Button>
      <Button @click="handleClick('updateResetButton')">초기화 버튼 수정</Button>
      <Button @click="handleClick('updateSubmitButton')">제출 버튼 수정</Button>
      <Button @click="handleClick('updateActionAlign')">
        작업 버튼 위치 조정
      </Button>
      <Button @click="handleClick('batchAddSchema')"> 폼 항목 일괄 추가 </Button>
      <Button @click="handleClick('batchDeleteSchema')">
        폼 항목 일괄 삭제
      </Button>
      <Button @click="handleClick('componentRef')">셀렉트박스 컴포넌트 포커스</Button>
    </Space>
    <Card title="조작 예제">
      <BaseForm />
    </Card>
  </Page>
</template>

