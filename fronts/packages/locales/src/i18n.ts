import type { App } from 'vue';
import type { Locale } from 'vue-i18n';

import type {
  ImportLocaleFn,
  LoadMessageFn,
  LocaleSetupOptions,
  SupportedLanguagesType,
} from './typing';

import { unref } from 'vue';
import { createI18n } from 'vue-i18n';

import { useSimpleLocale } from '@vben-core/composables';

const i18n = createI18n({
  // 언어 코드는 지역 없이 짧게 쓴다(ko · en).
  // `ko-KR` 처럼 지역이 붙어 있으면 vue-i18n 이 지역을 뗀 `ko` 도 한 번 더 찾아
  // 못 찾는 키마다 경고가 두 줄씩 났다. 짧게 쓰면 한 단계로 끝난다.
  fallbackLocale: {
    ko: ['en'],
    default: ['en'],
  },
  globalInjection: true,
  legacy: false,
  locale: '',
  messages: {},
});

const modules = import.meta.glob('./langs/**/*.json');

const { setSimpleLocale } = useSimpleLocale();

const localesMap = loadLocalesMapFromDir(
  /\.\/langs\/([^/]+)\/(.*)\.json$/,
  modules,
);
let loadMessages: LoadMessageFn;

/**
 * Load locale modules
 * @param modules
 */
function loadLocalesMap(modules: Record<string, () => Promise<unknown>>) {
  const localesMap: Record<Locale, ImportLocaleFn> = {};

  for (const [path, loadLocale] of Object.entries(modules)) {
    const key = path.match(/([\w-]*)\.(json)/)?.[1];
    if (key) {
      localesMap[key] = loadLocale as ImportLocaleFn;
    }
  }
  return localesMap;
}

/**
 * Load locale modules with directory structure
 * @param regexp - Regular expression to match language and file names
 * @param modules - The modules object containing paths and import functions
 * @returns A map of locales to their corresponding import functions
 */
function loadLocalesMapFromDir(
  regexp: RegExp,
  modules: Record<string, () => Promise<unknown>>,
): Record<Locale, ImportLocaleFn> {
  const localesRaw: Record<Locale, Record<string, () => Promise<unknown>>> = {};
  const localesMap: Record<Locale, ImportLocaleFn> = {};

  // Iterate over the modules to extract language and file names
  for (const path in modules) {
    const match = path.match(regexp);
    if (match) {
      const [_, locale, fileName] = match;
      if (locale && fileName) {
        if (!localesRaw[locale]) {
          localesRaw[locale] = {};
        }
        if (modules[path]) {
          localesRaw[locale][fileName] = modules[path];
        }
      }
    }
  }

  // Convert raw locale data into async import functions
  for (const [locale, files] of Object.entries(localesRaw)) {
    localesMap[locale] = async () => {
      const messages: Record<string, any> = {};
      for (const [fileName, importFn] of Object.entries(files)) {
        messages[fileName] = ((await importFn()) as any)?.default;
      }
      return { default: messages };
    };
  }

  return localesMap;
}

/**
 * Set i18n language
 * @param locale
 */
/**
 * **번역 키일 때만** 번역한다. 키가 아니면 받은 글자를 그대로 돌려준다.
 *
 * 이 포털은 메뉴·탭·브레드크럼의 이름을 DB(`scom.system_menus`)에서 가져오고,
 * 거기에는 `MSA상태정보` 처럼 **완성된 글자**가 들어 있다 — 번역 키가 아니다.
 * 그걸 그대로 `$t()` 에 넣으면 vue-i18n 이 못 찾고 대체 언어를 훑으며
 * 경고를 두 줄씩 찍는다. 메뉴 170개면 한 번 그릴 때 344줄이고,
 * **그 콘솔 출력만으로 115ms 가 막힌다**(찾는 일 자체는 3ms — 23배 차이).
 *
 * `te()` 는 키가 있는지만 보고 경고를 내지 않는다.
 * 이름을 키로 넣은 메뉴는 예전처럼 번역되고,
 * 다른 자리의 진짜 누락 키 경고는 그대로 남는다.
 *
 * 경위는 docs/analysis/18-i18n-fallback-warning.md 참고.
 */
function $tIfKey(text?: null | string) {
  if (!text) return '';
  return i18n.global.te(text) ? i18n.global.t(text) : text;
}

function setI18nLanguage(locale: Locale) {


//console.log(`[I18n] setI18nLanguage 1111111111111`, locale);



  i18n.global.locale.value = locale;


//console.log(`[I18n] setI18nLanguage 2222222222`, locale);

  document?.querySelector('html')?.setAttribute('lang', locale);
}

async function setupI18n(app: App, options: LocaleSetupOptions = {}) {


//console.log(`[I18n] setupI18n 1111111111111`, app);
//console.log(`[I18n] setupI18n 2222222222`, options);



  const { defaultLocale = 'ko' } = options;
  // 앱에서 직접 서드파티 라이브러리 및 컴포넌트 라이브러리의 국제화를 확장할 수 있습니다.
  loadMessages = options.loadMessages || (async () => ({}));
  app.use(i18n);
  await loadLocaleMessages(defaultLocale);





  // 콘솔에 경고 출력
  i18n.global.setMissingHandler((locale, key) => {
    // ko, en 같은 중간 단계 로케일은 무시하고 실제 설정된 ko, en에서만 경고 출력
    if (options.missingWarn && key.includes('.') && (locale === 'ko' || locale === 'en')) {
//      console.warn( `[intlify] Not found '${key}' key in '${locale}' locale messages. (Current global locale: ${unref(i18n.global.locale)})`,);
    }
  });
}

/**
 * Load locale messages
 * @param lang
 */
async function loadLocaleMessages(lang: SupportedLanguagesType) {

//console.log(`[I18n] loadLocaleMessages 1111111111111111111111111111 Loading locale messages for: ${lang}`, lang);

//console.log(`[I18n] loadLocaleMessages 222222222222222222 Loading i18n.global.locale messages for: ${i18n.global.locale}`, i18n.global.locale);


  if (unref(i18n.global.locale) === lang) {

//console.log(`[I18n] loadLocaleMessages 333333333333333333 `, lang);

    return setI18nLanguage(lang);
  }
  setSimpleLocale(lang);

  const message = await localesMap[lang]?.();

  if (message?.default) {
//console.log(`[I18n] loadLocaleMessages 4444444444444 `, lang);
    i18n.global.setLocaleMessage(lang, message.default);
  }

  const mergeMessage = await loadMessages(lang);
  if (mergeMessage) {

//console.log(`[I18n] loadLocaleMessages 5555555555 `, lang);

    i18n.global.mergeLocaleMessage(lang, mergeMessage);
  }

//console.log(`[I18n] loadLocaleMessages 666666666666 `, lang);


  return setI18nLanguage(lang);
}

export {
  $tIfKey,
  i18n,
  loadLocaleMessages,
  loadLocalesMap,
  loadLocalesMapFromDir,
  setupI18n,
};
