<script setup lang="ts">
/**
 * [JSini 서버 모니터]
 *
 * 원본: ProjMngWasm `Pages/Jsini.razor` (`/jsini`).
 * 원본은 `http://jsini.co.kr:61208` (Glances) 를 iframe 으로 띄우기만 했다.
 *
 * 그대로 옮기면 문제가 하나 생긴다. 포털은 https 로 서비스되므로
 * **http 페이지는 브라우저가 혼합 콘텐츠로 막는다.** iframe 이 빈 칸으로 뜬다.
 * 그래서 iframe 은 그대로 두되, 막혔을 때 새 창으로 열 수 있는 길을 함께 둔다.
 */
import { Page } from '@vben/common-ui';

import { Alert, Button } from 'ant-design-vue';

const TARGET = 'http://jsini.co.kr:61208';

function openTarget() {
  window.open(TARGET, '_blank', 'noopener');
}
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <Alert class="mb-2" show-icon type="info">
      <template #message>
        대상이 http 라 포털(https) 안에서는 브라우저가 표시를 막을 수 있습니다.
        빈 화면으로 보이면 새 창으로 여세요.
      </template>
      <template #action>
        <Button size="small" @click="openTarget">
          새 창으로 열기
        </Button>
      </template>
    </Alert>

    <iframe
      :src="TARGET"
      class="border-border h-full w-full rounded-md border"
      title="JSini 서버 모니터"
    ></iframe>
  </Page>
</template>
