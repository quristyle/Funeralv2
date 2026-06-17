<script setup lang="ts">
import { computed } from 'vue';

import { ProfileSecuritySetting } from '@vben/common-ui';

const formSchema = computed(() => {
  return [
    {
      value: true,
      fieldName: 'accountPassword',
      label: '계정 비밀번호',
      description: '현재 비밀번호 강도: 강함',
    },
    {
      value: true,
      fieldName: 'securityPhone',
      label: '보안용 휴대전화',
      description: '연결된 휴대전화: 138****8293',
    },
    {
      value: true,
      fieldName: 'securityQuestion',
      label: '보안 질문',
      description: '보안 질문이 설정되지 않았습니다. 보안 질문은 계정 보안을 효과적으로 보호할 수 있습니다.',
    },
    {
      value: true,
      fieldName: 'securityEmail',
      label: '복구 이메일',
      description: '연결된 이메일: ant***sign.com',
    },
    {
      value: false,
      fieldName: 'securityMfa',
      label: 'MFA 기기',
      description: 'MFA 기기가 연결되지 않았습니다. 연결 후 2차 인증을 진행할 수 있습니다.',
    },
  ];
});
</script>
<template>
  <ProfileSecuritySetting :form-schema="formSchema" />
</template>
