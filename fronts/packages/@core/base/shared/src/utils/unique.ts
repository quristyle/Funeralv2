/**
 * 지정된 필드에 따라 객체 배열의 중복 제거
 * @param arr 중복을 제거할 객체 배열
 * @param key 중복 제거 기준 필드명
 * @returns 중복이 제거된 객체 배열
 */
function uniqueByField<T>(arr: T[], key: keyof T): T[] {
  const seen = new Map<any, T>();
  return arr.filter((item) => {
    const value = item[key];
    return seen.has(value) ? false : (seen.set(value, item), true);
  });
}

export { uniqueByField };
