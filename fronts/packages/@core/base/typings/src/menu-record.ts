import type { Component } from 'vue';
import type { RouteRecordRaw } from 'vue-router';

import type { Recordable } from './helper';

/**
 * 기본 라우트 객체 확장
 */
type ExRouteRecordRaw = RouteRecordRaw & {
  parent?: string;
  parents?: string[];
  path?: any;
};

interface MenuRecordBadgeRaw {
  /**
   * 배지
   */
  badge?: string;
  /**
   * 배지 타입
   */
  badgeType?: 'dot' | 'normal';
  /**
   * 배지 색상
   */
  badgeVariants?: 'destructive' | 'primary' | string;
}

/**
 * 기본 메뉴 객체
 */
interface MenuRecordRaw extends MenuRecordBadgeRaw {
  /**
   * 활성화 시 아이콘 이름
   */
  activeIcon?: string;
  /**
   * 하위 메뉴
   */
  children?: MenuRecordRaw[];
  /**
   * 메뉴 비활성화 여부
   * @default false
   */
  disabled?: boolean;
  /**
   * 아이콘 이름
   */
  icon?: Component | string;
  /**
   * 메뉴 이름
   */
  name: string;
  /**
   * 정렬 번호
   */
  order?: number;
  /**
   * 부모 경로
   */
  parent?: string;
  /**
   * 모든 부모 경로
   */
  parents?: string[];
  /**
   * 메뉴 경로, 유일하며 키로 사용 가능
   */
  path: string;
  /**
   * 메뉴 파라미터
   */
  query?: Recordable<any>;
  /**
   * 메뉴 표시 여부
   * @default true
   */
  show?: boolean;
}

export type { ExRouteRecordRaw, MenuRecordBadgeRaw, MenuRecordRaw };
