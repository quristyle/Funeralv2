<script lang="ts" setup>
import { Fallback, VbenButton } from '@vben/common-ui';
import { useTabs } from '@vben/hooks';
import { X } from '@vben/icons';

const { closeCurrentTab } = useTabs();
</script>

<template>
  <Fallback
    description="현재 라우트가 메뉴에 표시되지 않습니다."
    status="coming-soon"
    title="숨겨진 하위 메뉴"
    show-back
  >
    <template #action>
      <VbenButton size="lg" @click="closeCurrentTab()">
        <X class="mr-2 size-4" />
        현재 탭 닫기
      </VbenButton>
    </template>
  </Fallback>
</template>
