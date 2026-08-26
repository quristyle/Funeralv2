<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { RouterLink, useRoute } from 'vue-router';

import { siteApi, type PostDetail } from '#/api/site';
import RichText from '#/components/rich-text.vue';
import { useSite } from '#/composables/use-site';

const route = useRoute();
const { locale, t, link } = useSite();

const post = ref<PostDetail | null>(null);
const loading = ref(true);

onMounted(async () => {
  post.value = await siteApi.post(locale.value, String(route.params.slug));
  loading.value = false;

  // 제목을 문서 제목에 반영한다. 이 화면은 프리렌더 대상이 아니라(주소가 자료에 달려 있다)
  // 브라우저에서 붙인다. 검색 노출이 중요해지면 `includedRoutes` 로 빌드 때 목록을
  // 받아와 프리렌더할 수 있다 — 그때는 빌드가 API 상태에 묶인다는 대가가 있다.
  if (post.value) {
    document.title = `${post.value.title} — JSINI`;
  }

  siteApi.recordVisit(`/${locale.value}/news/${route.params.slug}`, locale.value);
});
</script>

<template>
  <article class="mx-auto max-w-3xl px-6 py-20">
    <RouterLink :to="link('/news')" class="text-sm text-steel hover:text-ink">
      ← {{ t.common.backToList }}
    </RouterLink>

    <p v-if="loading" class="mt-10 text-sm text-steel">{{ t.common.loading }}</p>
    <p v-else-if="!post" class="mt-10 text-sm text-steel">{{ t.common.empty }}</p>

    <template v-else>
      <time v-if="post.publishedAt" class="mt-10 block text-xs text-steel">
        {{ post.publishedAt.slice(0, 10) }}
      </time>
      <h1 class="h-display mt-3 text-3xl">{{ post.title }}</h1>

      <div v-if="post.coverUrl" class="mt-10 overflow-hidden bg-mist">
        <img :src="post.coverUrl" :alt="post.title" class="w-full object-cover" />
      </div>

      <RichText v-if="post.body" :text="post.body" class="mt-10 text-sm leading-loose text-graphite" />
    </template>
  </article>
</template>
