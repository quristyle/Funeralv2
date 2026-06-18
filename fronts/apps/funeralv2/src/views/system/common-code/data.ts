import type { VxeGridProps } from '@vben/plugins/vxe-table';
import type { VbenFormProps } from '@vben/common-ui';
import { h } from 'vue';
import { Tag } from 'ant-design-vue';

/**
 * 공통코드 그룹 테이블 컬럼 정의
 */
export const groupGridOptions: VxeGridProps<any> = {
  columns: [
    { type: 'seq', width: 50 },
    { field: 'groupCode', title: '그룹코드', width: 120 },
    { field: 'groupName', title: '그룹명', minWidth: 150 },
    { 
      field: 'isHierarchical', 
      title: '계층구조', 
      width: 100,
      cellRender: {
        name: 'ASwitch',
        props: { disabled: true }
      }
    },
    { field: 'remark', title: '비고' },
    {
      field: 'action',
      title: '작업',
      width: 80,
      slots: { default: 'action' },
      fixed: 'right',
    },
  ],
};

/**
 * 공통코드 테이블 컬럼 정의
 */
export const codeGridOptions: VxeGridProps<any> = {
  treeConfig: {
    transform: true,
    rowField: 'id',
    parentField: 'parentId',
  },
  columns: [
    { type: 'seq', width: 50 },
    { field: 'codeValue', title: '코드값', width: 120, treeNode: true },
    { field: 'codeName', title: '코드명', minWidth: 150 },
    { field: 'i18nKey', title: '다국어키', width: 150 },
    { field: 'sortOrder', title: '순서', width: 80 },
    {
      field: 'status',
      title: '상태',
      width: 80,
      cellRender: {
        name: 'Tag',
        props: (row: any) => ({
          color: row.status === 1 ? 'green' : 'red',
        }),
        content: (row: any) => (row.status === 1 ? '사용' : '미사용'),
      },
    },
    {
      field: 'action',
      title: '작업',
      width: 120,
      slots: { default: 'action' },
      fixed: 'right',
    },
  ],
};

/**
 * 그룹 등록/수정 폼 스키마
 */
export const groupFormSchema: VbenFormProps = {
  schema: [
    {
      component: 'Input',
      componentProps: {
        placeholder: '그룹코드를 입력하세요',
      },
      fieldName: 'groupCode',
      label: '그룹코드',
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '그룹명을 입력하세요',
      },
      fieldName: 'groupName',
      label: '그룹명',
      rules: 'required',
    },
    {
      component: 'Switch',
      fieldName: 'isHierarchical',
      label: '계층구조 여부',
      defaultValue: false,
    },
    {
      component: 'InputTextArea',
      fieldName: 'remark',
      label: '비고',
    },
  ],
};

/**
 * 코드 등록/수정 폼 스키마
 */
export const codeFormSchema: VbenFormProps = {
  schema: [
    {
      component: 'Input',
      componentProps: {
        placeholder: '코드값을 입력하세요',
      },
      fieldName: 'codeValue',
      label: '코드값',
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '코드명을 입력하세요',
      },
      fieldName: 'codeName',
      label: '코드명',
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        placeholder: '다국어 키를 입력하세요',
      },
      fieldName: 'i18nKey',
      label: '다국어키',
    },
    {
      component: 'InputNumber',
      componentProps: {
        min: 0,
      },
      fieldName: 'sortOrder',
      label: '정렬순서',
      defaultValue: 0,
    },
    {
      component: 'Select',
      componentProps: {
        options: [
          { label: '사용', value: 1 },
          { label: '미사용', value: 0 },
        ],
      },
      fieldName: 'status',
      label: '상태',
      defaultValue: 1,
    },
    {
      component: 'InputTextArea',
      fieldName: 'remark',
      label: '비고',
    },
  ],
};
