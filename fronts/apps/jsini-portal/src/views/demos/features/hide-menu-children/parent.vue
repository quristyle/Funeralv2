<script lang="ts" setup>
import { Fallback } from '@vben/common-ui';
</script>

<template>
  <Fallback
    :description="`현재 라우트: ${String($route.name)}, 하위 메뉴가 표시되지 않습니다.`"
    status="coming-soon"
    title="하위 메뉴 숨기기"
  >
    <template #action>
      <RouterLink to="/demos/features/hide-menu-children/children">
        하위 라우트 열기
      </RouterLink>
    </template>
  </Fallback>
</template>
