<script lang="ts" setup>
import { useRouter } from 'vue-router';

import { Fallback } from '@vben/common-ui';

import { Button } from 'ant-design-vue';

const router = useRouter();
</script>

<template>
  <Fallback
    description="브레드크럼 내비게이션 - 평행 모드 - 상세 페이지"
    status="coming-soon"
    title="브레드크럼 내비게이션의 변화를 확인하세요."
  >
    <template #action>
      <Button @click="router.go(-1)">뒤로 가기</Button>
    </template>
  </Fallback>
</template>
