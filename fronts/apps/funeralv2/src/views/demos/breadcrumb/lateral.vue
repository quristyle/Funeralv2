<script lang="ts" setup>
import { useRouter } from 'vue-router';

import { Fallback } from '@vben/common-ui';

import { Button } from 'ant-design-vue';

const router = useRouter();

function details() {
  router.push({ name: 'BreadcrumbLateralDetailDemo' });
}
</script>

<template>
  <Fallback
    description="상세 정보를 보려면 클릭하고 브레드크럼 내비게이션의 변화를 확인하세요."
    status="coming-soon"
    title="브레드크럼 내비게이션 - 평행 모드"
  >
    <template #action>
      <Button type="primary" @click="details">상세 정보 보기</Button>
    </template>
  </Fallback>
</template>
