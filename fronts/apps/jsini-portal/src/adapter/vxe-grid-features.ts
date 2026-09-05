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
 *   │ [       ]│ [       ]│ [     ▾]│  ← 2행: 필터만
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
 * 필터줄은 **처음에 접혀 있다**
 * ============================================================
 *
 * 기능은 모든 그리드에 붙지만, 줄이 처음부터 펼쳐져 있으면 머리글이 늘 두 줄이라
 * 표 본문이 그만큼 깎인다(준수사항 4번 — 세로 스크롤 없이 한 화면). 거르는 일은
 * 늘 하는 일이 아니므로 **필요할 때 펼치는 쪽**으로 뒤집었다.
 *
 * 펼치고 접는 것은 아래 도구줄의 깔때기 아이콘이다 — '필터 초기화' 바로 옆이다.
 * 접을 때는 걸려 있던 값을 **함께 비운다.** 안 보이는 필터가 자료를 거르고 있으면
 * 왜 행이 안 나오는지 알아낼 방법이 없다. 그래서 접힌 동안에는 '필터 초기화'
 * 아이콘도 빼 둔다 — 눌러도 아무 일 없는 단추를 두지 않는다는 이 파일의 규칙이다.
 *
 * 감추는 방법은 **CSS 한 줄**이다(`styles/index.css` 의 `.jsini-nofilter`).
 * 컬럼 묶음 자체는 그대로 둔다 — 접을 때마다 컬럼을 다시 만들면 폭·정렬·
 * 보이는 컬럼 고르기 같은 상태가 매번 날아간다.
 *
 * **도구줄이 없는 그리드(`tools: false`)는 펼친 채로 둔다.** 접는 단추가 없으니
 * 접어 두면 다시 펼 길이 없다.
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
 * | `gridOptions.gridFeatures: { filterVisible: true }` | 필터줄을 **펼친 채로** 연다 |
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
 *     덮으면 그 화면이 깨진다 (지금은 그런 화면이 없다. `/system/menu` 가 마지막이었는데
 *     자체 머리글 필터를 걷어내고 이 공통 필터줄로 옮겼다)
 */

import { h, nextTick, ref, type Ref } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { preferences } from '@vben/preferences';

import { Select } from 'ant-design-vue';

import { $t, $te } from '#/locales';
import { can } from '#/utils/permission';

/** 값 칸이 아니라서 정렬·필터를 걸지 않는 칸의 `type`. */
const NON_DATA_TYPES = new Set(['checkbox', 'expand', 'html', 'radio', 'seq']);

/** 그리는 것이 값이 아니라 버튼인 칸. */
const NON_DATA_RENDERERS = new Set(['CellOperation']);

/** 이미 순번 칸이 있는가. 화면이 직접 둔 것을 존중한다. */
function hasSeqColumn(columns: any[]): boolean {
  return columns.some(
    (column: any) =>
      column?.type === 'seq' ||
      (Array.isArray(column?.children) && hasSeqColumn(column.children)),
  );
}

/**
 * 맨 앞 순번 칸.
 *
 * `type: 'seq'` 는 vxe 가 1 · 2 · 3 … 을 그려 주는 칸이다. 값 칸이 아니므로
 * (`NON_DATA_TYPES`) 정렬·필터줄이 붙지 않고, 머리글은 두 줄에 걸쳐 하나로 그려진다.
 *
 * `fixed: 'left'` 인 이유: 옆으로 스크롤해도 몇 번째 줄인지 보여야 하고, 뒤에
 * 고정 칸을 둔 화면(메뉴 관리 등)에서 **고정 칸은 왼쪽부터 붙어 있어야** 한다 —
 * 고정 아닌 칸이 그 앞에 끼면 머리글과 본문이 어긋난다.
 * 모바일은 고정 칸을 쓰지 않으므로(`adjustGridForMobile`) 여기서도 뺀다.
 */
function seqColumn() {
  return {
    align: 'center',
    field: '__seq',
    fixed: preferences.app.isMobile ? undefined : 'left',
    resizable: false,
    title: $t('common.seq'),
    type: 'seq',
    width: 56,
  };
}

/**
 * 셀을 두 번 눌러 고칠 수 있는 칸인가.
 *
 * vxe 의 판정과 같다 — `editRender` 가 있고 `enabled: false` 가 아니면 편집 칸이다.
 * (표 단위 `editConfig` 는 공통 프리셋이 늘 넣어 준다 — `use-vxe-grid.vue`)
 */
function isEditableColumn(column: any) {
  const editRender = column?.editRender;
  return Boolean(editRender) && editRender.enabled !== false;
}

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

/**
 * '필터 사용 안 함' 아이콘 — **깔때기에 빗금**. 여기만 lucide 가 아니다.
 *
 * lucide 에는 `filter-off` · `funnel-off` 가 **없다**(2026-09-05, `@iconify/json`
 * 2.2.519 확인). 없는 이름을 주면 `@iconify/vue` 가 빈 `<svg>` 만 그려서
 * 자리는 차지하는데 아무것도 안 보인다 — 처음에 `lucide:filter-off` 라 적었다가
 * 겪은 일이다. **다시 lucide 로 바꾸지 말 것.**
 *
 * 남은 후보 중 `funnel-x` 는 못 쓴다. 바로 왼쪽 '필터 초기화'(`filter-x`)와
 * 그림이 거의 같아 둘을 가를 수 없다.
 *
 * tabler 를 고른 이유는 **그리는 규격이 lucide 와 같아서**다 — 24 격자 ·
 * 선 두께 2 · 둥근 끝 · `currentColor`. 한 줄에 섞여도 굵기가 튀지 않는다.
 * (아이콘은 이 앱 전체가 iconify 로 받아 온다. 새로 여는 통로가 아니다.)
 */
const FILTER_OFF_ICON = 'tabler:filter-off';

/** 필터줄 칸에 붙는 표시. 정렬 화살표를 감추고 띠 모양을 주는 데 쓴다. */
const FILTER_ROW_CLASS = 'jsini-filterrow';

/** 이름줄 칸에 붙는 표시. 누를 수 있다는 손 모양을 준다. */
const TITLE_ROW_CLASS = 'jsini-titlerow';

/**
 * 필터줄이 **접혀 있을 때** 그리드 뿌리에 붙는 표시.
 *
 * 이것 하나로 `styles/index.css` 가 필터줄(2행)을 감춘다. 붙이는 자리는
 * `adapter/vxe-table.ts` 의 감싸개 — 그리드의 `gridClass` 로 넘긴다.
 */
export const FILTER_HIDDEN_CLASS = 'jsini-nofilter';

/**
 * 이 칸이 **필터줄(2행) 칸**인가.
 *
 * 필터줄 칸은 값 칸이 아니라 입력칸을 담으려고 우리가 만든 자리다. 이름이 없고
 * (`title: ''`) 보이는 컬럼 고르기·엑셀 머리글 같은 "칸 목록" 에 끼면 안 된다.
 * 판정 기준을 한 곳에 둔다 — 두 벌이 되면 한쪽만 고쳐지고 어긋난다.
 */
function isFilterRowColumn(column: any): boolean {
  return (
    typeof column?.headerClassName === 'string' &&
    column.headerClassName.includes(FILTER_ROW_CLASS)
  );
}

/**
 * 엑셀에서 **필터줄을 뺀다.**
 *
 * 필터줄은 칸을 묶음(1행 이름 · 2행 필터)으로 만들어 얻는다. 그런데 vxe 의
 * 엑셀 내보내기는 **머리글 층마다 한 줄**을 쓴다
 * (`vxe-table-plugin-export-xlsx` — `colgroups.forEach`). 필터줄 칸은 이름이
 * 비어 있으므로(`leaf.title = ''`) 받은 파일에는 **빈 줄 하나**가 이름줄 아래
 * 붙어 나온다. 자료를 붙여 쓰려면 그 줄을 매번 지워야 했다.
 *
 * `isColgroup: false` 로 층을 접으면 될 것 같지만 안 된다 — 그때 vxe 는
 * **맨 아래 칸**의 이름을 쓰는데 그것이 바로 이름이 빈 필터줄 칸이다.
 * 머리글이 통째로 비어 버린다. 화면이 진짜 묶음 머리글을 쓰는 경우
 * (묶음이 3층이 된다) 그 묶음 이름도 함께 사라진다.
 *
 * 그래서 층 목록에서 **필터줄 층만** 덜어 낸다. `beforeExportMethod` 는
 * vxe 가 층 목록을 다 만든 뒤 · 내보내기 플러그인이 그것을 읽기 전에 불린다.
 *
 * `_rowSpan` 은 **내보내기 경로에서만** 쓰이고 끝나면 vxe 가 지운다
 * (`clearColumnConvert`). 화면 머리글은 이 값을 보지 않으므로 고쳐도 안전하다.
 */
function dropFilterHeaderRow(options: any) {
  const levels: any[][] = options?.colgroups;
  if (!Array.isArray(levels) || levels.length < 2) return;

  /**
   * 필터줄은 늘 **맨 아래 층**이다. 그리고 그 층에는 필터줄 칸만 있다 —
   * 필터를 끈 칸(`params.filter: false`)은 묶이지 않아 위층에서 두 줄을
   * 걸치므로 이 층에 나타나지 않는다.
   */
  const last = levels.at(-1);
  if (!last?.length || !last.every(isFilterRowColumn)) return;

  levels.pop();

  /**
   * 필터를 끈 칸은 없어진 층까지 세로로 걸치고 있었다. 그대로 두면 병합이
   * 자료 첫 줄을 먹는다. 남은 층 수에 맞춰 줄인다.
   */
  levels.forEach((cols, rowIndex) => {
    const max = levels.length - rowIndex;
    cols.forEach((col: any) => {
      if (col._rowSpan > max) col._rowSpan = max;
    });
  });
}

/** 내보내기 그림의 기본 크기(px). 고인사진처럼 세로로 긴 사진 기준이다. */
const EXPORT_IMAGE_WIDTH = 60;
const EXPORT_IMAGE_HEIGHT = 72;

/** 엑셀 폭 단위 하나 ≈ 8px. 플러그인이 컬럼 폭을 `renderWidth / 8` 로 어림하는 것과 같다. */
const EXCEL_COL_PX = 8;

/** 엑셀 행 높이는 포인트다. 플러그인의 `setExcelRowHeight` 와 같은 px→pt 환산. */
const PX_TO_PT = 0.75;

export interface GridExportImageOptions {
  /** 사진이 든 컬럼의 `field`. */
  field: string;
  /** 행에서 사진 주소를 얻는다. 사진 없는 행은 `undefined` 를 돌려주면 건너뛴다. */
  urlOf: (row: any) => null | string | undefined;
  /** 셀에 앉힐 최대 폭(px). 비율은 지킨다. 기본 60 */
  width?: number;
  /** 셀에 앉힐 최대 높이(px). 기본 72 */
  height?: number;
}

/** 미리 내려받아 둔 사진 한 장 — 엑셀에 넣을 수 있는 형태다. */
interface PreparedImage {
  base64: string;
  height: number;
  width: number;
}

/**
 * 내보내기 전에 각 행의 사진을 **미리** 내려받아 둔다.
 *
 * 순서가 이런 이유: 플러그인은 `sheetMethod` 를 **동기로** 부르고 곧바로
 * `writeBuffer` 한다(`vxe-table-plugin-export-xlsx` — `exportMethod`).
 * 훅 안에서 내려받으면 파일이 먼저 나가 버리므로, 받는 일은 여기서 끝내고
 * 훅에는 완성된 그림만 넘긴다.
 *
 * 캔버스로 PNG 를 다시 만드는 이유: ExcelJS 는 jpeg·png·gif 만 받아서
 * 썸네일이 webp 로 와도 그대로는 못 넣는다. 캔버스를 거치면 브라우저가 여는
 * 형식이 전부 PNG 로 통일되고, 원본 비율에 맞춘 크기도 함께 얻는다.
 *
 * 못 받은 행(주소 없음 · 404 · 외부 주소의 CORS 거절)은 조용히 건너뛴다 —
 * 사진 한 장 때문에 내보내기 전체가 막히면 안 된다.
 */
async function prepareExportImages(
  grid: any,
  opts: GridExportImageOptions,
): Promise<Map<any, PreparedImage>> {
  const rows: any[] = grid?.getTableData?.()?.fullData ?? [];
  const boxWidth = opts.width ?? EXPORT_IMAGE_WIDTH;
  const boxHeight = opts.height ?? EXPORT_IMAGE_HEIGHT;
  const images = new Map<any, PreparedImage>();

  await Promise.all(
    rows.map(async (row) => {
      const url = opts.urlOf(row);
      if (!url) return;
      try {
        // FileServer 를 지나는 주소는 `jsini_file_at` 쿠키가 있어야 열린다(27번 문서 5절).
        const response = await fetch(url, { credentials: 'include' });
        if (!response.ok) return;
        const bitmap = await createImageBitmap(await response.blob());
        // 칸보다 크면 비율대로 줄이고, 작은 사진은 키우지 않는다.
        const scale = Math.min(
          boxWidth / bitmap.width,
          boxHeight / bitmap.height,
          1,
        );
        const width = Math.max(1, Math.round(bitmap.width * scale));
        const height = Math.max(1, Math.round(bitmap.height * scale));
        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        canvas.getContext('2d')?.drawImage(bitmap, 0, 0, width, height);
        bitmap.close();
        images.set(row, {
          base64: canvas.toDataURL('image/png'),
          height,
          width,
        });
      } catch {
        // 이 행만 그림 없이 나간다.
      }
    }),
  );

  return images;
}

/**
 * `sheetMethod` — 시트가 다 채워진 뒤 사진 칸 위에 그림을 앉힌다.
 *
 * 엑셀에서 그림은 셀 "안"의 값이 아니라 셀 위에 **앵커로 떠 있는** 개체다.
 * 보기에는 셀에 든 것처럼 나오지만, 받은 사람이 엑셀에서 다시 정렬하면
 * 그림은 따라가지 않는다(엑셀의 셀-내-이미지 기능을 ExcelJS 가 아직 모른다).
 *
 * 행 위치는 플러그인과 같은 조건으로 센다 — 머리글 줄 수는
 * `isColgroup && colgroups` 면 층 수, 아니면 한 줄이다(`exportXLSX` 의 colList).
 * `colgroups` 는 `dropFilterHeaderRow` 가 필터줄 층을 덜어 낸 **뒤의** 것이
 * 그대로 온다(같은 배열을 고치므로).
 */
export function embedExportImages(
  params: any,
  opts: GridExportImageOptions,
  images: Map<any, PreparedImage>,
) {
  const { colgroups, columns, datas, options, workbook, worksheet } = params;

  const columnIndex = columns.findIndex((c: any) => c.field === opts.field);
  // 보이는 컬럼 고르기로 사진 칸을 뺀 채 내보내면 그림도 뺀다.
  if (columnIndex < 0) return;

  const headerRowCount =
    options.isHeader === false
      ? 0
      : options.isColgroup && Array.isArray(colgroups) && colgroups.length > 0
        ? colgroups.length
        : 1;

  const boxWidth = opts.width ?? EXPORT_IMAGE_WIDTH;
  const boxHeight = opts.height ?? EXPORT_IMAGE_HEIGHT;

  // 사진 칸이 그림보다 좁으면 넓힌다.
  const column = worksheet.getColumn(columnIndex + 1);
  const wantColumnWidth = Math.ceil(boxWidth / EXCEL_COL_PX) + 1;
  if (!column.width || column.width < wantColumnWidth) {
    column.width = wantColumnWidth;
  }

  datas.forEach((item: any, index: number) => {
    // `addImage` 의 tl 좌표는 0 기준, ExcelJS 의 행·셀 접근은 1 기준이다.
    const rowIndex = headerRowCount + index;
    const excelRow = worksheet.getRow(rowIndex + 1);

    // 사진 칸의 글자 값(URL 따위)이 그림 뒤에 남지 않게 지운다.
    excelRow.getCell(columnIndex + 1).value = '';

    const image = images.get(item._row ?? item);
    if (!image) return;

    const wantRowHeight = (boxHeight + 8) * PX_TO_PT;
    if (!excelRow.height || excelRow.height < wantRowHeight) {
      excelRow.height = wantRowHeight;
    }

    worksheet.addImage(
      workbook.addImage({ base64: image.base64, extension: 'png' }),
      {
        editAs: 'oneCell',
        ext: { height: image.height, width: image.width },
        tl: { col: columnIndex + 0.1, row: rowIndex + 0.1 },
      },
    );
  });
}

export interface GridFeatureFlags {
  /**
   * 엑셀 내보내기에 **사진을 그림으로** 넣는다.
   *
   * 셀에 `<img>` 를 그리는 칸(고인 관리의 고인사진)은 그냥 내보내면 빈 칸이나
   * URL 문자열로 나간다. 이걸 적으면 내보내기 전에 사진을 내려받아 그 칸 위에
   * 그림으로 앉히고, 행 높이도 그림에 맞게 키운다.
   */
  exportImage?: GridExportImageOptions;
  /** 엑셀 파일 이름(확장자 제외). 기본 `export` */
  exportName?: string;
  /** 필터 전용 행. 기본 `true` */
  filter?: boolean;
  /**
   * 필터줄을 **펼친 채로** 열지. 기본 `false` — 접혀 있고, 도구줄의 깔때기로 편다.
   *
   * 도구줄이 없는 그리드(`tools: false`)는 접는 단추가 없으므로 이 값을 적지
   * 않아도 펼친 채로 뜬다.
   */
  filterVisible?: boolean;
  /**
   * 필터줄 값이 바뀐 뒤 불린다. `active` 는 걸린 필터가 하나라도 있는가.
   *
   * 필터줄은 `setFilter()` 로 거는 프로그램 방식이라 vxe 의 `filterChange`
   * 이벤트가 **오지 않는다.** 필터가 걸렸는지에 따라 동작을 바꿔야 하는 화면
   * (메뉴 관리 — 필터 중에는 드래그 저장을 잠근다)이 이걸로 상태를 받는다.
   */
  onFilterChange?: (active: boolean) => void;
  /**
   * 재조회를 화면이 직접 맡을 때 적는다.
   *
   * `proxyConfig.ajax.query` 로 자료를 받는 그리드는 안 적어도 된다 —
   * 그쪽은 `gridApi.query()` 로 다시 부르면 된다. `:table-data` 로 자료를
   * 넣는 그리드는 그리드가 조회 방법을 모르므로 이걸 안 주면 **재조회 아이콘이
   * 아예 안 나온다**(눌러도 아무 일 없는 단추를 두지 않으려는 것이다).
   */
  onRefresh?: () => void;
  /**
   * 이름줄 클릭으로 정렬이 바뀐 뒤 불린다. `active` 는 선 정렬이 하나라도 있는가.
   * `grid.sort()` 도 프로그램 호출이라 vxe 의 `sortChange` 가 오지 않는다.
   */
  onSortChange?: (active: boolean) => void;
  /**
   * 화면이 자료를 **추가**하는 방법. 주면 아래 도구줄에 [추가] 아이콘이 생긴다.
   *
   * 공통 그리드는 화면마다 무엇을 어떻게 추가하는지 알 수 없다. 그래서
   * 그 일을 하는 함수를 화면이 넘겨 준다 — 보통 위쪽 [등록] 단추가 부르는 것과
   * 같은 함수다. **안 주면 아이콘이 아예 안 나온다**(눌러도 아무 일 없는 단추를
   * 두지 않으려는 것이다 — 재조회 아이콘과 같은 규칙).
   *
   * 등록 권한이 없는 사람에게는 이 아이콘도 나오지 않는다.
   */
  onCreate?: () => void;
  /**
   * 맨 앞 순번 칸(1 · 2 · 3 …). 기본 `true`
   *
   * 화면이 이미 `type: 'seq'` 칸을 두었으면 더 넣지 않는다.
   */
  seq?: boolean;
  /** 컬럼 정렬. 기본 `true` */
  sort?: boolean;
  /**
   * 아래 도구줄(보이는 컬럼 · 전체화면 · 엑셀 · 재조회 · 필터 초기화 · 필터 펴기).
   * 기본 `true`
   */
  tools?: boolean;
  /**
   * 아래 도구줄의 **보이는 컬럼 고르기**. 기본 `true`
   *
   * 컬럼이 화면마다 갈아끼워지는 그리드(역할-메뉴 탭처럼 `loadColumn` 을 직접
   * 부르는 곳)에서 감추고 싶을 때 끈다.
   */
  toolsColumns?: boolean;
  /** 아래 도구줄의 **전체화면**. 기본 `true` */
  toolsZoom?: boolean;
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

/**
 * 입력칸에 붙일 이름. 눈에는 안 보이고 화면 낭독기만 읽는다.
 *
 * 2행 잎 칸의 `title` 은 비어 있으므로(이름은 1행 묶음이 갖는다) 원래 이름을
 * `params.filterTitle` 에 남겨 두고 여기서 꺼낸다. 이름이 번역 키인 화면이
 * 있어서 있으면 번역하고, 없으면 적힌 그대로 읽는다.
 */
function filterAriaLabel(column: any): string {
  const title = column?.params?.filterTitle;
  if (typeof title !== 'string' || !title) return $t('common.search');
  return `${$te(title) ? $t(title) : title} ${$t('common.search')}`;
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
function createHeaderRenderers(
  getGrid: () => any,
  hooks: Pick<GridFeatureFlags, 'onFilterChange' | 'onSortChange'>,
) {
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

      hooks.onFilterChange?.(
        Object.values(filterValues.value).some((v) => !isEmptyFilter(v)),
      );
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

    hooks.onSortChange?.((grid.getSortColumns?.() ?? []).length > 0);
  }

  /**
   * 1행 — 이름줄. 누르면 정렬된다.
   *
   * 묶음 머리글(`children` 을 가진 칸)에는 `field` 가 없으므로 아래 칸의 이름을
   * 닫아서(closure) 받는다.
   *
   * `editable` 이면 이름 옆에 연필을 그린다. vxe 도 편집 칸 머리글에 연필을
   * 붙이지만, `editRender` 가 남는 곳이 2행(필터줄)이라 입력칸 왼쪽에 끼었다.
   * 그 연필은 감추고(styles/index.css) 여기서 이름 옆에 그린다 —
   * 무엇을 고칠 수 있는지는 칸 이름 옆이 읽기 쉽다.
   */
  function createTitleHeader(
    field: string,
    title: any,
    sortable: boolean,
    editable: boolean,
  ) {
    return () => {
      const children: any[] = [
        h('span', { class: 'jsini-titlerow__text' }, title),
      ];

      if (editable) {
        children.push(
          h(IconifyIcon, {
            class: 'jsini-titlerow__edit',
            icon: 'lucide:pencil',
            // 이름줄 전체가 정렬 클릭 대상이라 연필도 눌리면 정렬된다 — 의도한 대로다.
            title: '셀을 두 번 누르면 이 칸을 고칠 수 있습니다',
          }),
        );
      }

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
         *
         * `showArrow` 를 **직접 켠다.** ant-design-vue 4 는 여럿 고르기일 때만
         * 화살표를 기본으로 감추는데(`mode: 'multiple'`), 안내 문구까지 없앤
         * 지금은 빈 칸이 입력칸과 똑같이 보인다. 이 ▾ 하나가 "고르는 칸" 이라는
         * 유일한 표시다(바탕색도 한 톤 낮춘다 — styles/index.css).
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
          showArrow: true,
          size: 'small',
          value: toArray(current),
        })
      : /**
         * 입력칸에는 **안내 문구를 두지 않는다.**
         *
         * 필터줄은 칸마다 같은 말('검색')이 늘어서서 줄 전체가 글자로 시끄러웠다.
         * 무엇을 거르는 칸인지는 바로 위 이름줄이 이미 말하고 있으므로,
         * 빈 칸으로 두는 편이 표가 깔끔하다.
         *
         * 대신 화면 낭독기에는 `aria-label` 로 알린다 — 안내 문구가 그 일도
         * 겸하고 있었기 때문에, 그냥 지우면 이름 없는 입력칸이 된다.
         */
        h('input', {
          'aria-label': filterAriaLabel(column),
          class: 'jsini-filterrow__input',
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

    hooks.onFilterChange?.(false);
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
  filters: {
    /** 지금 필터줄이 펴져 있는가. 렌더가 이 값을 읽으므로 바뀌면 아이콘이 바뀐다. */
    visible: Ref<boolean>;
    /** 필터줄에 든 값을 모두 비운다. */
    reset: () => void;
    /** 필터줄을 펴고 접는다. */
    toggle: () => void;
  },
  flags: Required<Pick<GridFeatureFlags, 'filter'>> & GridFeatureFlags,
  canQuery: boolean,
) {
  /**
   * 전체화면을 눌렀는지 세는 값.
   *
   * `isMaximized()` 는 vxe 내부 상태라 우리 렌더가 그 변화를 못 본다.
   * 이 값을 올려 주면 아이콘과 안내가 함께 바뀐다(정렬 표시와 같은 방식).
   */
  const zoomTick = ref(0);

  /** 보이는 컬럼 고르기. vxe 의 컬럼 설정 창을 연다(전역 설정으로 모달이다). */
  function onToggleColumns() {
    getGrid()?.toggleCustom?.();
  }

  /** 그리드만 전체화면. 다시 누르면 되돌린다. */
  function onToggleZoom() {
    getGrid()?.zoom?.();
    zoomTick.value += 1;
  }

  async function onExport() {
    const grid = getGrid();
    if (!grid) return;

    // 사진 칸이 있는 그리드는 그림부터 준비한다 (`prepareExportImages` 머리말 참고).
    const imageOpts = flags.exportImage;
    const images = imageOpts
      ? await prepareExportImages(grid, imageOpts)
      : undefined;

    grid.exportData?.({
      // 이름줄만 남기고 필터줄은 뺀다 (`dropFilterHeaderRow` 머리말 참고).
      beforeExportMethod: ({ options }: any) => dropFilterHeaderRow(options),
      filename: flags.exportName ?? 'export',
      isHeader: true,
      original: false,
      type: 'xlsx',
      // 그림이 하나도 준비되지 않아도 훅은 넘긴다 — URL 글자를 지우는 일은 남는다.
      ...(imageOpts
        ? {
            sheetMethod: (params: any) =>
              embedExportImages(params, imageOpts, images ?? new Map()),
          }
        : {}),
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
    const buttons: any[] = [];

    /**
     * [추가] — 화면이 `gridFeatures.onCreate` 를 준 경우에만 나온다.
     * 등록 권한이 없으면 화면의 [등록] 단추도 감춰지므로 여기서도 같이 뺀다.
     */
    if (flags.onCreate && can('create')) {
      buttons.push(button('lucide:plus', $t('common.addRow'), flags.onCreate));
    }

    /**
     * 위쪽 도구줄에 있던 둘을 여기로 옮겼다(`createGridFeatures` 가 위쪽을 끈다).
     * 그리드를 다루는 장치가 한자리에 모여야 찾기 쉽고, 화면마다 [등록] 단추와
     * 섞여 있던 위쪽 도구줄도 정리된다.
     */
    if (flags.toolsColumns !== false) {
      buttons.push(
        button('lucide:columns-3', $t('common.chooseColumns'), onToggleColumns),
      );
    }

    if (flags.toolsZoom !== false) {
      // 참조만 해도 이 렌더가 zoomTick 을 따라간다.
      const maximized = zoomTick.value >= 0 && Boolean(getGrid()?.isMaximized?.());
      buttons.push(
        button(
          maximized ? 'lucide:minimize' : 'lucide:maximize',
          maximized ? $t('common.exitFullscreen') : $t('common.fullscreen'),
          onToggleZoom,
        ),
      );
    }

    buttons.push(
      button('lucide:file-spreadsheet', $t('common.exportExcel'), onExport),
    );

    // 눌러도 아무 일 없는 단추는 두지 않는다.
    if (canQuery || flags.onRefresh) {
      buttons.push(button('lucide:refresh-cw', $t('common.refresh'), onRefresh));
    }
    /**
     * 필터 초기화 · 필터 펴기/접기 — 둘은 붙어 있다.
     *
     * 접혀 있는 동안 '초기화' 는 뺀다. 접을 때 값을 이미 비웠으므로 눌러도
     * 아무 일이 없다(재조회 아이콘과 같은 규칙).
     */
    if (flags.filter) {
      if (filters.visible.value) {
        buttons.push(
          button('lucide:filter-x', $t('common.resetFilter'), filters.reset),
        );
      }
      buttons.push(
        button(
          filters.visible.value ? FILTER_OFF_ICON : 'lucide:filter',
          filters.visible.value
            ? $t('common.hideFilter')
            : $t('common.showFilter'),
          filters.toggle,
        ),
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
  } else if (!leaf.filterMethod) {
    /**
     * 목록은 화면이 줬는데 **판정을 안 준** 경우다(회사 관리의 상태 칸).
     *
     * 판정이 없으면 vxe 기본 비교로 떨어진다. 그런데 필터줄은 고른 것마다
     * `checked` 를 세우는 방식이라(여러 개 고르기 = OR), 판정이 우리 것이 아니면
     * 여러 개를 골랐을 때와 빈 값(`null` · `''`)이 섞인 자료에서 결과가 어긋난다.
     * 목록만 그대로 쓰고 판정은 우리 것을 붙여 다른 칸과 똑같이 동작하게 한다.
     */
    const looksLikeChoices = leaf.filters.some(
      (f: any) => f?.label !== undefined,
    );
    leaf.filterMethod = looksLikeChoices
      ? choiceFilter(leaf.filters).filterMethod
      : textFilter(column.field, params.filterText).filterMethod;
  }

  // 팝업에 같은 입력칸이 두 번 생기지 않게 한다.
  if (leaf.filterRender) leaf.filterRender = undefined;

  leaf.title = '';
  // 이름은 1행이 가져가지만, 입력칸의 `aria-label` 이 원래 이름을 필요로 한다.
  leaf.params = { ...params, filterTitle: column.title };
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
    slots: {
      header: renderers.createTitleHeader(
        column.field,
        column.title,
        sortable,
        isEditableColumn(column),
      ),
    },
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
    seq: true,
    tools: true,
    ...(gridOptions?.gridFeatures ?? {}),
  };

  /**
   * 순번 칸을 맨 앞에 붙인다. 이미 있으면 그대로 둔다.
   *
   * 정렬·필터를 모두 끈 그리드에도 순번은 붙는다 — 그래서 아래 `noop` 경로에도
   * 이 함수를 물려 준다.
   */
  const withSeq = (columns: any) => {
    if (!flags.seq || !Array.isArray(columns)) return columns;
    if (hasSeqColumn(columns)) return columns;
    return [seqColumn(), ...columns];
  };

  const noop = {
    decorateColumns: withSeq,
    /** 필터줄이 없는 길이다. 감싸개가 이 값을 보고 표시를 붙일 일도 없다. */
    filtersVisible: null,
    options: gridOptions
      ? {
          ...options,
          gridOptions: (() => {
            const next = { ...gridOptions };
            delete next.gridFeatures;
            if (Array.isArray(next.columns)) next.columns = withSeq(next.columns);
            return next;
          })(),
        }
      : options,
    renderTools: null,
  };

  if (!gridOptions) return noop;
  if (!flags.sort && !flags.filter && !flags.tools) return noop;

  const renderers = createHeaderRenderers(getGrid, flags);

  /**
   * 필터줄이 지금 펴져 있는가.
   *
   * 기본은 **접힘**이다. 다만 도구줄이 없으면 펼 단추가 없으므로 펴 둔다.
   */
  const filtersVisible = ref(flags.filterVisible ?? !flags.tools);

  function toggleFilters() {
    const next = !filtersVisible.value;
    filtersVisible.value = next;

    // 접을 때는 걸려 있던 값을 비운다 — 안 보이는 필터가 자료를 거르면 안 된다.
    if (!next) renderers.resetFilters();

    /**
     * 머리글 높이가 한 줄만큼 바뀐다. vxe 는 그 높이를 **DOM 에서 재서** 본문
     * 높이를 잡으므로, 다시 재라고 알려 주지 않으면 표 아래가 그만큼 비거나 잘린다.
     */
    nextTick(() => getGrid()?.recalculate?.(true));
  }

  const decorateColumns = (columns: any) =>
    Array.isArray(columns)
      ? withSeq(columns).map((column: any) =>
          decorate(column, flags as any, renderers),
        )
      : columns;

  const nextGridOptions = { ...gridOptions };
  delete nextGridOptions.gridFeatures;

  /**
   * 위쪽 도구줄의 아이콘 넷을 끈다 — 아래 도구줄과 같은 기능이라 두 벌이 된다.
   * (공통 프리셋이 `custom · export · refresh · zoom` 을 전부 켜 둔다 —
   *  `use-vxe-grid.vue`. 여기서 적은 값이 프리셋보다 우선한다.)
   *
   * **도구줄 자체를 없애는 것이 아니다.** 그 줄은 화면이 넣은 제목과 [등록] 같은
   * 단추가 사는 자리이고, 그 유무는 슬롯으로만 결정된다(`showToolbar`).
   * 화면이 일부러 적어 둔 값은 덮지 않는다.
   */
  if (flags.tools) {
    nextGridOptions.toolbarConfig = {
      custom: false,
      export: false,
      refresh: false,
      zoom: false,
      ...gridOptions.toolbarConfig,
    };
  }
  /**
   * **보이는 컬럼 고르기 창에서 필터줄 칸을 뺀다.**
   *
   * vxe 의 컬럼 설정 창은 컬럼 나무를 통째로 훑으므로(`customColumnList`),
   * 아무것도 안 하면 값 칸 하나가 목록에 **두 줄**로 나온다 — 이름을 가진
   * 묶음(1행)과 이름이 빈 필터줄 칸(2행)이다. 빈 줄은 무엇을 끄는 것인지
   * 알 수 없고, 그것만 따로 꺼 봐야 머리글에 이름칸만 남아 어그러진다.
   *
   * `visibleMethod` 는 **목록에 줄을 그릴지**를 정한다(vxe 가 두 방식
   * — 모달 · 간단 팝업 — 에서 모두 본다). 필터줄 칸을 빼면 관리 대상은
   * 이름 있는 칸 하나로 정리된다.
   *
   * **끄고 켜는 것은 저절로 따라온다.** vxe 는 칸 하나를 껐다 켤 때
   * `eachTree([column], …)` 로 **자손까지** `visible` 을 함께 바꾼다
   * (`changeCheckboxOption`). 목록에서 뺀 것이지 나무에서 뺀 것이 아니므로
   * 필터줄은 늘 제 이름칸을 따라 사라지고 나타난다.
   *
   * 화면이 일부러 적어 둔 `customConfig` 는 덮지 않는다(도구줄과 같은 규칙).
   */
  if (flags.filter) {
    nextGridOptions.customConfig = {
      visibleMethod: ({ column }: any) => !isFilterRowColumn(column),
      ...gridOptions.customConfig,
    };
  }

  if (Array.isArray(nextGridOptions.columns)) {
    nextGridOptions.columns = decorateColumns(nextGridOptions.columns);
  }

  let renderTools = null;

  if (flags.tools) {
    renderTools = createToolsRenderer(
      getGrid,
      getApi,
      {
        reset: renderers.resetFilters,
        toggle: toggleFilters,
        visible: filtersVisible,
      },
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
    filtersVisible,
    options: { ...options, gridOptions: nextGridOptions },
    renderTools,
  };
}
