<script lang="ts" setup>
import { Page } from '@vben/common-ui';

import { Card, message } from 'ant-design-vue';

import { useVbenForm } from '#/adapter/form';

const [QueryForm] = useVbenForm({
  // 기본 펼침
  collapsed: false,
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
        placeholder: '사용자 이름을 입력하세요',
      },
      // 필드명
      fieldName: 'username',
      // 화면에 표시될 label
      label: '문자열',
    },
    {
      component: 'InputPassword',
      componentProps: {
        placeholder: '비밀번호를 입력하세요',
      },
      fieldName: 'password',
      label: '비밀번호',
    },
    {
      component: 'InputNumber',
      componentProps: {
        placeholder: '입력하세요',
      },
      fieldName: 'number',
      label: '숫자(접미사 포함)',
      suffix: () => '¥',
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
      fieldName: 'options',
      label: '셀렉트박스',
    },
    {
      component: 'DatePicker',
      fieldName: 'datePicker',
      label: '날짜 선택',
    },
  ],
  // 펼침/접기 가능 여부
  showCollapseButton: true,
  submitButtonOptions: {
    content: '조회',
  },
  // 대화면 한 줄에 3개, 중화면 2개, 소화면 1개 표시
  wrapperClass: 'grid-cols-1 md:grid-cols-2 lg:grid-cols-3',
});

const [InlineForm] = useVbenForm({
  layout: 'inline',
  schema: [
    {
      // 컴포넌트는 #/adapter.ts 내에 등록되어야 하며 타입을 포함해야 함
      component: 'Input',
      // 컴포넌트 파라미터
      componentProps: {
        placeholder: '사용자 이름을 입력하세요',
      },
      // 필드명
      fieldName: 'username',
      // 화면에 표시될 label
      label: '문자열',
    },
    {
      component: 'InputPassword',
      componentProps: {
        placeholder: '비밀번호를 입력하세요',
      },
      fieldName: 'password',
      label: '비밀번호',
    },
    {
      component: 'InputNumber',
      componentProps: {
        placeholder: '입력하세요',
      },
      fieldName: 'number',
      label: '숫자(접미사 포함)',
      suffix: () => '¥',
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
      fieldName: 'options',
      label: '셀렉트박스',
    },
  ],
});

const [QueryForm1] = useVbenForm({
  // 기본 펼침
  collapsed: true,
  collapsedRows: 2,
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
  schema: (() => {
    const schema = [];
    for (let index = 0; index < 14; index++) {
      schema.push({
        // 컴포넌트는 #/adapter.ts 내에 등록되어야 하며 타입을 포함해야 함
        component: 'Input',
        // 필드명
        fieldName: `field${index}`,
        // 화면에 표시될 label
        label: `필드${index}`,
      });
    }
    return schema;
  })(),
  // 펼침/접기 가능 여부
  showCollapseButton: true,
  submitButtonOptions: {
    content: '조회',
  },
  // 대화면 한 줄에 3개, 중화면 2개, 소화면 1개 표시
  wrapperClass: 'grid-cols-1 md:grid-cols-2 lg:grid-cols-3',
});

const [QueryForm2] = useVbenForm({
  // 작업 버튼 그룹 newLine: 새 줄에 표시. rowEnd: 한 줄에 표시, 오른쪽 정렬(기본). inline: grid 기본 스타일 사용
  actionLayout: 'newLine',
  actionPosition: 'left', // 작업 버튼 그룹을 왼쪽에 표시
  // 기본 접힘
  collapsed: true,
  collapsedRows: 3,
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
  layout: 'vertical',
  schema: [
    {
      // 컴포넌트는 #/adapter.ts 내에 등록되어야 하며 타입을 포함해야 함
      component: 'Input',
      // 컴포넌트 파라미터
      componentProps: {
        placeholder: '사용자 이름을 입력하세요',
      },
      // 필드명
      fieldName: 'username',
      // 화면에 표시될 label
      label: '문자열',
    },
    {
      component: 'InputPassword',
      componentProps: {
        placeholder: '비밀번호를 입력하세요',
      },
      fieldName: 'password',
      label: '비밀번호',
    },
    {
      component: 'InputNumber',
      componentProps: {
        placeholder: '입력하세요',
      },
      fieldName: 'number',
      label: '숫자(접미사 포함)',
      suffix: () => '¥',
    },
    {
      component: 'DatePicker',
      fieldName: 'datePicker',
      label: '날짜 선택',
    },
  ],
  // 펼침/접기 가능 여부
  showCollapseButton: true,
  submitButtonOptions: {
    content: '조회',
  },
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
  <Page
    description="조회 폼입니다. 보통 테이블과 함께 사용되며, 접고 펼칠 수 있습니다."
    title="폼 컴포넌트"
  >
    <Card class="mb-5" title="조회 폼, 기본 펼침">
      <QueryForm />
    </Card>

    <Card class="mb-5" title="조회 폼, 한 줄 폼">
      <InlineForm />
    </Card>

    <Card class="mb-5" title="조회 폼, 기본 펼침, 수직 레이아웃">
      <QueryForm2 />
    </Card>

    <Card title="조회 폼, 기본 접힘, 접었을 때 2행 유지">
      <QueryForm1 />
    </Card>
  </Page>
</template>

