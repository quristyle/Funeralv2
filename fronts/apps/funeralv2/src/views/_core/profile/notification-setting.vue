<script setup lang="ts">
import { computed } from 'vue';

import { ProfileNotificationSetting } from '@vben/common-ui';

const formSchema = computed(() => {
  return [
    {
      value: true,
      fieldName: 'accountPassword',
      label: '계정 비밀번호',
      description: '다른 사용자의 메시지가 알림으로 통지됩니다.',
    },
    {
      value: true,
      fieldName: 'systemMessage',
      label: '시스템 메시지',
      description: '시스템 메시지가 알림으로 통지됩니다.',
    },
    {
      value: true,
      fieldName: 'todoTask',
      label: '할 일',
      description: '할 일이 알림으로 통지됩니다.',
    },
  ];
});
</script>
<template>
  <ProfileNotificationSetting :form-schema="formSchema" />
</template>
