<script lang="ts" setup>
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { useFullscreen } from '@vueuse/core';
import { Button, Card } from 'ant-design-vue';

const domRef = ref<HTMLElement>();

const { enter, exit, isFullscreen, toggle } = useFullscreen();

const { isFullscreen: isDomFullscreen, toggle: toggleDom } =
  useFullscreen(domRef);
</script>

<template>
  <Page title="전체 화면 예시">
    <Card title="윈도우 전체 화면">
      <div class="flex flex-wrap items-center gap-4">
        <Button :disabled="isFullscreen" type="primary" @click="enter">
          윈도우 전체 화면 시작
        </Button>
        <Button @click="toggle"> 윈도우 전체 화면 토글 </Button>

        <Button :disabled="!isFullscreen" danger @click="exit">
          윈도우 전체 화면 종료
        </Button>

        <span class="text-nowrap"> 현재 상태: {{ isFullscreen }} </span>
      </div>
    </Card>

    <Card class="mt-5" title="DOM 전체 화면">
      <Button type="primary" @click="toggleDom"> DOM 전체 화면 시작 </Button>
    </Card>

    <div
      ref="domRef"
      class="mx-auto mt-10 flex-center h-64 w-1/2 rounded-md bg-yellow-400"
    >
      <Button class="mr-2" type="primary" @click="toggleDom">
        {{ isDomFullscreen ? 'DOM 전체 화면 종료' : 'DOM 전체 화면 시작' }}
      </Button>
    </div>
  </Page>
</template>
