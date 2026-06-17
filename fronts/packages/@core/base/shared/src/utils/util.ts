export function bindMethods<T extends object>(instance: T): void {
  const prototype = Object.getPrototypeOf(instance);
  const propertyNames = Object.getOwnPropertyNames(prototype);

  propertyNames.forEach((propertyName) => {
    const descriptor = Object.getOwnPropertyDescriptor(prototype, propertyName);
    const propertyValue = instance[propertyName as keyof T];

    if (
      typeof propertyValue === 'function' &&
      propertyName !== 'constructor' &&
      descriptor &&
      !descriptor.get &&
      !descriptor.set
    ) {
      instance[propertyName as keyof T] = propertyValue.bind(instance);
    }
  });
}

/**
 * 중첩된 객체의 필드 값 가져오기
 * @param obj - 찾고자 하는 객체
 * @param path - 필드를 찾기 위한 경로, 마침표(.)로 구분
 * @returns 필드 값, 찾지 못한 경우 undefined 반환
 */
export function getNestedValue<T>(obj: T, path: string): any {
  if (typeof path !== 'string' || path.length === 0) {
    throw new Error('Path must be a non-empty string');
  }
  // 경로 문자열을 "."으로 나누어 배열로 변환
  const keys = path.split('.') as (number | string)[];

  let current: any = obj;

  for (const key of keys) {
    if (current === null || current === undefined) {
      return undefined;
    }
    current = current[key as keyof typeof current];
  }

  return current;
}
