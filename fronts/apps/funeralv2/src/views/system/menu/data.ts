import type { OnActionClickFn, VxeTableGridColumns } from '#/adapter/vxe-table';
import type { SystemMenuApi } from '#/api/system/menu';

import { h } from 'vue';

import { IconifyIcon } from '@vben/icons';
import { $t } from '#/locales';

import { Button, Popconfirm, Tooltip } from 'ant-design-vue';

export function getMenuTypeOptions() {
  return [
    { color: 'processing', label: $t('system.menu.typeCatalog'), value: 'catalog', },
    { color: 'default', label: $t('system.menu.typeMenu'), value: 'menu' },
    { color: 'error', label: $t('system.menu.typeButton'), value: 'button' },
    { color: 'success', label: $t('system.menu.typeEmbedded'), value: 'embedded', },
    { color: 'warning', label: $t('system.menu.typeLink'), value: 'link' },
  ];
}

export function useColumns( onActionClick: OnActionClickFn<SystemMenuApi.SystemMenu>,): VxeTableGridColumns<SystemMenuApi.SystemMenu> {
  return [
    { align: 'left', field: 'meta.title', fixed: 'left', slots: { default: 'title' },
      title: $t('system.menu.menuTitle'), treeNode: true, width: 250,
    },
    { field: 'type', title: $t('system.menu.type'), width: 100, params: { filterList: true },
      align: 'center', cellRender: { name: 'CellTag', options: getMenuTypeOptions() },
    },
    { field: 'authCode', title: $t('system.menu.authCode'), width: 200, },
    { align: 'left', field: 'path', title: $t('system.menu.path'), width: 200,
      editRender: { name: 'VxeInput' }
     },
    { align: 'left', field: 'component', minWidth: 200, title: $t('system.menu.component'),

      editRender: { name: 'VxeInput' },

      formatter: ({ row }) => {
        switch (row.type) {
          case 'catalog':
          case 'menu': { return row.component ?? ''; }
          case 'embedded': { return row.meta?.iframeSrc ?? ''; }
          case 'link': { return row.meta?.link ?? ''; }
        }
        return '';
      },
    },
    { cellRender: { name: 'CellTag' }, field: 'status', title: $t('system.menu.status'), width: 100, params: { filterList: true },},
    { align: 'right', field: 'operation', fixed: 'right', headerAlign: 'center', showOverflow: false,
      slots: {
        default: (record) => {
          return h('div', { class: 'flex justify-center gap-2' }, [
            // 하위 추가
            h( Tooltip,
              { title: '하위 추가' },
              { default: () =>
                  h(
                    Button,
                    { size: 'small', type: 'link', onClick: () => onActionClick({ code: 'append', row: record.row }), },
                    { icon: () => h(IconifyIcon, { class: 'size-4', icon: 'lucide:plus', }), },
                  ),
              },
            ),
            // 수정
            h( Tooltip,
              { title: $t('common.edit') },
              {
                default: () =>
                  h(
                    Button,
                    { size: 'small', type: 'link', onClick: () => onActionClick({ code: 'edit', row: record.row }), },
                    { icon: () => h(IconifyIcon, { class: 'size-4', icon: 'lucide:edit', }), },
                  ),
              },
            ),
            // 삭제
            h( Popconfirm,
              { getPopupContainer: () => document.body,
                onConfirm: () => onActionClick({ code: 'delete', row: record.row }),
                placement: 'topLeft',
                title: $t('ui.actionMessage.deleteConfirm', [record.row.name]),
              },
              { default: () =>
                  h(
                    Tooltip,
                    { title: $t('common.delete') },
                    {
                      default: () =>
                        h(
                          Button, { danger: true, size: 'small', type: 'link', },
                          { icon: () => h(IconifyIcon, { class: 'size-4', icon: 'lucide:trash-2', }), },
                        ),
                    },
                  ),
              },
            ),
          ]);
        },
      },
      title: $t('system.menu.operation'), width: 150,
    },
  ];
}
