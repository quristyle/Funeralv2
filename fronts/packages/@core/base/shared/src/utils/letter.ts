/**
 * 문자열의 첫 글자를 대문자로 변환
 * @param string
 */
function capitalizeFirstLetter(string: string): string {
  return string.charAt(0).toUpperCase() + string.slice(1);
}

/**
 * 문자열의 첫 글자를 소문자로 변환합니다.
 *
 * @param str 변환할 문자열
 * @returns 첫 글자가 소문자인 문자열
 */
function toLowerCaseFirstLetter(str: string): string {
  if (!str) return str; // 문자열이 비어있으면 즉시 반환
  return str.charAt(0).toLowerCase() + str.slice(1);
}

/**
 *  카멜 케이스(CamelCase) 키 이름 생성
 * @param key
 * @param parentKey
 */
function toCamelCase(key: string, parentKey: string): string {
  if (!parentKey) {
    return key;
  }
  return parentKey + key.charAt(0).toUpperCase() + key.slice(1);
}

function kebabToCamelCase(str: string): string {
  return str
    .split('-')
    .filter(Boolean)
    .map((word, index) =>
      index === 0 ? word : word.charAt(0).toUpperCase() + word.slice(1),
    )
    .join('');
}

export {
  capitalizeFirstLetter,
  kebabToCamelCase,
  toCamelCase,
  toLowerCaseFirstLetter,
};
