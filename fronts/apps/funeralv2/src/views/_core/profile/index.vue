<script setup lang="ts">
import { ref } from 'vue';

import { Profile } from '@vben/common-ui';
import { useUserStore } from '@vben/stores';
import { updateProfileApi } from '#/api';

import ProfileBase from './base-setting.vue';
import ProfileNotificationSetting from './notification-setting.vue';
import ProfilePasswordSetting from './password-setting.vue';
import ProfileSecuritySetting from './security-setting.vue';

const userStore = useUserStore();

const tabsValue = ref<string>('basic');

const tabs = ref([
  {
    label: '기본 설정',
    value: 'basic',
  },
  {
    label: '보안 설정',
    value: 'security',
  },
  {
    label: '비밀번호 변경',
    value: 'password',
  },
  {
    label: '새 메시지 알림',
    value: 'notice',
  },
]);

const handleAvatarChange = async (avatarUrl: string) => {
  try {
    // 1. 백엔드 API 호출하여 DB에 아바타 저장
    await updateProfileApi({
      avatar: avatarUrl
    });
    
    // 2. 전역 스토어 상태 변경하여 화면 즉시 갱신
    if (userStore.userInfo) {
      userStore.userInfo.avatar = avatarUrl;
    }
  } catch (err: any) {
    console.error('아바타 변경 실패:', err);
  }
};
</script>
<template>
  <Profile
    v-model:model-value="tabsValue"
    title="개인 센터"
    :user-info="userStore.userInfo"
    :tabs="tabs"
    @change-avatar="handleAvatarChange"
  >
    <template #content>
      <ProfileBase v-if="tabsValue === 'basic'" />
      <ProfileSecuritySetting v-if="tabsValue === 'security'" />
      <ProfilePasswordSetting v-if="tabsValue === 'password'" />
      <ProfileNotificationSetting v-if="tabsValue === 'notice'" />
    </template>
  </Profile>
</template>
