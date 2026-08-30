import type { OnActionClickFn, VxeTableGridColumns } from '#/adapter/vxe-table';
import type { SystemMenuApi } from '#/api/portal/system/menu';

import { h } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { Button, Popconfirm, Tag } from 'ant-design-vue';

import { $t, $tIfKey } from '#/locales';
import { can } from '#/utils/permission';

/**
 * [필터는 공통 필터줄이 단다]
 *
 * 예전에는 이 파일이 `filters` · `filterMethod` 와 머리글 슬롯(`col-filter`)을
 * 직접 들고 있었다 — 공통 필터줄(`adapter/vxe-grid-features.ts`)이 생기기 전의
 * 방식이다. 공통 레이어는 `slots.header` 가 있는 칸을 건너뛰므로, 그대로 두면
 * 이 화면만 다른 화면과 다른 머리글(한 칸에 이름+입력칸)이 된다.
 *
 * 지금은 값 칸이면 공통 레이어가 알아서 필터를 달고, 특별한 칸만 `params` 로
 * 알려 준다 — 훑을 글자가 따로 있는 칸은 `filterText`, 고르는 칸은
 * `filterOptions`(CellTag 칸은 그 옵션에서 저절로 만든다), 빼는 칸은 `filter: false`.
 */

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
  /**
   * 화면 크기별 노출 배지를 눌렀을 때. 상태 배지와 같은 규칙으로
   * 넘기지 않거나 수정 권한이 없으면 읽기 전용으로 그려진다.
   */
  onSizeToggle?: (
    row: SystemMenuApi.SystemMenu,
    field: 'useMobile' | 'useTablet',
  ) => void,
): VxeTableGridColumns<SystemMenuApi.SystemMenu> {
  /**
   * 화면 크기별 노출 칸 하나를 만든다(휴대폰 · 태블릿).
   *
   * 상태 칸과 같은 모양으로 그린다 — 눌러서 그 자리에서 켜고 끈다.
   * `BUTTON` 은 애초에 메뉴목록에 나오지 않으므로 '-' 로 비워 둔다.
   */
  const sizeColumn = (field: 'useMobile' | 'useTablet', title: string) => ({
    align: 'center' as const,
    field: `meta.${field}`,
    // 슬롯으로 그려 `CellTag` 가 없다 — 고르는 칸 목록을 직접 준다(상태 칸과 같다).
    params: {
      filterOptions: [
        { label: $t('common.enabled'), value: true },
        { label: $t('common.disabled'), value: false },
      ],
    },
    slots: {
      default: ({ row }: { row: SystemMenuApi.SystemMenu }) => {
        if (row.type === 'BUTTON') {
          return h('span', { class: 'text-muted-foreground' }, '-');
        }

        // 값이 없던 시절의 메뉴는 보이는 쪽이다(백엔드 기본값과 같다).
        const shown = (row.meta as any)?.[field] !== false;
        const clickable = Boolean(onSizeToggle) && can('update');
        const saving = Boolean((row as any).__sizeSaving?.[field]);

        return h(
          Tag,
          {
            class: clickable && !saving ? 'cursor-pointer select-none' : '',
            color: shown ? 'success' : 'default',
            onClick:
              clickable && !saving ? () => onSizeToggle?.(row, field) : undefined,
            style: saving ? { opacity: 0.45, pointerEvents: 'none' } : {},
            title: clickable
              ? `눌러서 ${shown ? $t('common.disabled') : $t('common.enabled')} 으로 바꿉니다`
              : undefined,
          },
          {
            default: () =>
              shown ? $t('common.enabled') : $t('common.disabled'),
          },
        );
      },
    },
    sortable: true,
    title,
    width: 130,
  });

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
      align: 'left',
      field: 'meta.title',
      fixed: 'left',
      minWidth: 260,
      // 화면에 보이는 것은 번역된 글자인데 저장된 값은 번역 키일 수 있다.
      // 보이는 이름·번역 키·원래 이름 어느 것으로 쳐도 걸리게 훑을 글자를 준다.
      params: { filterText: titleHaystack },
      slots: { default: 'title' },
      // 정렬은 **형제끼리** 이뤄진다(트리 칸이라 부모 밑에서만 다시 선다).
      // 저장된 값(`meta.title`)을 기준으로 서므로 번역 키인 메뉴는 키 순서로 선다 —
      // 눈에 보이는 글자와 다를 수 있다. 찾는 것이 목적이면 아래 필터가 낫다.
      sortable: true,
      title: $t('system.menu.menuTitle'),
      treeNode: true,
    },
    {
      align: 'center',
      // 고르는 칸의 목록은 공통 레이어가 이 `CellTag` 옵션에서 만든다 —
      // 배지에 쓰는 이름표(디렉토리·메뉴…)와 필터 목록이 저절로 같아진다.
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
      // 필터는 뺀다 — 형제 안에서만 뜻이 있는 순번이라 입력 필터가 쓸모없다(34번 문서 2절).
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
    },
    {
      editRender: { name: 'VxeInput' },
      field: 'authCode',
      sortable: true,
      title: $t('system.menu.authCode'),
      width: 180,
    },
    {
      align: 'left',
      editRender: { name: 'VxeInput' },
      field: 'path',
      sortable: true,
      title: $t('system.menu.path'),
      width: 200,
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
      // 이 칸은 종류에 따라 다른 값을 보여 준다(컴포넌트 · iframe 주소 · 링크).
      // 그래서 `component` 하나만 보면 화면과 어긋난다 — 세 값을 함께 훑는다.
      params: {
        filterText: (row: any) =>
          [row.component, row.meta?.iframeSrc, row.meta?.link]
            .filter(Boolean)
            .join(' '),
      },
      sortable: true,
      title: $t('system.menu.component'),
    },
    {
      align: 'center',
      field: 'status',
      // 이 칸은 배지를 슬롯으로 그려서 `CellTag` 가 없다 — 공통 레이어가 옵션을
      // 만들 재료가 없으므로 고르는 칸 목록을 직접 준다. 배지에 쓰는 말과 맞춘다.
      params: {
        filterOptions: [
          { label: $t('common.enabled'), value: 1 },
          { label: $t('common.disabled'), value: 0 },
        ],
      },
      sortable: true,
      title: $t('system.menu.status'),
      width: 90,
      // CellTag 대신 슬롯으로 그린다. 배지를 눌러 그 자리에서 켜고 끌 수 있어야 하기 때문이다.
      // 저장 중에는 흐리게 하고 클릭을 막는다(list.vue 가 `__statusSaving` 을 세운다).
      slots: {
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
    // 작은 화면에서 이 메뉴를 메뉴목록에 보일지. 데스크톱은 이 값과 무관하게 늘 보인다.
    // **메뉴목록에서만** 빠진다 — 라우트는 남아 있어 주소·즐겨찾기로는 열린다.
    sizeColumn('useMobile', $t('system.menu.useMobile')),
    sizeColumn('useTablet', $t('system.menu.useTablet')),
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
