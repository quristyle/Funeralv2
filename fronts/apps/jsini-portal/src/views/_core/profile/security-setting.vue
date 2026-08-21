<script setup lang="ts">
import { onMounted, ref } from 'vue';

import { ProfileSecuritySetting } from '@vben/common-ui';
import { message } from 'ant-design-vue';

import { getUserInfoApi, updateSettingApi } from '#/api';

const settings = ref({
  securityPhone: false,
  securityQuestion: false,
  securityEmail: false,
  securityMfa: false,
});

const formSchema = ref<any[]>([]);

function buildSchema() {
  formSchema.value = [
    {
      value: true,
      fieldName: 'accountPassword',
      label: '계정 비밀번호',
      description: '현재 비밀번호 강도: 강함',
      disabled: true,
    },
    {
      value: settings.value.securityPhone,
      fieldName: 'securityPhone',
      label: '보안용 휴대전화',
      description: '보안용 휴대전화 사용 여부를 설정합니다.',
    },
    {
      value: settings.value.securityQuestion,
      fieldName: 'securityQuestion',
      label: '보안 질문',
      description: '보안 질문 사용 여부를 설정합니다.',
    },
    {
      value: settings.value.securityEmail,
      fieldName: 'securityEmail',
      label: '복구 이메일',
      description: '복구용 이메일 사용 여부를 설정합니다.',
    },
    {
      value: settings.value.securityMfa,
      fieldName: 'securityMfa',
      label: 'MFA 기기',
      description: '2차 인증용 MFA 기기 사용 여부를 설정합니다.',
    },
  ];
}

async function loadSettings() {
  try {
    const userInfo: any = await getUserInfoApi();
    settings.value.securityPhone = !!userInfo.securityPhone;
    settings.value.securityQuestion = !!userInfo.securityQuestion;
    settings.value.securityEmail = !!userInfo.securityEmail;
    settings.value.securityMfa = !!userInfo.securityMfa;
    buildSchema();
  } catch (error) {
    console.error('Failed to load security settings:', error);
  }
}

async function handleChange(e: any) {
  const { fieldName, value } = e || {};
  if (fieldName === 'accountPassword') return;
  
  try {
    const detailType = fieldName.charAt(0).toUpperCase() + fieldName.slice(1);
    await updateSettingApi({
      fieldName: detailType,
      value,
    });
    message.success('보안 설정이 변경되었습니다.');
    await loadSettings();
  } catch (error) {
    message.error('보안 설정 변경에 실패했습니다.');
  }
}

onMounted(() => {
  loadSettings();
});
</script>
<template>
  <ProfileSecuritySetting :form-schema="formSchema" @change="handleChange" />
</template>
