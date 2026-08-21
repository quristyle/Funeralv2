import type { VbenFormProps } from '#/adapter/form';
import type { VxeGridProps } from '#/adapter/vxe-table';

import { h } from 'vue';
import { IconifyIcon } from '@vben/icons';
import { Button, Popconfirm, Tooltip } from 'ant-design-vue';

/**
 * BizSelect 설정 테이블 컬럼 정의
 */
export const useColumns = (onActionClick: (params: any) => void): VxeGridProps['columns'] => [
  { type: 'seq', width: 50 },
  { field: 'bizType', title: '비즈니스 타입', width: 150 },
  { field: 'apiUrl', title: 'API 호출 주소', minWidth: 200 },
  { field: 'httpMethod', title: 'HTTP 메소드', width: 120 },
  { field: 'labelField', title: '라벨 필드명', width: 120 },
  { field: 'valueField', title: '밸류 필드명', width: 120 },
  { field: 'resultPath', title: '결과 경로(JSON Path)', width: 150 },
  { field: 'processorType', title: '전처리 프로세서', width: 150 },
  { field: 'remark', title: '설명/비고', minWidth: 150 },
  { field: 'createdAt', title: '등록일시', width: 180, formatter: 'formatDate' },
  {
    title: '작업', fixed: 'right', width: 120,
    slots: {
      default: (record) => {
        return h('div', { class: 'flex justify-center gap-2' }, [
          h(
            Tooltip,
            { title: '수정' },
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
          h(
            Popconfirm,
            {
              getPopupContainer: () => document.body,
              onConfirm: () =>
                onActionClick({ code: 'delete', row: record.row }),
              placement: 'topLeft',
              title: `"${record.row.bizType}" 설정을 삭제하시겠습니까?`,
            },
            {
              default: () =>
                h(
                  Tooltip,
                  { title: '삭제' },
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
 * 등록/수정 폼의 스키마 및 설정 정의
 */
export const formSchema: VbenFormProps = {
  commonConfig: { labelWidth: 140 },
  schema: [
    { fieldName: 'bizType', label: '비즈니스 타입', component: 'Input', rules: 'required', componentProps: { placeholder: '예: company, dept' } },
    { fieldName: 'apiUrl', label: 'API 호출 주소', component: 'Input', rules: 'required', componentProps: { placeholder: '예: /auth/system/companies' } },
    {
      fieldName: 'httpMethod',
      label: 'HTTP 메소드',
      component: 'Select',
      defaultValue: 'GET',
      rules: 'required',
      componentProps: {
        options: [
          { label: 'GET', value: 'GET' },
          { label: 'POST', value: 'POST' },
        ],
      },
    },
    { fieldName: 'labelField', label: '라벨 필드명', component: 'Input', rules: 'required', defaultValue: 'name', componentProps: { placeholder: '예: name' } },
    { fieldName: 'valueField', label: '밸류 필드명', component: 'Input', rules: 'required', defaultValue: 'id', componentProps: { placeholder: '예: id' } },
    { fieldName: 'resultPath', label: '결과 경로 (JSON Path)', component: 'Input', componentProps: { placeholder: '예: result (비어있는 경우 전체 응답값 사용)' } },
    {
      fieldName: 'processorType',
      label: '전처리 프로세서',
      component: 'Select',
      componentProps: {
        allowClear: true,
        options: [
          { label: '트리 평탄화 (FLATTEN)', value: 'FLATTEN' },
        ],
      },
    },
    { fieldName: 'remark', label: '설명/비고', component: 'Textarea' },
  ],
};
