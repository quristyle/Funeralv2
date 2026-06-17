<script lang="ts" setup>
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button, Card, message, Step, Steps, Switch } from 'ant-design-vue';

import { useVbenForm } from '#/adapter/form';

const currentTab = ref(0);
function onFirstSubmit(values: Record<string, any>) {
  message.success({
    content: `form1 values: ${JSON.stringify(values)}`,
  });
  currentTab.value = 1;
}
function onSecondReset() {
  currentTab.value = 0;
}
function onSecondSubmit(values: Record<string, any>) {
  message.success({
    content: `form2 values: ${JSON.stringify(values)}`,
  });
}

const [FirstForm, firstFormApi] = useVbenForm({
  commonConfig: {
    componentProps: {
      class: 'w-full',
    },
  },
  handleSubmit: onFirstSubmit,
  layout: 'horizontal',
  resetButtonOptions: {
    show: false,
  },
  schema: [
    {
      component: 'Input',
      componentProps: {
        placeholder: '입력하세요',
      },
      fieldName: 'formFirst',
      label: '폼1 필드',
      rules: 'required',
    },
  ],
  submitButtonOptions: {
    content: '다음',
  },
  wrapperClass: 'grid-cols-1 md:grid-cols-1 lg:grid-cols-1',
});
const [SecondForm, secondFormApi] = useVbenForm({
  commonConfig: {
    componentProps: {
      class: 'w-full',
    },
  },
  handleReset: onSecondReset,
  handleSubmit: onSecondSubmit,
  layout: 'horizontal',
  resetButtonOptions: {
    content: '이전',
  },
  schema: [
    {
      component: 'Input',
      componentProps: {
        placeholder: '입력하세요',
      },
      fieldName: 'formSecond',
      label: '폼2 필드',
      rules: 'required',
    },
  ],
  wrapperClass: 'grid-cols-1 md:grid-cols-1 lg:grid-cols-1',
});
const needMerge = ref(true);
async function handleMergeSubmit() {
  const values = await firstFormApi
    .merge(secondFormApi)
    .submitAllForm(needMerge.value);
  message.success({
    content: `merged form values: ${JSON.stringify(values)}`,
  });
}
</script>

<template>
  <Page
    description="폼 컴포넌트 병합 예제: 단계별 폼과 같은 일부 시나리오에서는 여러 폼을 병합하여 통합 제출해야 할 수 있습니다. 기본적으로 Object.assign 규칙을 사용하여 폼을 병합합니다. 특수한 데이터 처리가 필요한 경우 false를 전달할 수 있습니다."
    title="폼 컴포넌트"
  >
    <Card title="기본 예제">
      <template #extra>
        <Switch
          v-model="needMerge"
          checked-children="필드 병합 켜기"
          class="mr-4"
          un-checked-children="필드 병합 끄기"
        />
        <Button type="primary" @click="handleMergeSubmit">병합 제출</Button>
      </template>
      <div class="mx-auto max-w-lg">
        <Steps :current="currentTab" class="steps">
          <Step title="폼1" />
          <Step title="폼2" />
        </Steps>
        <div class="p-20">
          <FirstForm v-show="currentTab === 0" />
          <SecondForm v-show="currentTab === 1" />
        </div>
      </div>
    </Card>
  </Page>
</template>

