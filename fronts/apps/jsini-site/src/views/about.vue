<script setup lang="ts">
import { onMounted, ref } from 'vue';

import { siteApi, type Section } from '#/api/site';
import RichText from '#/components/rich-text.vue';
import { useSite } from '#/composables/use-site';

const { locale, t } = useSite();
const sections = ref<Section[]>([]);

onMounted(async () => {
  sections.value = await siteApi.sections(locale.value, 'about.');
  siteApi.recordVisit(`/${locale.value}/about`, locale.value);
});
</script>

<template>
  <div>
    <section class="border-b border-mist">
      <div class="mx-auto max-w-6xl px-6 py-20">
        <h1 class="h-display text-4xl">{{ t.nav.about }}</h1>
      </div>
    </section>

    <section class="mx-auto max-w-3xl px-6 py-20">
      <p v-if="!sections.length" class="text-sm text-steel">{{ t.common.empty }}</p>

      <article v-for="s in sections" :key="s.sectionKey" class="mb-16 last:mb-0">
        <div class="shard-rule mb-6 w-10 bg-ink" />
        <h2 class="h-display text-2xl">{{ s.title }}</h2>
        <p v-if="s.subtitle" class="mt-2 text-sm text-steel">{{ s.subtitle }}</p>
        <!-- 문단과 굵은 글씨만 살린다. v-html 을 쓰지 않는 이유는 RichText 주석에 있다. -->
        <RichText v-if="s.body" :text="s.body" class="mt-6 text-sm leading-loose text-graphite" />
      </article>
    </section>
  </div>
</template>
