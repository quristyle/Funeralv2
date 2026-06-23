<script setup lang="ts">
import { onMounted, ref } from 'vue';

import { ProfileNotificationSetting } from '@vben/common-ui';
import { message } from 'ant-design-vue';

import { getUserInfoApi, updateSettingApi } from '#/api';

const settings = ref({
  accountPasswordNotify: false,
  systemMessage: false,
  todoTask: false,
});

const formSchema = ref<any[]>([]);

function buildSchema() {
  formSchema.value = [
    {
      value: settings.value.accountPasswordNotify,
      fieldName: 'accountPassword',
      label: '계정 비밀번호',
      description: '다른 사용자의 메시지가 알림으로 통지됩니다.',
    },
    {
      value: settings.value.systemMessage,
      fieldName: 'systemMessage',
      label: '시스템 메시지',
      description: '시스템 메시지가 알림으로 통지됩니다.',
    },
    {
      value: settings.value.todoTask,
      fieldName: 'todoTask',
      label: '할 일',
      description: '할 일이 알림으로 통지됩니다.',
    },
  ];
}

async function loadSettings() {
  try {
    const userInfo: any = await getUserInfoApi();
    settings.value.accountPasswordNotify = !!userInfo.accountPasswordNotify;
    settings.value.systemMessage = !!userInfo.systemMessage;
    settings.value.todoTask = !!userInfo.todoTask;
    buildSchema();
  } catch (error) {
    console.error('Failed to load notification settings:', error);
  }
}

async function handleChange(e: any) {
  const { fieldName, value } = e || {};
  try {
    let detailType = fieldName.charAt(0).toUpperCase() + fieldName.slice(1);
    if (fieldName === 'accountPassword') {
      detailType = 'AccountPasswordNotify';
    }
    await updateSettingApi({
      fieldName: detailType,
      value,
    });
    message.success('알림 설정이 변경되었습니다.');
    await loadSettings();
  } catch (error) {
    message.error('알림 설정 변경에 실패했습니다.');
  }
}

onMounted(() => {
  loadSettings();
});
</script>
<template>
  <ProfileNotificationSetting :form-schema="formSchema" @change="handleChange" />
</template>
