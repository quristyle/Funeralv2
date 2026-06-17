<script lang="ts" setup>
import type { BreadcrumbProps } from './types';

import { useForwardPropsEmits } from 'reka-ui';

import BreadcrumbBackground from './breadcrumb-background.vue';
import Breadcrumb from './breadcrumb.vue';

interface Props extends BreadcrumbProps {
  class?: any;
}

const props = withDefaults(defineProps<Props>(), {});

const emit = defineEmits<{ select: [string] }>();

const forward = useForwardPropsEmits(props, emit);
</script>
<template>
  <Breadcrumb
    v-if="styleType === 'normal'"
    v-bind="forward"
    class="vben-breadcrumb"
  />
  <BreadcrumbBackground
    v-if="styleType === 'background'"
    v-bind="forward"
    class="vben-breadcrumb"
  />
</template>
<style lang="scss" scoped>
/** Antd를 전역으로 도입할 때 ol 및 ul의 기본 스타일이 수정되는 문제를 수정합니다. */
.vben-breadcrumb {
  :deep(ol),
  :deep(ul) {
    margin-bottom: 0;
  }
}
</style>
