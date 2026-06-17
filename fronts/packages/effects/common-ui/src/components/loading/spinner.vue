<script lang="ts" setup>
import { VbenSpinner } from '@vben-core/shadcn-ui';
import { cn } from '@vben-core/shared/utils';

interface SpinnerProps {
  class?: string;
  /**
   * @ko_KR 최소 로딩 시간
   * @en_US Minimum loading time
   */
  minLoadingTime?: number;
  /**
   * @ko_KR 로딩 상태 활성화
   */
  spinning?: boolean;
}
defineOptions({ name: 'Spinner' });
const props = defineProps<SpinnerProps>();
</script>
<template>
  <div :class="cn('relative min-h-20', props.class)">
    <slot></slot>
    <VbenSpinner
      :min-loading-time="props.minLoadingTime"
      :spinning="props.spinning"
    />
  </div>
</template>
