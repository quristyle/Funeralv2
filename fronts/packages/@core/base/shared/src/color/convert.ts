import { TinyColor } from '@ctrl/tinycolor';

/**
 * 색상을 HSL 형식으로 변환합니다.
 *
 * HSL은 색상(Hue), 채도(Saturation), 명도(Lightness)의 세 부분으로 구성된 색상 모델입니다.
 *
 * @param {string} color 입력 색상.
 * @returns {string} HSL 형식의 색상 문자열.
 */
function convertToHsl(color: string): string {
  const { a, h, l, s } = new TinyColor(color).toHsl();
  const hsl = `hsl(${Math.round(h)} ${Math.round(s * 100)}% ${Math.round(l * 100)}%)`;
  return a < 1 ? `${hsl} ${a}` : hsl;
}

/**
 * 색상을 HSL CSS 변수로 변환합니다.
 *
 * 이 함수는 convertToHsl 함수와 유사하지만, 반환되는 문자열 형식이 약간 다릅니다.
 * CSS 변수로 사용할 수 있도록 하기 위함입니다.
 *
 * @param {string} color 입력 색상.
 * @returns {string} CSS 변수로 사용할 수 있는 HSL 형식의 색상 문자열.
 */
function convertToHslCssVar(color: string): string {
  const { a, h, l, s } = new TinyColor(color).toHsl();
  const hsl = `${Math.round(h)} ${Math.round(s * 100)}% ${Math.round(l * 100)}%`;
  return a < 1 ? `${hsl} / ${a}` : hsl;
}

/**
 * 색상을 RGB 색상 문자열로 변환합니다.
 * TinyColor는 hsl 내에 'deg', 'grad', 'rad' 또는 'turn'이 포함된 문자열을 처리할 수 없습니다.
 * 예를 들어, hsl(231deg 98% 65%)은 rgb(0, 0, 0)으로 해석됩니다.
 * 여기서는 변환 전에 이러한 단위를 먼저 제거합니다.
 * @param str HSL 색상값을 나타내는 문자열
 * @returns 색상값이 유효하면 해당 RGB 색상 문자열을 반환하고, 유효하지 않으면 rgb(0, 0, 0)을 반환합니다.
 */
function convertToRgb(str: string): string {
  return new TinyColor(str.replaceAll(/deg|grad|rad|turn/g, '')).toRgbString();
}

/**
 * 색상의 유효성 확인
 * @param {string} color - 확인할 색상
 * 색상이 유효하면 true를 반환하고, 그렇지 않으면 false를 반환합니다.
 */
function isValidColor(color?: string) {
  if (!color) {
    return false;
  }
  return new TinyColor(color).isValid;
}

export {
  convertToHsl,
  convertToHslCssVar,
  convertToRgb,
  isValidColor,
  TinyColor,
};
