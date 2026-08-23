import type { ComputedRef, VNode } from 'vue';
import type {
  RouteLocationNormalized,
  RouteLocationNormalizedLoaded,
  RouteLocationNormalizedLoadedGeneric,
  Router,
  RouteRecordNormalized,
} from 'vue-router';

import type { TabDefinition } from '@vben-core/typings';

import { markRaw, nextTick, toRaw } from 'vue';

import { preferences } from '@vben-core/preferences';
import {
  createStack,
  openWindow,
  Stack,
  startProgress,
  stopProgress,
} from '@vben-core/shared/utils';

import { acceptHMRUpdate, defineStore } from 'pinia';

interface RouteCached {
  component: VNode;
  key: string;
  route: RouteLocationNormalizedLoadedGeneric;
}

interface TabbarState {
  cachedRoutes: Map<string, RouteCached>;
  /**
   * @ko_KR 현재 열려 있는 탭 목록 캐시
   */
  cachedTabs: Set<string>;
  /**
   * @ko_KR 드래그 종료 인덱스
   */
  dragEndIndex: number;
  /**
   * @ko_KR 캐시에서 제외할 탭
   */
  excludeCachedTabs: Set<string>;
  /**
   * @ko_KR 탭 우클릭 메뉴 목록
   */
  menuList: string[];
  /**
   * @ko_KR 새로고침 여부
   */
  renderRouteView?: boolean;
  /**
   * @ko_KR 현재 열려 있는 탭 목록
   */
  tabs: TabDefinition[];
  /**
   * @ko_KR 업데이트 시간. 일부 업데이트 시나리오에 사용되며, watch 딥 리스닝을 사용하면 성능이 저하될 수 있습니다.
   */
  updateTime?: number;
  /**
   * @ko_KR 이전에 열었던 탭
   */
  visitHistory: Stack<string>;
}

/**
 * @ko_KR 방문 기록 최대 수
 */
const MAX_VISIT_HISTORY = 50;

/**
 * @ko_KR 방문 권한 관련
 */
export const useTabbarStore = defineStore('core-tabbar', {
  actions: {
    /**
     * Close tabs in bulk
     */
    async _bulkCloseByKeys(keys: string[]) {
      const keySet = new Set(keys);
      this.tabs = this.tabs.filter(
        (item) => !keySet.has(getTabKeyFromTab(item)),
      );
      if (isVisitHistory()) {
        this.visitHistory.remove(...keys);
      }

      await this.updateCacheTabs();
    },
    /**
     * @ko_KR 탭 닫기
     * @param tab
     */
    _close(tab: TabDefinition) {
      if (isAffixTab(tab)) {
        return;
      }
      const index = this.tabs.findIndex((item) => equalTab(item, tab));
      index !== -1 && this.tabs.splice(index, 1);
    },
    /**
     * @ko_KR 기본 탭으로 이동
     */
    async _goToDefaultTab(router: Router) {
      if (this.getTabs.length <= 0) {
        return;
      }
      const firstTab = this.getTabs[0];
      if (firstTab) {
        await this._goToTab(firstTab, router);
      }
    },
    /**
     * @ko_KR 탭으로 이동
     * @param tab
     * @param router
     */
    async _goToTab(tab: TabDefinition, router: Router) {
      const { params, path, query } = tab;
      const toParams = {
        params: params || {},
        path,
        query: query || {},
      };
      await router.replace(toParams);
    },
    /**
     * @ko_KR 탭 추가
     * @param routeTab
     */
    addTab(routeTab: TabDefinition): TabDefinition {
      let tab = cloneTab(routeTab);
      if (!tab.key) {
        tab.key = getTabKey(routeTab);
      }
      if (!isTabShown(tab)) {
        return tab;
      }

      const tabIndex = this.tabs.findIndex((item) => {
        return equalTab(item, tab);
      });

      if (tabIndex === -1) {
        const maxCount = preferences.tabbar.maxCount;
        // 동적 라우트 열기 수를 가져옵니다. 0보다 크면 열기 수를 제어해야 함을 의미합니다.
        const maxNumOfOpenTab = (routeTab?.meta?.maxNumOfOpenTab ??
          -1) as number;
        // 동적 라우트 레벨이 0보다 크면 해당 라우트의 열기 수를 제한해야 합니다.
        // 이미 열려 있는 동적 라우트 수를 가져와 특정 값보다 큰지 판단합니다.
        if (
          maxNumOfOpenTab > 0 &&
          this.tabs.filter((tab) => tab.name === routeTab.name).length >=
            maxNumOfOpenTab
        ) {
          // 첫 번째 항목 닫기
          const index = this.tabs.findIndex(
            (item) => item.name === routeTab.name,
          );
          index !== -1 && this.tabs.splice(index, 1);
        } else if (maxCount > 0 && this.tabs.length >= maxCount) {
          // 첫 번째 항목 닫기
          const index = this.tabs.findIndex(
            (item) =>
              !Reflect.has(item.meta, 'affixTab') || !item.meta.affixTab,
          );
          index !== -1 && this.tabs.splice(index, 1);
        }
        this.tabs.push(tab);
      } else {
        // 페이지가 이미 존재하므로 탭을 중복 추가하지 않고 탭 매개변수만 업데이트합니다.
        const currentTab = toRaw(this.tabs)[tabIndex];
        const mergedTab = {
          ...currentTab,
          ...tab,
          meta: { ...currentTab?.meta, ...tab.meta },
        };
        if (currentTab) {
          const curMeta = currentTab.meta;
          if (Reflect.has(curMeta, 'affixTab')) {
            mergedTab.meta.affixTab = curMeta.affixTab;
          }
          if (Reflect.has(curMeta, 'newTabTitle')) {
            mergedTab.meta.newTabTitle = curMeta.newTabTitle;
          }
        }
        tab = mergedTab;
        this.tabs.splice(tabIndex, 1, mergedTab);
      }
      this.updateCacheTabs();
      // 방문 기록 추가
      if (isVisitHistory()) {
        this.visitHistory.push(tab.key as string);
      }
      return tab;
    },
    /**
     * @ko_KR 모든 탭 닫기
     */
    async closeAllTabs(router: Router) {
      const newTabs = this.tabs.filter((tab) => isAffixTab(tab));
      this.tabs = newTabs.length > 0 ? newTabs : [...this.tabs].splice(0, 1);
      // 방문 기록 설정
      if (isVisitHistory()) {
        this.visitHistory.retain(
          this.tabs.map((item) => getTabKeyFromTab(item)),
        );
      }
      await this._goToDefaultTab(router);
      this.updateCacheTabs();
    },
    /**
     * @ko_KR 왼쪽 탭 닫기
     * @param tab
     */
    async closeLeftTabs(tab: TabDefinition) {
      const index = this.tabs.findIndex((item) => equalTab(item, tab));

      if (index < 1) {
        return;
      }

      const leftTabs = this.tabs.slice(0, index);
      const keys: string[] = [];

      for (const item of leftTabs) {
        if (!isAffixTab(item)) {
          keys.push(item.key as string);
        }
      }
      await this._bulkCloseByKeys(keys);
    },
    /**
     * @ko_KR 다른 탭 닫기
     * @param tab
     */
    async closeOtherTabs(tab: TabDefinition) {
      const closeKeys = this.tabs.map((item) => getTabKeyFromTab(item));

      const keys: string[] = [];

      for (const key of closeKeys) {
        if (key !== getTabKeyFromTab(tab)) {
          const closeTab = this.tabs.find(
            (item) => getTabKeyFromTab(item) === key,
          );
          if (!closeTab) {
            continue;
          }
          if (!isAffixTab(closeTab)) {
            keys.push(closeTab.key as string);
          }
        }
      }
      await this._bulkCloseByKeys(keys);
    },
    /**
     * @ko_KR 오른쪽 탭 닫기
     * @param tab
     */
    async closeRightTabs(tab: TabDefinition) {
      const index = this.tabs.findIndex((item) => equalTab(item, tab));

      if (index !== -1 && index < this.tabs.length - 1) {
        const rightTabs = this.tabs.slice(index + 1);

        const keys: string[] = [];
        for (const item of rightTabs) {
          if (!isAffixTab(item)) {
            keys.push(item.key as string);
          }
        }
        await this._bulkCloseByKeys(keys);
      }
    },

    /**
     * @ko_KR 탭 닫기
     * @param tab
     * @param router
     */
    async closeTab(tab: TabDefinition, router: Router) {
      const { currentRoute } = router;
      const currentTabKey = getTabKey(currentRoute.value);
      // 활성화되지 않은 탭 닫기
      if (currentTabKey !== getTabKeyFromTab(tab)) {
        this._close(tab);
        this.updateCacheTabs();
        // 방문 기록 제거
        if (isVisitHistory()) {
          this.visitHistory.remove(getTabKeyFromTab(tab));
        }
        return;
      }
      if (this.getTabs.length <= 1) {
        console.error('Failed to close the tab; only one tab remains open.');
        return;
      }
      // 방문 기록에서 현재 닫힌 탭 제거
      if (isVisitHistory()) {
        this.visitHistory.remove(currentTabKey);
        this._close(tab);

        let previousTab: TabDefinition | undefined;
        let previousTabKey: string | undefined;
        while (true) {
          previousTabKey = this.visitHistory.pop();
          if (!previousTabKey) {
            break;
          }
          previousTab = this.getTabByKey(previousTabKey);
          if (previousTab) {
            break;
          }
        }
        await (previousTab
          ? this._goToTab(previousTab, router)
          : this._goToDefaultTab(router));
        return;
      }
      // 방문 기록이 활성화되지 않은 경우 다음 또는 이전 탭으로 직접 이동
      const index = this.getTabs.findIndex(
        (item) => getTabKeyFromTab(item) === getTabKey(currentRoute.value),
      );

      const before = this.getTabs[index - 1];
      const after = this.getTabs[index + 1];

      // 다음 탭이 존재하면 다음으로 이동
      if (after) {
        this._close(tab);
        await this._goToTab(after, router);
        // 이전 탭이 존재하면 이전으로 이동
      } else if (before) {
        this._close(tab);
        await this._goToTab(before, router);
      }
    },

    /**
     * @ko_KR 키로 탭 닫기
     * @param key
     * @param router
     */
    async closeTabByKey(key: string, router: Router) {
      const originKey = decodeURIComponent(key);
      const index = this.tabs.findIndex(
        (item) => getTabKeyFromTab(item) === originKey,
      );
      if (index === -1) {
        return;
      }

      const tab = this.tabs[index];
      if (tab) {
        await this.closeTab(tab, router);
      }
    },

    /**
     * 탭 키로 탭 가져오기
     * @param key
     */
    getTabByKey(key: string) {
      return this.getTabs.find(
        (item) => getTabKeyFromTab(item) === key,
      ) as TabDefinition;
    },
    /**
     * @ko_KR 새 창에서 탭 열기
     * @param tab
     */
    async openTabInNewWindow(tab: TabDefinition, router: Router) {
      const href = router.resolve(tab.fullPath || tab.path).href;
      openWindow(new URL(href, location.href).href, { target: '_blank' });
    },

    /**
     * @ko_KR 탭 고정
     * @param tab
     */
    async pinTab(tab: TabDefinition) {
      const index = this.tabs.findIndex((item) => equalTab(item, tab));
      if (index === -1) {
        return;
      }
      const oldTab = this.tabs[index];
      tab.meta.affixTab = true;
      tab.meta.title = oldTab?.meta?.title as string;
      // this.addTab(tab);
      this.tabs.splice(index, 1, tab);
      // 고정 탭을 필터링합니다. 나중에 affixTabOrder 값을 변경하면 문제가 발생할 수 있습니다. 현재 464행의 affixTabs 정렬에는 값이 설정되어 있지 않습니다.
      const affixTabs = this.tabs.filter((tab) => isAffixTab(tab));
      // 고정 탭 인덱스 가져오기
      const newIndex = affixTabs.findIndex((item) => equalTab(item, tab));
      // 위치를 교체하여 재정렬
      await this.sortTabs(index, newIndex);
    },

    /**
     * 탭 새로고침
     */
    async refresh(router: Router | string) {
      // Router 라우트인 경우 현재 라우트에 따라 새로고침
      // 문자열인 경우 라우트 이름이며, 지정된 탭을 정밀 새로고침합니다. 현재 라우트 이름일 수 없으며, 그렇지 않으면 새로고침되지 않습니다.
      if (typeof router === 'string') {
        return await this.refreshByName(router);
      }

      const { currentRoute } = router;
      const { name } = currentRoute.value;

      this.excludeCachedTabs.add(name as string);
      this.renderRouteView = false;
      startProgress();

      await nextTick();
      // await new Promise((resolve) => setTimeout(resolve, 200));

      this.excludeCachedTabs.delete(name as string);
      this.renderRouteView = true;
      stopProgress();
    },

    /**
     * 라우트 이름으로 지정된 탭 새로고침
     */
    async refreshByName(name: string) {
      this.excludeCachedTabs.add(name);
      await new Promise((resolve) => setTimeout(resolve, 200));
      this.excludeCachedTabs.delete(name);
    },

    /**
     * @ko_KR 탭 제목 재설정
     */
    async resetTabTitle(tab: TabDefinition) {
      if (tab?.meta?.newTabTitle) {
        return;
      }
      const findTab = this.tabs.find((item) => equalTab(item, tab));
      if (findTab) {
        findTab.meta.newTabTitle = undefined;
        await this.updateCacheTabs();
      }
    },

    /**
     * 고정 탭 설정
     * @param tabs
     */
    setAffixTabs(tabs: RouteRecordNormalized[]) {
      for (const tab of tabs) {
        tab.meta.affixTab = true;
        this.addTab(routeToTab(tab));
      }
    },

    /**
     * @ko_KR 메뉴 목록 업데이트
     * @param list
     */
    setMenuList(list: string[]) {
      this.menuList = list;
    },

    /**
     * @ko_KR 탭 제목 설정
     *
     * @ko_KR 정적 제목 문자열 또는 계산된 속성을 동적 제목으로 설정할 수 있습니다.
     * @ko_KR 제목이 계산된 속성인 경우, 계산된 속성 값의 변화에 따라 제목이 자동으로 업데이트됩니다.
     * @ko_KR 상태 또는 다국어에 따라 제목을 동적으로 업데이트해야 하는 시나리오에 적합합니다.
     *
     * @param {TabDefinition} tab - 탭 객체
     * @param {ComputedRef<string> | string} title - 제목 내용, 정적 문자열 또는 계산된 속성 지원
     *
     * @example
     * // 정적 제목 설정
     * setTabTitle(tab, '새 탭');
     *
     * @example
     * // 동적 제목 설정
     * setTabTitle(tab, computed(() => t('common.dashboard')));
     */
    async setTabTitle(tab: TabDefinition, title: ComputedRef<string> | string) {
      const findTab = this.tabs.find((item) => equalTab(item, tab));

      if (findTab) {
        findTab.meta.newTabTitle = title;

        await this.updateCacheTabs();
      }
    },
    setUpdateTime() {
      this.updateTime = Date.now();
    },
    /**
     * @ko_KR 탭 순서 설정
     * @param oldIndex
     * @param newIndex
     */
    async sortTabs(oldIndex: number, newIndex: number) {
      const currentTab = this.tabs[oldIndex];
      if (!currentTab) {
        return;
      }
      this.tabs.splice(oldIndex, 1);
      this.tabs.splice(newIndex, 0, currentTab);
      this.dragEndIndex = this.dragEndIndex + 1;
    },

    /**
     * @ko_KR 탭 고정 전환
     * @param tab
     */
    async toggleTabPin(tab: TabDefinition) {
      const affixTab = tab?.meta?.affixTab ?? false;

      await (affixTab ? this.unpinTab(tab) : this.pinTab(tab));
    },

    /**
     * @ko_KR 탭 고정 해제
     * @param tab
     */
    async unpinTab(tab: TabDefinition) {
      const index = this.tabs.findIndex((item) => equalTab(item, tab));
      if (index === -1) {
        return;
      }
      const oldTab = this.tabs[index];
      tab.meta.affixTab = false;
      tab.meta.title = oldTab?.meta?.title as string;
      // this.addTab(tab);
      this.tabs.splice(index, 1, tab);
      // 고정 탭을 필터링합니다. 나중에 affixTabOrder 값을 변경하면 문제가 발생할 수 있습니다. 현재 464행의 affixTabs 정렬에는 값이 설정되어 있지 않습니다.
      const affixTabs = this.tabs.filter((tab) => isAffixTab(tab));
      // 고정 탭 인덱스를 가져오며, 고정 탭의 다음 위치 즉 활성 탭의 첫 번째 위치를 사용합니다.
      const newIndex = affixTabs.length;
      // 위치를 교체하여 재정렬
      await this.sortTabs(index, newIndex);
    },
    /**
     * 현재 열려 있는 탭에 따라 캐시 업데이트
     */
    async updateCacheTabs() {
      const cacheMap = new Set<string>();

      for (const tab of this.tabs) {
        // 지속성이 필요하지 않은 탭 건너뛰기
        const keepAlive = tab.meta?.keepAlive;
        if (!keepAlive) {
          continue;
        }
        (tab.matched || []).forEach((t, i) => {
          if (i > 0) {
            cacheMap.add(t.name as string);
          }
        });

        const name = tab.name as string;
        cacheMap.add(name);
      }
      this.cachedTabs = cacheMap;
    },
    /**
     * 캐시된 라우트 추가
     * @param component
     * @param route
     */
    addCachedRoute(component: VNode, route: RouteLocationNormalizedLoaded) {
      const key = getTabKey(route);
      if (this.cachedRoutes.has(key)) {
        return;
      }
      this.cachedRoutes.set(key, {
        key,
        component: markRaw(component),
        route: markRaw(route),
      });
    },
    removeCachedRoute(key: string) {
      this.cachedRoutes.delete(key);
    },
  },
  getters: {
    affixTabs(): TabDefinition[] {
      const affixTabs = this.tabs.filter((tab) => isAffixTab(tab));

      return affixTabs.toSorted((a, b) => {
        const orderA = (a.meta?.affixTabOrder ?? 0) as number;
        const orderB = (b.meta?.affixTabOrder ?? 0) as number;
        return orderA - orderB;
      });
    },
    getCachedTabs(): string[] {
      return [...this.cachedTabs];
    },
    getExcludeCachedTabs(): string[] {
      return [...this.excludeCachedTabs];
    },
    getMenuList(): string[] {
      return this.menuList;
    },
    getTabs(): TabDefinition[] {
      const normalTabs = this.tabs.filter((tab) => !isAffixTab(tab));
      return [...this.affixTabs, ...normalTabs].filter(Boolean);
    },
    getCachedRoutes(): Map<string, RouteCached> {
      return this.cachedRoutes;
    },
  },
  persist: [
    // 탭은 localStorage에 저장할 필요가 없습니다.
    {
      pick: ['tabs', 'visitHistory'],
      storage: sessionStorage,
      serializer: {
        serialize: JSON.stringify,
        deserialize(value: string) {
          const parsed = JSON.parse(value);
          // Stack 클래스 인스턴스는 JSON 직렬화 후 일반 객체 {dedup, items, maxSize}로 변환됩니다.
          // 모든 메서드와 게터를 잃게 되므로 Stack 인스턴스를 다시 빌드해야 합니다.
          if (parsed.visitHistory && !(parsed.visitHistory instanceof Stack)) {
            const raw = parsed.visitHistory;
            const stack = createStack<string>(true, MAX_VISIT_HISTORY);
            if (Array.isArray(raw.items)) {
              stack.push(...raw.items);
            }
            parsed.visitHistory = stack;
          }
          return parsed;
        },
      },
    },
  ],
  state: (): TabbarState => ({
    visitHistory: createStack<string>(true, MAX_VISIT_HISTORY),
    cachedRoutes: new Map<string, RouteCached>(),
    cachedTabs: new Set(),
    dragEndIndex: 0,
    excludeCachedTabs: new Set(),
    menuList: [
      'close',
      'affix',
      'maximize',
      'reload',
      'open-in-new-window',
      'close-left',
      'close-right',
      'close-other',
      'close-all',
    ],
    renderRouteView: true,
    tabs: [],
    updateTime: Date.now(),
  }),
});

// Hot Module Replacement(HMR) 문제 해결
const hot = import.meta.hot;
if (hot) {
  hot.accept(acceptHMRUpdate(useTabbarStore, hot));
}

/**
 * @ko_KR 라우트 복제, 라우트 수정 방지
 * @param route
 */
function cloneTab(route: TabDefinition): TabDefinition {
  if (!route) {
    return route;
  }
  const { matched, meta, ...opt } = route;
  return {
    ...opt,
    matched: (matched
      ? matched.map((item) => ({
          meta: item.meta,
          name: item.name,
          path: item.path,
        }))
      : undefined) as RouteRecordNormalized[],
    meta: {
      ...meta,
      newTabTitle: meta.newTabTitle,
    },
  };
}

/**
 * @ko_KR 고정 탭 여부
 * @param tab
 */
function isAffixTab(tab: TabDefinition) {
  return tab?.meta?.affixTab ?? false;
}

/**
 * @ko_KR 탭 표시 여부
 * @param tab
 */
function isTabShown(tab: TabDefinition) {
  const matched = tab?.matched ?? [];
  return !tab.meta.hideInTab && matched.every((item) => !item.meta.hideInTab);
}

/**
 * 라우트에서 탭 키 가져오기
 * @param tab
 */
function getTabKey(tab: RouteLocationNormalized | RouteRecordNormalized) {
  const {
    fullPath,
    path,
    meta: { fullPathKey } = {},
    query = {},
  } = tab as RouteLocationNormalized;
  // pageKey는 배열일 수 있습니다 (쿼리 매개변수가 중복될 때 발생 가능).
  const pageKey = Array.isArray(query.pageKey)
    ? query.pageKey[0]
    : query.pageKey;
  let rawKey;
  if (pageKey) {
    rawKey = pageKey;
  } else {
    rawKey = fullPathKey === false ? path : (fullPath ?? path);
  }
  try {
    return decodeURIComponent(rawKey);
  } catch {
    return rawKey;
  }
}

/**
 * @ko_KR 방문 기록 활성화 여부
 */
function isVisitHistory() {
  return preferences.tabbar.visitHistory;
}

/**
 * 탭에서 탭 키 가져오기
 * 탭에 키가 없으면 라우트에서 키를 가져옵니다.
 * @param tab
 */
function getTabKeyFromTab(tab: TabDefinition): string {
  return tab.key ?? getTabKey(tab);
}

/**
 * 두 탭이 동일한지 비교
 * @param a
 * @param b
 */
function equalTab(a: TabDefinition, b: TabDefinition) {
  return getTabKeyFromTab(a) === getTabKeyFromTab(b);
}

function routeToTab(route: RouteRecordNormalized) {
  return {
    meta: route.meta,
    name: route.name,
    path: route.path,
    key: getTabKey(route),
  } as TabDefinition;
}

export { getTabKey };
