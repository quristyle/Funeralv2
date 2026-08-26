<script setup lang="ts">
import { RouterView } from 'vue-router';

import SiteFooter from '#/components/site-footer.vue';
import SiteHeader from '#/components/site-header.vue';
</script>

<template>
  <div class="flex min-h-screen flex-col">
    <SiteHeader />
    <!--
      본문은 세로로 길어진다. 준수사항 4(세로 스크롤 없이 한 화면)는
      **업무 화면의 규칙이라 이 앱은 대상이 아니다** — 소개 사이트는 스크롤이 서술 수단이다.
      `docs/준수사항.md` 규칙 4 의 예외 절에 적어 두었다.
    -->
    <main class="flex-1">
      <RouterView />
    </main>
    <SiteFooter />
  </div>
</template>
