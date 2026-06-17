<script lang="ts" setup>
import { ChevronDown } from '@vben-core/icons';
import { cn } from '@vben-core/shared/utils';

const props = defineProps<{
  class?: string;
}>();

// 화살표의 펼침/접힘 상태 제어
const collapsed = defineModel({ default: false });
</script>

<template>
  <div
    :class="cn('vben-link inline-flex items-center', props.class)"
    @click="collapsed = !collapsed"
  >
    <slot :is-expanded="collapsed">
      {{ collapsed }}
      <!-- <span>{{ isExpanded ? '접기' : '펼치기' }}</span> -->
    </slot>
    <div
      :class="{ 'rotate-180': !collapsed }"
      class="transition-transform duration-300"
    >
      <slot name="icon">
        <ChevronDown class="size-4" />
      </slot>
    </div>
  </div>
</template>
