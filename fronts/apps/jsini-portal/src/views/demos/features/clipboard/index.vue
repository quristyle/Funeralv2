<script setup lang="ts">
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { useClipboard } from '@vueuse/core';
import { Button, Card, Input } from 'ant-design-vue';

const source = ref('Hello');
const { copy, text } = useClipboard({ legacy: true, source });
</script>

<template>
  <Page title="클립보드 예시">
    <Card title="기본 사용법">
      <p class="mb-3">
        현재 복사됨: <code>{{ text || '없음' }}</code>
      </p>
      <div class="flex">
        <Input v-model:value="source" class="mr-3 flex w-50" />
        <Button type="primary" @click="copy(source)"> 복사 </Button>
      </div>
    </Card>
  </Page>
</template>
