import { markRaw } from 'vue';
import type { VbenFormSchema } from '#/adapter/form';
import type { VxeGridProps } from '#/adapter/vxe-table';

import { $t } from '#/locales';
import { z } from '#/adapter/form';
import BizSelect from '#/components/BizSelect.vue';

/**
 * 값이 들어 있는지 비교하는 컬럼 필터.
 *
 * 계정 목록은 서버가 한 번에 다 내려주므로(getAccounts) 걸러내기·정렬을 화면에서 한다.
 * 회사·부서처럼 값 종류가 정해져 있지 않은 칸은 **입력창 필터**가 목록형보다 낫다 —
 * 회사가 14개, 부서가 16개인데 목록으로 만들면 고르는 것이 더 번거롭고,
 * 계정이 늘면 선택지도 같이 늘어난다.
 *
 * 대소문자를 무시하고, 배열(역할처럼 여러 값)은 이어 붙여 비교한다.
 */
function textFilter() {
  return {
    filters: [{ data: '' }],
    filterRender: { name: 'VxeInput' },
    filterMethod: ({ option, row, column }: any) => {
      const keyword = String(option.data ?? '').trim().toLowerCase();
      if (!keyword) return true;

      const raw = row[column.field];
      const text = Array.isArray(raw) ? raw.join(' ') : String(raw ?? '');
      return text.toLowerCase().includes(keyword);
    },
  };
}

/**
 * 사용자 계정 관리 테이블 컬럼 정의
 *
 * 모든 값 칸에 정렬(`sortable`)과 필터를 걸었다. 계정이 43개인데 소속 회사가 14개로
 * 흩어져 있어서, 정렬·필터가 없으면 "이 회사 사람만 보기" 를 눈으로 훑어야 했다.
 */
export const useColumns = (): VxeGridProps['columns'] => [
  {
    field: 'userName',
    title: $t('system.account.userName'),
    // 아바타가 앞에 붙는 만큼 넓힌다. 좁으면 이름이 바로 잘린다.
    minWidth: 160,
    sortable: true,
    // 얼굴은 **이름 옆**에 둔다. 칸을 따로 만들면 이미 넓은 표가 더 넓어지고,
    // 사람을 알아보는 데 필요한 두 값(얼굴·이름)이 떨어져 놓인다.
    // 정렬·필터는 `field` 를 그대로 쓰므로 이름 기준으로 계속 동작한다.
    slots: { default: 'user-name' },
    ...textFilter(),
  },
  {
    field: 'companyName',
    title: '소속 회사명',
    minWidth: 150,
    sortable: true,
    ...textFilter(),
  },
  {
    field: 'loginId',
    title: $t('system.account.loginId'),
    minWidth: 120,
    sortable: true,
    ...textFilter(),
  },
  {
    field: 'deptName',
    title: $t('system.account.deptName'),
    minWidth: 150,
    sortable: true,
    ...textFilter(),
  },
  {
    field: 'email',
    title: $t('system.account.email'),
    minWidth: 180,
    sortable: true,
    ...textFilter(),
  },
  {
    field: 'phone',
    title: $t('system.account.phone'),
    minWidth: 130,
    sortable: true,
    ...textFilter(),
  },
  {
    field: 'roleNames',
    title: '역할',
    minWidth: 150,
    slots: { default: 'role-tag' },
    // 역할은 배열이라 정렬 기준이 애매하다. 첫 역할 이름으로 줄을 세운다.
    sortable: true,
    sortBy: ({ row }: any) => (row.roleNames ?? []).join(', '),
    ...textFilter(),
  },
  {
    field: 'status',
    title: $t('system.account.status'),
    minWidth: 120,
    slots: { default: 'status-tag' },
    sortable: true,
    // 상태는 값이 셋으로 정해져 있으니 목록에서 고르는 것이 빠르다.
    // 화면의 status-tag 슬롯이 쓰는 값과 같아야 한다.
    filters: [
      { label: '정상 작동', value: 'ACTIVE' },
      { label: '일시 잠금', value: 'LOCKED' },
      { label: '비활성화', value: 'INACTIVE' },
    ],
    filterMethod: ({ option, row }: any) => {
      // 비활성화는 화면에서 '그 밖의 값' 을 모두 묶어 보여 준다. 필터도 같게 맞춘다.
      if (option.value === 'INACTIVE') {
        return row.status !== 'ACTIVE' && row.status !== 'LOCKED';
      }
      return row.status === option.value;
    },
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
    rules: z.string({ error: '회사를 선택해주세요' }),
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
    rules: z.string({ error: '부서를 선택해주세요' }),
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
