import { describe, expect, it, vi } from 'vitest';

vi.mock('#/locales', () => ({ $t: (k: string) => k }));
vi.mock('#/utils/permission', () => ({ can: () => true }));
vi.mock('@vben/preferences', () => ({ preferences: { app: { isMobile: false } } }));

import { createGridFeatures } from './vxe-grid-features';

const statusColumn = {
  cellRender: { name: 'CellTag', options: [] },
  field: 'status',
  filters: [
    { label: 'common.enabled', value: 1 },
    { label: 'common.disabled', value: 0 },
  ],
  title: 'status',
};

function leafOf(column: any) {
  const { options } = createGridFeatures(
    { gridOptions: { columns: [column] } } as any,
    () => undefined,
    () => undefined,
  );
  const cols = (options as any).gridOptions.columns;
  return cols.find((c: any) => c.children)?.children?.[0];
}

describe('화면이 filters 만 준 칸', () => {
  it('공통 판정이 붙는다', () => {
    const leaf = leafOf(statusColumn);
    expect(typeof leaf.filterMethod).toBe('function');
    // 고른 옵션마다 판정이 불리고, 하나라도 맞으면 남는다(vxe 가 OR 로 합친다)
    const call = (rowStatus: any, optValue: any) =>
      leaf.filterMethod({
        column: { field: 'status' },
        option: { value: optValue },
        row: { status: rowStatus },
      });
    expect(call(1, 1)).toBe(true);
    expect(call(1, 0)).toBe(false);
    expect(call(0, 0)).toBe(true);
    // 빈 값이 섞인 자료: null 과 '' 를 같은 '값 없음' 으로 본다
    expect(call(null, '')).toBe(true);
  });

  it('선택 목록이 있으면 고르는 칸으로 그려진다(다중선택)', () => {
    const leaf = leafOf(statusColumn);
    expect(leaf.filters.some((f: any) => f.label !== undefined)).toBe(true);
  });
});
