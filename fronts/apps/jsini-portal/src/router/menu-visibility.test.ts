import { beforeEach, describe, expect, it, vi } from 'vitest';

import { collectMenuRules, setVisibleMenus } from './menu-visibility';

/** 화면 크기를 흉내낸다. matchMedia 는 두 질의만 받는다. */
function setViewport(kind: 'desktop' | 'phone' | 'tablet') {
  vi.stubGlobal('window', {
    matchMedia: (q: string) => ({
      addEventListener: () => {},
      matches:
        kind === 'phone'
          ? q.includes('max-width: 767px')
          : kind === 'tablet' && q.includes('min-width: 768px'),
    }),
  });
}

/** 백엔드 `/menu/all` 이 내려주는 모양 */
const api = [
  {
    children: [
      {
        meta: { title: '메뉴관리', type: 'MENU', useMobile: false, useTablet: true },
        name: '메뉴관리',
        path: '/system/menu',
      },
      {
        meta: { title: '조직도', type: 'MENU', useMobile: false, useTablet: false },
        name: '조직도',
        path: '/system/org',
      },
    ],
    meta: { title: 'system.title', type: 'CATALOG', useMobile: true, useTablet: true },
    name: 'System',
    path: '/system',
  },
  {
    children: [
      {
        meta: { title: '배치편집', type: 'MENU', useMobile: false, useTablet: false },
        name: '배치편집',
        path: '/wide/layout',
      },
    ],
    meta: { title: '데스크톱전용묶음', type: 'CATALOG', useMobile: true, useTablet: true },
    name: '데스크톱전용묶음',
    path: '/wide',
  },
  // 화면 크기 설정을 모르던 시절의 메뉴 — meta 에 값이 없다
  { meta: { title: '옛메뉴', type: 'MENU' }, name: '옛메뉴', path: '/legacy' },
];

/** `generateMenus` 가 만드는 모양(name = meta.title, meta 는 떨어져 나간다) */
const menus = [
  {
    children: [
      { children: [], name: '메뉴관리', path: '/system/menu' },
      { children: [], name: '조직도', path: '/system/org' },
    ],
    name: 'system.title',
    path: '/system',
  },
  {
    children: [{ children: [], name: '배치편집', path: '/wide/layout' }],
    name: '데스크톱전용묶음',
    path: '/wide',
  },
  { children: [], name: '옛메뉴', path: '/legacy' },
] as any;

/** 권한 스토어의 `canViewMenu` 자리. 기본은 전부 열람 가능. */
const allowAll: (path: string) => boolean = () => true;

function visiblePaths(
  kind: 'desktop' | 'phone' | 'tablet',
  canView: (path: string) => boolean = allowAll,
) {
  setViewport(kind);
  collectMenuRules(api);
  let got: any[] = [];
  setVisibleMenus(menus, (m) => (got = m), canView);

  const flat: string[] = [];
  const walk = (list: any[]) =>
    list.forEach((m) => {
      flat.push(m.path);
      walk(m.children ?? []);
    });
  walk(got);
  return flat;
}

describe('사이드바 메뉴목록 거르기', () => {
  beforeEach(() => vi.unstubAllGlobals());

  describe('화면 크기', () => {
    it('데스크톱은 전부 보인다', () => {
      expect(visiblePaths('desktop')).toEqual([
        '/system',
        '/system/menu',
        '/system/org',
        '/wide',
        '/wide/layout',
        '/legacy',
      ]);
    });

    it('휴대폰은 useMobile=false 를 뺀다', () => {
      // /system 과 /wide 는 자기 값이 true 지만 자식이 모두 빠져
      // 빈 묶음이 되므로 함께 빠진다
      expect(visiblePaths('phone')).toEqual(['/legacy']);
    });

    it('태블릿은 useTablet 만 본다', () => {
      // 메뉴관리는 useMobile=false 지만 useTablet=true 라 태블릿에는 남는다
      expect(visiblePaths('tablet')).toEqual([
        '/system',
        '/system/menu',
        '/legacy',
      ]);
    });

    it('크기 값이 없는 옛 메뉴는 어디서나 보인다', () => {
      expect(visiblePaths('phone')).toContain('/legacy');
      expect(visiblePaths('tablet')).toContain('/legacy');
    });
  });

  describe('열람 권한', () => {
    it('열람할 수 없는 화면은 목록에서 뺀다', () => {
      const onlyMenu = (path: string) => path === '/system/menu';
      // /wide 와 /legacy 는 열람 불가라 빠지고, /wide 는 빈 묶음이 되어 함께 빠진다
      expect(visiblePaths('desktop', onlyMenu)).toEqual([
        '/system',
        '/system/menu',
      ]);
    });

    it('묶음(CATALOG)에는 권한을 따지지 않는다', () => {
      // 묶음은 화면이 없어 열람 권한이 늘 꺼져 있다. 여기에 권한을 따지면
      // 볼 수 있는 자식이 있어도 트리가 통째로 사라진다.
      const onlyOrg = (path: string) => path === '/system/org';
      expect(visiblePaths('desktop', onlyOrg)).toEqual([
        '/system',
        '/system/org',
      ]);
    });

    it('자식이 모두 걸러져도 자기 화면이 있는 메뉴는 남는다', () => {
      // 실제로 겪은 문제다. `/status`(현황관리)는 자식이 다섯인 **화면 있는 메뉴**인데
      // 자식이 모두 열람 불가라 통째로 사라졌고, 그 위 묶음까지 빈 묶음이 되어
      // 사이드바가 0건이 됐다. 자식이 있다고 다 묶음으로 다루면 안 된다.
      const withScreen = [
        {
          children: [
            {
              meta: { title: '빈소 현황', type: 'MENU' },
              name: '빈소 현황',
              path: '/status/funeral-status',
            },
          ],
          meta: { title: '현황관리', type: 'MENU' },
          name: '현황관리',
          path: '/status',
        },
      ];
      collectMenuRules([
        {
          children: withScreen[0]!.children,
          meta: { title: '장례식장', type: 'CATALOG' },
          name: '장례식장',
          path: '/funerals',
        },
        ...withScreen,
      ]);

      setViewport('desktop');
      let got: any[] = [];
      setVisibleMenus(
        withScreen as any,
        (m) => (got = m),
        (path) => path === '/status',
      );

      expect(got.map((m: any) => m.path)).toEqual(['/status']);
      // 자식이 없어졌으므로 펼치는 묶음이 아니라 눌러서 바로 열리는 항목이어야 한다.
      expect(got[0].children).toEqual([]);
    });

    it('열람할 수 있는 화면이 하나도 없으면 목록이 빈다', () => {
      expect(visiblePaths('desktop', () => false)).toEqual([]);
    });

    it('크기와 권한이 함께 걸린다', () => {
      // 태블릿에서 살아남는 것은 /system/menu 뿐인데 열람 권한이 없다
      const onlyOrg = (path: string) => path === '/system/org';
      expect(visiblePaths('tablet', onlyOrg)).toEqual([]);
    });
  });
});
