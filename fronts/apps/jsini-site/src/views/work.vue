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
 * **`public/` 이 아니라 `assets/` 다.** 사례 다섯 중 그림이 있는 것과 없는 것이 섞여 있는데,
 * `public/` 에 두고 주소를 문자열로 조립하면 없는 그림도 `<img>` 로 일단 나가고
 * 404 가 돌아온 뒤에야 지워진다 — 그 사이에 깨진 그림과 "재현 이미지입니다" 라는
 * 설명만 남는다. 아래처럼 훑으면 **없는 그림은 애초에 마크업에 들어가지 않는다.**
 * 덤으로 파일 이름에 해시가 붙어 캐시를 길게 걸 수 있다.
 *
 * (프리렌더 걱정은 아니다. 사례 목록은 `onMounted` 로 받아 오는데 그것은 SSG 때
 *  돌지 않으므로, 이 페이지의 본문은 어차피 통째로 브라우저에서 그려진다.)
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

/**
 * 한 사례에 그림이 여럿일 수 있다. `work.utility` 면
 * `utility.svg` · `utility-trend.svg` 처럼 이름 뒤에 하이픈을 붙여 늘린다.
 *
 * `utility` 로 시작하는 것을 다 집으면 `utility2.svg` 같은 남의 이름까지 걸리므로,
 * **정확히 같거나 하이픈이 이어지는 것**만 집는다.
 */
function mockups(sectionKey: string): string[] {
  const name = sectionKey.replace(/^work\./, '');
  const bases = Object.keys(MOCKUPS)
    .map((p) => p.slice('../assets/work/'.length, -'.svg'.length))
    .filter((base) => base === name || base.startsWith(`${name}-`));

  // 이름만 있는 것(대표 화면)이 먼저, 하이픈이 붙은 것은 그 뒤에 이름순.
  // 그냥 정렬하면 `-`(0x2D) 가 `.`(0x2E) 보다 앞서서 `utility-trend` 가
  // `utility` 를 제치고 올라온다 — 대표 화면이 두 번째로 밀린다.
  bases.sort((a, b) => (a === name ? -1 : b === name ? 1 : a.localeCompare(b)));

  return bases.map((base) => MOCKUPS[`../assets/work/${base}.svg`]!);
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
          <figure v-if="mockups(s.sectionKey).length" class="mt-10">
            <div
              v-for="(src, k) in mockups(s.sectionKey)"
              :key="src"
              class="work-shot"
              :class="{ 'mt-8': k > 0 }"
            >
              <img :src="src" :alt="s.title" loading="lazy" class="w-full border border-mist" />
            </div>
            <!-- 그림이 몇 장이든 설명은 한 번만. 같은 문장을 반복하면 읽지 않게 된다. -->
            <figcaption class="mt-5 text-xs text-steel">{{ t.work.mockupNote }}</figcaption>
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

<style scoped>
/**
 * 화면 그림을 살짝 비스듬히 눕힌다.
 *
 * **각도를 아무렇게나 고르지 않았다.** 브랜드 모티프 '상승 조각' 의 기울기가 22:100
 * (약 12.4°) 인데, 1200px 짜리 가로 그림에 그대로 주면 한쪽 끝이 264px 들려서
 * 글자를 못 읽는다. 그래서 **같은 방향으로 4:100 (약 2.3°)** 만 준다.
 * 모티프와 같은 쪽으로 기울되 읽는 것을 방해하지 않는 선이다.
 *
 * 브랜드 규칙 5절이 금지하는 '기울이기' 는 **로고 · 심볼 · 워드마크**에 대한 것이라
 * 화면 그림에는 걸리지 않는다. 다만 같은 절이 금지하는 그림자 · 그라디언트는
 * 여기서도 쓰지 않는다 — 홍보 사이트가 흔히 얹는 것이지만 이 브랜드는 평면이다.
 *
 * 좁은 화면에서는 눕히지 않는다. 폭이 좁을수록 기울기가 잡아먹는 세로 공간의
 * 비중이 커지고, 손에 들고 보는 화면에서 비뚤어진 표는 읽기 나쁘다.
 */
.work-shot {
  transform: skewY(-2.3deg);
}

@media (max-width: 767px) {
  .work-shot {
    transform: none;
  }
}

/* 움직임을 원하지 않는 사람에게는 기운 것도 되돌린다 — 어지럼의 원인이 된다. */
@media (prefers-reduced-motion: reduce) {
  .work-shot {
    transform: none;
  }
}
</style>
