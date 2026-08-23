<script setup lang="ts">
/**
 * [장례 프레임 서버 모니터]
 *
 * 원본: ProjMngWasm `Pages/Funeralfr.razor` (`/funeral`).
 * 원본은 `http://funeralfr.jsini.co.kr:61208` (Glances) 를 iframe 으로 띄웠다.
 *
 * 이름을 "장례 프레임 서버 모니터" 로 둔 이유가 있다. 포털에는 이미
 * 장례식장 MSA(`/funerals`) 와 서버 상태(`/system/server-status`) 화면이 있어
 * 그냥 "장례식장" 이나 "서버 상태" 로 두면 어느 것인지 알 수 없다.
 * 이 화면이 보는 것은 **구 장례 프레임 시스템이 올라간 장비**다.
 */
import { Page } from '@vben/common-ui';

import { Alert, Button } from 'ant-design-vue';

const TARGET = 'http://funeralfr.jsini.co.kr:61208';

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
      title="장례 프레임 서버 모니터"
    ></iframe>
  </Page>
</template>
