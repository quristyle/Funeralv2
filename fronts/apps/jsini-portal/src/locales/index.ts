import type { Locale } from 'ant-design-vue/es/locale';

import type { App } from 'vue';

import type { LocaleSetupOptions, SupportedLanguagesType } from '@vben/locales';

import { nextTick, ref } from 'vue';

import {
  $t,
  $te,
  $tIfKey,
  setupI18n as coreSetup,
  i18n,
  loadLocalesMapFromDir,
} from '@vben/locales';
import { preferences } from '@vben/preferences';

import antdEnLocale from 'ant-design-vue/es/locale/en_US';
import antdKoLocale from 'ant-design-vue/es/locale/ko_KR';
import dayjs from 'dayjs';

import { getI18nListByLocale, ensureI18nResource, type SystemI18nApi } from '#/api/portal/system/i18n';

const antdLocale = ref<Locale>(antdKoLocale);

// 중복 요청 방지를 위한 누락 키 캐시
const reportedMissingKeys = new Set<string>();

const modules = import.meta.glob('./langs/**/*.json');

const localesMap = loadLocalesMapFromDir(
  /\.\/langs\/([^/]+)\/(.*)\.json$/,
  modules,
);

/**
 * DB에서 가져온 다국어 리소스를 중첩된 객체 구조로 변환합니다.
 * @param resources
 */
function transformDbResourcesToMessages(resources: SystemI18nApi.I18nResource[]) {
  const messages: Record<string, any> = {};
  resources.forEach((res) => {
    // 키가 'vxe.pager.total' 인 경우 점(.)을 기준으로 쪼개어 중첩 객체 생성
    const keys = res.key.split('.');
    let current = messages;
    for (let i = 0; i < keys.length; i++) {
      const key = keys[i]!;
      if (i === keys.length - 1) {
        // 이미 해당 경로에 객체가 있다면(카테고리와 키가 겹침) 덮어씌우지 않고 조심스럽게 처리하거나 무시
        if (typeof current[key] === 'object' && current[key] !== null) {
          console.warn(`[I18n Debug] Conflict detected for key: ${res.key}. Cannot set value as it is already an object.`);
        } else {
          current[key] = res.value;
        }
      } else {
        // 경로상의 객체 생성
        if (typeof current[key] !== 'object' || current[key] === null) {
          current[key] = {};
        }
        current = current[key];
      }
    }
  });
  return messages;
}

/**
 * DB에서 다국어 정보를 가져와 현재 i18n 인스턴스에 병합합니다.
 * @param lang
 */
async function fetchAndMergeDbMessages(lang: SupportedLanguagesType) {
  //return null;
  try {
    console.log(`[11111111111111I18n Debug] Fetching DB messages for locale: ${lang}`);
    const data = await getI18nListByLocale(lang);
    console.log(`[22222222222222222I18n Debug] Received data from DB:`, data);
    
    if (data && data.length > 0) {
      const dbMessages = transformDbResourcesToMessages(data);
      console.log(`[I18n Debug] Transformed messages to nest structure:`, dbMessages);
      // 테이블에서 가져온 메시지를 현재 i18n 인스턴스에 병합
      // 병합이 올바르게 안되는것같아서 일딴 보류.
      i18n.global.mergeLocaleMessage(lang, dbMessages);
      
      // [반응성 강제 트리거] 
      // 이미 렌더링된 컴포넌트들이 새로 병합된 다국어 정보를 인식하게 하려면,
      // i18n의 locale 값을 일시적으로 변경했다가 되돌려야 전역 리렌더링이 발생합니다.
      const currentLocale = i18n.global.locale.value;
      if (currentLocale === lang) {
        // 잠시 비운 뒤 다음 틱에서 원래 언어로 복구 (전체 화면 리렌더링 유발)
        i18n.global.locale.value = '' as any;
        nextTick(() => {
          i18n.global.locale.value = lang;
          console.log(`[I18n Debug] Successfully triggered reactivity for ${lang}`);
        });
      }
    } else {
      console.warn(`[I18n Debug] No data returned from DB for locale: ${lang}`);
    }
  } catch (error: any) {
    // 에러 발생 시 상세 정보 출력
    console.error(`[I18n Debug] Error fetching messages for ${lang}:`, {
      message: error.message,
      stack: error.stack,
      response: error.response?.data,
      status: error.response?.status,
      config: {
        url: error.config?.url,
        method: error.config?.method,
        baseURL: error.config?.baseURL
      }
    });
  }


  console.log('[I18n Debug] Final merged messages  for', lang, i18n.global.getLocaleMessage(lang));
}

/**
 * 앱 전용 언어 팩 로드
 * 초기 로딩 및 언어 전환 시 호출됩니다.
 * @param lang
 */
async function loadMessages(lang: SupportedLanguagesType) {
  const [appLocaleMessages] = await Promise.all([
    localesMap[lang]?.(),
    loadThirdPartyMessage(lang),
  ]);

  // DB에서 다국어 정보를 가져와 병합하는 프로세스 실행
  // 초기 bootstrap 단계에서 initStores가 먼저 호출되므로, 
  // 대부분의 경우 여기서 바로 실행 가능합니다.
  try {
    // 비동기로 실행하되, 에러 발생 시 로그만 남김 (앱 로딩 방해 금지)
    fetchAndMergeDbMessages(lang);
  } catch (error) {
    console.error(`[I18n] Failed to trigger DB fetch for ${lang}:`, error);
  }

  return appLocaleMessages?.default;
}

/**
 * 타사 컴포넌트 라이브러리의 언어 팩 로드
 * @param lang
 */
async function loadThirdPartyMessage(lang: SupportedLanguagesType) {
  await Promise.all([
    loadAntdLocale(lang),
    loadDayjsLocale(lang),
    loadVxeTableLocale(lang),
  ]);
}

/**
 * dayjs 언어 팩 로드
 * @param lang
 */
async function loadDayjsLocale(lang: SupportedLanguagesType) {
  let locale;
  switch (lang) {
    case 'en': {
      locale = await import('dayjs/locale/en');
      break;
    }
    case 'ko': {
      console.log(`[Dayjs I18n] Loading dayjs locale for: ${lang}`);
      locale = await import('dayjs/locale/ko');
      break;
    }
    // 기본값으로 영어 사용
    default: {
      locale = await import('dayjs/locale/en');
    }
  }
  if (locale) {
    dayjs.locale(locale);
  } else {
    console.error(`Failed to load dayjs locale for ${lang}`);
  }
}

/**
 * vxe-table 언어 팩 로드 및 vue-i18n 병합
 * @param lang
 */
async function loadVxeTableLocale(lang: SupportedLanguagesType) {
  let locale;
  switch (lang) {
    case 'en': {
      locale = await import('vxe-table/es/locale/lang/en-US'); // vxe 쪽 규격은 긴 코드다
      break;
    }
    case 'ko': {
      console.log(`[Vxe I18n] Loading vxe-table locale for: ${lang}`);
      locale = await import('vxe-table/es/locale/lang/ko-KR'); // vxe 쪽 규격은 긴 코드다
      break;
    }
    default: {
      locale = await import('vxe-table/es/locale/lang/en-US'); // vxe 쪽 규격은 긴 코드다
    }
  }
  if (locale && locale.default) {
    i18n.global.mergeLocaleMessage(lang, locale.default);
  }
  if (lang === 'ko') {
    i18n.global.mergeLocaleMessage(lang, VXE_KO_PATCH);
  }
}

/**
 * vxe 한국어 팩에 **중국어로 남아 있는 것**을 영어로 덮는다.
 *
 * vxe 의 `ko-KR` 은 번역이 덜 된 팩이다. 우리말로 옮겨진 것과 영어로 남은 것
 * (`Title` · `Width (pixels)` · `Freeze`)이 섞여 있고, 그 사이에 **중국어가
 * 그대로 남은 자리**가 있다 — 열 설정 창의 '对齐方式' 이 그것이다.
 *
 * 여기 적은 값은 전부 vxe 의 `en-US` 팩에 있는 그대로다. 우리말로 새로 옮기지
 * 않은 이유: 같은 창의 이웃 항목(`Title` · `Width (pixels)` · `Freeze`)이 이미
 * 영어라, 한 칸만 우리말이면 오히려 더 튄다. 창 전체를 우리말로 옮기는 것은
 * 이 수정의 목적이 아니다.
 *
 * **팩을 덮어쓰는 것이지 우리 사전(`langs/`)에 넣는 것이 아니다.** 이 키들은
 * vxe 것이므로 vxe 팩을 올린 **바로 뒤에** 얹어야 순서가 분명하다.
 * (DB 다국어가 나중에 또 덮을 수 있다 — 운영에서 문구를 바꾸는 통로다.)
 *
 * vxe 를 올릴 때 이 목록을 다시 본다. 팩이 채워졌으면 그만큼 지운다.
 * 확인은 이렇게 한다 (중국어가 남아 있으면 찍힌다).
 * ```bash
 * node -e "const s=require('fs').readFileSync(require.resolve('vxe-table/es/locale/lang/ko-KR'),'utf8'); console.log(s.match(/'[^']*[一-鿿][^']*'/g))"
 * ```
 */
const VXE_KO_PATCH = {
  vxe: {
    custom: {
      setting: {
        alignCenter: 'Center',
        alignLeft: 'Left',
        alignRight: 'Right',
        anCenterTitle: 'Align center',
        anLeftTitle: 'Align left',
        anRightTitle: 'Align right',
        colAlign: 'Align',
        colFootAlign: 'Footer align',
        colHeadAlign: 'Header align',
        moveDn: 'Down',
        moveDnTitle: 'Click to move downward',
        moveUp: 'Up',
        moveUpTitle: 'Click to move upwards',
        putBottom: 'Bottom',
        putBottomTitle: 'Click to end',
        putTop: 'Top',
        putTopTitle: 'Click to start',
        sortHelpTip: 'Click on the icon and then start dragging.',
      },
    },
  },
};

/**
 * antd 언어 팩 로드
 * @param lang
 */
async function loadAntdLocale(lang: SupportedLanguagesType) {
  switch (lang) {
    case 'en': {
      antdLocale.value = antdEnLocale;
      break;
    }
    case 'ko': {
      antdLocale.value = antdKoLocale;
      break;
    }
  }
}

/**
 * 예전 언어 코드를 짧은 코드로 다듬는다.
 *
 * 이 포털은 `ko` · `en` 만 쓴다. 지역 코드(`ko-KR`)를 붙이면 vue-i18n 이
 * 지역을 뗀 코드도 한 번 더 찾아서 못 찾는 키마다 경고가 두 줄씩 났다.
 *
 * 다만 **이미 쓰던 브라우저의 로컬스토리지에는 `ko-KR` 이 남아 있다.**
 * 그대로 두면 그 사람만 언어를 못 찾으므로 읽는 자리에서 한 번 바꿔 준다.
 * 한동안 지나면 지워도 되는 코드다.
 */
function shortenLocale(locale: string) {
  const map: Record<string, string> = { 'en-US': 'en', 'ko-KR': 'ko' };
  return map[locale] ?? locale;
}

async function setupI18n(app: App, options: LocaleSetupOptions = {}) {
  // 언어 코드는 ko · en 하나로 관리한다(지역 코드를 붙이지 않는다).
  // 예전에 저장해 둔 'ko-KR' / 'en-US' 를 들고 오는 브라우저가 있어 여기서 한 번 다듬는다.
  const appLocale = (options.defaultLocale || preferences.app.locale) as string;
  const mappedLocale = shortenLocale(appLocale);

  await coreSetup(app, {
    loadMessages,
    missingWarn: !import.meta.env.PROD,
    ...options,
    defaultLocale: mappedLocale as SupportedLanguagesType, // ...options 이후에 선언하여 무조건 덮어쓰기 방지
  });

  // 누락된 다국어 키 발견 시 DB에 자동 등록하는 핸들러 추가

  /*
  i18n.global.setMissingHandler((locale, key) => {
    // 1. 이미 보고된 적이 있거나, 점(.)이 없는 단순 문자열은 무시
    const cacheKey = `${locale}:${key}`;
    if (reportedMissingKeys.has(cacheKey)) return;

    // 2. 캐시에 추가하여 중복 요청 방지
    reportedMissingKeys.add(cacheKey);

    // 3. 백엔드에 보고
    if (key.includes('.')) {
      // [Fallback 추출] 영어(en-US) 메시지에서 해당 키의 값을 찾아봅니다.
      const enMessages = i18n.global.getLocaleMessage('en') as any;
      const keys = key.split('.');
      let defaultValue = '';
      
      try {
        // 중첩 객체 탐색 (예: common.operation)
        let current = enMessages;
        for (const k of keys) {
          current = current[k];
        }
        // 값이 문자열인 경우에만 기본값으로 채택
        if (typeof current === 'string') {
          defaultValue = current;
        }
      } catch (e) {
        // 탐색 실패 시 무시
      }

      console.warn(`[I18n] Missing key detected: ${key}. reporting with fallback: ${defaultValue || 'None'}`);
      ensureI18nResource({ 
        locale, 
        key, 
        defaultValue: defaultValue || undefined 
      }).catch((err) => {
        console.error(`[I18n] Failed to report missing key ${key}:`, err);
      });
    }
  });

  */
}
/**
 * 로컬 메모리에 보관된 특정 다국어 키의 번역 값을 즉시 갱신하고 화면을 리렌더링합니다.
 * @param locale 언어셋 (ko, en)
 * @param key 다국어 번역 키
 * @param value 변경된 번역 값
 */
export function updateLocalI18n(locale: string, key: string, value: string) {
  try {
    const keys = key.split('.');
    const messages = i18n.global.getLocaleMessage(locale) as Record<string, any>;
    
    if (messages) {
      let current = messages;
      for (let i = 0; i < keys.length; i++) {
        const k = keys[i]!;
        if (i === keys.length - 1) {
          current[k] = value;
        } else {
          if (!current[k] || typeof current[k] !== 'object') {
            current[k] = {};
          }
          current = current[k];
        }
      }
      
      // vue-i18n 인스턴스에 수정된 메시지 세트를 강제 등록
      i18n.global.setLocaleMessage(locale, messages);
      
      // 반응성 강제 트리거: locale 값을 흔들어 화면을 동적으로 리렌더링합니다.
      const currentLocale = i18n.global.locale.value;
      if (currentLocale === locale) {
        i18n.global.locale.value = '' as any;
        nextTick(() => {
          i18n.global.locale.value = locale as any;
        });
      }
    }
  } catch (error) {
    console.error(`[I18n Local Update Error] Failed to update key: ${key} for locale: ${locale}`, error);
  }
}

export { $t, $te, $tIfKey, antdLocale, setupI18n };
