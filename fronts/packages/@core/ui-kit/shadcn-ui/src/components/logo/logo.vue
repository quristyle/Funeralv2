<script setup lang="ts">
import { computed } from 'vue';

import { VbenAvatar } from '../avatar';

interface Props {
  /**
   * @ko_KR 텍스트 접기 여부
   */
  collapsed?: boolean;
  /**
   * @ko_KR Logo 이미지 맞춤 방식
   */
  fit?: 'contain' | 'cover' | 'fill' | 'none' | 'scale-down';
  /**
   * @ko_KR Logo 이동 주소
   */
  href?: string;
  /**
   * @ko_KR Logo 이미지 크기
   */
  logoSize?: number;
  /**
   * @ko_KR Logo 아이콘
   */
  src?: string;
  /**
   * @ko_KR 어두운 테마 Logo 아이콘 (선택 사항, 설정하지 않으면 src 사용)
   */
  srcDark?: string;
  /**
   * @ko_KR Logo 텍스트
   */
  text: string;
  /**
   * @ko_KR Logo 테마
   */
  theme?: string;
}

defineOptions({
  name: 'VbenLogo',
});

const props = withDefaults(defineProps<Props>(), {
  collapsed: false,
  href: 'javascript:void 0',
  logoSize: 32,
  src: '',
  srcDark: '',
  theme: 'light',
  fit: 'cover',
});

/**
 * @ko_KR 테마에 따라 적절한 logo 아이콘 선택
 */
const logoSrc = computed(() => {
  // 테마가 'dark'이고 srcDark가 제공된 경우 어두운 테마의 logo 사용
  if (props.theme === 'dark' && props.srcDark) {
    return props.srcDark;
  }
  // 그렇지 않으면 기본 src 사용
  return props.src;
});
</script>

<template>
  <div :class="theme" class="flex h-full items-center text-lg">
    <a
      :class="$attrs.class"
      :href="href"
      class="flex h-full items-center gap-2 overflow-hidden px-3 text-lg leading-normal transition-all duration-500"
    >
      <VbenAvatar
        v-if="logoSrc"
        :alt="text"
        :src="logoSrc"
        :size="logoSize"
        :fit="fit"
        class="relative rounded-none bg-transparent"
      />
      <template v-if="!collapsed">
        <slot name="text">
          <span class="text-foreground truncate font-semibold text-nowrap">
            {{ text }}
          </span>
        </slot>
      </template>
    </a>
  </div>
</template>
