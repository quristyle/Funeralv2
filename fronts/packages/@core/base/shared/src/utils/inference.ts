import { isFunction, isObject, isString } from '@vue/shared';

/**
 * 전달된 값이 undefined인지 확인합니다.
 *
 * @param {unknown} value 검사할 값.
 * @returns {boolean} 값이 undefined이면 true를 반환하고, 그렇지 않으면 false를 반환합니다.
 */
function isUndefined(value?: unknown): value is undefined {
  return value === undefined;
}

/**
 * 전달된 값이 boolean인지 확인합니다.
 * @param value
 * @returns 값이 불리언이면 true를 반환하고, 그렇지 않으면 false를 반환합니다.
 */
function isBoolean(value: unknown): value is boolean {
  return typeof value === 'boolean';
}

/**
 * 전달된 값이 비어 있는지 확인합니다.
 *
 * 다음의 경우 비어 있는 것으로 간주됩니다:
 * - 값이 null인 경우.
 * - 값이 undefined인 경우.
 * - 값이 빈 문자열인 경우.
 * - 값이 길이가 0인 배열인 경우.
 * - 요소가 없는 Map 또는 Set인 경우.
 * - 속성이 없는 객체인 경우.
 *
 * @param {T} value 검사할 값.
 * @returns {boolean} 값이 비어 있으면 true를 반환하고, 그렇지 않으면 false를 반환합니다.
 */
function isEmpty<T = unknown>(value?: T): value is T {
  if (value === null || value === undefined) {
    return true;
  }

  if (Array.isArray(value) || isString(value)) {
    return value.length === 0;
  }

  if (value instanceof Map || value instanceof Set) {
    return value.size === 0;
  }

  if (isObject(value)) {
    return Object.keys(value).length === 0;
  }

  return false;
}

/**
 * 전달된 문자열이 유효한 HTTP 또는 HTTPS URL인지 확인합니다.
 *
 * @param {string} url 검사할 문자열.
 * @return {boolean} 문자열이 유효한 HTTP 또는 HTTPS URL이면 true를 반환하고, 그렇지 않으면 false를 반환합니다.
 */
function isHttpUrl(url?: string): boolean {
  if (!url) {
    return false;
  }
  // 정규식을 사용하여 URL이 http:// 또는 https:// 로 시작하는지 테스트합니다.
  const httpRegex = /^https?:\/\/.*$/;
  return httpRegex.test(url);
}

/**
 * 전달된 값이 window 객체인지 확인합니다.
 *
 * @param {any} value 검사할 값.
 * @returns {boolean} 값이 window 객체이면 true를 반환하고, 그렇지 않으면 false를 반환합니다.
 */
function isWindow(value: any): value is Window {
  return (
    typeof window !== 'undefined' && value !== null && value === value.window
  );
}

/**
 * 현재 실행 환경이 Mac OS인지 확인합니다.
 *
 * 이 함수는 navigator.userAgent 문자열을 검사하여 현재 실행 환경을 판단합니다.
 * userAgent 문자열에 "macintosh" 또는 "mac os x"(대소문자 구분 없음)가 포함되어 있으면 현재 환경을 Mac OS로 간주합니다.
 *
 * @returns {boolean} 현재 환경이 Mac OS이면 true를 반환하고, 그렇지 않으면 false를 반환합니다.
 */
function isMacOs(): boolean {
  const macRegex = /macintosh|mac os x/i;
  return macRegex.test(navigator.userAgent);
}

/**
 * 현재 실행 환경이 Windows OS인지 확인합니다.
 *
 * 이 함수는 navigator.userAgent 문자열을 검사하여 현재 실행 환경을 판단합니다.
 * userAgent 문자열에 "windows" 또는 "win32"(대소문자 구분 없음)가 포함되어 있으면 현재 환경을 Windows OS로 간주합니다.
 *
 * @returns {boolean} 현재 환경이 Windows OS이면 true를 반환하고, 그렇지 않으면 false를 반환합니다.
 */
function isWindowsOs(): boolean {
  const windowsRegex = /windows|win32/i;
  return windowsRegex.test(navigator.userAgent);
}

/**
 * 전달된 값이 숫자인지 확인합니다.
 * @param value
 */
function isNumber(value: any): value is number {
  return typeof value === 'number' && Number.isFinite(value);
}

/**
 * Returns the first value in the provided list that is neither `null` nor `undefined`.
 *
 * This function iterates over the input values and returns the first one that is
 * not strictly equal to `null` or `undefined`. If all values are either `null` or
 * `undefined`, it returns `undefined`.
 *
 * @template T - The type of the input values.
 * @param {...(T | null | undefined)[]} values - A list of values to evaluate.
 * @returns {T | undefined} - The first value that is not `null` or `undefined`, or `undefined` if none are found.
 *
 * @example
 * // Returns 42 because it is the first non-null, non-undefined value.
 * getFirstNonNullOrUndefined(undefined, null, 42, 'hello'); // 42
 *
 * @example
 * // Returns 'hello' because it is the first non-null, non-undefined value.
 * getFirstNonNullOrUndefined(null, undefined, 'hello', 123); // 'hello'
 *
 * @example
 * // Returns undefined because all values are either null or undefined.
 * getFirstNonNullOrUndefined(undefined, null); // undefined
 */
function getFirstNonNullOrUndefined<T>(
  ...values: (null | T | undefined)[]
): T | undefined {
  for (const value of values) {
    if (value !== undefined && value !== null) {
      return value;
    }
  }
  return undefined;
}

export {
  getFirstNonNullOrUndefined,
  isBoolean,
  isEmpty,
  isFunction,
  isHttpUrl,
  isMacOs,
  isNumber,
  isObject,
  isString,
  isUndefined,
  isWindow,
  isWindowsOs,
};
