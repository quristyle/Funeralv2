<script lang="ts" setup>
import { computed } from 'vue';
import { useRoute } from 'vue-router';

import { Page } from '@vben/common-ui';
import { useTabs } from '@vben/hooks';

const route = useRoute();

const { setTabTitle } = useTabs();

const index = computed(() => {
  return route.params?.id ?? -1;
});

setTabTitle(`No.${index.value} - 상세 정보`);
</script>

<template>
  <Page :title="`탭 ${index} 상세 페이지`">
    <template #description> {{ index }} - 상세 페이지 내용이 여기에 표시됩니다 </template>
  </Page>
</template>
