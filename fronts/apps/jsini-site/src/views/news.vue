<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { RouterLink } from 'vue-router';

import { siteApi, type PostListItem } from '#/api/site';
import { useSite } from '#/composables/use-site';

const { locale, t, link } = useSite();
const posts = ref<PostListItem[]>([]);
const loading = ref(true);

onMounted(async () => {
  posts.value = await siteApi.posts(locale.value, 50);
  loading.value = false;
  siteApi.recordVisit(`/${locale.value}/news`, locale.value);
});
</script>

<template>
  <div>
    <section class="border-b border-mist">
      <div class="mx-auto max-w-6xl px-6 py-20">
        <h1 class="h-display text-4xl">{{ t.nav.news }}</h1>
      </div>
    </section>

    <section class="mx-auto max-w-6xl px-6 py-20">
      <p v-if="loading" class="text-sm text-steel">{{ t.common.loading }}</p>
      <p v-else-if="!posts.length" class="text-sm text-steel">{{ t.common.empty }}</p>

      <div v-else class="grid gap-12 md:grid-cols-2 lg:grid-cols-3">
        <RouterLink
          v-for="p in posts"
          :key="p.slug"
          :to="link(`/news/${p.slug}`)"
          class="group block"
        >
          <div v-if="p.coverUrl" class="mb-5 aspect-[3/2] overflow-hidden bg-mist">
            <img
              :src="p.coverUrl"
              :alt="p.title"
              loading="lazy"
              class="size-full object-cover transition-transform duration-500 group-hover:scale-105"
            />
          </div>
          <time v-if="p.publishedAt" class="text-xs text-steel">
            {{ p.publishedAt.slice(0, 10) }}
          </time>
          <h2 class="mt-2 text-base font-bold transition-colors group-hover:text-steel">
            {{ p.title }}
          </h2>
          <p v-if="p.summary" class="mt-2 line-clamp-3 text-sm text-steel">{{ p.summary }}</p>
        </RouterLink>
      </div>
    </section>
  </div>
</template>
