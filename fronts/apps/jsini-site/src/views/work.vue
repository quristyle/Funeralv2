<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { RouterLink } from 'vue-router';

import { siteApi, type Section } from '#/api/site';
import RichText from '#/components/rich-text.vue';
import { useSite } from '#/composables/use-site';

const { locale, t, link } = useSite();
const sections = ref<Section[]>([]);

onMounted(async () => {
  sections.value = await siteApi.sections(locale.value, 'work.');
  siteApi.recordVisit(`/${locale.value}/work`, locale.value);
});

/**
 * 번호를 붙인다 (01 · 02 …). DB 의 `sortorder` 가 아니라 **화면에 실제로 그려진 순서**다.
 * 한 건이 비공개로 빠져도 번호가 건너뛰지 않는다.
 */
function ordinal(index: number): string {
  return String(index + 1).padStart(2, '0');
}

/**
 * 사례 그림. `work.funeral` → `src/assets/work/funeral.svg` 다.
 *
 * DB 에 열을 더하지 않는다. 그림은 문구와 달리 화면에서 편집하는 것이 아니라
 * 저장소에서 만들어 배포되는 것이라(브랜드 자산과 같다), 정적 파일로 두는 편이 맞다.
 *
 * **`public/` 이 아니라 `assets/` 다.** 빌드 시점에 어떤 파일이 있는지 알아야 하기 때문이다.
 * `public/` 에 두고 주소를 조립하면 파일이 없어도 `<img>` 가 프리렌더된 HTML 에 남고,
 * 브라우저에서 404 가 난 뒤에야 지워진다 — 그 사이에 깨진 그림과 "재현 이미지입니다"
 * 라는 설명만 남는다. JS 를 끈 방문자에게는 영영 그 상태다.
 * 아래처럼 훑으면 **없는 그림은 애초에 마크업에 들어가지 않는다.**
 *
 * **이 그림은 실제 화면의 캡처가 아니다.** 고객사 시스템 화면에는 고인·상주·담당자·
 * 설비 운전값이 들어 있어 공개 사이트에 올릴 수 없다. 레이아웃과 구성만 본떠
 * 브랜드 무채색으로 다시 그린 것이고, 표시된 자료는 전부 가상이다.
 */
const MOCKUPS = import.meta.glob<string>('../assets/work/*.svg', {
  eager: true,
  import: 'default',
  query: '?url',
});

function mockupSrc(sectionKey: string): string | undefined {
  const name = sectionKey.replace(/^work\./, '');
  return MOCKUPS[`../assets/work/${name}.svg`];
}
</script>

<template>
  <div>
    <section class="border-b border-mist">
      <div class="mx-auto max-w-6xl px-6 py-20">
        <h1 class="h-display text-4xl">{{ t.work.title }}</h1>
        <p class="mt-6 max-w-xl text-sm leading-relaxed text-steel">{{ t.work.lead }}</p>
      </div>
    </section>

    <section class="mx-auto max-w-6xl px-6 py-20">
      <p v-if="!sections.length" class="text-sm text-steel">{{ t.common.empty }}</p>

      <!--
        한 시스템이 한 줄이다. 왼쪽에 번호, 오른쪽에 내용.
        카드로 감싸지 않는다 — 테두리 상자가 늘어서면 각진 브랜드가 격자로만 읽힌다.
        구분은 상자가 아니라 가로 실선이 맡는다.
      -->
      <article
        v-for="(s, i) in sections"
        :key="s.sectionKey"
        class="grid gap-6 border-t border-mist py-12 first:border-t-0 first:pt-0 md:grid-cols-[6rem_1fr] md:gap-10"
      >
        <div class="text-sm tracking-[0.2em] text-steel" aria-hidden="true">
          {{ ordinal(i) }}
        </div>

        <div>
          <!-- 분야가 제목보다 먼저 온다. 고객사명을 쓰지 않으므로 분야가 곧 신원이다. -->
          <p v-if="s.subtitle" class="text-xs uppercase tracking-[0.2em] text-steel">
            {{ s.subtitle }}
          </p>
          <h2 class="h-display mt-3 text-2xl">{{ s.title }}</h2>
          <RichText
            v-if="s.body"
            :text="s.body"
            class="mt-5 max-w-2xl text-sm leading-loose text-graphite"
          />

          <!--
            화면 그림. 캡처가 아니라 재현 이미지라 아래 한 줄을 **늘 함께** 둔다.
            그 한 줄이 없으면 보는 사람이 고객사의 실제 화면으로 읽는다.
          -->
          <figure v-if="mockupSrc(s.sectionKey)" class="mt-8">
            <img
              :src="mockupSrc(s.sectionKey)"
              :alt="s.title"
              loading="lazy"
              class="w-full border border-mist"
            />
            <figcaption class="mt-3 text-xs text-steel">{{ t.work.mockupNote }}</figcaption>
          </figure>
        </div>
      </article>
    </section>

    <section class="border-t border-mist bg-ink text-paper">
      <div class="mx-auto flex max-w-6xl flex-col gap-6 px-6 py-16 md:flex-row md:items-center md:justify-between">
        <p class="h-display max-w-md text-xl">{{ t.work.ctaLead }}</p>
        <RouterLink
          :to="link('/contact')"
          class="inline-block shrink-0 border border-paper px-7 py-3 text-sm transition-colors hover:bg-paper hover:text-ink"
        >
          {{ t.nav.contact }}
        </RouterLink>
      </div>
    </section>
  </div>
</template>
