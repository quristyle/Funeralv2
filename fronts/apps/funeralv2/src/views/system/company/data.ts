import type { VbenFormProps } from '#/adapter/form';
import type { VxeGridProps } from '#/adapter/vxe-table';

import { h } from 'vue';

import { IconifyIcon } from '@vben/icons';
import { $t } from '@vben/locales';

import { Button, Popconfirm, Tooltip } from 'ant-design-vue';

/**
 * 회사 관리 테이블 컬럼 정의
 */
export const useColumns = (onActionClick: (params: any) => void): VxeGridProps['columns'] => [
  { type: 'seq', width: 50 },
  { field: 'name', title: $t('system.company.name'), minWidth: 150, editRender: { name: 'VxeInput' } },
  { field: 'businessNumber', title: $t('system.company.businessNumber'), width: 150, editRender: { name: 'VxeInput' } },
  { 
    field: 'representative', 
    title: $t('system.company.representative'), 
    width: 120,
    params: { filterList: true }, // 데이터 기반 그룹핑(고유값) 리스트 필터 사용
    editRender: { name: 'VxeInput' }
  },
  {
    field: 'status',
    title: $t('system.company.status'),
    width: 100,
    // 컬럼 필터 옵션 추가
    filters: [
      { label: $t('common.enabled'), value: 1 },
      { label: $t('common.disabled'), value: 0 },
    ],
    cellRender: {
      name: 'CellTag',
      options: [
        { label: $t('common.enabled'), value: 1, color: 'success' },
        { label: $t('common.disabled'), value: 0, color: 'error' },
      ],
    },
  },
  { field: 'remark', title: $t('system.company.remark'), minWidth: 200, editRender: { name: 'VxeInput' } },
  /**  
   * 만약 금액(콤마 추가)이나 특정 형태의 문자열 등 새로운 포매터가 필요하시다면, 
   * fronts/packages/effects/plugins/src/vxe-table/extends.ts 파일의 
   * extendsDefaultFormatter 내부에 다음과 같이 추가로 등록하여 사용
   * formatDateTime , formatDate 가 준비되어 있다.
   * 
  */
  { 
    field: 'createdAt', title: $t('common.createdAt'), width: 180, formatter: 'formatDate',
    editRender: { 
      name: 'input',
      attrs: { type: 'date' }
    }
  },
  {
    title: $t('common.action'), fixed: 'right', width: 120,
    slots: {
      default: (record) => {
        return h('div', { class: 'flex justify-center gap-2' }, [
          // 수정
          h(
            Tooltip,
            { title: $t('common.edit') },
            {
              default: () =>
                h(
                  Button,
                  {
                    size: 'small',
                    type: 'link',
                    onClick: () =>
                      onActionClick({ code: 'edit', row: record.row }),
                  },
                  {
                    icon: () =>
                      h(IconifyIcon, {
                        class: 'size-4',
                        icon: 'lucide:edit',
                      }),
                  },
                ),
            },
          ),
          // 삭제
          h(
            Popconfirm,
            {
              getPopupContainer: () => document.body,
              onConfirm: () =>
                onActionClick({ code: 'delete', row: record.row }),
              placement: 'topLeft',
              title: $t('ui.actionMessage.deleteConfirm', [record.row.name]),
            },
            {
              default: () =>
                h(
                  Tooltip,
                  { title: $t('common.delete') },
                  {
                    default: () =>
                      h(
                        Button,
                        {
                          danger: true,
                          size: 'small',
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
            },
          ),
        ]);
      },
    },
  },
];

/**
 * 회사 관리 등록/수정 폼 스키마 정의
 */
export const formSchema: VbenFormProps = {
  commonConfig: { labelWidth: 100, },
  schema: [
    { fieldName: 'name', label: $t('system.company.name'), component: 'Input', rules: 'required', },
    { fieldName: 'businessNumber', label: $t('system.company.businessNumber'), component: 'Input', },
    { fieldName: 'representative', label: $t('system.company.representative'), component: 'Input', },
    { fieldName: 'status', label: $t('system.company.status'), component: 'RadioGroup', defaultValue: 1,
      componentProps: {
        options: [
          { label: $t('common.enabled'), value: 1 },
          { label: $t('common.disabled'), value: 0 },
        ],
      },
    },
    { fieldName: 'remark', label: $t('system.company.remark'), component: 'Textarea', },
  ],
};
