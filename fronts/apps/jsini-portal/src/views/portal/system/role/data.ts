import type { VbenFormSchema } from '#/adapter/form';
import type { OnActionClickFn, VxeTableGridColumns } from '#/adapter/vxe-table';
import type { SystemRoleApi } from '#/api';

import { h } from 'vue';
import { z } from '#/adapter/form';
import { IconifyIcon } from '@vben/icons';
import { $t } from '#/locales';
import { can } from '#/utils/permission';
import { Button, Popconfirm, Tooltip } from 'ant-design-vue';

export function useFormSchema(): VbenFormSchema[] {
  return [
    {
      component: 'Input',
      fieldName: 'id',
      label: $t('system.role.id'),
      rules: z.string({ required_error: '역할 ID를 입력해주세요' }).min(1, '역할 ID를 입력해주세요'),
    },
    {
      component: 'Input',
      fieldName: 'name',
      label: $t('system.role.roleName'),
      rules: 'required',
    },
    {
      component: 'RadioGroup',
      componentProps: {
        buttonStyle: 'solid',
        options: [
          { label: $t('common.enabled'), value: 1 },
          { label: $t('common.disabled'), value: 0 },
        ],
        optionType: 'button',
      },
      defaultValue: 1,
      fieldName: 'status',
      label: $t('system.role.status'),
    },
    {
      component: 'Textarea',
      fieldName: 'remark',
      label: $t('system.role.remark'),
    },
    {
      component: 'Input',
      fieldName: 'permissions',
      formItemClass: 'items-start',
      label: $t('system.role.setPermissions'),
      modelPropName: 'modelValue',
    },
  ];
}

export function useGridFormSchema(): VbenFormSchema[] {
  return [
    {
      component: 'Input',
      fieldName: 'name',
      label: $t('system.role.roleName'),
    },
    { component: 'Input', fieldName: 'id', label: $t('system.role.id') },
    {
      component: 'Select',
      componentProps: {
        allowClear: true,
        options: [
          { label: $t('common.enabled'), value: 1 },
          { label: $t('common.disabled'), value: 0 },
        ],
      },
      fieldName: 'status',
      label: $t('system.role.status'),
    },
    {
      component: 'Input',
      fieldName: 'remark',
      label: $t('system.role.remark'),
    },
    {
      component: 'RangePicker',
      fieldName: 'createTime',
      label: $t('system.role.createTime'),
    },
  ];
}

export function useColumns<T = SystemRoleApi.SystemRole>(
  onActionClick: OnActionClickFn<T>,
  onStatusChange?: (newStatus: any, row: T) => PromiseLike<boolean | undefined>,
): VxeTableGridColumns {
  return [
    {
      field: 'name',
      title: $t('system.role.roleName'),
      width: 200,
    },
    {
      field: 'id',
      title: $t('system.role.id'),
      width: 200,
    },
    {
      cellRender: {
        attrs: { beforeChange: onStatusChange },
        name: onStatusChange ? 'CellSwitch' : 'CellTag',
      },
      field: 'status',
      title: $t('system.role.status'),
      width: 100,
    },
    {
      field: 'remark',
      minWidth: 100,
      title: $t('system.role.remark'),
    },
    {
      field: 'createTime',
      title: $t('system.role.createTime'),
      width: 200,
    },
    {
      title: $t('system.role.operation'), fixed: 'right', width: 130,
      align: 'center',
      slots: {
        default: (record) => {
          return h('div', { class: 'flex justify-center gap-2' }, [
            can('update') &&
            /** 수정 버튼: Tooltip 및 Icon 사용 */
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
            can('delete') &&
            /** 삭제 버튼: Popconfirm을 통한 확인 절차 포함 */
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
            )].filter(Boolean));
        },
      },
    },
  ];
}
