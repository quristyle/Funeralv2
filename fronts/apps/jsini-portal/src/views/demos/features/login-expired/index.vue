<script lang="ts" setup>
import type { LoginExpiredModeType } from '@vben/types';

import { Page } from '@vben/common-ui';
import { preferences, updatePreferences } from '@vben/preferences';

import { Button, Card } from 'ant-design-vue';

import { getMockStatusApi } from '#/api';

async function handleClick(type: LoginExpiredModeType) {
  const loginExpiredMode = preferences.app.loginExpiredMode;

  updatePreferences({ app: { loginExpiredMode: type } });
  await getMockStatusApi('401');
  updatePreferences({ app: { loginExpiredMode } });
}
</script>

<template>
  <Page title="로그인 만료 데모">
    <template #description>
      <div class="mt-2 text-foreground/80">
        API 요청 시 401 상태 코드를 만나면 다시 로그인해야 합니다. 두 가지 방식이 있습니다:
        <p>1. 로그인 페이지로 이동하여 로그인 성공 후 원래 페이지로 돌아가기</p>
        <p>
          2. 다시 로그인 팝업을 띄워 로그인 후 팝업을 닫고 페이지 이동을 하지 않음 (새로고침 후에는 여전히 로그인 페이지로 이동함)
        </p>
      </div>
    </template>

    <Card class="mb-5" title="로그인 페이지 이동 방식">
      <Button type="primary" @click="handleClick('page')"> 클릭하여 실행 </Button>
    </Card>
    <Card class="mb-5" title="로그인 팝업 방식">
      <Button type="primary" @click="handleClick('modal')"> 클릭하여 실행 </Button>
    </Card>
  </Page>
</template>
