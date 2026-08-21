<script lang="ts" setup>
/**
 * [내 일정]
 *
 * 원본(JinReception MyScheduleView.vue, `/my-schedules`).
 * 전체 일정 화면과 달력·편집 동작이 같고, 보이는 범위만 내 회사(+공통)로 좁힌 것이라
 * 같은 컴포넌트를 `my-only` 로 재사용한다.
 */
import ScheduleCalendar from './index.vue';
</script>

<template>
  <ScheduleCalendar my-only />
</template>
