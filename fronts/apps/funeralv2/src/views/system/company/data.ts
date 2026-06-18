import type { VbenFormProps } from '#/adapter/form';
import type { VxeGridProps } from '#/adapter/vxe-table';

import { h } from 'vue';

import { IconifyIcon } from '@vben/icons';
import { $t } from '@vben/locales';

import { Button, Popconfirm, Tooltip } from 'ant-design-vue';

/**
 * 회사 관리 테이블 컬럼 정의를 반환하는 함수
 * @param onActionClick 버튼 클릭 이벤트를 처리할 콜백 함수
 * @returns VXETable 컬럼 설정 배열
 */
export const useColumns = (onActionClick: (params: any) => void): VxeGridProps['columns'] => [
  { type: 'seq', width: 50 },
  /** 회사명 - 수정 가능(VxeInput 사용) */
  { field: 'name', title: $t('system.company.name'), minWidth: 150, editRender: { name: 'VxeInput' } },
  /** 사업자번호 - 수정 가능 */
  { field: 'businessNumber', title: $t('system.company.businessNumber'), width: 150, editRender: { name: 'VxeInput' } },
  /** 대표자 - 데이터 기반 그룹핑 필터 지원 */
  { 
    field: 'representative', 
    title: $t('system.company.representative'), 
    width: 120,
    params: { filterList: true },
    editRender: { name: 'VxeInput' }
  },
  /** 상태 - 태그 형태로 렌더링 및 필터 지원 */
  {
    field: 'status',
    title: $t('system.company.status'),
    width: 100,
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
  /** 비고 */
  { field: 'remark', title: $t('system.company.remark'), minWidth: 200, editRender: { name: 'VxeInput' } },
  /** 
   * 생성일시 - formatDate 포매터 사용
   * extendsDefaultFormatter에 등록된 형식으로 출력됩니다.
   */
  { 
    field: 'createdAt', title: $t('common.createdAt'), width: 180, formatter: 'formatDate',
    editRender: { 
      name: 'input',
      attrs: { type: 'date' }
    }
  },
  /** 작업 열 - 수정/삭제 버튼 포함 */
  {
    title: $t('common.action'), fixed: 'right', width: 120,
    slots: {
      default: (record) => {
        return h('div', { class: 'flex justify-center gap-2' }, [
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
          ),
        ]);
      },
    },
  },
];

/**
 * 회사 등록/수정 폼의 스키마 및 설정 정의
 */
export const formSchema: VbenFormProps = {
  /** 폼 공통 설정: 라벨 너비 지정 */
  commonConfig: { labelWidth: 100, },
  /** 입력 항목 구성 */
  schema: [
    /** 회사명: 필수 입력 항목 */
    { fieldName: 'name', label: $t('system.company.name'), component: 'Input', rules: 'required', },
    /** 사업자 등록 번호 */
    { fieldName: 'businessNumber', label: $t('system.company.businessNumber'), component: 'Input', },
    /** 대표자 성함 */
    { fieldName: 'representative', label: $t('system.company.representative'), component: 'Input', },
    /** 상태: 라디오 그룹 사용 (기본값: 활성) */
    { fieldName: 'status', label: $t('system.company.status'), component: 'RadioGroup', defaultValue: 1,
      componentProps: {
        options: [
          { label: $t('common.enabled'), value: 1 },
          { label: $t('common.disabled'), value: 0 },
        ],
      },
    },
    /** 비고: 멀티라인 텍스트 입력 */
    { fieldName: 'remark', label: $t('system.company.remark'), component: 'Textarea', },
  ],
};
