<script lang="ts" setup>
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button, Card, Switch } from 'ant-design-vue';

import { useVbenForm } from '#/adapter/form';

defineOptions({
  name: 'ScrollToErrorTest',
});

const scrollEnabled = ref(true);

const [Form, formApi] = useVbenForm({
  scrollToFirstError: scrollEnabled.value,
  schema: [
    {
      component: 'Input',
      componentProps: {
        placeholder: '사용자 이름을 입력하세요',
      },
      fieldName: 'username',
      label: '사용자 이름',
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '이메일을 입력하세요',
      },
      fieldName: 'email',
      label: '이메일',
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '휴대폰 번호를 입력하세요',
      },
      fieldName: 'phone',
      label: '휴대폰 번호',
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '주소를 입력하세요',
      },
      fieldName: 'address',
      label: '주소',
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '비고를 입력하세요',
      },
      fieldName: 'remark',
      label: '비고',
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '회사명을 입력하세요',
      },
      fieldName: 'company',
      label: '회사명',
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '직책을 입력하세요',
      },
      fieldName: 'position',
      label: '직책',
      rules: 'required',
    },
    {
      component: 'Select',
      componentProps: {
        options: [
          { label: '남', value: 'male' },
          { label: '여', value: 'female' },
        ],
        placeholder: '성별을 선택하세요',
      },
      fieldName: 'gender',
      label: '성별',
      rules: 'selectRequired',
    },
  ],
  showDefaultActions: false,
});

// validateAndSubmitForm(검증 및 제출) 테스트
async function testValidateAndSubmit() {
  await formApi.validateAndSubmitForm();
}

// validate(전체 폼 수동 검증) 테스트
async function testValidate() {
  await formApi.validate();
}

// validateField(개별 필드 검증) 테스트
async function testValidateField() {
  await formApi.validateField('username');
}

// 스크롤 기능 전환
function toggleScrollToError() {
  formApi.setState({ scrollToFirstError: scrollEnabled.value });
}

// 일부 데이터 채우기 테스트
async function fillPartialData() {
  await formApi.resetForm();
  await formApi.setFieldValue('username', '테스트 사용자');
  await formApi.setFieldValue('email', 'test@example.com');
}
</script>

<template>
  <Page
    description="폼 검증 실패 시 오류 필드로 자동 스크롤되는 기능을 테스트합니다"
    title="오류 필드 스크롤 테스트"
  >
    <Card title="기능 테스트">
      <template #extra>
        <div class="flex items-center gap-2">
          <Switch v-model="scrollEnabled" @change="toggleScrollToError" />
          <span>오류 필드로 스크롤 활성화</span>
        </div>
      </template>

      <div class="space-y-4">
        <div class="rounded-sm bg-blue-50 p-4">
          <h3 class="mb-2 font-medium">테스트 설명:</h3>
          <ul class="list-inside list-disc space-y-1 text-sm">
            <li>모든 검증 방법은 검증 실패 시 첫 번째 오류 필드로 자동 스크롤됩니다.</li>
            <li>우측 상단의 스위치를 통해 자동 스크롤 기능 활성화 여부를 제어할 수 있습니다.</li>
          </ul>
        </div>

        <div class="rounded-sm border p-4">
          <h4 class="mb-3 font-medium">검증 방법 테스트:</h4>
          <div class="flex flex-wrap gap-2">
            <Button type="primary" @click="testValidateAndSubmit">
              validateAndSubmitForm() 테스트
            </Button>
            <Button @click="testValidate"> validate() 테스트 </Button>
            <Button @click="testValidateField"> validateField() 테스트 </Button>
          </div>
          <div class="mt-2 text-xs text-gray-500">
            <p>• validateAndSubmitForm(): 폼 검증 및 제출</p>
            <p>• validate(): 전체 폼 수동 검증</p>
            <p>• validateField(): 개별 필드 검증(여기서는 사용자 이름 필드 테스트)</p>
          </div>
        </div>

        <div class="rounded-sm border p-4">
          <h4 class="mb-3 font-medium">데이터 채우기 테스트:</h4>
          <div class="flex flex-wrap gap-2">
            <Button @click="fillPartialData"> 일부 데이터 채우기 </Button>
            <Button @click="() => formApi.resetForm()"> 폼 초기화 </Button>
          </div>
          <div class="mt-2 text-xs text-gray-500">
            <p>• 일부 데이터 채우기 후 검증 시 첫 번째 오류 필드로 스크롤됩니다.</p>
          </div>
        </div>

        <Form />
      </div>
    </Card>
  </Page>
</template>

