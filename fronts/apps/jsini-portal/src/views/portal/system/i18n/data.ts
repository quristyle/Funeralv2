import type { OnActionClickParams, VxeGridProps } from '#/adapter/vxe-table';

import { h } from 'vue';

import { IconifyIcon } from '@vben/icons';
import { $t } from '@vben/locales';

import { Button, Tag, Tooltip } from 'ant-design-vue';
import { can } from '#/utils/permission';

export function useColumns(onActionClick: (params: OnActionClickParams) => void) {
  const columns: VxeGridProps['columns'] = [
    { type: 'checkbox', width: 50, },
    { title: 'ID', field: 'id', width: 80, },
    { title: $t('ui.i18n.locale'), field: 'locale', width: 100, params: { filterList: true },
      slots: {
        default: (record: any) => {
          const color = record.row.locale === 'ko' ? 'blue' : 'orange';
          return h(Tag, { color }, { default: () => record.row.locale });
        },
      },
    },
    { title: $t('ui.i18n.category'), field: 'category', width: 150, params: { filterList: true } },
    { title: $t('ui.i18n.key'), field: 'key', minWidth: 250, align: 'left', },
    { title: $t('ui.i18n.value'), field: 'value', minWidth: 300, align: 'left',
      editRender: { name: 'input', props: { placeholder: $t('ui.i18n.valuePlaceholder'), }, },
    },
    {
      title: $t('common.operation'), field: 'action', fixed: 'right', width: 120,
      slots: {
        default: (record: any) => {
          return h(
            'div',
            { class: 'flex justify-center gap-2' },
            [
              // 수정 아이콘 버튼
              can('update') &&
              h(
                Tooltip,
                { title: $t('common.edit') },
                {
                  default: () => h(
                    Button,
                    { type: 'link', size: 'small', onClick: () => onActionClick({ code: 'edit', row: record.row }), },
                    { icon: () => h(IconifyIcon, { icon: 'lucide:edit', class: 'size-4' }), }
                  )
                }
              ),
              // 삭제 아이콘 버튼
              can('delete') &&
              h(
                Tooltip,
                { title: $t('common.delete') },
                {
                  default: () => h(
                    Button,
                    { type: 'link', size: 'small', danger: true, onClick: () => onActionClick({ code: 'delete', row: record.row }), },
                    { icon: () => h(IconifyIcon, { icon: 'lucide:trash-2', class: 'size-4' }), }
                  )
                }
              ),
            ].filter(Boolean),
          );
        },
      },
    },
  ];

  return columns;
}
