<script setup lang="ts">
import { computed } from 'vue';

import { Page, SliderRotateCaptcha } from '@vben/common-ui';
import { preferences } from '@vben/preferences';
import { useUserStore } from '@vben/stores';

import { Card, message } from 'ant-design-vue';

const userStore = useUserStore();
function handleSuccess() {
  message.success('success!');
}

const avatar = computed(() => {
  return userStore.userInfo?.avatar || preferences.app.defaultAvatar;
});
</script>

<template>
  <Page description="프론트엔드의 간단한 드래그 검증 시나리오에 사용됩니다." title="슬라이더 회전 검증">
    <Card class="mb-5" title="기본 예제">
      <div class="flex-center p-4">
        <SliderRotateCaptcha :src="avatar" @success="handleSuccess" />
      </div>
    </Card>
  </Page>
</template>
