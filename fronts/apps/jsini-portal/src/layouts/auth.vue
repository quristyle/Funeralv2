<script lang="ts" setup>
import { computed } from 'vue';

import { AuthPageLayout } from '@vben/layouts';
import { preferences, usePreferences } from '@vben/preferences';

import { $t } from '#/locales';

const appName = computed(() => preferences.app.name);

// `:logo` · `:logo-dark` 는 넘기지 않는다. 아래에서 `#logo` 슬롯을 통째로 덮어쓰기 때문에
// 그 값이 쓰이지 않는다. 넘겨 두면 "여기를 바꾸면 로고가 바뀐다" 는 오해를 남긴다.

/**
 * 로그인 화면은 브랜드가 가장 크게 보이는 자리다. 그래서 아이콘 + 앱 이름 텍스트 대신
 * **가로 조합(심볼 + JSINI 워드마크)** 을 그대로 쓴다.
 *
 * 기본 레이아웃은 `<img width="42">` 에 앱 이름을 옆 글자로 붙이는데, 그러면 워드마크가
 * 화면 글꼴로 써진 'JSINI ADMIN' 이 된다. 워드마크는 그린 글자라 글꼴로 흉내내면 안 된다
 * (`docs/brand/README.md`).
 *
 * 'ADMIN' 은 로고의 일부가 아니라 **어느 시스템인지 알려 주는 꼬리표**다.
 * 그래서 로고와 붙이지 않고 세로선으로 끊어 Steel 로 작게 둔다.
 */
// `theme.mode` 는 'auto' 도 될 수 있어 그것만 보면 안 된다.
// 프레임워크가 실제로 어떤 테마로 그리고 있는지는 usePreferences 의 isDark 가 안다
// (authentication.vue 도 같은 값으로 로고를 고른다).
const { isDark } = usePreferences();
</script>

<template>
  <AuthPageLayout
    :app-name="appName"
    :page-description="$t('authentication.pageDesc')"
    :page-title="$t('authentication.pageTitle')"
  >
    <template #logo>
      <div class="absolute top-0 left-0 z-10 flex flex-1">
        <div class="mt-4 ml-4 flex items-center gap-3 sm:mt-6 sm:ml-6">
          <img
            :key="isDark ? 'dark' : 'light'"
            :src="isDark ? '/brand/logo-horizontal-knockout.svg' : '/brand/logo-horizontal.svg'"
            alt="JSINI"
            height="26"
            class="h-[26px] w-auto"
          />
          <span class="h-4 w-px bg-border" aria-hidden="true"></span>
          <span class="text-xs tracking-[0.2em] text-muted-foreground uppercase">
            Admin
          </span>
        </div>
      </div>
    </template>
  </AuthPageLayout>
</template>
