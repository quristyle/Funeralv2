<script lang="ts" setup>
import { VBEN_DOC_URL } from '@vben/constants';
import { openWindow } from '@vben/utils';

import { Button } from 'ant-design-vue';

const props = defineProps<{ path: string }>();

function handleClick() {
  // .html이 없으면 페이지를 열 때 404 에러가 발생할 수 있습니다.
  const path =
    VBEN_DOC_URL +
    (props.path.toLowerCase().endsWith('.html')
      ? props.path
      : `${props.path}.html`);
  openWindow(path);
}
</script>

<template>
  <Button @click="handleClick">컴포넌트 문서 보기</Button>
</template>
