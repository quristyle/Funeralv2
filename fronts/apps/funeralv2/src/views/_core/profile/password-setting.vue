<script setup lang="ts">
import type { VbenFormSchema } from '#/adapter/form';

import { computed } from 'vue';

import { ProfilePasswordSetting, z } from '@vben/common-ui';

import { message } from 'ant-design-vue';

import { changePasswordApi } from '#/api';

const formSchema = computed((): VbenFormSchema[] => {
  return [
    {
      fieldName: 'oldPassword',
      label: '이전 비밀번호',
      component: 'VbenInputPassword',
      componentProps: {
        placeholder: '이전 비밀번호를 입력해주세요',
      },
    },
    {
      fieldName: 'newPassword',
      label: '새 비밀번호',
      component: 'VbenInputPassword',
      componentProps: {
        passwordStrength: true,
        placeholder: '새 비밀번호를 입력해주세요',
      },
    },
    {
      fieldName: 'confirmPassword',
      label: '비밀번호 확인',
      component: 'VbenInputPassword',
      componentProps: {
        passwordStrength: true,
        placeholder: '새 비밀번호를 다시 입력해주세요',
      },
      dependencies: {
        rules(values) {
          const { newPassword } = values;
          return z
            .string({ required_error: '새 비밀번호를 다시 입력해주세요' })
            .min(1, { message: '새 비밀번호를 다시 입력해주세요' })
            .refine((value) => value === newPassword, {
              message: '비밀번호가 일치하지 않습니다',
            });
        },
        triggerFields: ['newPassword'],
      },
    },
  ];
});

async function handleSubmit(values: any) {
  try {
    await changePasswordApi({
      oldPassword: values.oldPassword,
      newPassword: values.newPassword,
    });
    message.success('비밀번호가 성공적으로 변경되었습니다');
  } catch (error) {
    message.error('비밀번호 변경에 실패했습니다. 이전 비밀번호를 확인해주세요.');
  }
}
</script>
<template>
  <ProfilePasswordSetting
    class="w-1/3"
    :form-schema="formSchema"
    @submit="handleSubmit"
  />
</template>
