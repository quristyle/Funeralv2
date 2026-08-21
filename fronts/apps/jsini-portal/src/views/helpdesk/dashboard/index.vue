<script lang="ts" setup>
import { onMounted } from 'vue';

import { Spin } from 'ant-design-vue';

import { useHelpdeskStore } from '#/store/helpdesk';

import RequestMonitor from '../request/monitor.vue';
import CustomerDashboard from './customer.vue';

/**
 * [헬프데스크 현황]
 *
 * 원본(Dashboard.vue)과 동일하게 로그인 종류에 따라 다른 화면을 보여준다.
 * 담당자는 처리 현황(요청 모니터), 고객은 자기 회사 현황을 본다.
 */
const helpdesk = useHelpdeskStore();

onMounted(() => helpdesk.loadIdentity());
</script>

<template>
  <Spin :spinning="!helpdesk.identityChecked">
    <RequestMonitor v-if="helpdesk.isAdmin" />
    <CustomerDashboard v-else />
  </Spin>
</template>
