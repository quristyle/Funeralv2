<script setup lang="ts">
import type { BasicOption } from '@vben/types';

import type { VbenFormSchema } from '#/adapter/form';

import { computed, nextTick, onMounted, ref } from 'vue';

import { ProfileBaseSetting } from '@vben/common-ui';
import { message } from 'ant-design-vue';

import { getUserInfoApi, updateProfileApi } from '#/api';

const profileBaseSettingRef = ref();

const MOCK_ROLES_OPTIONS: BasicOption[] = [
  {
    label: '관리자',
    value: 'super',
  },
  {
    label: '사용자',
    value: 'user',
  },
  {
    label: '테스트',
    value: 'test',
  },
];

const formSchema = computed((): VbenFormSchema[] => {
  return [
    {
      fieldName: 'realName',
      component: 'Input',
      label: '이름',
    },
    {
      fieldName: 'username',
      component: 'Input',
      componentProps: {
        disabled: true,
      },
      label: '사용자명',
    },
    {
      fieldName: 'email',
      component: 'Input',
      label: '이메일',
    },
    {
      fieldName: 'phone',
      component: 'Input',
      label: '전화번호',
    },
    {
      fieldName: 'roles',
      component: 'Select',
      componentProps: {
        mode: 'tags',
        options: MOCK_ROLES_OPTIONS,
        disabled: true,
      },
      label: '역할',
    },
    {
      fieldName: 'introduction',
      component: 'Textarea',
      label: '자기소개',
    },
  ];
});

async function loadData() {
  try {
    const data = await getUserInfoApi();
    let formApi = profileBaseSettingRef.value?.getFormApi();
    if (!formApi) {
      await nextTick();
      formApi = profileBaseSettingRef.value?.getFormApi();
    }
    if (formApi) {
      formApi.setValues(data);
    } else {
      console.warn('[ProfileBaseSetting] Form API is not initialized yet.');
    }
  } catch (error) {
    console.error('Failed to load user info:', error);
  }
}

async function handleSubmit(values: any) {
  try {
    await updateProfileApi({
      realName: values.realName,
      introduction: values.introduction,
      email: values.email,
      phone: values.phone,
    });
    message.success('프로필 정보가 성공적으로 수정되었습니다.');
    await loadData();
  } catch (error) {
    message.error('프로필 정보 수정에 실패했습니다.');
  }
}

onMounted(() => {
  loadData();
});
</script>
<template>
  <ProfileBaseSetting 
    ref="profileBaseSettingRef" 
    :form-schema="formSchema" 
    @submit="handleSubmit"
  />
</template>
