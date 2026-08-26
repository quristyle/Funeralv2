<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { RouterLink } from 'vue-router';

import { siteApi, type PostListItem, type Section } from '#/api/site';
import RichText from '#/components/rich-text.vue';
import ShardMotion from '#/components/shard-motion.vue';
import { useSite } from '#/composables/use-site';

const { locale, t, link } = useSite();

const sections = ref<Section[]>([]);
const posts = ref<PostListItem[]>([]);

onMounted(async () => {
  // DB 가 비어 있어도 화면은 그려진다. 아래 블록들은 있으면 붙고 없으면 빠진다.
  [sections.value, posts.value] = await Promise.all([
    siteApi.sections(locale.value, 'home.'),
    siteApi.posts(locale.value, 3),
  ]);
  siteApi.recordVisit(`/${locale.value}`, locale.value);
});
</script>

<template>
  <div>
    <!--
      히어로. 문구는 왼쪽, 모션은 오른쪽이다.
      모션 위에 글자를 얹지 않는다 — 움직이는 배경 위의 글자는 읽기 어렵다.
    -->
    <section class="relative overflow-hidden bg-ink text-paper">
      <div class="mx-auto grid max-w-6xl items-center gap-10 px-6 py-24 md:grid-cols-2 md:py-32">
        <div>
          <p class="text-xs uppercase tracking-[0.3em] text-steel">
            {{ t.hero.eyebrow }}
          </p>
          <h1 class="h-display mt-6 whitespace-pre-line text-4xl md:text-5xl">
            {{ t.hero.headline }}
          </h1>
          <p class="mt-7 max-w-md text-sm leading-relaxed text-mist">
            {{ t.hero.lead }}
          </p>
          <RouterLink
            :to="link('/about')"
            class="mt-10 inline-block border border-paper px-7 py-3 text-sm transition-colors hover:bg-paper hover:text-ink"
          >
            {{ t.hero.cta }}
          </RouterLink>
        </div>

        <div class="h-56 md:h-80" aria-hidden="true">
          <ShardMotion />
        </div>
      </div>
    </section>

    <!-- DB 의 문구 블록. `home.` 으로 시작하는 열쇠만 가져온다. -->
    <section v-if="sections.length" class="mx-auto max-w-6xl px-6 py-24">
      <div class="grid gap-14 md:grid-cols-3">
        <article v-for="s in sections" :key="s.sectionKey">
          <div class="shard-rule mb-6 w-10 bg-ink" />
          <h2 class="h-display text-xl">{{ s.title }}</h2>
          <p v-if="s.subtitle" class="mt-2 text-sm text-steel">{{ s.subtitle }}</p>
          <RichText v-if="s.body" :text="s.body" class="mt-4 text-sm leading-relaxed text-graphite" />
        </article>
      </div>
    </section>

    <!-- 뉴스 셋. 없으면 이 절 자체가 빠진다. -->
    <section v-if="posts.length" class="border-t border-mist">
      <div class="mx-auto max-w-6xl px-6 py-24">
        <div class="flex items-end justify-between">
          <h2 class="h-display text-2xl">{{ t.nav.news }}</h2>
          <RouterLink :to="link('/news')" class="text-sm text-steel hover:text-ink">
            {{ t.common.readMore }} →
          </RouterLink>
        </div>

        <div class="mt-10 grid gap-10 md:grid-cols-3">
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
            <h3 class="mt-2 text-base font-bold transition-colors group-hover:text-steel">
              {{ p.title }}
            </h3>
            <p v-if="p.summary" class="mt-2 line-clamp-2 text-sm text-steel">{{ p.summary }}</p>
          </RouterLink>
        </div>
      </div>
    </section>
  </div>
</template>
