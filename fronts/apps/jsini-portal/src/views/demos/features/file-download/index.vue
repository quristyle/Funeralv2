<script setup lang="ts">
import { ref } from 'vue';

import { Page } from '@vben/common-ui';
import {
  downloadFileFromBase64,
  downloadFileFromBlobPart,
  downloadFileFromImageUrl,
  downloadFileFromUrl,
} from '@vben/utils';

import { Button, Card } from 'ant-design-vue';

import { downloadFile1, downloadFile2 } from '#/api/examples/download';

import imageBase64 from './base64';

const downloadResult = ref('');

function getBlob() {
  downloadFile1().then((res) => {
    downloadResult.value = `Blob 획득 성공, 길이: ${res.size}`;
  });
}

function getResponse() {
  downloadFile2().then((res) => {
    downloadResult.value = `Response 획득 성공, headers: ${JSON.stringify(res.headers)}, 길이: ${res.data.size}`;
  });
}
</script>

<template>
  <Page title="파일 다운로드 예제">
    <Card title="파일 주소로 파일 다운로드">
      <Button
        type="primary"
        @click="
          downloadFileFromUrl({
            source:
              'https://codeload.github.com/vbenjs/vue-vben-admin-doc/zip/main',
            target: '_self',
          })
        "
      >
        Download File
      </Button>
    </Card>

    <Card class="my-5" title="주소로 이미지 다운로드">
      <Button
        type="primary"
        @click="
          downloadFileFromImageUrl({
            source:
              'https://unpkg.com/@vbenjs/static-source@0.1.7/source/logo-v1.webp',
            fileName: 'vben-logo.png',
          })
        "
      >
        Download File
      </Button>
    </Card>

    <Card class="my-5" title="base64 스트림 다운로드">
      <Button
        type="primary"
        @click="
          downloadFileFromBase64({
            source: imageBase64,
            fileName: 'image.png',
          })
        "
      >
        Download Image
      </Button>
    </Card>
    <Card class="my-5" title="텍스트 다운로드">
      <Button
        type="primary"
        @click="
          downloadFileFromBlobPart({
            source: 'text content',
            fileName: 'test.txt',
          })
        "
      >
        Download TxT
      </Button>
    </Card>

    <Card class="my-5" title="요청 다운로드">
      <Button type="primary" @click="getBlob"> Blob 획득 </Button>
      <Button type="primary" class="ml-4" @click="getResponse">
        Response 획득
      </Button>
      <div class="mt-4">{{ downloadResult }}</div>
    </Card>
  </Page>
</template>
