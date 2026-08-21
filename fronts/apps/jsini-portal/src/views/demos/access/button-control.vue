<script lang="ts" setup>
import type { Recordable } from '@vben/types';

import { useRouter } from 'vue-router';

import { AccessControl, useAccess } from '@vben/access';
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

const { accessMode, hasAccessByCodes } = useAccess();
const authStore = useAuthStore();
const userStore = useUserStore();
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
    await authStore.authLogin(account, async () => {
      router.go(0);
    });
  }
}
</script>

<template>
  <Page
    :title="`${accessMode === 'frontend' ? '프론트엔드' : '백엔드'} 버튼 접근 권한 데모`"
    description="다른 계정으로 전환하여 버튼의 변화를 확인하세요."
  >
    <Card class="mb-5">
      <template #title>
        <span class="font-semibold">현재 역할:</span>
        <span class="mx-4 text-lg text-primary">
          {{ userStore.userRoles?.[0] }}
        </span>
      </template>

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

    <Card class="mb-5" title="컴포넌트 방식 제어 - 권한 코드">
      <AccessControl :codes="['AC_100100']" type="code">
        <Button class="mr-4"> Super 계정 표시 ["AC_100100"] </Button>
      </AccessControl>
      <AccessControl :codes="['AC_100030']" type="code">
        <Button class="mr-4"> Admin 계정 표시 ["AC_100030"] </Button>
      </AccessControl>
      <AccessControl :codes="['AC_1000001']" type="code">
        <Button class="mr-4"> User 계정 표시 ["AC_1000001"] </Button>
      </AccessControl>
      <AccessControl :codes="['AC_100100', 'AC_100030']" type="code">
        <Button class="mr-4">
          Super & Admin 계정 표시 ["AC_100100","AC_100030"]
        </Button>
      </AccessControl>
    </Card>

    <Card
      v-if="accessMode === 'frontend'"
      class="mb-5"
      title="컴포넌트 방식 제어 - 역할"
    >
      <AccessControl :codes="['super']" type="role">
        <Button class="mr-4"> Super 역할 표시 </Button>
      </AccessControl>
      <AccessControl :codes="['admin']" type="role">
        <Button class="mr-4"> Admin 역할 표시 </Button>
      </AccessControl>
      <AccessControl :codes="['user']" type="role">
        <Button class="mr-4"> User 역할 표시 </Button>
      </AccessControl>
      <AccessControl :codes="['super', 'admin']" type="role">
        <Button class="mr-4"> Super & Admin 역할 표시 </Button>
      </AccessControl>
    </Card>

    <Card class="mb-5" title="함수 방식 제어">
      <Button v-if="hasAccessByCodes(['AC_100100'])" class="mr-4">
        Super 계정 표시 ["AC_100100"]
      </Button>
      <Button v-if="hasAccessByCodes(['AC_100030'])" class="mr-4">
        Admin 계정 표시 ["AC_100030"]
      </Button>
      <Button v-if="hasAccessByCodes(['AC_1000001'])" class="mr-4">
        User 계정 표시 ["AC_1000001"]
      </Button>
      <Button v-if="hasAccessByCodes(['AC_100100', 'AC_100030'])" class="mr-4">
        Super & Admin 계정 표시 ["AC_100100","AC_100030"]
      </Button>
    </Card>

    <Card class="mb-5" title="디렉티브 방식 - 권한 코드">
      <Button class="mr-4" v-access:code="['AC_100100']">
        Super 계정 표시 ["AC_100100"]
      </Button>
      <Button class="mr-4" v-access:code="['AC_100030']">
        Admin 계정 표시 ["AC_100030"]
      </Button>
      <Button class="mr-4" v-access:code="['AC_1000001']">
        User 계정 표시 ["AC_1000001"]
      </Button>
      <Button class="mr-4" v-access:code="['AC_100100', 'AC_100030']">
        Super & Admin 계정 표시 ["AC_100100","AC_100030"]
      </Button>
    </Card>

    <Card v-if="accessMode === 'frontend'" class="mb-5" title="디렉티브 방식 - 역할">
      <Button class="mr-4" v-access:role="['super']"> Super 역할 표시 </Button>
      <Button class="mr-4" v-access:role="['admin']"> Admin 역할 표시 </Button>
      <Button class="mr-4" v-access:role="['user']"> User 역할 표시 </Button>
      <Button class="mr-4" v-access:role="['super', 'admin']">
        Super & Admin 역할 표시
      </Button>
    </Card>
  </Page>
</template>
