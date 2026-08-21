import type { Preferences } from './types';

import { generatorColorVariables } from '@vben-core/shared/color';
import { updateCSSVariables as executeUpdateCSSVariables } from '@vben-core/shared/utils';

import { BUILT_IN_THEME_PRESETS } from './constants';

/**
 * 테마의 CSS 변수 및 기타 CSS 변수를 업데이트합니다.
 * @param preferences - 현재 환경 설정 객체이며, 해당 테마 값은 문서의 테마를 설정하는 데 사용됩니다.
 */
function updateCSSVariables(preferences: Preferences) {
  // 색상 변수가 수정될 때 CSS 변수 업데이트
  const root = document.documentElement;
  if (!root) {
    return;
  }

  const theme = preferences?.theme ?? {};

  const { builtinType, mode, radius } = theme;

  // html에 dark 클래스 설정
  if (Reflect.has(theme, 'mode')) {
    const dark = isDarkTheme(mode);
    root.classList.toggle('dark', dark);
  }

  // html에 data-theme=[builtinType] 설정
  if (Reflect.has(theme, 'builtinType')) {
    const rootTheme = root.dataset.theme;
    if (rootTheme !== builtinType) {
      root.dataset.theme = builtinType;
    }
  }

  // 현재 기본 제공 테마 가져오기
  const currentBuiltType = [...BUILT_IN_THEME_PRESETS].find(
    (item) => item.type === builtinType,
  );

  let builtinTypeColorPrimary: string | undefined = '';

  if (currentBuiltType) {
    const isDark = isDarkTheme(preferences.theme.mode);
    // 다양한 테마의 기본 색상 설정
    const color = isDark
      ? currentBuiltType.darkPrimaryColor || currentBuiltType.primaryColor
      : currentBuiltType.primaryColor;
    builtinTypeColorPrimary = color || currentBuiltType.color;
  }

  // 기본 제공 테마 색상과 사용자 정의 색상이 모두 존재하지 않으면 테마 색상을 업데이트하지 않음
  if (
    builtinTypeColorPrimary ||
    Reflect.has(theme, 'colorPrimary') ||
    Reflect.has(theme, 'colorDestructive') ||
    Reflect.has(theme, 'colorSuccess') ||
    Reflect.has(theme, 'colorWarning')
  ) {
    // 기본 제공 테마를 고른 경우에는 그 테마가 밝기별로 들고 있는 색을 쓴다.
    // 예를 들어 gray 테마는 라이트에서 hsl(240 5.9% 10%), 다크에서 hsl(0 0% 98%) 이다.
    // 이렇게 해야 모드를 바꿀 때 색이 따라 뒤집힌다.
    //
    // 계산한 값을 preferences 에 다시 써넣지는 않는다.
    // 써넣으면 그 순간의 밝기에 맞춘 색이 저장소에 굳어서,
    // 다음에 모드를 바꿔도 예전 색이 그대로 남는다.
    updateMainColorVariables(preferences, builtinTypeColorPrimary);
  }

  // 둥근 모서리 업데이트
  if (Reflect.has(theme, 'radius')) {
    document.documentElement.style.setProperty('--radius', `${radius}rem`);
  }

  // 글꼴 크기 업데이트
  if (Reflect.has(theme, 'fontSize')) {
    const fontSize = theme.fontSize;
    document.documentElement.style.setProperty(
      '--font-size-base',
      `${fontSize}px`,
    );
    document.documentElement.style.setProperty(
      '--menu-font-size',
      `calc(${fontSize}px * 0.875)`,
    );
  }
}

/**
 * 주요 CSS 변수를 업데이트합니다.
 * @param preference - 현재 환경 설정 객체이며, 해당 색상 값은 HSL 형식으로 변환되어 CSS 변수로 설정됩니다.
 * @param builtinPrimary - 기본 제공 테마가 지금 밝기에 맞춰 정한 주 색상.
 *                         값이 있으면 사용자 지정 색보다 우선한다.
 */
function updateMainColorVariables(
  preference: Preferences,
  builtinPrimary?: string,
) {
  if (!preference.theme) {
    return;
  }
  const { colorDestructive, colorPrimary, colorSuccess, colorWarning } =
    preference.theme;

  const effectivePrimary = builtinPrimary || colorPrimary;

  const colorVariables = generatorColorVariables([
    { color: effectivePrimary, name: 'primary' },
    { alias: 'warning', color: colorWarning, name: 'yellow' },
    { alias: 'success', color: colorSuccess, name: 'green' },
    { alias: 'destructive', color: colorDestructive, name: 'red' },
  ]);

  // 설정할 CSS 변수 매핑
  const colorMappings = {
    '--green-500': '--success',
    '--primary-500': '--primary',
    '--red-500': '--destructive',
    '--yellow-500': '--warning',
  };

  // 색상 변수 업데이트 일괄 처리
  Object.entries(colorMappings).forEach(([sourceVar, targetVar]) => {
    const colorValue = colorVariables[sourceVar];
    if (colorValue) {
      document.documentElement.style.setProperty(targetVar, colorValue);
    }
  });

  executeUpdateCSSVariables(colorVariables);
}

function isDarkTheme(theme: string) {
  let dark = theme === 'dark';
  if (theme === 'auto') {
    dark = window.matchMedia('(prefers-color-scheme: dark)').matches;
  }
  return dark;
}

export { isDarkTheme, updateCSSVariables };
