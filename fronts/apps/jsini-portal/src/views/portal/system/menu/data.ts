import type { OnActionClickFn, VxeTableGridColumns } from '#/adapter/vxe-table';
import type { SystemMenuApi } from '#/api/portal/system/menu';

import { h } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { Button, Popconfirm } from 'ant-design-vue';

import { $t } from '#/locales';
import { can } from '#/utils/permission';

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
      align: 'left',
      field: 'meta.title',
      fixed: 'left',
      minWidth: 260,
      slots: { default: 'title' },
      title: $t('system.menu.menuTitle'),
      treeNode: true,
    },
    {
      align: 'center',
      cellRender: { name: 'CellTag', options: getMenuTypeOptions() },
      field: 'type',
      params: { filterList: true },
      title: $t('system.menu.type'),
      width: 100,
    },
    {
      align: 'right',
      editRender: { name: 'VxeNumberInput' },
      field: 'meta.order',
      title: $t('system.menu.order'),
      width: 80,
    },
    {
      align: 'left',
      editRender: { name: 'VxeInput' },
      field: 'name',
      title: $t('system.menu.menuName'),
      width: 180,
    },
    {
      editRender: { name: 'VxeInput' },
      field: 'authCode',
      title: $t('system.menu.authCode'),
      width: 180,
    },
    {
      align: 'left',
      editRender: { name: 'VxeInput' },
      field: 'path',
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
      title: $t('system.menu.component'),
    },
    {
      cellRender: { name: 'CellTag' },
      field: 'status',
      params: { filterList: true },
      title: $t('system.menu.status'),
      width: 90,
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
