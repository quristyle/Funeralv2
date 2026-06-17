<script lang="ts" setup>
import type { Recordable } from '@vben/types';

import { useRouter } from 'vue-router';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';
import { resetAllStores, useUserStore } from '@vben/stores';

import { Button, Card } from 'ant-design-vue';

import { useAuthStore } from '#/store';

const accounts: Record<string, Recordable<any>> = {
  admin: {
    password: '123456',
    username: 'admin',
  },
  super: {
    password: '123456',
    username: 'vben',
  },
  user: {
    password: '123456',
    username: 'jack',
  },
};

const { accessMode, toggleAccessMode } = useAccess();
const userStore = useUserStore();
const accessStore = useAuthStore();
const router = useRouter();

function roleButtonType(role: string) {
  return userStore.userRoles.includes(role) ? 'primary' : 'default';
}

async function changeAccount(role: string) {
  if (userStore.userRoles.includes(role)) {
    return;
  }

  const account = accounts[role];
  resetAllStores();
  if (account) {
    await accessStore.authLogin(account, async () => {
      router.go(0);
    });
  }
}

async function handleToggleAccessMode() {
  if (!accounts.super) {
    return;
  }
  await toggleAccessMode();
  resetAllStores();

  await accessStore.authLogin(accounts.super, async () => {
    setTimeout(() => {
      router.go(0);
    }, 150);
  });
}
</script>

<template>
  <Page
    :title="`${accessMode === 'frontend' ? '프론트엔드' : '백엔드'} 페이지 액세스 권한 데모`"
    description="다른 계정으로 전환하여 왼쪽 메뉴의 변화를 확인하세요."
  >
    <Card class="mb-5" title="권한 모드">
      <span class="font-semibold">현재 권한 모드:</span>
      <span class="mx-4 text-primary">{{
        accessMode === 'frontend' ? '프론트엔드 권한 제어' : '백엔드 권한 제어'
      }}</span>
      <Button type="primary" @click="handleToggleAccessMode">
        {{ accessMode === 'frontend' ? '백엔드' : '프론트엔드' }} 권한 모드로 전환
      </Button>
    </Card>
    <Card title="계정 전환">
      <Button :type="roleButtonType('super')" @click="changeAccount('super')">
        Super 계정으로 전환
      </Button>

      <Button
        :type="roleButtonType('admin')"
        class="mx-4"
        @click="changeAccount('admin')"
      >
        Admin 계정으로 전환
      </Button>
      <Button :type="roleButtonType('user')" @click="changeAccount('user')">
        User 계정으로 전환
      </Button>
    </Card>
  </Page>
</template>
