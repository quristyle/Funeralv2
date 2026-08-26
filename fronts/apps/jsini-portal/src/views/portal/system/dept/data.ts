import { h, markRaw } from 'vue';

import { IconifyIcon } from '@vben/icons';
import { $t } from '#/locales';
import { can } from '#/utils/permission';

import { Button, Popconfirm, Tooltip } from 'ant-design-vue';

import type { VxeTableGridColumns } from '@vben/plugins/vxe-table';
import type { VbenFormSchema } from '#/adapter/form';
import type { OnActionClickFn } from '#/adapter/vxe-table';
import type { SystemDeptApi } from '#/api/portal/system/dept';

import { z } from '#/adapter/form';
import BizSelect from '#/components/BizSelect.vue';

/**
 * 편집 폼의 필드 구성을 가져옵니다. 다국어를 사용하지 않는 경우 배열 상수를 직접 export할 수 있습니다.
 */
export function useSchema(): VbenFormSchema[] {
  return [
    {
      component: markRaw(BizSelect),
      componentProps: {
        type: 'company',
        placeholder: '회사를 선택해주세요',
      },
      fieldName: 'companyId',
      label: '소속 회사',
      rules: z.string({ error: '회사를 선택해주세요' }),
    },
    {
      component: 'Input',
      fieldName: 'name',
      label: $t('system.dept.deptName'),
      rules: z
        .string()
        .min(2, $t('ui.formRules.minLength', [$t('system.dept.deptName'), 2]))
        .max(
          20,
          $t('ui.formRules.maxLength', [$t('system.dept.deptName'), 20]),
        ),
    },
    {
      component: markRaw(BizSelect),
      componentProps: {
        type: 'dept',
        placeholder: '상위 부서를 선택해주세요',
        allowClear: true,
        class: 'w-full',
      },
      dependencies: {
        componentProps(values) {
          return {
            params: {
              companyId: values.companyId,
            },
          };
        },
        triggerFields: ['companyId'],
      },
      fieldName: 'pid',
      label: $t('system.dept.parentDept'),
    },
    {
      component: 'InputNumber',
      componentProps: {
        min: 0,
        step: 1,
        precision: 0,
      },
      defaultValue: 0,
      fieldName: 'sortOrder',
      label: '정렬 순서',
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
      label: $t('system.dept.status'),
    },
    {
      component: 'Textarea',
      componentProps: {
        maxLength: 50,
        rows: 3,
        showCount: true,
      },
      fieldName: 'remark',
      label: $t('system.dept.remark'),
      rules: z
        .string()
        .max(50, $t('ui.formRules.maxLength', [$t('system.dept.remark'), 50]))
        .optional(),
    },
  ];
}

/**
 * 테이블 열 구성을 가져옵니다.
 * @description 언어 전환 시 헤더를 다시 번역하기 위해 배열 상수 대신 함수 형태로 열 데이터를 반환합니다.
 * @param onActionClick 테이블 작업 버튼 클릭 이벤트
 * @param showCompany 회사명 열을 보여줄지. 왼쪽에서 한 회사만 골라 보고 있으면
 *                    모든 행에 같은 값이 들어가 자리만 차지하므로 '전체' 일 때만 켠다.
 */
export function useColumns(
  onActionClick?: OnActionClickFn<SystemDeptApi.SystemDept>,
  showCompany = true,
): VxeTableGridColumns<SystemDeptApi.SystemDept> {
  return [
    {
      align: 'left',
      field: 'name',
      fixed: 'left',
      title: $t('system.dept.deptName'),
      treeNode: true,
      minWidth: 200,
    },
    // 소속 인원. 어느 부서에 사람이 있는지 눌러 보지 않고 알아야 조직을 옮길 판단을 할 수 있다.
    // 하위 부서가 있으면 '직접 / 전체' 로 보여 준다 — 접어 둔 상태에서 상위 부서만 보면
    // 그 아래 인원이 안 보여서 사람 없는 조직으로 착각하게 된다.
    {
      align: 'right',
      field: 'userCount',
      title: '사용자',
      width: 90,
      formatter: ({ row }) => {
        const own = Number(row.userCount ?? 0);
        const total = Number(row.totalUserCount ?? own);
        const hasChildren = !!row.children?.length;
        if (hasChildren && total !== own) return `${own} / ${total}`;
        return own > 0 ? String(own) : '-';
      },
    },
    ...(showCompany
      ? [
          {
            field: 'companyName',
            title: '회사명',
            width: 150,
          },
        ]
      : []),
    {
      field: 'sortOrder',
      title: '정렬 순서',
      width: 100,
    },
    {
      cellRender: { name: 'CellTag' },
      field: 'status',
      title: $t('system.dept.status'),
      width: 100,
    },
    {
      field: 'createTime',
      title: $t('system.dept.createTime'),
      width: 180,
    },
    {
      field: 'remark',
      title: $t('system.dept.remark'),
    },
    {
      align: 'right',
      fixed: 'right',
      headerAlign: 'center',
      showOverflow: false,
      slots: {
        default: (record) => {
          const hasChildren = !!(record.row.children && record.row.children.length > 0);
          return h('div', { class: 'flex justify-center gap-2' }, [
            can('create') &&
            // 하위 추가
            h(
              Tooltip,
              { title: '하위 추가' },
              {
                default: () =>
                  h(
                    Button,
                    {
                      size: 'small',
                      type: 'link',
                      onClick: () =>
                        onActionClick?.({ code: 'append', row: record.row }),
                    },
                    {
                      icon: () =>
                        h(IconifyIcon, {
                          class: 'size-4',
                          icon: 'lucide:plus',
                        }),
                    },
                  ),
              },
            ),
            can('update') &&
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
                        onActionClick?.({ code: 'edit', row: record.row }),
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
            // 삭제
            h(
              Popconfirm,
              {
                disabled: hasChildren,
                getPopupContainer: () => document.body,
                onConfirm: () =>
                  onActionClick?.({ code: 'delete', row: record.row }),
                placement: 'topLeft',
                title: $t('ui.actionMessage.deleteConfirm', [record.row.name]),
              },
              {
                default: () =>
                  h(
                    Tooltip,
                    { title: hasChildren ? '하위 부서가 있어 삭제할 수 없습니다' : $t('common.delete') },
                    {
                      default: () =>
                        h(
                          Button,
                          {
                            danger: true,
                            disabled: hasChildren,
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
      title: $t('common.action'),
      width: 150,
    },
  ];
}
