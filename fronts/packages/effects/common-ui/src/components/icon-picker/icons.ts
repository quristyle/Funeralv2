import type { Recordable } from '@vben/types';

/**
 * 페이지를 새로고침하지 않을 때 원격 인터페이스를 중복 요청하지 않도록 하는 캐시 객체
 */
export const ICONS_MAP: Recordable<string[]> = {};

interface IconifyResponse {
  prefix: string;
  total: number;
  title: string;
  uncategorized?: string[];
  categories?: Recordable<string[]>;
  aliases?: Recordable<string>;
}

const PENDING_REQUESTS: Recordable<Promise<string[]>> = {};

/**
 * Iconify 인터페이스를 통해 아이콘 세트 데이터를 가져옵니다.
 * 동시에 여러 아이콘 선택기가 동일한 아이콘 세트를 요청할 경우, 실제로는 한 번의 요청만 발생합니다 (모든 요청이 동일한 결과를 공유함).
 * 요청 결과는 캐시되며, 페이지를 새로고침하기 전까지 동일한 아이콘 세트는 다시 요청되지 않습니다.
 * @param prefix 아이콘 세트 이름
 * @returns 아이콘 세트에 포함된 모든 아이콘 이름
 */
export async function fetchIconsData(prefix: string): Promise<string[]> {
  if (Reflect.has(ICONS_MAP, prefix) && ICONS_MAP[prefix]) {
    return ICONS_MAP[prefix];
  }
  if (Reflect.has(PENDING_REQUESTS, prefix) && PENDING_REQUESTS[prefix]) {
    return PENDING_REQUESTS[prefix];
  }
  PENDING_REQUESTS[prefix] = (async () => {
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 1000 * 10);
      const response: IconifyResponse = await fetch(
        `https://api.iconify.design/collection?prefix=${prefix}`,
        { signal: controller.signal },
      ).then((res) => res.json());
      clearTimeout(timeoutId);
      const list = response.uncategorized || [];
      if (response.categories) {
        for (const category in response.categories) {
          list.push(...(response.categories[category] || []));
        }
      }
      ICONS_MAP[prefix] = list.map((v) => `${prefix}:${v}`);
    } catch (error) {
      console.error(`Failed to fetch icons for prefix ${prefix}:`, error);
      return [] as string[];
    }
    return ICONS_MAP[prefix];
  })();
  return PENDING_REQUESTS[prefix];
}
