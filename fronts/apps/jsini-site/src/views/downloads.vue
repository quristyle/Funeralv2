<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';

import { siteApi, type DownloadItem } from '#/api/site';
import { useSite } from '#/composables/use-site';

const { locale, t } = useSite();

const items = ref<DownloadItem[]>([]);
const loading = ref(true);
const active = ref<string>('');

const categories = computed(() => {
  const set = new Set(items.value.map((x) => x.category).filter(Boolean) as string[]);
  return [...set];
});

const shown = computed(() =>
  active.value ? items.value.filter((x) => x.category === active.value) : items.value,
);

function size(bytes?: number) {
  if (!bytes) return '';
  const mb = bytes / 1024 / 1024;
  return mb >= 1 ? `${mb.toFixed(1)} MB` : `${Math.round(bytes / 1024)} KB`;
}

onMounted(async () => {
  items.value = await siteApi.downloads(locale.value);
  loading.value = false;
  siteApi.recordVisit(`/${locale.value}/downloads`, locale.value);
});
</script>

<template>
  <div>
    <section class="border-b border-mist">
      <div class="mx-auto max-w-6xl px-6 py-20">
        <h1 class="h-display text-4xl">{{ t.nav.downloads }}</h1>
      </div>
    </section>

    <section class="mx-auto max-w-4xl px-6 py-20">
      <div v-if="categories.length" class="mb-10 flex flex-wrap gap-2">
        <button
          type="button"
          class="border px-4 py-2 text-xs transition-colors"
          :class="active === '' ? 'border-ink bg-ink text-paper' : 'border-mist text-steel hover:border-ink hover:text-ink'"
          @click="active = ''"
        >
          ALL
        </button>
        <button
          v-for="c in categories"
          :key="c"
          type="button"
          class="border px-4 py-2 text-xs transition-colors"
          :class="active === c ? 'border-ink bg-ink text-paper' : 'border-mist text-steel hover:border-ink hover:text-ink'"
          @click="active = c"
        >
          {{ c }}
        </button>
      </div>

      <p v-if="loading" class="text-sm text-steel">{{ t.common.loading }}</p>
      <p v-else-if="!shown.length" class="text-sm text-steel">{{ t.common.empty }}</p>

      <ul v-else class="divide-y divide-mist border-y border-mist">
        <li v-for="d in shown" :key="d.id" class="flex items-center gap-6 py-5">
          <div class="min-w-0 flex-1">
            <p class="truncate text-sm font-bold">{{ d.title }}</p>
            <p v-if="d.description" class="mt-1 truncate text-xs text-steel">
              {{ d.description }}
            </p>
            <p class="mt-1 text-xs text-mist">
              <span v-if="d.fileName">{{ d.fileName }}</span>
              <span v-if="d.fileSize"> · {{ size(d.fileSize) }}</span>
              <span> · {{ d.downloadCount }}</span>
            </p>
          </div>

          <!--
            SiteServer 를 한 번 거친다. 그쪽이 횟수를 세고 FileServer 로 302 로 넘긴다.
            FileServer 를 직접 가리키면 셀 수가 없다.
            넘겨받는 파일은 FileServer 의 `is_public` 이 켜져 있어야 한다.
          -->
          <a
            :href="d.downloadUrl"
            class="shrink-0 border border-ink px-5 py-2 text-xs transition-colors hover:bg-ink hover:text-paper"
          >
            {{ t.common.download }}
          </a>
        </li>
      </ul>
    </section>
  </div>
</template>
