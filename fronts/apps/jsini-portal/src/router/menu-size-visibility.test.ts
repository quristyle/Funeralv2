import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  collectMenuSizeRules,
  setMenusForViewport,
} from './menu-size-visibility';

/** 화면 크기를 흉내낸다. matchMedia 는 두 질의만 받는다. */
function setViewport(kind: 'desktop' | 'phone' | 'tablet') {
  vi.stubGlobal('window', {
    matchMedia: (q: string) => ({
      matches:
        kind === 'phone'
          ? q.includes('max-width: 767px')
          : kind === 'tablet' && q.includes('min-width: 768px'),
      addEventListener: () => {},
    }),
  });
}

/** 백엔드 /menu/all 이 내려주는 모양 */
const api = [
  {
    name: 'System',
    path: '/system',
    meta: { title: 'system.title', useMobile: true, useTablet: true },
    children: [
      {
        name: '메뉴관리',
        path: '/system/menu',
        meta: { title: '메뉴관리', useMobile: false, useTablet: true },
      },
      {
        name: '조직도',
        path: '/system/org',
        meta: { title: '조직도', useMobile: false, useTablet: false },
      },
    ],
  },
  {
    name: '데스크톱전용묶음',
    path: '/wide',
    meta: { title: '데스크톱전용묶음', useMobile: true, useTablet: true },
    children: [
      {
        name: '배치편집',
        path: '/wide/layout',
        meta: { title: '배치편집', useMobile: false, useTablet: false },
      },
    ],
  },
  // 이 기능을 모르던 시절의 메뉴 — meta 에 값이 없다
  {
    name: '옛메뉴',
    path: '/legacy',
    meta: { title: '옛메뉴' },
  },
];

/** generateMenus 가 만드는 모양(name = meta.title, meta 는 떨어져 나간다) */
const menus = [
  {
    name: 'system.title',
    path: '/system',
    children: [
      { name: '메뉴관리', path: '/system/menu', children: [] },
      { name: '조직도', path: '/system/org', children: [] },
    ],
  },
  {
    name: '데스크톱전용묶음',
    path: '/wide',
    children: [{ name: '배치편집', path: '/wide/layout', children: [] }],
  },
  { name: '옛메뉴', path: '/legacy', children: [] },
] as any;

function visiblePaths(kind: 'desktop' | 'phone' | 'tablet') {
  setViewport(kind);
  collectMenuSizeRules(api);
  let got: any[] = [];
  setMenusForViewport(menus, (m) => (got = m));
  const flat: string[] = [];
  const walk = (list: any[]) =>
    list.forEach((m) => {
      flat.push(m.path);
      walk(m.children ?? []);
    });
  walk(got);
  return flat;
}

describe('메뉴 화면 크기별 노출', () => {
  beforeEach(() => vi.unstubAllGlobals());

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

  it('값이 없는 옛 메뉴는 어디서나 보인다', () => {
    expect(visiblePaths('phone')).toContain('/legacy');
    expect(visiblePaths('tablet')).toContain('/legacy');
  });
});
