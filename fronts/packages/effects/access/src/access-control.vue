<!--
 세밀한 액세스 제어를 위한 액세스 제어 컴포넌트입니다.
 TODO: 더 완벽한 기능을 확장할 수 있습니다:
 1. 여러 권한 코드를 지원하며, 하나만 만족하거나 모두 만족하는 경우를 설정 가능
 2. 여러 역할을 지원하며, 하나만 만족하거나 모두 만족하는 경우를 설정 가능
 3. 권한 코드 및 역할에 대한 사용자 정의 판단 로직 지원
-->
<script lang="ts" setup>
import { computed } from 'vue';

import { useAccess } from './use-access';

interface Props {
  /**
   * 지정된 코드가 표시되는지 여부
   * @default []
   */
  codes?: string[];

  /**
   * 컴포넌트를 제어하는 방식. 'role'인 경우 역할을 전달하고, 'code'인 경우 권한 코드를 전달합니다.
   * @default 'role'
   */
  type?: 'code' | 'role';
}

defineOptions({
  name: 'AccessControl',
});

const props = withDefaults(defineProps<Props>(), {
  codes: () => [],
  type: 'role',
});

const { hasAccessByCodes, hasAccessByRoles } = useAccess();

const hasAuth = computed(() => {
  const { codes, type } = props;
  return type === 'role' ? hasAccessByRoles(codes) : hasAccessByCodes(codes);
});
</script>

<template>
  <slot v-if="!codes"></slot>
  <slot v-else-if="hasAuth"></slot>
</template>
