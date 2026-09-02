# 콘솔의 `[intlify] Fall back to translate ...` 경고 — 원인과 대응

작성: 2026-08-23
질문: "이 메시지가 어디서 나오는가 · 안 나오게 하려면 · 화면이 멈춘 것처럼 느껴진다 ·
`en`/`en-US`, `ko`/`ko-KR` 로 두 번 찾는데 `ko`/`en` 하나로 관리하려면"

> **2026-09-02** — 여기서 진단한 것과 **똑같은 원인**이 메뉴 관리 화면(`/system/menu`)에도
> 있었다. 목록을 한 번 새로 그릴 때마다 경고가 492줄이었다.
> 4절의 권장안 **B**(키일 때만 옮긴다) 를 쓰고, 거기에 **번역을 백엔드에서 붙여** 내려보내는
> 방법을 더해 0줄로 만들었다 — [43-menu-admin-save-and-i18n.md](43-menu-admin-save-and-i18n.md) 4절.
> 이 문서가 다루는 **사이드바 쪽(`layout.vue` 의 `wrapperMenus`)은 아직 그대로다.**

---

## 1. 어디서 나오는가

경고 문구 자체는 **vue-i18n 이 낸다**(`vue-i18n.js` 의 `warn()`).
우리 코드가 찍는 게 아니라, `t(키)` 가 현재 언어에서 키를 못 찾아
**대체 언어로 넘어갈 때마다** 한 줄씩 찍는다.

부르는 쪽을 실제로 잡아 봤다. 로그인 직후 한 번에 **344줄**이 찍히고, 그중 **332줄이 한 곳**이다.

```
332회  packages/effects/layouts/src/basic/layout.vue:181
  2회씩 apps/jsini-portal/src/router/routes/core.ts:81,87,93,99,105 …
```

문제의 자리:

```ts
// packages/effects/layouts/src/basic/layout.vue
function wrapperMenus(menus: MenuRecordRaw[], deep: boolean = true) {
  return deep
    ? mapTree(menus, (item) => ({ ...cloneDeep(item), name: $t(item.name) }))
    :  menus.map((item) => ({ ...cloneDeep(item), name: $t(item.name) }));
}
```

**메뉴 이름을 통째로 `$t()` 에 넣는다.** 그런데 이 포털의 메뉴 이름은
`scom.system_menus` 에서 내려오는 **그냥 한국어 글자**다 — `MSA상태정보`, `AI쳇`, `프로젝트` …
번역 키가 아니다. 그래서 **찾을 수 없는 게 정상**이고, 메뉴 하나마다 경고가 난다.

> vben 원본은 메뉴 이름이 `page.dashboard.analytics` 같은 **키**여서 이 코드가 성립한다.
> 우리는 메뉴를 DB 로 관리하면서 이름에 완성된 글자를 넣었기 때문에 어긋난다.

## 2. 왜 `en` 과 `en-US` 두 번인가

현재 설정을 실행 중인 브라우저에서 직접 읽어 봤다.

```json
{ "locale": "ko-KR",
  "fallbackLocale": { "ko-KR": ["en-US"], "default": ["en-US"] },
  "availableLocales": ["ko-KR"],
  "ko-KR 메시지 그룹 수": 15,
  "en-US 메시지 그룹 수": 0 }
```

키 하나를 못 찾으면 이렇게 흘러간다.

```
ko-KR (현재 언어, 없음)   → 경고 없음(현재 언어라서)
  → en-US (설정된 대체)    → 경고 1
    → en   (vue-i18n 이 지역 코드를 떼고 한 번 더 본다) → 경고 2
```

**`en` 은 우리가 설정한 적이 없다.** `en-US` 처럼 `언어-지역` 형태면 vue-i18n 이
지역을 뗀 `en` 도 자동으로 한 번 더 찾아본다. 그래서 같은 키가 두 줄씩 찍힌다.

**그리고 `en-US` 메시지는 아예 로드돼 있지 않다**(`0` 그룹).
`loadLocaleMessages()` 가 **현재 언어 하나만** 불러오기 때문이다.
즉 지금의 대체 언어 설정은 **찾아봐야 절대 못 찾는, 비용만 드는 경로**다.

## 3. "화면이 멈춘 것 같다" — 실제로 얼마나 느린가

같은 브라우저에서 키 200개를 번역해 시간을 쟀다.

| | 걸린 시간 |
|---|---|
| 못 찾는 키 + 경고 출력 | **67.6 ms** |
| 못 찾는 키 (`console.warn` 만 막음) | **2.9 ms** |
| 찾는 키 (정상) | 3.4 ms |

**느린 것은 찾는 일이 아니라 콘솔에 찍는 일이다 — 23배 차이다.**

메뉴가 170개면 한 번 그릴 때 344줄이고 **약 115ms 가 메인 스레드에서 통째로 막힌다.**
`wrapperMenus` 는 사이드바를 접거나 펼칠 때마다 다시 도니까 그때마다 반복된다.
**개발자 도구를 열어 두면 훨씬 더 느려진다**(콘솔이 스택까지 붙잡는다).
"멈춘 것 같다"는 느낌은 정확한 관찰이다.

---

## 4. 안 나오게 하는 방법 — 셋 중 고르기

### A. 경고만 끈다 (한 줄, 즉효)

`packages/locales/src/i18n.ts` 의 `createI18n` 에 두 줄을 더한다.

```ts
const i18n = createI18n({
  fallbackWarn: false,   // "Fall back to translate ..." 끄기
  missingWarn: false,    // "Not found ... key" 끄기
  fallbackLocale: { 'ko-KR': ['en-US'], default: ['en-US'] },
  ...
});
```

- 장점: 한 줄이고, 느려지는 원인(콘솔 출력)이 바로 사라진다.
- 단점: **진짜로 빠진 번역 키도 같이 안 보이게 된다.**
  운영에서만 끄고 개발에서는 켜 두려면 `fallbackWarn: import.meta.env.PROD ? false : true` 처럼 나눈다.
  다만 그러면 개발 중에는 여전히 느리다.

### B. 키가 아닌 것은 번역하지 않는다 (원인 제거) ✅ 권장

`wrapperMenus` 가 **키일 때만** 번역하게 바꾼다.

```ts
import { $t, $te } from '@vben/locales';

const translate = (name: string) => ($te(name) ? $t(name) : name);

function wrapperMenus(menus: MenuRecordRaw[], deep = true) {
  return deep
    ? mapTree(menus, (item) => ({ ...cloneDeep(item), name: translate(item.name) }))
    :  menus.map((item) => ({ ...cloneDeep(item), name: translate(item.name) }));
}
```

`$te`(= `i18n.global.te`)는 **키가 있는지만 확인하고 경고를 내지 않는다.**

- 장점: 경고가 사라지고, 헛수고(대체 언어 두 번 탐색)도 사라진다.
  **다른 진짜 누락 키는 계속 보인다.**
- 단점: 없음에 가깝다. 메뉴 이름이 키인 경우(`page.dashboard.analytics`)도 그대로 동작한다.
- 곁들여: `router/routes/core.ts` 의 12줄은 `$t('page.auth.login')` 처럼 **진짜 키**인데
  `en-US` 팩이 없어서 경고가 난다. 이건 B 로는 안 없어지고 **5번(언어 정리)** 으로 없어진다.

### C. A + B 를 같이 한다

B 로 원인을 없애고, 그래도 남는 잡음은 A 로 덮는다. 가장 조용하다.

**의견: B 를 먼저 하고, 그래도 시끄러우면 A 를 얹는 것**을 권한다.
B 만으로 344줄 중 332줄이 사라진다.

---

## 5. `ko-KR`/`en-US` → `ko`/`en` 으로 하나로 관리하기

지역 코드를 떼면 **대체 탐색이 한 단계로 줄어** 경고도 절반이 되고 구조도 단순해진다.
다만 **손댈 곳이 코드만이 아니다.** 정리하면 이렇다.

### 5.1 손댈 곳

| 어디 | 무엇 | 규모 |
|---|---|---|
| 언어 파일 폴더 | `packages/locales/src/langs/{ko-KR,en-US}` → `{ko,en}` | 폴더 2개 |
| | `apps/jsini-portal/src/locales/langs/{ko-KR,en-US}` → `{ko,en}` | 폴더 2개 |
| 타입 | `packages/locales/src/typing.ts` 의 `SupportedLanguagesType` | 1곳 |
| | `packages/@core/preferences/src/types.ts` | 1곳 |
| | `packages/@core/composables/src/use-simple-locale/messages.ts` | 1곳 |
| 목록 | `packages/constants/src/core.ts` 의 `SUPPORT_LANGUAGES` | 1곳 |
| 기본값 | `packages/@core/preferences/src/config.ts` 의 `locale: 'ko-KR'` | 1곳 |
| 대체 설정 | `packages/locales/src/i18n.ts` 의 `fallbackLocale` | 1곳 |
| 앱 변환 | `apps/jsini-portal/src/locales/index.ts` 의 `ko → ko-KR` 매핑 **삭제** | 1곳 |
| 곁다리 팩 | dayjs · antd · vxe-table 언어 분기 (`case 'ko-KR': case 'ko':`) | 3곳 |
| 그 밖 | 문자열이 박힌 화면들 | **소스 24개 파일** |
| **DB** | `scom.i18n_resources.locale` | **1,259행** (ko-KR 634 · en-US 625) |
| 사용자 | 브라우저에 저장된 환경설정의 `app.locale` | 쓰던 사람 전부 |

### 5.2 순서

1. **타입·상수·기본값**을 `ko`/`en` 으로 바꾼다.
2. **언어 폴더 이름**을 바꾼다(`git mv`). 파일 내용은 그대로다.
3. `fallbackLocale` 을 `{ ko: ['en'], default: ['en'] }` 로 바꾼다.
4. 앱의 `ko → ko-KR` 변환을 **지운다**. dayjs/antd/vxe 분기에서 `-KR`/`-US` 가지를 지운다.
5. **DB 를 옮긴다.** 반복 실행해도 안전하게:

   ```sql
   -- docs/sql/i18n_locale_shorten.sql (아직 만들지 않았다)
   BEGIN;
   UPDATE scom.i18n_resources SET locale = 'ko' WHERE locale = 'ko-KR';
   UPDATE scom.i18n_resources SET locale = 'en' WHERE locale = 'en-US';
   COMMIT;
   ```

6. **이미 쓰던 사람을 위한 보정.** 브라우저에 `ko-KR` 이 저장돼 있으면
   그 사람만 언어를 못 찾는다. 설정을 읽을 때 한 번 다듬어 준다.

   ```ts
   // @core/preferences 의 initPreferences 에서 캐시를 읽은 직후
   const SHORTEN: Record<string, string> = { 'ko-KR': 'ko', 'en-US': 'en' };
   const locale = cached?.app?.locale;
   if (locale && SHORTEN[locale]) cached.app.locale = SHORTEN[locale];
   ```

   이 보정은 **한두 달 뒤에 지워도 된다.**

7. `document.documentElement.lang` 도 짧은 값으로 나간다(이미 `setI18nLanguage` 가 넣는다).

### 5.3 곁들여 정해야 할 것 🟠

**대체 언어를 살릴 것인가.** 지금은 `en-US` 팩을 아예 안 불러와서 대체가 무의미하다.
`ko` 에 없는 키를 `en` 으로 보여 주고 싶다면 **시작할 때 `en` 을 같이 로드해야** 한다
(번들이 조금 커진다). 아니면 대체 자체를 끄는 편이 정직하다.

| | 방법 | 비고 |
|---|---|---|
| **A** | 대체를 끈다 (`fallbackLocale: false`) | 없는 키는 키 그대로 보인다. 가장 단순하고 빠르다 |
| B | `en` 을 항상 함께 로드한다 | 대체가 실제로 동작한다. 번들 +α |

### 5.4 얼마나 걸리나

코드 변경은 반나절, **검증이 그보다 오래 걸린다** — 화면 200곳의 문구,
다국어 관리 화면, 이미 쓰던 브라우저에서의 동작을 봐야 한다.
**DB 를 건드리므로 되돌릴 지점을 잡고 시작하는 것**을 권한다.

---

## 6. 처리 결과 (2026-08-23)

지시대로 **4-B** 와 **5번(ko/en 통합)** 을 모두 적용했다. 경고를 끄는 방법(4-A)은 쓰지 않았다 —
원인을 없앴기 때문에 필요가 없었고, 앞으로 **진짜 누락 키는 그대로 보인다.**

```
로그인 직후 경고        344줄  →  0줄
화면 6곳을 돌아본 뒤     (계속)  →  0줄
```

### 6.1 키일 때만 번역 (4-B)

`packages/locales` 에 공용 헬퍼를 두고, DB 에서 온 이름을 `$t()` 에 넣던 자리를 전부 바꿨다.

```ts
/** 번역 키일 때만 번역한다. 키가 아니면 받은 글자를 그대로 돌려준다. */
function $tIfKey(text?: null | string) {
  if (!text) return '';
  return i18n.global.te(text) ? i18n.global.t(text) : text;
}
```

| 어디 | 무엇 |
|---|---|
| `layouts/src/basic/layout.vue` | 사이드바 메뉴 이름 (경고 344줄 중 332줄이 여기였다) |
| `layouts/src/basic/tabbar/use-tabbar.ts` | 탭 제목 |
| `layouts/src/widgets/breadcrumb.vue` | 브레드크럼 |
| `layouts/src/widgets/global-search/search-panel.vue` | 전역 검색 결과 |
| `apps/.../bootstrap.ts` | 브라우저 탭(문서) 제목 |

곁들여 `apps/.../router/routes/core.ts` 의 `title: $t('page.auth.login')` 5곳을
**키 문자열 그대로** 두도록 바꿨다. 라우트 파일은 언어 팩이 실리기 **전에** 읽히기 때문에
그 시점의 `$t()` 는 반드시 실패했다. 화면에 그릴 때 위 헬퍼들이 번역한다 —
브라우저 탭 제목이 `로그인 - JSINI ADMIN` 으로 잘 나오는 것을 확인했다.

### 6.2 `ko` / `en` 으로 통합 (5번)

| 어디 | 무엇 |
|---|---|
| 언어 폴더 | `langs/{ko-KR,en-US}` → `{ko,en}` (앞단 두 곳) |
| 타입 | `SupportedLanguagesType = 'en' \| 'ko'` (locales · preferences · use-simple-locale) |
| 목록 | `SUPPORT_LANGUAGES` — **`ab-AB`(키확인용) 항목도 함께 뺐다** (타입에 안 맞았다) |
| 기본값 | `preferences.app.locale = 'ko'` |
| 대체 사슬 | `{ ko: ['en'], default: ['en'] }` |
| 앱 | `ko → ko-KR` 변환 삭제, dayjs·antd 분기에서 긴 코드 가지 제거 |
| 다국어 화면 | 관리 화면·편집 팝업의 `ko-KR`/`en-US` 값 |
| 백엔드 | `I18nResourceService` 의 영어 대체 조회, `LLMService` 의 언어 판정 |
| **DB** | `docs/sql/i18n_locale_shorten.sql` — **적용함** (ko 634 · en 625) |

**Intl 표준 태그는 건드리지 않았다.** `new Date().toLocaleString('ko-KR')` 같은 자리와
vxe-table 이 요구하는 자기 규격(`vxe-table/es/locale/lang/ko-KR`)은 그대로다.
우리 코드(`ko`) → vxe 규격(`ko-KR`) 로 옮기는 표를 두 곳에 뒀다.

**이미 쓰던 브라우저 보정.** 로컬스토리지에 `ko-KR` 이 남아 있으면 그 사람만 언어를 못 찾는다.
`apps/jsini-portal/src/locales/index.ts` 의 `shortenLocale()` 이 읽는 자리에서 한 번 다듬는다.
**한동안 지나면 지워도 되는 코드**라고 주석에 적어 뒀다.

### 6.3 확인

```
콘솔 경고    344줄 → 0줄 (로그인 직후 · 화면 6곳 이동 후 모두)
i18n 상태    locale=ko · fallback={ko:['en']} · html lang="ko"
번역 동작    common.confirm → "확인"
문서 제목    "로그인 - JSINI ADMIN" · "요청 처리 - JSINI ADMIN"
다국어 관리   DB 이관 뒤 en/ko 로 정상 조회 (30행 확인)
타입 검사    오류 파일 25개 (통합 전 26개 — SUPPORT_LANGUAGES 오류가 없어졌다)
단위 테스트   456개 전부 통과
빌드         프론트 vite build 성공 · 백엔드 dotnet build 오류 0
```

### 6.4 남은 것 🟡

**대체 언어(`en`) 팩은 여전히 로드하지 않는다** — 현재 언어 하나만 불러온다.
지금은 못 찾는 키가 없어서 문제가 안 되지만, 앞으로 `ko` 에만 있고 `en` 에 없는 키가 생기면
대체가 동작하지 않는다. 5.3 의 선택지가 그대로 남아 있다(A: 대체를 끈다 · B: `en` 을 함께 로드).
**지금 당장 급하지 않아 손대지 않았다.**

---

## 7. 환경설정 화면에 다국어 키가 그대로 보이던 것 (2026-08-25)

### 7.1 증상

설정(톱니) → **레이아웃 → 위젯** 목록의 select 항목에 키가 그대로 나왔다.

```
전역 검색 사용   preferences.widget.header / preferences.widget.userDropdown / common.notShow
설정             자동 · 헤더 · 고정 은 정상, 나머지 둘이
                 preferences.position.userDropdown / common.notShow
```

### 7.2 원인

**vben 상위 동기화(`a8e93fa`, 2026-08-23)가 넣은 새 키가 `ko` 팩에 없었다.**
`en` 에는 다 있다. 6.4 에 적어 둔 위험이 그대로 실현된 것이다 — 대체 언어 팩을
로드하지 않으므로 `ko` 에 없는 키는 `en` 으로 대체되지 못하고 키가 그대로 찍힌다.

### 7.3 어떻게 찾았나

눈에 보이는 것만 고치면 같은 문제가 다른 화면에 남는다. `en` ↔ `ko` 키를 양방향으로
비교하고, **소스에서 실제로 참조하는 키만** 골라냈다.

```
en ↔ ko 키 차이            72건
그중 코드가 참조하는 것     13건  ← 실제로 화면에 나오는 것
참조하지 않는 것            59건  ← 화면에 안 나오므로 무해
```

키 이름이 다른 키의 접두어인 경우가 있어 단순 문자열 포함으로 세면 안 된다.
`authentication.naver` 는 안 쓰이고 `authentication.naverLogin` 이 쓰인다
(둘 다 있는 키다). 뒤에 영숫자가 이어지면 다른 키로 보도록 걸러야 한다.

### 7.4 고친 것

`ko` 에 10개, `en` 에 2개를 채웠다.

| 파일 | 키 | 값 |
|---|---|---|
| ko/common.json | `notShow` | 표시 안 함 |
| ko/preferences.json | `timezone` | 시간대 |
| | `position.userDropdown` | 사용자 메뉴 |
| | `shortcutKeys.escape` | 현재 창 닫기 |
| | `widget.header` | 헤더 |
| | `widget.userDropdown` | 사용자 메뉴 |
| | `widget.hidden` | 숨김 |
| | `widget.timezone` | 시간대 사용 |
| | `widget.logoutButtonPosition` | 로그아웃 버튼 위치 |
| en/common.json | `expandAll` | Expand All |
| | `collapseAll` | Collapse All |

`expandAll`·`collapseAll` 은 **반대 방향 구멍**이었다. `ko` 에만 있고 `en` 에 없어서
메뉴 관리 화면(`portal/system/menu/list.vue`)을 영어로 보면 키가 찍혔다.

기존 표기와 맞췄다 — `position.header` 가 이미 "헤더" 라서 `widget.header` 도 "헤더" 로,
아바타 드롭다운은 "사용자 메뉴" 로 했다.

### 7.5 손대지 않은 것

`ui.tiptap.*` 36개는 **tiptap 편집기 컴포넌트가 이 저장소에 없다.** 상위에만 있고
동기화 때 문자열만 들어왔다. 쓰는 곳이 생기면 그때 번역하면 된다.
`preferences.antd.*` 10개도 참조하는 코드가 없다.

### 7.6 확인

실제 화면(:5555)에서 드로어를 열어 확인했다.

```
위젯 섹션 전체        남은 키 0개 (전부 한국어)
전역 검색 사용 선택지  헤더 / 사용자 메뉴 / 표시 안 함        ← 3개 모두
설정 선택지            자동 / 헤더 / 고정 / 사용자 메뉴 / 표시 안 함  ← 5개 모두
키 비교 재실행        코드가 렌더링하는 미번역 키 0건
빌드                  pnpm vite build 성공
```

### 7.7 앞으로도 생길 문제다 🟡

상위 동기화를 할 때마다 새 키가 `en` 에만 들어오고 `ko` 는 비는 일이 반복된다.
**6.4 의 선택지가 여전히 유효하다** — `en` 팩을 함께 로드해 대체가 동작하게 하면
적어도 키가 그대로 찍히는 대신 영어로 보인다. 7.3 의 비교를 동기화 뒤에 한 번
돌리는 것으로도 잡을 수 있다.
