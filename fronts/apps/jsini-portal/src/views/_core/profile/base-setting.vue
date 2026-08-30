<script setup lang="ts">
import type { BasicOption } from '@vben/types';

import type { VbenFormSchema } from '#/adapter/form';

import { computed, nextTick, onMounted, ref } from 'vue';

import { ProfileBaseSetting } from '@vben/common-ui';
import { message } from 'ant-design-vue';

import { getUserInfoApi, updateProfileApi } from '#/api';

const profileBaseSettingRef = ref();

/**
 * [역할 표시]
 *
 * 역할 칸에는 식별자(`ADMINISTRATOR`)가 아니라 이름(`관리자`)이 보여야 한다.
 *
 * 원래는 vben 템플릿이 준 목업 목록(`super` · `user` · `test`)을 옵션으로 두고 있었다.
 * 실제 배정값은 `ADMINISTRATOR` · `PARTNER` 같은 식별자라 어느 옵션과도 맞지 않았고,
 * antd Select 는 맞는 옵션이 없으면 **값을 그대로** 그린다 — 그래서 식별자가 보였다.
 *
 * `/auth/user/info` 가 식별자(`roles`)와 이름(`roleNames`)을 짝으로 내려주므로
 * 그것으로 옵션을 만든다. 역할 목록을 따로 부르지 않아도 되고,
 * 역할 이름을 바꾸면 이 화면에도 그대로 따라온다.
 */
const roleOptions = ref<BasicOption[]>([]);

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
      fieldName: 'birthDate',
      component: 'DatePicker',
      componentProps: {
        // 폼 값은 문자열로 다룬다 — 서버가 'yyyy-MM-dd' 를 받는다.
        valueFormat: 'YYYY-MM-DD',
        placeholder: '생년월일을 선택하세요',
        class: 'w-full',
      },
      label: '생년월일',
    },
    {
      fieldName: 'birthDateIsLunar',
      component: 'Checkbox',
      renderComponentContent: () => ({
        default: () => '음력 생일입니다',
      }),
      label: ' ',
    },
    {
      fieldName: 'roles',
      component: 'Select',
      componentProps: {
        // 여러 개일 수 있다(회사 · 부서 · 사람 단계에서 걸린 역할을 모두 합친 값이다).
        // 고쳐 쓰는 칸이 아니라 보여 주는 칸이라 잠가 둔다 —
        // 역할 배정은 권한 관리 화면에서 한다.
        mode: 'multiple',
        options: roleOptions.value,
        disabled: true,
        placeholder: '배정된 역할이 없습니다.',
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

    // 역할 옵션을 받은 값으로 만든다. 이름이 없으면 식별자를 그대로 쓴다 —
    // 이름이 비어 있어도 칸이 텅 비어 보이지 않게 한다.
    const roleIds = (data as any)?.roles ?? [];
    const roleNames = (data as any)?.roleNames ?? [];
    roleOptions.value = roleIds.map((id: string, index: number) => ({
      label: roleNames[index] || id,
      value: id,
    }));

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
      // null 은 '건드리지 않음' 이라 빈 값은 '' 로 보내 지운다.
      birthDate: values.birthDate ?? '',
      birthDateIsLunar: !!values.birthDateIsLunar,
    });
    message.success('프로필 정보가 성공적으로 수정되었습니다.');
    await loadData();
  } catch (error: any) {
    // 중복 이메일·전화번호는 서버가 이유를 담아 준다(409). 그대로 보여 준다.
    message.error(error?.message ?? '프로필 정보 수정에 실패했습니다.');
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
