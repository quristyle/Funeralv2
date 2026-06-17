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
  fallbackLocale: {
    'ko-KR': ['en-US'],
    default: ['en-US'],
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
function setI18nLanguage(locale: Locale) {


//console.log(`[I18n] setI18nLanguage 1111111111111`, locale);



  i18n.global.locale.value = locale;


//console.log(`[I18n] setI18nLanguage 2222222222`, locale);

  document?.querySelector('html')?.setAttribute('lang', locale);
}

async function setupI18n(app: App, options: LocaleSetupOptions = {}) {


//console.log(`[I18n] setupI18n 1111111111111`, app);
//console.log(`[I18n] setupI18n 2222222222`, options);



  const { defaultLocale = 'ko-KR' } = options;
  // 앱에서 직접 서드파티 라이브러리 및 컴포넌트 라이브러리의 국제화를 확장할 수 있습니다.
  loadMessages = options.loadMessages || (async () => ({}));
  app.use(i18n);
  await loadLocaleMessages(defaultLocale);





  // 콘솔에 경고 출력
  i18n.global.setMissingHandler((locale, key) => {
    // ko, en 같은 중간 단계 로케일은 무시하고 실제 설정된 ko-KR, en-US에서만 경고 출력
    if (options.missingWarn && key.includes('.') && (locale === 'ko-KR' || locale === 'en-US')) {
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
  i18n,
  loadLocaleMessages,
  loadLocalesMap,
  loadLocalesMapFromDir,
  setupI18n,
};
