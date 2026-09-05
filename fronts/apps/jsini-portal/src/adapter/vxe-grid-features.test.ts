import { describe, expect, it, vi } from 'vitest';

vi.mock('#/locales', () => ({ $t: (k: string) => k }));
vi.mock('#/utils/permission', () => ({ can: () => true }));
vi.mock('@vben/preferences', () => ({ preferences: { app: { isMobile: false } } }));

import { createGridFeatures, embedExportImages } from './vxe-grid-features';

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

describe('보이는 컬럼 고르기 — 필터줄 칸은 목록에 없다', () => {
  /** 컬럼 설정 창이 줄을 그릴지 정하는 함수와, 손질된 컬럼을 함께 얻는다. */
  function customSetup() {
    const { options } = createGridFeatures(
      { gridOptions: { columns: [{ field: 'name', title: '이름' }] } } as any,
      () => undefined,
      () => undefined,
    );
    const gridOptions = (options as any).gridOptions;
    const group = gridOptions.columns.find((c: any) => c.children);
    return {
      group,
      leaf: group.children[0],
      seq: gridOptions.columns.find((c: any) => c.type === 'seq'),
      visibleMethod: gridOptions.customConfig.visibleMethod,
    };
  }

  it('이름줄 칸은 목록에 나오고 필터줄 칸은 빠진다', () => {
    const { group, leaf, seq, visibleMethod } = customSetup();

    expect(visibleMethod({ column: group })).toBe(true);
    expect(visibleMethod({ column: seq })).toBe(true);
    // 이름이 빈 이 줄이 목록에 끼면 무엇을 끄는 것인지 알 수 없다.
    expect(leaf.title).toBe('');
    expect(visibleMethod({ column: leaf })).toBe(false);
  });

  it('화면이 적어 둔 customConfig 는 덮지 않는다', () => {
    const mine = () => true;
    const { options } = createGridFeatures(
      {
        gridOptions: {
          columns: [{ field: 'name', title: '이름' }],
          customConfig: { visibleMethod: mine },
        },
      } as any,
      () => undefined,
      () => undefined,
    );

    expect((options as any).gridOptions.customConfig.visibleMethod).toBe(mine);
  });
});

describe('필터줄 펴기 · 접기', () => {
  /** 도구줄을 그려 단추 목록을 얻는다. `renderTools` 를 다시 부르면 다시 그린다. */
  function tools(gridFeatures: any = {}) {
    const grid = { clearFilter: vi.fn(), recalculate: vi.fn(), updateData: vi.fn() };
    const { filtersVisible, renderTools } = createGridFeatures(
      {
        gridOptions: {
          columns: [{ field: 'name', title: 'name' }],
          gridFeatures,
        },
      } as any,
      () => grid,
      () => undefined,
    );
    const titles = () =>
      ((renderTools as any)('bottom').children as any[]).map(
        (n: any) => n?.props?.title,
      );
    const click = (title: string) =>
      ((renderTools as any)('bottom').children as any[])
        .find((n: any) => n?.props?.title === title)
        ?.props?.onClick();
    return { click, filtersVisible, grid, titles };
  }

  it('기본은 접혀 있다 — 펴는 단추만 있고 초기화는 없다', () => {
    const { filtersVisible, titles } = tools();

    expect(filtersVisible?.value).toBe(false);
    expect(titles()).toContain('common.showFilter');
    expect(titles()).not.toContain('common.resetFilter');
    expect(titles()).not.toContain('common.hideFilter');
  });

  it('펴면 초기화가 함께 나오고, 접는 단추로 바뀐다', () => {
    const { click, filtersVisible, titles } = tools();

    click('common.showFilter');

    expect(filtersVisible?.value).toBe(true);
    // '필터 초기화' 다음이 '필터 사용 안 함' — 두 아이콘은 붙어 있다.
    const shown = titles();
    expect(shown).toContain('common.resetFilter');
    expect(shown.indexOf('common.hideFilter')).toBe(
      shown.indexOf('common.resetFilter') + 1,
    );
  });

  it('접을 때 걸려 있던 값을 비운다 — 안 보이는 필터가 자료를 거르면 안 된다', () => {
    const { click, grid } = tools();

    click('common.showFilter');
    click('common.hideFilter');

    expect(grid.clearFilter).toHaveBeenCalled();
  });

  it('화면이 filterVisible 을 주면 펴진 채로 연다', () => {
    const { filtersVisible, titles } = tools({ filterVisible: true });

    expect(filtersVisible?.value).toBe(true);
    expect(titles()).toContain('common.hideFilter');
  });

  it('도구줄이 없는 그리드는 펴 둔다 — 접으면 다시 펼 길이 없다', () => {
    const { filtersVisible } = tools({ tools: false });

    expect(filtersVisible?.value).toBe(true);
  });
});

describe('엑셀 내보내기 — 필터줄', () => {
  /** 내보내기 아이콘을 눌러 vxe 에 넘어가는 설정을 잡아낸다. */
  function exportOptions() {
    let captured: any = null;
    const grid = {
      exportData: (opts: any) => {
        captured = opts;
      },
    };
    const { renderTools } = createGridFeatures(
      {
        gridOptions: {
          columns: [{ field: 'name', title: 'name' }],
          proxyConfig: { ajax: { query: () => [] } },
        },
      } as any,
      () => grid,
      () => ({ query: () => undefined }),
    );

    // 도구줄은 단추들을 감싼 `div` 하나로 나온다.
    const toolbar = (renderTools as any)('bottom');
    const button = (toolbar.children as any[]).find(
      (n: any) => n?.props?.title === 'common.exportExcel',
    );
    button.props.onClick();
    return captured;
  }

  /**
   * 실제 구조를 흉내낸 층 목록.
   *
   * `name` 은 필터가 붙어 묶음이 된 칸 — 1층에 이름, 2층에 필터줄.
   * `op` 는 필터를 끈 칸이라 묶이지 않고 1층에서 두 줄을 걸친다.
   */
  function colgroups() {
    return [
      [
        { _rowSpan: 1, headerClassName: 'jsini-titlerow', title: 'name' },
        { _rowSpan: 2, headerClassName: undefined, title: 'op' },
      ],
      [{ _rowSpan: 1, headerClassName: 'jsini-filterrow', title: '' }],
    ];
  }

  it('필터줄 층을 덜어 낸다', () => {
    const opts = exportOptions();
    expect(typeof opts.beforeExportMethod).toBe('function');

    const options = { colgroups: colgroups() };
    opts.beforeExportMethod({ options });

    expect(options.colgroups).toHaveLength(1);
    expect(options.colgroups[0]!.map((c: any) => c.title)).toEqual([
      'name',
      'op',
    ]);
  });

  it('없어진 층까지 걸치던 칸의 세로 병합을 줄인다', () => {
    const opts = exportOptions();
    const options = { colgroups: colgroups() };
    opts.beforeExportMethod({ options });

    // 그대로 2 로 두면 병합이 자료 첫 줄을 먹는다
    expect(options.colgroups[0]!.map((c: any) => c._rowSpan)).toEqual([1, 1]);
  });

  it('필터줄이 없으면 손대지 않는다', () => {
    const opts = exportOptions();
    const options = {
      colgroups: [[{ _rowSpan: 1, headerClassName: undefined, title: 'name' }]],
    };
    opts.beforeExportMethod({ options });

    expect(options.colgroups).toHaveLength(1);
  });

  it('맨 아래 층에 필터줄 아닌 칸이 섞여 있으면 손대지 않는다', () => {
    const opts = exportOptions();
    const options = {
      colgroups: [
        [{ _rowSpan: 1, headerClassName: 'jsini-titlerow', title: 'g' }],
        [
          { _rowSpan: 1, headerClassName: 'jsini-filterrow', title: '' },
          { _rowSpan: 1, headerClassName: undefined, title: '진짜칸' },
        ],
      ],
    };
    opts.beforeExportMethod({ options });

    expect(options.colgroups).toHaveLength(2);
  });
});

describe('엑셀 내보내기 — 사진 넣기 (embedExportImages)', () => {
  /** ExcelJS 워크시트 흉내 — 그림·행·셀 접근만 기록한다. */
  function fakeWorksheet() {
    const rows = new Map<number, any>();
    return {
      addedImages: [] as any[],
      addImage(imageId: any, range: any) {
        this.addedImages.push({ imageId, range });
      },
      photoColumn: { width: 10 } as any,
      getColumn() {
        return this.photoColumn;
      },
      getRow(n: number) {
        let row = rows.get(n);
        if (!row) {
          const cells = new Map<number, any>();
          row = {
            height: undefined as number | undefined,
            getCell(c: number) {
              let cell = cells.get(c);
              if (!cell) {
                // 지우기 전의 셀에는 URL 문자열이 들어 있다고 치자.
                cell = { value: 'https://…/photo.jpg' };
                cells.set(c, cell);
              }
              return cell;
            },
          };
          rows.set(n, row);
        }
        return row;
      },
    };
  }

  const rowA = { id: 1 };
  const rowB = { id: 2 };
  const image = { base64: 'data:image/png;base64,xx', height: 40, width: 30 };

  function embed(worksheet: any, overrides: any = {}) {
    const workbook = { addImage: vi.fn(() => 7) };
    embedExportImages(
      {
        // 필터줄을 덜어 낸 뒤의 모양 — 머리글 한 층
        colgroups: [[{}]],
        columns: [{ field: 'photo' }, { field: 'name' }],
        datas: [{ _row: rowA }, { _row: rowB }],
        options: { isColgroup: true, isHeader: true },
        workbook,
        worksheet,
        ...overrides,
      },
      { field: 'photo', urlOf: () => undefined },
      new Map([[rowA, image]]),
    );
    return workbook;
  }

  it('머리글 층 수만큼 내려서 그림을 앉힌다', () => {
    const worksheet = fakeWorksheet();
    const workbook = embed(worksheet);

    // 사진이 준비된 행(rowA)만 그림이 붙는다.
    expect(workbook.addImage).toHaveBeenCalledTimes(1);
    expect(worksheet.addedImages).toHaveLength(1);
    const { range } = worksheet.addedImages[0]!;
    // 머리글 1층 → 첫 자료 행의 tl.row 는 1 (0 기준). 사진 칸은 첫 칸(col 0).
    expect(Math.floor(range.tl.row)).toBe(1);
    expect(Math.floor(range.tl.col)).toBe(0);
    expect(range.ext).toEqual({ height: 40, width: 30 });
  });

  it('사진 칸의 글자 값은 모든 행에서 지운다 — 그림 없는 행도', () => {
    const worksheet = fakeWorksheet();
    embed(worksheet);

    // 머리글 1층 → 자료는 2행(rowA)·3행(rowB). ExcelJS 는 1 기준.
    expect(worksheet.getRow(2).getCell(1).value).toBe('');
    expect(worksheet.getRow(3).getCell(1).value).toBe('');
    // 다른 칸은 건드리지 않는다.
    expect(worksheet.getRow(2).getCell(2).value).not.toBe('');
  });

  it('그림 있는 행만 높이를 키운다', () => {
    const worksheet = fakeWorksheet();
    embed(worksheet);

    expect(worksheet.getRow(2).height).toBeGreaterThan(0);
    expect(worksheet.getRow(3).height).toBeUndefined();
  });

  it('사진 칸이 내보내기 컬럼에 없으면(숨김) 아무것도 하지 않는다', () => {
    const worksheet = fakeWorksheet();
    const workbook = embed(worksheet, { columns: [{ field: 'name' }] });

    expect(workbook.addImage).not.toHaveBeenCalled();
    expect(worksheet.getRow(2).getCell(1).value).not.toBe('');
  });

  it('묶음 머리글이 아니면(isColgroup=false) 머리글을 한 줄로 센다', () => {
    const worksheet = fakeWorksheet();
    embed(worksheet, { options: { isColgroup: false, isHeader: true } });

    expect(Math.floor(worksheet.addedImages[0]!.range.tl.row)).toBe(1);
  });
});
