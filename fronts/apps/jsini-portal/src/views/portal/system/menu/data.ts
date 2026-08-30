import type { OnActionClickFn, VxeTableGridColumns } from '#/adapter/vxe-table';
import type { SystemMenuApi } from '#/api/portal/system/menu';

import { h } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { Button, Popconfirm, Tag } from 'ant-design-vue';

import { $t, $tIfKey } from '#/locales';
import { can } from '#/utils/permission';

/**
 * 값이 들어 있는지 비교하는 컬럼 필터.
 *
 * [깔때기 팝업이 아니라 **머리글 아래 칸**으로 쓴다]
 *
 * 필터 조건과 판정은 vxe 의 것을 그대로 쓰되(`filters` · `filterMethod`),
 * 값을 넣는 자리만 머리글 아래로 옮겼다. 화면(`list.vue`)의 `#col-filter` 슬롯이
 * 입력칸을 그리고 `setFilter()` 로 아래 `filters[0].data` 를 채운다.
 *
 * 그래서 **`filterRender` 는 두지 않는다** — 두면 깔때기 팝업에도 입력칸이 생겨
 * 같은 조건을 넣는 자리가 두 곳이 된다.
 *
 * @param resolve 비교할 글자를 뽑는 함수. 화면에 보이는 값과 저장된 값이 다른
 *   칸(메뉴명은 번역 키일 수 있다)에서 쓴다.
 */
function textFilter(resolve?: (row: any) => string) {
  return {
    filters: [{ data: '' }],
    filterMethod: ({ column, option, row }: any) => {
      const keyword = String(option.data ?? '').trim().toLowerCase();
      if (!keyword) return true;

      const text = resolve
        ? resolve(row)
        : String(getByPath(row, column.field) ?? '');

      return text.toLowerCase().includes(keyword);
    },
    // 머리글 아래 입력칸을 그린다. 어느 칸인지는 슬롯이 `column` 으로 받는다.
    slots: { header: 'col-filter' },
  };
}

/**
 * 값의 종류가 정해진 칸의 필터. 머리글 아래에 **고르는 칸**을 그린다.
 *
 * 입력창으로 두면 `CATALOG` 같은 저장값을 외워서 쳐야 한다.
 */
function choiceFilter(options: { label: string; value: any }[]) {
  return {
    filters: options.map((o) => ({ label: o.label, value: o.value })),
    slots: { header: 'col-filter' },
  };
}

/**
 * `meta.title` 처럼 점이 든 필드 이름을 따라 값을 꺼낸다.
 * vxe 는 컬럼 값을 그렇게 읽지만, 필터 안에서는 `row[field]` 가 통하지 않는다.
 */
function getByPath(row: any, path: string) {
  return path
    .split('.')
    .reduce((acc, key) => (acc === null || acc === undefined ? acc : acc[key]), row);
}

/**
 * 이 메뉴가 **화면에 보이는 이름**.
 *
 * `meta.title` 에는 번역 키(`page.dashboard.analytics`)와 완성된 글자(`상태관리`)가
 * 섞여 있다. 사이드바와 같은 규칙(`$tIfKey`)으로 옮겨야 사람이 눈으로 본 글자로
 * 찾을 수 있다. 원래 이름(`name`)으로도 걸리게 해 둔다 — 화면에는 '분석' 으로
 * 떠도 사람은 'Analytics' 로 기억하고 있을 수 있다.
 */
function titleHaystack(row: any) {
  return [$tIfKey(row?.meta?.title), row?.meta?.title, row?.name]
    .filter(Boolean)
    .join(' ');
}

export function getMenuTypeOptions() {
  return [
    {
      color: 'processing',
      label: $t('system.menu.typeCatalog'),
      value: 'CATALOG',
    },
    { color: 'default', label: $t('system.menu.typeMenu'), value: 'MENU' },
    { color: 'error', label: $t('system.menu.typeButton'), value: 'BUTTON' },
    {
      color: 'success',
      label: $t('system.menu.typeEmbedded'),
      value: 'EMBEDDED',
    },
    { color: 'warning', label: $t('system.menu.typeLink'), value: 'LINK' },
  ];
}

/**
 * 메뉴 관리 그리드 컬럼.
 *
 * 트리 뷰와 그리드 뷰를 하나로 합치면서 두 가지를 바꿨다.
 *
 * 1. 맨 앞에 `dragSort` 컬럼을 뒀다. 이 칸이 드래그 손잡이가 된다.
 *    (이전에는 템플릿에 `#drag` 슬롯만 있고 이 컬럼이 없어서 그리드 드래그가 동작하지 않았다.)
 * 2. 작업 컬럼에서 Tooltip 을 걷어냈다. Tooltip 은 행마다 팝업 관리자를 만드는데,
 *    메뉴가 200개를 넘어가면 이것만으로 스크롤과 드래그가 눈에 띄게 느려진다.
 *    같은 안내는 `title` 속성으로 대신한다.
 */
export function useColumns(
  onActionClick: OnActionClickFn<SystemMenuApi.SystemMenu>,
  /**
   * 상태 배지를 눌렀을 때. 넘기지 않으면 배지는 읽기 전용으로 그려진다.
   * 수정 권한이 없을 때도 읽기 전용이 된다.
   */
  onStatusToggle?: (row: SystemMenuApi.SystemMenu) => void,
): VxeTableGridColumns<SystemMenuApi.SystemMenu> {
  return [
    {
      align: 'center',
      dragSort: true,
      field: 'drag',
      fixed: 'left',
      resizable: false,
      slots: { default: 'drag' },
      title: '',
      width: 40,
    },
    {
      // 보이는 이름·번역 키·원래 이름 어느 것으로 쳐도 걸린다.
      ...textFilter(titleHaystack),
      align: 'left',
      field: 'meta.title',
      fixed: 'left',
      minWidth: 260,
      // **위 스프레드가 넣어 준 `slots` 를 여기서 덮는다.** 이 칸은 본문도 슬롯으로
      // 그리므로 둘을 합쳐 적어야 한다 — 하나만 적으면 다른 하나가 사라진다.
      slots: { default: 'title', header: 'col-filter' },
      // 정렬은 **형제끼리** 이뤄진다(트리 칸이라 부모 밑에서만 다시 선다).
      // 저장된 값(`meta.title`)을 기준으로 서므로 번역 키인 메뉴는 키 순서로 선다 —
      // 눈에 보이는 글자와 다를 수 있다. 찾는 것이 목적이면 아래 필터가 낫다.
      sortable: true,
      title: $t('system.menu.menuTitle'),
      treeNode: true,
    },
    {
      // [값의 종류가 정해져 있으므로 고르는 칸으로 둔다]
      //
      // `params.filterList` 는 쓰지 않는다. 그 옵션은 프레임워크가 필터를 열 때마다
      // **화면 데이터에서 값을 긁어 목록을 새로 만들어 덮어쓴다**
      // (`use-vxe-grid.vue` 의 `onFilterVisible`). 그러면 두 가지가 나빠진다 —
      //   · 이름표가 **저장된 값 그대로** 나온다(`디렉토리` 가 아니라 `CATALOG`).
      //   · 트리 그리드에서는 긁어 오는 범위가 일부라 **없는 종류가 목록에서 빠진다.**
      // 여기서는 종류 다섯 개를 이미 알고 있으므로 직접 적어 준다.
      ...choiceFilter(getMenuTypeOptions()),
      align: 'center',
      cellRender: { name: 'CellTag', options: getMenuTypeOptions() },
      field: 'type',
      sortable: true,
      title: $t('system.menu.type'),
      width: 100,
    },
    {
      align: 'right',
      editRender: { name: 'VxeNumberInput' },
      field: 'meta.order',
      // 형제끼리의 순번이다. 정렬을 걸면 형제 안에서 순번대로 서므로
      // "이 부모 밑이 순번대로 되어 있나" 를 확인할 때 쓴다.
      //
      // 필터는 뺀다 — 숫자라 입력 필터가 쓸모없다(34번 문서 2절).
      // 공통 레이어(`adapter/vxe-grid-features.ts`)가 값 칸에 자동으로 필터를 다는데,
      // 이 칸은 머리글을 직접 그리지 않아 그 대상이 되므로 여기서 꺼 준다.
      params: { filter: false },
      sortable: true,
      title: $t('system.menu.order'),
      width: 80,
    },
    {
      align: 'left',
      editRender: { name: 'VxeInput' },
      field: 'name',
      sortable: true,
      title: $t('system.menu.menuName'),
      width: 180,
      ...textFilter(),
    },
    {
      editRender: { name: 'VxeInput' },
      field: 'authCode',
      sortable: true,
      title: $t('system.menu.authCode'),
      width: 180,
      ...textFilter(),
    },
    {
      align: 'left',
      editRender: { name: 'VxeInput' },
      field: 'path',
      sortable: true,
      title: $t('system.menu.path'),
      width: 200,
      ...textFilter(),
    },
    {
      align: 'left',
      editRender: { name: 'VxeInput' },
      field: 'component',
      formatter: ({ row }) => {
        switch (row.type) {
          case 'CATALOG':
          case 'MENU': {
            return row.component ?? '';
          }
          case 'EMBEDDED': {
            return row.meta?.iframeSrc ?? '';
          }
          case 'LINK': {
            return row.meta?.link ?? '';
          }
        }
        return '';
      },
      minWidth: 200,
      sortable: true,
      title: $t('system.menu.component'),
      // 이 칸은 종류에 따라 다른 값을 보여 준다(컴포넌트 · iframe 주소 · 링크).
      // 그래서 `component` 하나만 보면 화면과 어긋난다 — 세 값을 함께 훑는다.
      ...textFilter((row) =>
        [row.component, row.meta?.iframeSrc, row.meta?.link]
          .filter(Boolean)
          .join(' '),
      ),
    },
    {
      // 위 `type` 과 같은 이유로 `params.filterList` 를 쓰지 않는다.
      // 그대로 두면 이름표가 `1` · `0` 으로 나온다. 배지에 쓰는 말과 맞춘다.
      ...choiceFilter([
        { label: $t('common.enabled'), value: 1 },
        { label: $t('common.disabled'), value: 0 },
      ]),
      align: 'center',
      field: 'status',
      sortable: true,
      title: $t('system.menu.status'),
      width: 90,
      // CellTag 대신 슬롯으로 그린다. 배지를 눌러 그 자리에서 켜고 끌 수 있어야 하기 때문이다.
      // 저장 중에는 흐리게 하고 클릭을 막는다(list.vue 가 `__statusSaving` 을 세운다).
      //
      // 머리글 슬롯도 함께 적어야 한다 — 위 스프레드의 `slots` 를 여기서 덮기 때문이다.
      slots: {
        header: 'col-filter',
        default: ({ row }: { row: SystemMenuApi.SystemMenu }) => {
          const active = row.status === 1;
          const clickable = Boolean(onStatusToggle) && can('update');
          const saving = Boolean((row as any).__statusSaving);
          const label = active ? $t('common.enabled') : $t('common.disabled');

          return h(
            Tag,
            {
              class: clickable && !saving ? 'cursor-pointer select-none' : '',
              color: active ? 'success' : 'error',
              onClick:
                clickable && !saving ? () => onStatusToggle?.(row) : undefined,
              style: saving ? { opacity: 0.45, pointerEvents: 'none' } : {},
              title: clickable
                ? `눌러서 ${active ? $t('common.disabled') : $t('common.enabled')} 으로 바꿉니다`
                : undefined,
            },
            { default: () => label },
          );
        },
      },
    },
    {
      align: 'center',
      field: 'operation',
      fixed: 'right',
      headerAlign: 'center',
      showOverflow: false,
      slots: {
        // 권한이 없는 동작은 아예 그리지 않는다.
        // 권한은 JSini 포털 한 곳(scom.role_menus)에서만 관리하고
        // 장례식장·헬프데스크 등 모든 화면이 이 결과를 따른다.
        default: ({ row }) => {
          const iconButton = (
            icon: string,
            title: string,
            code: string,
            danger = false,
          ) =>
            h(
              Button,
              {
                danger,
                onClick: () => onActionClick({ code, row }),
                size: 'small',
                title,
                type: 'link',
              },
              { icon: () => h(IconifyIcon, { class: 'size-4', icon }) },
            );

          const actions = [
            can('create') && iconButton('lucide:plus', '하위 추가', 'append'),
            can('update') &&
              iconButton('lucide:globe', '다국어 번역 수정', 'i18n'),
            can('update') &&
              iconButton('lucide:edit', $t('common.edit'), 'edit'),
            can('delete') &&
              h(
                Popconfirm,
                {
                  getPopupContainer: () => document.body,
                  onConfirm: () => onActionClick({ code: 'delete', row }),
                  placement: 'topLeft',
                  title: $t('ui.actionMessage.deleteConfirm', [row.name]),
                },
                {
                  default: () =>
                    h(
                      Button,
                      {
                        danger: true,
                        size: 'small',
                        title: $t('common.delete'),
                        type: 'link',
                      },
                      {
                        icon: () =>
                          h(IconifyIcon, {
                            class: 'size-4',
                            icon: 'lucide:trash-2',
                          }),
                      },
                    ),
                },
              ),
          ].filter(Boolean);

          return h(
            'div',
            { class: 'flex items-center justify-center gap-1' },
            actions,
          );
        },
      },
      title: $t('system.menu.operation'),
      width: 140,
    },
  ];
}
