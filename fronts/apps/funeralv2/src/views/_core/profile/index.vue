<script setup lang="ts">
import { ref } from 'vue';

import { Profile } from '@vben/common-ui';
import { useUserStore } from '@vben/stores';

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
</script>
<template>
  <Profile
    v-model:model-value="tabsValue"
    title="개인 센터"
    :user-info="userStore.userInfo"
    :tabs="tabs"
  >
    <template #content>
      <ProfileBase v-if="tabsValue === 'basic'" />
      <ProfileSecuritySetting v-if="tabsValue === 'security'" />
      <ProfilePasswordSetting v-if="tabsValue === 'password'" />
      <ProfileNotificationSetting v-if="tabsValue === 'notice'" />
    </template>
  </Profile>
</template>
