<script setup lang="ts">
import { ref, computed } from 'vue';
import { useRoute } from 'vue-router';

import { Profile, ImageGroupManager } from '@vben/common-ui';
import { useUserStore } from '@vben/stores';
import { updateProfileApi } from '#/api';

import ProfileAccountInfo from './account-info.vue';
import ProfileBase from './base-setting.vue';
import ProfileNotificationSetting from './notification-setting.vue';
import ProfilePasswordSetting from './password-setting.vue';
import ProfileSecuritySetting from './security-setting.vue';

const userStore = useUserStore();
const route = useRoute();

// 비밀번호가 만료되어 강제로 끌려온 경우에는 곧바로 변경 탭을 띄운다.
// 라우터 가드와 로그인 흐름이 `?tab=password` 를 붙여 보낸다.
const tabsValue = ref<string>(
  route.query.tab === 'password' ? 'password' : 'basic',
);

const avatarGroupId = computed<string | null>({
  get: () => (userStore.userInfo as any)?.avatarGroupId || null,
  set: (val) => {
    if (userStore.userInfo) {
      (userStore.userInfo as any).avatarGroupId = val;
    }
  }
});

const tabs = ref([
  {
    label: '기본 설정',
    value: 'basic',
  },
  {
    label: '계정 정보',
    value: 'account',
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
  {
    label: '프로필 사진 관리',
    value: 'avatar',
  },
]);

const handleGroupIdChange = async (newGroupId: string) => {
  console.log('[Profile Index Debug] handleGroupIdChange triggered. newGroupId:', newGroupId);
  avatarGroupId.value = newGroupId;
  try {
    console.log('[Profile Index Debug] Calling updateProfileApi with:', {
      avatarGroupId: newGroupId,
      avatar: userStore.userInfo?.avatar || undefined
    });
    const res = await updateProfileApi({
      avatarGroupId: newGroupId,
      avatar: userStore.userInfo?.avatar || undefined
    });
    console.log('[Profile Index Debug] updateProfileApi response:', res);
  } catch (err: any) {
    console.error('[Profile Index Debug] 아바타 그룹 ID 저장 실패:', err);
  }
};

const handleAvatarChange = async (avatarUrl: string) => {
  try {
    // 1. 백엔드 API 호출하여 DB에 아바타 저장
    await updateProfileApi({
      avatar: avatarUrl,
      avatarGroupId: avatarGroupId.value || undefined
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
      <ProfileAccountInfo v-if="tabsValue === 'account'" />
      <ProfileSecuritySetting v-if="tabsValue === 'security'" />
      <ProfilePasswordSetting v-if="tabsValue === 'password'" />
      <ProfileNotificationSetting v-if="tabsValue === 'notice'" />
      <ImageGroupManager
        v-if="tabsValue === 'avatar'"
        v-model="avatarGroupId"
        :limit="30"
        biz-type="PROFILE"
        @update:modelValue="handleGroupIdChange"
        @change-representative="handleAvatarChange"
      />
    </template>
  </Profile>
</template>
