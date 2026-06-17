<script setup lang="ts">
import { Page, SliderTranslateCaptcha } from '@vben/common-ui';

import { Card, message } from 'ant-design-vue';

function handleSuccess() {
  message.success('success!');
}
</script>

<template>
  <Page
    description="프론트엔드의 간단한 퍼즐 슬라이더 수평 드래그 검증 시나리오에 사용됩니다."
    title="퍼즐 슬라이더 검증"
  >
    <Card class="mb-5" title="기본 예제">
      <div class="flex-center p-4">
        <SliderTranslateCaptcha
          src="https://unpkg.com/@vbenjs/static-source@0.1.7/source/pro-avatar.webp"
          :canvas-width="420"
          :canvas-height="420"
          @success="handleSuccess"
        />
      </div>
    </Card>
  </Page>
</template>
