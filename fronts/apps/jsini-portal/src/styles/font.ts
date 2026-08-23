/**
 * 시스템 글꼴 — **여기 한 곳에서만 정한다.**
 *
 * 같은 값을 두 곳에서 써야 한다.
 *   1) CSS 변수 `--font-family` — body 와 우리가 만든 화면
 *   2) ant-design-vue 테마 토큰 `fontFamily` — antd 가 만들어 내는 규칙
 *
 * antd 는 CSS-in-JS 로 거의 모든 컴포넌트에 자기 `font-family` 를 넣기 때문에,
 * CSS 변수만 바꾸면 antd 컴포넌트는 antd 기본 글꼴로 남는다(실제로 26,000개가 그랬다).
 * 두 곳이 어긋나지 않도록 값을 여기 한 번만 적고 양쪽에서 가져다 쓴다.
 *
 * 사용자가 환경설정에서 고르는 값(`preferences.app.fontFamily`)은 '열쇠'다.
 * 실제 글꼴 목록은 아래 표가 정한다 — 프레임워크 쪽 환경설정 부품이
 * 특정 글꼴 이름을 알고 있을 이유가 없기 때문이다.
 *
 * 글꼴 파일은 `public/fonts/` 에 있다(내부망에서도 쓰려고 저장소에 넣어 두었다).
 */

/** 어느 글꼴을 고르든 마지막에 붙는 기본 묶음. 글꼴 파일을 못 읽어도 화면이 깨지지 않게 한다. */
const SYSTEM_STACK = [
  '-apple-system',
  'blinkmacsystemfont',
  "'Segoe UI'",
  'roboto',
  "'Helvetica Neue'",
  'arial',
  "'Noto Sans'",
  'sans-serif',
  "'Apple Color Emoji'",
  "'Segoe UI Emoji'",
  "'Segoe UI Symbol'",
  "'Noto Color Emoji'",
];

/** 환경설정에서 고를 수 있는 값 */
export type FontFamilyKey = 'Play' | 'S-CoreDream' | 'system';

/** 고른 값 → 실제 글꼴 목록 */
const STACKS: Record<FontFamilyKey, string[]> = {
  // 기본. 한글·라틴을 모두 갖고 있어 화면 대부분이 이 글꼴로 나온다.
  'S-CoreDream': ["'S-CoreDream'", "'Play'", ...SYSTEM_STACK],
  // 라틴 전용이라 한글은 S-CoreDream 이 받는다.
  Play: ["'Play'", "'S-CoreDream'", ...SYSTEM_STACK],
  // 내려받은 글꼴을 쓰지 않고 장비의 기본 글꼴을 쓴다.
  system: SYSTEM_STACK,
};

/** 기본값. 환경설정에 값이 없거나 모르는 값이면 이것을 쓴다. */
export const DEFAULT_FONT_FAMILY: FontFamilyKey = 'S-CoreDream';

/** 고른 값에 해당하는 `font-family` 문자열을 돌려준다. */
export function resolveFontFamily(key?: null | string): string {
  const stack = STACKS[(key ?? '') as FontFamilyKey] ?? STACKS[DEFAULT_FONT_FAMILY];
  return stack.join(', ');
}

/** 기본 글꼴 목록. CSS 가 아직 못 읽는 시점(초기 렌더)에 쓰는 값이다. */
export const FONT_FAMILY = resolveFontFamily(DEFAULT_FONT_FAMILY);
