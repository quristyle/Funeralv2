<script lang="ts" setup>
/**
 * [WBS 열람]
 *
 * 원본(WbsReadonly.vue)은 간트를 readonly 로 띄운 것이 전부였다.
 * 같은 화면을 읽기 전용으로 재사용한다.
 */
import WbsGantt from './wbs-gantt.vue';
</script>

<template>
  <WbsGantt readonly />
</template>
