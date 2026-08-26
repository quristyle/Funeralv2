import { computed } from 'vue';
import { useRoute } from 'vue-router';

import { MESSAGES, normalizeLocale } from '#/i18n/messages';

/**
 * 지금 화면의 언어와 그 언어의 문구.
 *
 * 언어는 주소가 정한다(`/ko/...` · `/en/...`). 스토어에 두지 않는 이유는
 * 정적 프리렌더 때문이다 — 빌드 시점에는 스토어가 없고 주소만 있다.
 */
export function useSite() {
  const route = useRoute();

  const locale = computed(() => normalizeLocale(route.params.locale as string | undefined));
  const t = computed(() => MESSAGES[locale.value]);

  /** 같은 화면의 다른 언어 주소. 언어 전환 링크가 쓴다. */
  const otherLocale = computed(() => (locale.value === 'ko' ? 'en' : 'ko'));
  const otherLocalePath = computed(() => {
    const rest = route.path.replace(/^\/(?:ko|en)/, '');
    return `/${otherLocale.value}${rest}`;
  });

  /** 언어를 붙인 내부 주소를 만든다. `link('/about')` → `/ko/about` */
  const link = (path: string) => `/${locale.value}${path === '/' ? '' : path}`;

  return { locale, t, otherLocale, otherLocalePath, link };
}
