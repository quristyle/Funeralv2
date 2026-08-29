<script setup lang="ts">
import { $t } from '@vben/locales';

import SelectItem from '../select-item.vue';

defineOptions({
  name: 'PreferenceFontFamilyConfig',
});

const appFontFamily = defineModel<string>();

/**
 * 고를 수 있는 글꼴.
 *
 * 값은 '어떤 글꼴 묶음을 쓸지' 고르는 열쇠일 뿐이고, 실제 글꼴 목록은 앱이 정한다
 * (각 앱의 `src/styles/font.ts`). 프레임워크 쪽 환경설정 부품이
 * 특정 글꼴 이름을 알고 있을 이유가 없기 때문이다.
 */
const FONT_OPTIONS = [
  { label: 'S-CoreDream', value: 'S-CoreDream' },
  { label: '나눔스퀘어라운드', value: 'NanumSquareRound' },
  { label: 'Play', value: 'Play' },
  { label: $t('preferences.followSystem'), value: 'system' },
];
</script>

<template>
  <SelectItem v-model="appFontFamily" :items="FONT_OPTIONS">
    {{ $t('preferences.fontFamily') }}
  </SelectItem>
</template>
