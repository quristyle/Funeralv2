<script lang="ts" setup>
import { onMounted } from 'vue';

import { Alert } from 'ant-design-vue';

import { useHelpdeskStore } from '#/store/helpdesk';

/**
 * 헬프데스크 계정이 연결되지 않았을 때 띄우는 안내.
 *
 * funeralv2 로 계정을 단일화했지만, 기존 헬프데스크 데이터(요청 작성자·담당자)는
 * 헬프데스크 내부 계정 ID 를 참조한다. 두 계정이 이어져 있지 않으면 "누구로서" 조회할지
 * 정할 수 없어 화면이 빈 채로 뜬다. 그 상황을 사용자가 알 수 있게 알려준다.
 */
const helpdesk = useHelpdeskStore();

onMounted(() => helpdesk.loadIdentity());
</script>

<template>
  <Alert
    v-if="helpdesk.identityChecked && !helpdesk.helpdeskUserId"
    class="mb-3"
    message="이 계정에 연결된 헬프데스크 사용자가 없습니다."
    description="헬프데스크 설정 › 계정 연결 화면에서 현재 로그인 계정을 헬프데스크 담당자 또는 고객 계정과 연결해야 요청 데이터를 볼 수 있습니다."
    show-icon
    type="warning"
  />
</template>
