import type { Component } from 'vue';
import type { Router, RouteRecordRaw } from 'vue-router';

interface RouteMeta {
  /**
   * 활성 아이콘 (메뉴/탭)
   */
  activeIcon?: string;
  /**
   * 현재 활성화된 메뉴, 기존 메뉴 대신 부모 메뉴를 활성화하고 싶을 때 사용
   */
  activePath?: string;
  /**
   * 탭 페이지 고정 여부
   * @default false
   */
  affixTab?: boolean;
  /**
   * 고정된 탭 페이지 순서
   * @default 0
   */
  affixTabOrder?: number;
  /**
   * 액세스를 위해 특정 역할 식별자가 필요함
   * @default []
   */
  authority?: string[];
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
  badgeVariants?:
    | 'default'
    | 'destructive'
    | 'primary'
    | 'success'
    | 'warning'
    | string;
  /**
   * 라우트에 대응하는 DOM 캐시 여부
   */
  domCached?: boolean;
  /**
   * 라우트의 전체 경로를 키로 사용 (기본값 true)
   */
  fullPathKey?: boolean;
  /**
   * 현재 라우트의 하위 항목을 메뉴에 표시하지 않음
   * @default false
   */
  hideChildrenInMenu?: boolean;
  /**
   * 현재 라우트를 브레드크럼에 표시하지 않음
   * @default false
   */
  hideInBreadcrumb?: boolean;
  /**
   * 현재 라우트를 메뉴에 표시하지 않음
   * @default false
   */
  hideInMenu?: boolean;
  /**
   * 현재 라우트를 탭 페이지에 표시하지 않음
   * @default false
   */
  hideInTab?: boolean;
  /**
   * 아이콘 (메뉴/탭)
   */
  icon?: Component | string;
  /**
   * iframe 주소
   */
  iframeSrc?: string;
  /**
   * 권한을 무시하고 직접 액세스 가능
   * @default false
   */
  ignoreAccess?: boolean;
  /**
   * KeepAlive 캐시 활성화
   */
  keepAlive?: boolean;
  /**
   * 외부 링크 - 이동 경로
   */
  link?: string;
  /**
   * 라우트가 이미 로드되었는지 여부
   */
  loaded?: boolean;
  /**
   * 최대 탭 페이지 오픈 수
   * @default -1
   */
  maxNumOfOpenTab?: number;
  /**
   * 메뉴는 보이지만 액세스 시 403으로 리다이렉트됨
   */
  menuVisibleWithForbidden?: boolean;
  /**
   * 기본 레이아웃을 사용하지 않음 (최상위에서만 유효)
   */
  noBasicLayout?: boolean;
  /**
   * 새 창에서 열기
   */
  openInNewWindow?: boolean;
  /**
   * 라우트 -> 메뉴 정렬용
   */
  order?: number;
  /**
   * 메뉴에 포함된 파라미터
   */
  query?: Recordable;
  /**
   * 제목 이름
   */
  title: string;
}

// RouteRecordRaw의 component 속성을 string으로 변경하기 위한 재귀 타입 정의
type RouteRecordStringComponent<T = string> = Omit<
  RouteRecordRaw,
  'children' | 'component'
> & {
  children?: RouteRecordStringComponent<T>[];
  component: T;
};

type ComponentRecordType = Record<string, () => Promise<Component>>;

interface GenerateMenuAndRoutesOptions {
  fetchMenuListAsync?: () => Promise<RouteRecordStringComponent[]>;
  forbiddenComponent?: RouteRecordRaw['component'];
  layoutMap?: ComponentRecordType;
  pageMap?: ComponentRecordType;
  roles?: string[];
  router: Router;
  routes: RouteRecordRaw[];
}

export type {
  ComponentRecordType,
  GenerateMenuAndRoutesOptions,
  RouteMeta,
  RouteRecordRaw,
  RouteRecordStringComponent,
};
