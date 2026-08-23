import { markRaw } from 'vue';
import type { VbenFormSchema } from '#/adapter/form';
import type { VxeGridProps } from '#/adapter/vxe-table';

import { $t } from '#/locales';
import { z } from '#/adapter/form';
import BizSelect from '#/components/BizSelect.vue';

/**
 * 사용자 계정 관리 테이블 컬럼 정의
 */
export const useColumns = (): VxeGridProps['columns'] => [
  { field: 'userName', title: $t('system.account.userName'), minWidth: 120 },
  { field: 'companyName', title: '소속 회사명', minWidth: 150 },
  { field: 'loginId', title: $t('system.account.loginId'), minWidth: 120 },
  { field: 'deptName', title: $t('system.account.deptName'), minWidth: 150 },
  { field: 'email', title: $t('system.account.email'), minWidth: 180 },
  { field: 'phone', title: $t('system.account.phone'), minWidth: 130 },
  {
    field: 'roleNames',
    title: '역할',
    minWidth: 150,
    slots: { default: 'role-tag' },
  },
  {
    field: 'status',
    title: $t('system.account.status'),
    minWidth: 120,
    slots: { default: 'status-tag' },
  },
  // ── MSA 사용자 대조 ────────────────────────────────────
  // 각 MSA 의 API 를 읽어 이 계정이 그쪽에 어떤 사용자로 있는지 보여 준다.
  // 저장·수정은 하지 않는다. 서비스가 죽어 있으면 '확인 불가' 로 표시된다.
  {
    field: 'msaHelpdesk',
    title: '헬프데스크',
    minWidth: 170,
    slots: { default: 'msa-helpdesk' },
  },
  {
    field: 'msaProjMng',
    title: '프로젝트관리',
    minWidth: 150,
    slots: { default: 'msa-projmng' },
  },
  {
    field: 'action',
    title: $t('common.action'),
    width: 150,
    fixed: 'right',
    slots: { default: 'action' },
  },
];

/**
 * 사용자 계정 등록/수정 폼의 스키마 및 설정 정의
 */
export const useSchema = (): VbenFormSchema[] => [
  {
    component: 'Input',
    componentProps: {
      placeholder: $t('system.account.placeholder.loginId'),
    },
    fieldName: 'loginId',
    label: $t('system.account.loginId'),
    rules: 'required',
  },
  {
    component: 'Input',
    componentProps: {
      placeholder: $t('system.account.placeholder.userName'),
    },
    fieldName: 'userName',
    label: $t('system.account.userName'),
    rules: 'required',
  },
  {
    component: markRaw(BizSelect),
    componentProps: {
      type: 'company',
      placeholder: '회사를 선택해주세요',
    },
    fieldName: 'companyId',
    label: '소속 회사',
    rules: z.string({ required_error: '회사를 선택해주세요' }),
  },
  {
    component: markRaw(BizSelect),
    componentProps: {
      type: 'dept',
      placeholder: '부서를 선택해주세요',
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
    fieldName: 'deptId',
    label: $t('system.account.dept'),
    rules: z.string({ required_error: '부서를 선택해주세요' }),
  },
  {
    component: 'Input',
    componentProps: {
      placeholder: $t('system.account.placeholder.email'),
    },
    fieldName: 'email',
    label: $t('system.account.email'),
  },
  {
    component: 'Input',
    componentProps: {
      placeholder: $t('system.account.placeholder.phone'),
    },
    fieldName: 'phone',
    label: $t('system.account.phone'),
  },
  {
    component: 'Select',
    componentProps: {
      mode: 'multiple',
      placeholder: '역할을 선택해주세요',
      options: [],
      allowClear: true,
    },
    fieldName: 'roleIds',
    label: '역할 권한',
  },
  {
    component: 'Select',
    componentProps: {
      options: [
        { label: $t('system.account.statusActive'), value: 'ACTIVE' },
        { label: $t('system.account.statusLocked'), value: 'LOCKED' },
        { label: $t('system.account.statusDisabled'), value: 'DISABLED' },
      ],
    },
    defaultValue: 'ACTIVE',
    fieldName: 'status',
    label: $t('system.account.lockStatus'),
  },
];
