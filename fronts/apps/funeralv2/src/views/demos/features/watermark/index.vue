<script lang="ts" setup>
import { Page } from '@vben/common-ui';
import { useWatermark } from '@vben/hooks';

import { Button, Card } from 'ant-design-vue';

const { destroyWatermark, updateWatermark, watermark } = useWatermark();

async function recreateWaterMark() {
  destroyWatermark();
  await createWaterMark();
}

async function createWaterMark() {
  await updateWatermark({
    advancedStyle: {
      colorStops: [
        {
          color: 'red',
          offset: 0,
        },
        {
          color: 'blue',
          offset: 1,
        },
      ],
      type: 'linear',
    },
    content: `hello my watermark\n${new Date().toLocaleString()}`,
    globalAlpha: 0.5,
    gridLayoutOptions: {
      cols: 2,
      gap: [20, 20],
      matrix: [
        [1, 0],
        [0, 1],
      ],
      rows: 2,
    },
    height: 200,
    layout: 'grid',
    rotate: 22,
    width: 200,
  });
}
</script>

<template>
  <Page title="워터마크">
    <template #description>
      <div class="mt-2 text-foreground/80">
        워터마크는
        <a
          class="text-primary"
          href="https://zhensherlock.github.io/watermark-js-plus/"
          target="_blank"
        >
          watermark-js-plus
        </a>
        오픈 소스 플러그인을 사용하며, 자세한 설정은 플러그인 설정을 확인하세요.
      </div>
    </template>

    <Card title="사용법">
      <Button
        :disabled="!!watermark"
        class="mr-2"
        type="primary"
        @click="recreateWaterMark"
      >
        워터마크 생성
      </Button>
      <Button
        :disabled="!watermark"
        class="mr-2"
        type="primary"
        @click="createWaterMark"
      >
        워터마크 업데이트
      </Button>
      <Button :disabled="!watermark" danger @click="destroyWatermark">
        워터마크 제거
      </Button>
    </Card>
  </Page>
</template>
