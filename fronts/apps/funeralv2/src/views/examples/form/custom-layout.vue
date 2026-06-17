<script lang="ts" setup>
import { h } from 'vue';

import { Page } from '@vben/common-ui';

import { Card } from 'ant-design-vue';

import { useVbenForm } from '#/adapter/form';

import DocButton from '../doc-button.vue';

const [CustomLayoutForm] = useVbenForm({
  // 모든 폼 항목에서 공유되며, 개별 항목에서 재정의 가능
  commonConfig: {
    // 모든 폼 항목
    componentProps: {
      class: 'w-full',
    },
  },
  layout: 'horizontal',
  schema: [
    {
      component: 'Select',
      fieldName: 'field1',
      label: '문자열',
    },
    {
      component: 'TreeSelect',
      fieldName: 'field2',
      label: '문자열',
    },
    {
      component: 'Mentions',
      fieldName: 'field3',
      label: '문자열',
    },
    {
      component: 'Input',
      fieldName: 'field4',
      label: '문자열',
    },
    {
      component: 'InputNumber',
      fieldName: 'field5',
      // 세 번째 열부터 시작 (중간에 한 열이 비어 있는 것과 같음)
      formItemClass: 'col-start-3',
      label: '앞에 한 열이 비어 있음',
    },
    {
      component: 'Divider',
      fieldName: '_divider',
      formItemClass: 'col-span-3',
      hideLabel: true,
      renderComponentContent: () => {
        return {
          default: () => h('div', '구분선'),
        };
      },
    },
    {
      component: 'Textarea',
      fieldName: 'field6',
      // 세 열의 공간을 모두 차지하며 기준선 정렬
      formItemClass: 'col-span-3 items-baseline',
      label: '세 열 모두 차지',
    },
    {
      component: 'Input',
      fieldName: 'field7',
      // 두 열의 공간을 차지하며 두 번째 열부터 시작 (앞에 한 열이 비어 있는 것과 같음)
      formItemClass: 'col-span-2 col-start-2',
      label: '두 열 모두 차지',
    },
    {
      component: 'Input',
      fieldName: 'field8',
      // 좌우 여백
      formItemClass: 'col-start-2',
      label: '좌우 여백',
    },
    {
      component: 'InputPassword',
      fieldName: 'field9',
      formItemClass: 'col-start-1',
      label: '문자열',
    },
  ],
  // 총 세 열
  wrapperClass: 'grid-cols-3',
});
</script>

<template>
  <Page
    content-class="flex flex-col gap-4"
    description="Tailwind CSS를 사용하여 폼 항목의 레이아웃을 사용자 정의합니다"
    title="폼 사용자 정의 레이아웃"
  >
    <template #description>
      <div class="text-muted-foreground">
        <p>Tailwind CSS를 사용하여 폼 항목의 레이아웃을 사용자 정의하고, Divider를 사용하여 폼을 나눕니다.</p>
      </div>
    </template>
    <template #extra>
      <DocButton class="mb-2" path="/components/common-ui/vben-form" />
    </template>
    <Card title="Tailwind CSS를 사용한 사용자 정의 레이아웃">
      <CustomLayoutForm />
    </Card>
  </Page>
</template>

