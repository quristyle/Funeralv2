import type { VbenFormProps } from '#/adapter/form';
import type { VxeGridProps } from '#/adapter/vxe-table';

import { h } from 'vue';
import { IconifyIcon } from '@vben/icons';
import { Button, Popconfirm, Tooltip } from 'ant-design-vue';
import { BIZ_SELECT_SERVICES } from '#/api/portal/system/biz-select-config';
import { can } from '#/utils/permission';

/**
 * BizSelect 설정 테이블 컬럼 정의
 */
export const useColumns = (onActionClick: (params: any) => void): VxeGridProps['columns'] => [
  { type: 'seq', width: 50 },
  { field: 'bizType', title: '비즈니스 타입', width: 150 },
  { field: 'serviceCode', title: 'MSA', width: 110 },
  { field: 'apiUrl', title: 'API 경로 (MSA 내부)', minWidth: 200 },
  { field: 'httpMethod', title: 'HTTP 메소드', width: 110 },
  { field: 'labelField', title: '라벨 필드명', width: 120 },
  { field: 'valueField', title: '밸류 필드명', width: 120 },
  { field: 'resultPath', title: '결과 경로(JSON Path)', width: 150 },
  { field: 'processorType', title: '전처리 프로세서', width: 130 },
  { field: 'staticParams', title: '고정 파라미터', width: 200 },
  { field: 'paramPath', title: '파라미터 경로', width: 120 },
  { field: 'remark', title: '설명/비고', minWidth: 150 },
  { field: 'createdAt', title: '등록일시', width: 180, formatter: 'formatDate' },
  {
    title: '작업', fixed: 'right', width: 120,
    slots: {
      default: (record) => {
        return h('div', { class: 'flex justify-center gap-2' }, [
          can('update') &&
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
          can('delete') &&
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
          )].filter(Boolean));
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
    { fieldName: 'bizType', label: '비즈니스 타입', component: 'Input', rules: 'required', componentProps: { placeholder: '예: company, helpdesk_admin' } },
    {
      fieldName: 'serviceCode',
      label: 'MSA',
      component: 'Select',
      defaultValue: 'auth',
      rules: 'required',
      help: '어느 서비스를 부를지. 게이트웨이 프리픽스이면서, 서비스마다 다른 응답 봉투를 벗길 클라이언트를 고르는 값이다.',
      componentProps: {
        options: [...BIZ_SELECT_SERVICES],
      },
    },
    { fieldName: 'apiUrl', label: 'API 경로', component: 'Input', rules: 'required', help: 'MSA 프리픽스는 빼고 적는다.', componentProps: { placeholder: '예: /system/companies' } },
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
    {
      fieldName: 'resultPath',
      label: '결과 경로 (JSON Path)',
      component: 'Input',
      help: '요청 클라이언트가 봉투를 벗기고 남은 것에서 목록을 찾는 경로. auth·funeral 은 result, helpdesk 는 비움, projmng 은 data.',
      componentProps: { placeholder: '예: result (비어있는 경우 전체 응답값 사용)' },
    },
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
    {
      fieldName: 'staticParams',
      label: '고정 파라미터',
      component: 'Textarea',
      help: '호출할 때 항상 함께 보내는 값 (JSON 객체). 프로시저 이름을 본문에 실어야 하는 프로젝트관리에서 쓴다.',
      componentProps: { placeholder: '예: {"ProcName":"sp_projCommon","ProcType":"srch"}', rows: 2 },
    },
    {
      fieldName: 'paramPath',
      label: '파라미터 경로',
      component: 'Input',
      help: '화면이 넘긴 파라미터를 본문의 어느 자리에 넣을지. 비우면 최상위.',
      componentProps: { placeholder: '예: MainParam' },
    },
    { fieldName: 'remark', label: '설명/비고', component: 'Textarea' },
  ],
};
