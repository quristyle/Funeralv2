<script setup lang="ts">
import { ref } from 'vue';
import { RouterLink } from 'vue-router';

import BrandLogo from '#/components/brand-logo.vue';
import { useSite } from '#/composables/use-site';

const { t, link, otherLocale, otherLocalePath } = useSite();
const open = ref(false);
</script>

<template>
  <header class="sticky top-0 z-50 border-b border-mist bg-paper/95 backdrop-blur">
    <div class="mx-auto flex h-14 max-w-6xl items-center justify-between px-6">
      <RouterLink :to="link('/')" class="flex items-center" aria-label="JSINI">
        <BrandLogo :height="24" />
      </RouterLink>

      <nav class="hidden items-center gap-8 md:flex" aria-label="주요 메뉴">
        <RouterLink
          v-for="item in [
            { to: link('/about'), label: t.nav.about },
            { to: link('/work'), label: t.nav.work },
            { to: link('/news'), label: t.nav.news },
            { to: link('/downloads'), label: t.nav.downloads },
            { to: link('/contact'), label: t.nav.contact },
          ]"
          :key="item.to"
          :to="item.to"
          class="text-sm text-steel transition-colors hover:text-ink"
          active-class="text-ink"
        >
          {{ item.label }}
        </RouterLink>

        <!--
          언어 전환. 지금 화면의 같은 경로로 넘긴다.
          `<a>` 가 아니라 RouterLink 라 새로고침 없이 바뀐다.
        -->
        <RouterLink
          :to="otherLocalePath"
          class="border border-mist px-2 py-1 text-xs text-steel transition-colors hover:border-ink hover:text-ink"
          :aria-label="`${t.common.langLabel}: ${otherLocale.toUpperCase()}`"
        >
          {{ otherLocale.toUpperCase() }}
        </RouterLink>
      </nav>

      <button
        type="button"
        class="flex size-8 flex-col items-center justify-center gap-1 md:hidden"
        :aria-expanded="open"
        aria-label="메뉴"
        @click="open = !open"
      >
        <span class="h-px w-5 bg-ink" />
        <span class="h-px w-5 bg-ink" />
        <span class="h-px w-5 bg-ink" />
      </button>
    </div>

    <nav v-if="open" class="border-t border-mist md:hidden" aria-label="주요 메뉴">
      <RouterLink
        v-for="item in [
          { to: link('/about'), label: t.nav.about },
          { to: link('/work'), label: t.nav.work },
          { to: link('/news'), label: t.nav.news },
          { to: link('/downloads'), label: t.nav.downloads },
          { to: link('/contact'), label: t.nav.contact },
          { to: otherLocalePath, label: otherLocale.toUpperCase() },
        ]"
        :key="item.to"
        :to="item.to"
        class="block border-b border-mist px-6 py-3 text-sm text-steel"
        @click="open = false"
      >
        {{ item.label }}
      </RouterLink>
    </nav>
  </header>
</template>
