/**
 * 그리드 공통 기능 — 정렬 · **필터 전용 행**
 *
 * ============================================================
 * 왜 여기인가
 * ============================================================
 *
 * 그리드를 쓰는 화면은 **예외 없이** `#/adapter/vxe-table` 의 `useVbenVxeGrid` 를
 * 거친다. 그래서 화면을 하나도 건드리지 않고 전 화면의 그리드 동작을 바꿀 수 있는
 * 자리가 여기 하나뿐이다. 모바일 보정(`adjustGridForMobile`, 40번 문서)이 이미
 * 같은 방식으로 걸려 있다.
 *
 * **`fronts/packages` 에 넣지 않는다.** 거기는 vben 상위와 동기화하는 포크라
 * 우리 기능을 쌓을수록 동기화 비용이 커진다(CLAUDE.md).
 *
 * ============================================================
 * 머리글은 두 줄이고, 아랫줄은 **필터만 있는 줄**이다
 * ============================================================
 *
 *   ┌──────────┬──────────┬──────────┐
 *   │ 사용자명 ▲│ 소속 회사│ 상태     │  ← 1행: 이름. 누르면 정렬
 *   ├──────────┼──────────┼──────────┤
 *   │ [검색   ]│ [검색   ]│ [전체 ▾]│  ← 2행: 필터만
 *   ├──────────┴──────────┴──────────┤
 *
 * 한 칸 안에 이름과 입력칸을 쌓는 것과 다르다. **표의 행이 실제로 둘**이라
 * 이름줄과 필터줄 사이에 선이 그어지고, 필터줄이 하나의 띠로 읽힌다
 * (`portal/system/role/modules/RoleUserTab.vue` 가 보여 준 모양이다).
 *
 * vxe 에서 머리글을 두 줄로 만드는 방법은 **컬럼 묶음(`children`)** 뿐이다.
 * 그래서 값 칸 하나를 이렇게 바꾼다.
 *
 *   { field:'userName', title:'사용자명' }
 *     → { title:'사용자명', children:[ { field:'userName', title:'' } ] }
 *
 * 필터를 걸지 않는 칸(작업 버튼 등)은 묶지 않는다. 묶이지 않은 칸은 vxe 가
 * 두 줄을 합쳐(rowspan) 그리므로 줄이 어긋나지 않는다.
 *
 * ============================================================
 * 정렬은 1행이 맡는다
 * ============================================================
 *
 * 정렬 화살표가 필터줄에 끼면 입력칸과 뒤엉킨다. 그래서 **이름줄을 누르면 정렬**
 * 되게 하고, 필터줄의 화살표는 감춘다(styles/index.css).
 *
 * vxe 의 정렬 자체는 그대로 쓴다 — 아래 칸에 `sortable: true` 를 남겨 두고
 * 이름줄 클릭이 `grid.sort()` 를 부른다. 판정 규칙을 따로 만들지 않는다.
 * (`sortConfig.trigger` 기본값이 '화살표를 눌렀을 때'라, 화살표를 감추면
 *  필터줄을 눌러도 정렬되지 않는다.)
 *
 * ============================================================
 * 필터의 조건과 판정은 vxe 것을 그대로 쓴다
 * ============================================================
 *
 * `filters` · `filterMethod` 는 vxe 의 것이고, 필터줄은 `setFilter()` 로 그 조건에
 * 값을 넣어 줄 뿐이다 — 걸러내는 규칙을 두 벌로 만들면 언젠가 둘이 어긋난다
 * (34번 문서, `/system/menu` 에서 검증된 방식).
 *
 * 그래서 **`filterRender` 를 두지 않는다.** 두면 깔때기 팝업에도 입력칸이 생겨
 * 같은 조건을 넣는 자리가 두 곳이 된다. 이미 적혀 있으면 떼어 낸다.
 * 깔때기 아이콘은 전역 `filterConfig.showIcon: false` 로 감춘다(vxe-table.ts).
 *
 * ============================================================
 * 화면에서 조절하는 법
 * ============================================================
 *
 * 기본은 **켜짐**이다. 목표가 "모든 그리드에 적용" 이므로 화면마다 켜게 하면
 * 새로 만드는 화면이 계속 빠진다. 안 맞는 자리에서만 끈다.
 *
 * | 적는 곳 | 뜻 |
 * |---|---|
 * | `gridOptions.gridFeatures: { sort: false }` | 이 그리드는 정렬 안 함 |
 * | `gridOptions.gridFeatures: { filter: false }` | 이 그리드는 필터줄 없음 |
 * | 컬럼 `params: { sort: false }` | 이 칸만 정렬 제외 |
 * | 컬럼 `params: { filter: false }` | 이 칸만 필터 제외 |
 * | 컬럼 `params: { filterOptions: [{label,value}] }` | 입력칸 대신 **고르는 칸** |
 * | 컬럼 `params: { filterText: (row) => string }` | 필터가 훑을 글자를 직접 지정 |
 *
 * 아래는 **손대지 않아도 알아서 빠진다** — 필터·정렬이 의미 없는 자리다.
 *   · `type` 이 `seq` · `checkbox` · `radio` · `expand` 인 칸
 *   · `field` 가 없는 칸 (드래그 손잡이처럼 슬롯만 그리는 칸)
 *   · `field` 가 `action` · `operation` 처럼 조작 장치인 칸
 *   · `cellRender.name` 이 `CellOperation` 인 칸
 *   · **이미 `slots.header` 를 적어 둔 칸** — 화면이 직접 머리글을 그리는 중이므로
 *     덮으면 그 화면이 깨진다 (`/system/menu` 가 이 경우다)
 */

import { h, ref, type Ref } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { Select } from 'ant-design-vue';

import { $t } from '#/locales';

/** 값 칸이 아니라서 정렬·필터를 걸지 않는 칸의 `type`. */
const NON_DATA_TYPES = new Set(['checkbox', 'expand', 'html', 'radio', 'seq']);

/** 그리는 것이 값이 아니라 버튼인 칸. */
const NON_DATA_RENDERERS = new Set(['CellOperation']);

/**
 * 값이 아니라 조작 장치를 그리는 칸의 `field` 이름.
 *
 * 작업 버튼 칸은 `cellRender` 대신 `slots: { default: 'action' }` 로 그리는 화면이
 * 훨씬 많아서(계정 관리 등 25곳) 렌더러 이름만으로는 걸러지지 않는다.
 * 이름이 다른 작업 칸이 나오면 그 칸에 `params: { filter: false }` 를 적는다.
 */
const NON_DATA_FIELDS = new Set([
  'action',
  'actions',
  'drag',
  'handle',
  'op',
  'operate',
  'operation',
]);

/** 글자를 칠 때 다시 거르기까지 쉬는 시간(ms). 매 글자마다 다시 세우면 입력이 끊긴다. */
const TYPING_PAUSE = 250;

/** 필터줄 칸에 붙는 표시. 정렬 화살표를 감추고 띠 모양을 주는 데 쓴다. */
const FILTER_ROW_CLASS = 'jsini-filterrow';

/** 이름줄 칸에 붙는 표시. 누를 수 있다는 손 모양을 준다. */
const TITLE_ROW_CLASS = 'jsini-titlerow';

export interface GridFeatureFlags {
  /** 엑셀 파일 이름(확장자 제외). 기본 `export` */
  exportName?: string;
  /** 필터 전용 행. 기본 `true` */
  filter?: boolean;
  /**
   * 재조회를 화면이 직접 맡을 때 적는다.
   *
   * `proxyConfig.ajax.query` 로 자료를 받는 그리드는 안 적어도 된다 —
   * 그쪽은 `gridApi.query()` 로 다시 부르면 된다. `:table-data` 로 자료를
   * 넣는 그리드는 그리드가 조회 방법을 모르므로 이걸 안 주면 **재조회 아이콘이
   * 아예 안 나온다**(눌러도 아무 일 없는 단추를 두지 않으려는 것이다).
   */
  onRefresh?: () => void;
  /** 컬럼 정렬. 기본 `true` */
  sort?: boolean;
  /** 아래 도구줄(엑셀 · 재조회 · 필터 초기화). 기본 `true` */
  tools?: boolean;
}

/** `meta.title` 처럼 점이 든 경로도 읽는다. `filterMethod` 는 이걸 안 해 준다. */
function getByPath(row: any, path: string): any {
  if (!row || !path) return undefined;
  if (!path.includes('.')) return row[path];
  return path
    .split('.')
    .reduce((acc: any, key) => (acc == null ? undefined : acc[key]), row);
}

/** 필터 값이 비었는가. `0` 과 `false` 는 **값이 있는 것**이다. */
function isEmptyFilter(value: any): boolean {
  if (Array.isArray(value)) return value.length === 0;
  return value === '' || value === null || value === undefined;
}

/** 고르는 칸의 값은 배열이다. 예전 값(하나짜리)이 남아 있어도 받아 준다. */
function toArray(value: any): any[] {
  if (Array.isArray(value)) return value;
  return isEmptyFilter(value) ? [] : [value];
}

/**
 * 두 값이 같은가. 자료에서 `null` · `undefined` · `''` 가 섞여 오므로
 * 셋을 같은 "값 없음" 으로 본다.
 */
function sameValue(a: any, b: any): boolean {
  const norm = (v: any) => (v === null || v === undefined ? '' : v);
  return norm(a) === norm(b);
}

/** 이 칸이 고르는 칸인가. 이름표가 붙은 옵션이 있으면 그렇다. */
function isChoiceColumn(column: any): boolean {
  return (column?.filters ?? []).some((f: any) => f?.label !== undefined);
}

/**
 * 입력칸용 필터 조건. 판정은 vxe 가 하고 값만 `filters[0].data` 로 들어온다.
 *
 * `resolve` 를 주면 그것이 돌려주는 글자를 훑는다 — 화면에 보이는 것과
 * 저장된 값이 다른 칸(번역 키 · 코드값)에 필요하다.
 */
function textFilter(field: string, resolve?: (row: any) => string) {
  return {
    filters: [{ data: '' }],
    filterMethod: ({ option, row }: any) => {
      const keyword = String(option?.data ?? '')
        .trim()
        .toLowerCase();
      if (!keyword) return true;

      const raw = resolve ? resolve(row) : getByPath(row, field);
      if (raw === null || raw === undefined) return false;

      const text = Array.isArray(raw) ? raw.join(' ') : String(raw);
      return text.toLowerCase().includes(keyword);
    },
  };
}

/**
 * 값이 정해진 칸.
 *
 * vxe 의 기본 판정(엄격한 같음)을 쓰지 않고 직접 적는 이유는 **빈 값** 때문이다.
 * 같은 "값 없음" 이 자료에서는 `null` · `undefined` · `''` 로 섞여 들어온다.
 * 기본 판정으로는 `''` 옵션이 `null` 인 행을 못 잡아서, '(소속 없음)' 같은
 * 항목을 골랐을 때 아무것도 안 나온다.
 */
function choiceFilter(options: { label: string; value: any }[]) {
  const normalize = (value: any) =>
    value === null || value === undefined ? '' : value;

  return {
    filters: options.map((o) => ({ label: o.label, value: o.value })),
    filterMethod: ({ column, option, row }: any) =>
      normalize(getByPath(row, column.field)) === normalize(option?.value),
  };
}

/**
 * `cellRender: { name: 'CellTag', options: [...] }` 로 그리는 칸은
 * 이미 "값 → 이름표" 표를 들고 있다. 그걸 그대로 고르는 칸으로 쓴다.
 *
 * 화면 데이터에서 값을 긁어 목록을 만드는 방식(`params.filterList`)은 쓰지 않는다 —
 * 이름표가 **저장된 값 그대로**(`CATALOG` · `1` · `0`) 나오고, 트리 그리드에서는
 * 긁어 오는 범위가 일부라 종류가 빠진다 (34번 문서 4절).
 */
function optionsFromCellTag(column: any): null | { label: string; value: any }[] {
  if (column?.cellRender?.name !== 'CellTag') return null;

  const options = column.cellRender?.props?.options ?? column.cellRender?.options;

  // 옵션을 안 적은 `CellTag` 는 활성/비활성 두 개를 그린다(vxe-table.ts 의 기본값).
  // 화면에 그렇게 그려지고 있으므로 고르는 칸도 같은 두 개여야 한다.
  if (!Array.isArray(options) || options.length === 0) {
    return [
      { label: $t('common.enabled'), value: 1 },
      { label: $t('common.disabled'), value: 0 },
    ];
  }

  return options
    .filter((o: any) => o?.label !== undefined && o?.value !== undefined)
    .map((o: any) => ({ label: String(o.label), value: o.value }));
}

/** 필터줄에서 친 글쇠·눌림이 표로 새어 나가지 않게 한다. */
function swallow(event: Event) {
  event.stopPropagation();
}

/**
 * 화면 하나(그리드 하나)가 쓰는 공통 기능 묶음.
 *
 * 필터 값과 정렬 표시를 여기에 담아 두고 머리글 렌더 함수가 그것을 읽는다.
 * 렌더 함수 안에서 `ref` 를 읽으므로, 값이 바뀌면 머리글이 다시 그려진다.
 */
function createHeaderRenderers(getGrid: () => any) {
  /** 칸마다 지금 들어 있는 필터 값. 키는 `column.field`. */
  const filterValues: Ref<Record<string, any>> = ref({});

  /** 정렬이 바뀐 것을 머리글에 알리는 표시. 값 자체에는 뜻이 없다. */
  const sortTick = ref(0);

  const timers = new Map<string, ReturnType<typeof setTimeout>>();

  /**
   * 값을 vxe 의 필터 조건에 넣는다.
   *
   * 값이 든 칸의 종류에 따라 vxe 가 읽는 자리가 다르다.
   *   · 입력칸(`filterMethod` 를 쓴다) → `option.data`
   *   · 고르는 칸(기본 판정을 쓴다)   → `option.checked`
   */
  function applyFilter(field: string, value: any, immediate: boolean) {
    filterValues.value = { ...filterValues.value, [field]: value };

    const run = () => {
      timers.delete(field);
      const grid = getGrid();
      if (!grid) return;

      const column = grid.getColumnByField?.(field);
      if (!column) return;

      // 고르는 칸은 **여러 개**를 고를 수 있다. 고른 것마다 `checked` 를 세우면
      // vxe 가 그중 하나라도 맞는 행을 남긴다(OR).
      const picked = toArray(value);

      const options = (column.filters ?? []).map((opt: any) =>
        opt?.label === undefined
          ? { ...opt, checked: !isEmptyFilter(value), data: value }
          : {
              ...opt,
              checked: picked.some((v) => sameValue(v, opt.value)),
            },
      );

      grid.setFilter?.(column, options);
      grid.updateData?.();
    };

    const pending = timers.get(field);
    if (pending) clearTimeout(pending);

    if (immediate) {
      run();
    } else {
      timers.set(field, setTimeout(run, TYPING_PAUSE));
    }
  }

  /** 지금 이 칸이 어느 방향으로 서 있는가. `null` 이면 안 서 있다. */
  function currentOrder(field: string): null | string {
    // 이 줄이 `sortTick` 을 읽어야 정렬 뒤에 머리글이 다시 그려진다.
    void sortTick.value;
    const grid = getGrid();
    const found = (grid?.getSortColumns?.() ?? []).find(
      (c: any) => c.field === field,
    );
    return found?.order ?? null;
  }

  /** 오름차순 → 내림차순 → 해제 순으로 돈다. */
  function toggleSort(field: string) {
    const grid = getGrid();
    if (!grid) return;

    const order = currentOrder(field);
    const next = order === 'asc' ? 'desc' : order === 'desc' ? null : 'asc';

    if (next) {
      grid.sort?.(field, next);
    } else {
      grid.clearSort?.(field);
    }
    sortTick.value += 1;
  }

  /**
   * 1행 — 이름줄. 누르면 정렬된다.
   *
   * 묶음 머리글(`children` 을 가진 칸)에는 `field` 가 없으므로 아래 칸의 이름을
   * 닫아서(closure) 받는다.
   */
  function createTitleHeader(field: string, title: any, sortable: boolean) {
    return () => {
      const children: any[] = [
        h('span', { class: 'jsini-titlerow__text' }, title),
      ];

      if (sortable) {
        const order = currentOrder(field);
        children.push(
          h(
            'span',
            {
              class: [
                'jsini-titlerow__sort',
                order ? 'jsini-titlerow__sort--on' : '',
              ],
            },
            order === 'desc' ? '▼' : '▲',
          ),
        );
      }

      return [
        h(
          'span',
          {
            class: 'jsini-titlerow__inner',
            onClick: sortable ? () => toggleSort(field) : undefined,
          },
          children,
        ),
      ];
    };
  }

  /** 2행 — 필터줄. 이 줄에는 입력칸 말고 아무것도 없다. */
  function filterHeader(params: any) {
    const column = params?.column ?? {};
    const field = column.field as string;
    const current = filterValues.value[field] ?? '';
    const choices = column.filters ?? [];

    const control = isChoiceColumn(column)
      ? /**
         * 고르는 칸은 **엑셀 필터처럼 여러 개**를 고를 수 있다.
         *
         * 목록이 표 머리글 안에서 잘리지 않게 팝업을 `body` 에 붙인다
         * (머리글은 `overflow: hidden` 이다).
         * 고른 것이 많으면 태그가 칸을 넘치므로 `maxTagCount: 'responsive'` 로
         * "+N" 으로 접는다 — 필터줄 높이가 흔들리면 표가 다시 그려진다.
         */
        h(Select, {
          allowClear: true,
          class: 'jsini-filterrow__select',
          getPopupContainer: () => document.body,
          maxTagCount: 'responsive',
          mode: 'multiple',
          onClick: swallow,
          onDblclick: swallow,
          onKeydown: swallow,
          onMousedown: swallow,
          'onUpdate:value': (next: any) =>
            applyFilter(field, Array.isArray(next) ? next : [], true),
          options: choices.map((f: any) => ({ label: f.label, value: f.value })),
          placeholder: $t('common.all'),
          size: 'small',
          value: toArray(current),
        })
      : h('input', {
          class: 'jsini-filterrow__input',
          placeholder: $t('common.search'),
          type: 'text',
          value: current,
          onClick: swallow,
          onDblclick: swallow,
          onInput: (event: Event) =>
            applyFilter(field, (event.target as HTMLInputElement).value, false),
          onKeydown: swallow,
          onMousedown: swallow,
        });

    return [h('div', { class: 'jsini-filterrow__cell' }, [control])];
  }

  /** 필터줄에 든 값을 모두 비운다. 도구줄의 '필터 초기화' 가 부른다. */
  function resetFilters() {
    timers.forEach((t) => clearTimeout(t));
    timers.clear();
    filterValues.value = {};

    const grid = getGrid();
    grid?.clearFilter?.();
    grid?.updateData?.();
  }

  return { createTitleHeader, filterHeader, resetFilters };
}

/**
 * 그리드 아래 도구줄.
 *
 * ============================================================
 * 어디에 그려지는가
 * ============================================================
 *
 * **페이저가 있든 없든 그리드 아래에 줄이 하나 있고, 그 왼쪽에 아이콘이 선다.**
 * 자리가 둘이라 두 곳에 모두 심어 두고 CSS 로 하나만 보인다.
 *
 * | 그리드 | 그리는 자리 |
 * |---|---|
 * | 페이저 있음 | `pagerConfig.slots.left` → 페이저 줄 **왼쪽 끝** |
 * | 페이저 없음 | 그리드의 `bottom` 슬롯 → 표 아래 줄 |
 *
 * 어느 쪽인지 코드로 미리 알기 어렵다(프리셋·전역·화면 설정이 겹쳐 정해진다).
 * 그래서 둘 다 그려 두고, 페이저 영역이 실제로 있으면 아래쪽 것을 숨긴다
 * (`styles/index.css` 의 `:has(> .vxe-grid--pager-wrapper)`).
 */
function createToolsRenderer(
  getGrid: () => any,
  getApi: () => any,
  resetFilters: () => void,
  flags: Required<Pick<GridFeatureFlags, 'filter'>> & GridFeatureFlags,
  canQuery: boolean,
) {
  function onExport() {
    getGrid()?.exportData?.({
      filename: flags.exportName ?? 'export',
      isHeader: true,
      original: false,
      type: 'xlsx',
    });
  }

  function onRefresh() {
    if (flags.onRefresh) {
      flags.onRefresh();
      return;
    }
    getApi()?.query?.();
  }

  function button(icon: string, title: string, onClick: () => void) {
    return h(
      'button',
      { class: 'jsini-gridtools__btn', onClick, title, type: 'button' },
      [h(IconifyIcon, { class: 'size-4', icon })],
    );
  }

  return (placement: 'bottom' | 'pager') => {
    const buttons = [
      button('lucide:file-spreadsheet', $t('common.exportExcel'), onExport),
    ];

    // 눌러도 아무 일 없는 단추는 두지 않는다.
    if (canQuery || flags.onRefresh) {
      buttons.push(button('lucide:refresh-cw', $t('common.refresh'), onRefresh));
    }
    if (flags.filter) {
      buttons.push(
        button('lucide:filter-x', $t('common.resetFilter'), resetFilters),
      );
    }

    return h(
      'div',
      { class: `jsini-gridtools jsini-gridtools--${placement}` },
      buttons,
    );
  };
}

/** 이 칸에 정렬·필터를 걸 수 있는가. */
function isDataColumn(column: any): boolean {
  if (!column?.field) return false;
  if (NON_DATA_FIELDS.has(String(column.field).toLowerCase())) return false;
  if (column.type && NON_DATA_TYPES.has(column.type)) return false;
  if (column.cellRender?.name && NON_DATA_RENDERERS.has(column.cellRender.name)) {
    return false;
  }
  return true;
}

type Renderers = ReturnType<typeof createHeaderRenderers>;

function decorate(
  column: any,
  flags: Required<GridFeatureFlags>,
  renderers: Renderers,
): any {
  // 화면이 이미 묶어 둔 머리글은 자식만 손본다.
  if (Array.isArray(column?.children) && column.children.length > 0) {
    return {
      ...column,
      children: column.children.map((child: any) =>
        decorate(child, flags, renderers),
      ),
    };
  }

  if (!isDataColumn(column)) return column;

  const params = column.params ?? {};
  const leaf: any = { ...column };

  const sortable =
    flags.sort && params.sort !== false
      ? (leaf.sortable ?? true)
      : leaf.sortable === true;
  leaf.sortable = sortable;

  const wantFilter =
    flags.filter && params.filter !== false && !column.slots?.header;

  if (!wantFilter) return leaf;

  // 이미 화면이 조건을 적어 두었으면 그대로 쓴다. 없을 때만 만들어 준다.
  if (!Array.isArray(leaf.filters) || leaf.filters.length === 0) {
    const choices = params.filterOptions ?? optionsFromCellTag(column);
    Object.assign(
      leaf,
      choices ? choiceFilter(choices) : textFilter(column.field, params.filterText),
    );
  }

  // 팝업에 같은 입력칸이 두 번 생기지 않게 한다.
  if (leaf.filterRender) leaf.filterRender = undefined;

  leaf.title = '';
  leaf.slots = { ...leaf.slots, header: renderers.filterHeader };
  leaf.headerClassName = [column.headerClassName, FILTER_ROW_CLASS]
    .filter(Boolean)
    .join(' ');

  /**
   * 묶음(1행) — 이름과 정렬만 맡는다.
   *
   * `fixed` 는 **묶음 쪽에 있어야 한다.** 아래 칸에 남겨 두면 vxe 가 고정 열을
   * 가르는 기준을 잃어 머리글과 본문이 어긋난다.
   */
  const group: any = {
    align: column.align,
    children: [leaf],
    headerAlign: column.headerAlign ?? column.align ?? 'center',
    headerClassName: TITLE_ROW_CLASS,
    slots: { header: renderers.createTitleHeader(column.field, column.title, sortable) },
    title: column.title,
  };

  if (column.fixed) {
    group.fixed = column.fixed;
    leaf.fixed = undefined;
  }

  return group;
}

/**
 * `useVbenVxeGrid` 에 넘어가는 설정을 받아 컬럼에 정렬·필터줄을 심는다.
 *
 * `getGrid` 는 **나중에** 만들어지는 그리드 인스턴스를 꺼내는 함수다 —
 * 컬럼을 손볼 시점에는 아직 그리드가 없어서 이렇게 미뤄 받는다.
 *
 * 돌려주는 `decorateColumns` 를 함께 쓰는 이유가 있다. **컬럼을 처음 한 번만
 * 주는 화면이 전부가 아니다.** 부서 관리처럼 `gridApi.setGridOptions({ columns })`
 * 로 나중에 갈아끼우거나, 역할-메뉴 탭처럼 `grid.loadColumn()` 으로 넣는 화면이 있다.
 * 그 경로로 들어온 컬럼도 같은 손질을 거쳐야 필터줄이 사라지지 않는다.
 * 필터 값을 함께 물려주므로 갈아끼운 뒤에도 치고 있던 값이 남는다.
 */
export function createGridFeatures(
  options: any,
  getGrid: () => any,
  getApi: () => any,
) {
  const gridOptions = options?.gridOptions;

  const flags: GridFeatureFlags & Required<Pick<GridFeatureFlags, 'filter' | 'sort' | 'tools'>> = {
    filter: true,
    sort: true,
    tools: true,
    ...(gridOptions?.gridFeatures ?? {}),
  };

  const noop = {
    decorateColumns: (columns: any) => columns,
    options,
    renderTools: null,
  };

  if (!gridOptions) return noop;
  if (!flags.sort && !flags.filter && !flags.tools) return noop;

  const renderers = createHeaderRenderers(getGrid);

  const decorateColumns = (columns: any) =>
    Array.isArray(columns)
      ? columns.map((column: any) => decorate(column, flags as any, renderers))
      : columns;

  const nextGridOptions = { ...gridOptions };
  delete nextGridOptions.gridFeatures;
  if (Array.isArray(nextGridOptions.columns)) {
    nextGridOptions.columns = decorateColumns(nextGridOptions.columns);
  }

  let renderTools = null;

  if (flags.tools) {
    renderTools = createToolsRenderer(
      getGrid,
      getApi,
      renderers.resetFilters,
      flags,
      // 조회 함수가 있어야 재조회를 부를 수 있다.
      Boolean(gridOptions.proxyConfig?.ajax?.query),
    );

    /**
     * 페이저가 뜨는 그리드에서는 페이저 줄 왼쪽에 붙는다.
     *
     * 슬롯 **이름**(문자열) 대신 **렌더 함수**를 준다. vxe 는 둘 다 받는데,
     * 이름을 주면 그리드의 슬롯 목록에서 찾아보고 없으면
     * "슬롯 …은 존재하지 않습니다" 를 콘솔에 찍는다 — vben 래퍼를 한 번 거치느라
     * 그 목록에 안 잡히는 그리드가 있었다. 함수는 그 조회를 건너뛴다.
     */
    const toolsRenderer = renderTools;
    nextGridOptions.pagerConfig = {
      ...nextGridOptions.pagerConfig,
      slots: {
        ...nextGridOptions.pagerConfig?.slots,
        left: () => toolsRenderer('pager'),
      },
    };
  }

  return {
    decorateColumns,
    options: { ...options, gridOptions: nextGridOptions },
    renderTools,
  };
}
