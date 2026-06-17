import type { RouteLocationNormalized } from 'vue-router';

export interface TabDefinition extends RouteLocationNormalized {
  /**
   * 탭 페이지의 키
   */
  key?: string;
}
