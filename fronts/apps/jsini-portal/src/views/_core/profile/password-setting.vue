<script setup lang="ts">
import type { VbenFormSchema } from '#/adapter/form';

import { computed, onMounted, ref } from 'vue';

import { ProfilePasswordSetting, z } from '@vben/common-ui';

import { Alert, message } from 'ant-design-vue';

import { changePasswordApi, getUserInfoApi } from '#/api';
import { useAuthStore } from '#/store';

const authStore = useAuthStore();

/** 90일 만료 정책 상태. 만료되어 강제로 끌려온 경우 안내가 필요하다. */
const expiryDays = ref<null | number>(null);
const daysRemaining = ref<null | number>(null);
const expired = ref(false);

const noticeType = computed(() => {
  if (expired.value) return 'error';
  return daysRemaining.value !== null && daysRemaining.value <= 7
    ? 'warning'
    : 'info';
});

const noticeMessage = computed(() => {
  if (expired.value) {
    return `비밀번호를 바꾼 지 ${expiryDays.value}일이 지났습니다. 지금 변경해야 다른 기능을 이용할 수 있습니다.`;
  }
  if (daysRemaining.value !== null && daysRemaining.value <= 7) {
    return `비밀번호 사용 기간이 ${daysRemaining.value}일 남았습니다.`;
  }
  return null;
});

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
      // 지금 쓰는 비밀번호와 같은 값은 서버가 거부한다.
      // 90일마다 바꾸라고 하면서 같은 값을 허용하면 정책이 아무 일도 하지 않기 때문이다.
      // 서버까지 가기 전에 여기서 먼저 걸러 준다.
      dependencies: {
        rules(values) {
          const { oldPassword } = values;
          return z
            .string({ error: '새 비밀번호를 입력해주세요' })
            .min(1, { message: '새 비밀번호를 입력해주세요' })
            .refine((value) => !oldPassword || value !== oldPassword, {
              message: '지금 쓰는 비밀번호와 다른 값으로 입력해주세요',
            });
        },
        triggerFields: ['oldPassword'],
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
            .string({ error: '새 비밀번호를 다시 입력해주세요' })
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
  } catch {
    // 실패 이유는 서버가 구분해서 내려주고(이전 비밀번호 불일치 · 같은 값 등)
    // 요청 클라이언트의 공통 오류 처리가 그 메시지를 그대로 띄운다.
    return;
  }

  // ── 바꾼 뒤에는 반드시 다시 로그인시킨다 ────────────────
  //
  // 만료 판정은 토큰의 `PwdChangedAt` 클레임으로 하고, 토큰 수명은 7일이다.
  // 지금 들고 있는 토큰에는 **바꾸기 전** 시각이 들어 있으므로,
  // 그대로 두면 두 가지가 어긋난다.
  //   1. 만료되어 들어온 사람은 비밀번호를 바꿨는데도 계속 막힌다.
  //   2. 미리 바꿔 둔 사람도 옛 시각 기준으로 며칠 뒤 갑자기 막힌다.
  // 새 토큰을 받는 가장 확실한 길이 재로그인이다.
  message.success('비밀번호를 변경했습니다. 다시 로그인해주세요.');
  await authStore.logout(false);
}

onMounted(async () => {
  try {
    const info: any = await getUserInfoApi();
    expiryDays.value = info?.passwordExpiryDays ?? null;
    daysRemaining.value = info?.passwordDaysRemaining ?? null;
    expired.value = !!info?.passwordExpired;
  } catch {
    // 안내 문구를 못 띄우는 것뿐이므로 화면은 그대로 쓴다.
  }
});
</script>
<template>
  <div class="flex flex-col gap-4">
    <Alert
      v-if="noticeMessage"
      :type="noticeType"
      show-icon
      :message="noticeMessage"
    />
    <ProfilePasswordSetting
      class="w-1/3"
      :form-schema="formSchema"
      @submit="handleSubmit"
    />
  </div>
</template>
