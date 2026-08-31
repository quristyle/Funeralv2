import type { VbenFormProps } from '#/adapter/form';
import type { VxeGridProps } from '#/adapter/vxe-table';

import { h, markRaw } from 'vue';

import { IconifyIcon } from '@vben/icons';
import AddressSearchInput from '#/components/AddressSearchInput.vue';
import DictSelect from '#/components/DictSelect.vue';
import { $t } from '@vben/locales';

import { Button, Input, Popconfirm, Tag, Tooltip } from 'ant-design-vue';
import { can } from '#/utils/permission';

/** 사용처에 쓰는 공통코드 그룹 */
export const USAGE_LOCATION_GROUP = 'COMPANY_USAGE_LOCATION';

/** 사용처 코드 하나 */
export interface UsageLocationOption {
  label: string;
  value: string;
}

/**
 * 회사 관리 테이블 컬럼 정의를 반환하는 함수
 * @param onActionClick 버튼 클릭 이벤트를 처리할 콜백 함수
 * @param usageOptions  사용처 공통코드 목록. 코드값을 이름으로 바꿔 보여 주고,
 *                      필터의 고를 목록으로도 쓴다. 아직 못 받았으면 빈 배열이다.
 * @returns VXETable 컬럼 설정 배열
 */
export const useColumns = (
  onActionClick: (params: any) => void,
  usageOptions: UsageLocationOption[] = [],
): VxeGridProps['columns'] => [
  { type: 'seq', width: 50 },
  /** 회사명 - 수정 가능(VxeInput 사용) */
  { field: 'name', title: $t('system.company.name'), minWidth: 150, editRender: { name: 'VxeInput' } },
  /** 짧은명칭 - 수정 가능 */
  { field: 'shortName', title: '짧은명칭', width: 120, editRender: { name: 'VxeInput' } },
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
  /** 정렬 순서 - 수정 가능 */
  {
    field: 'sortOrder',
    title: '정렬 순서',
    width: 100,
    editRender: {
      name: 'input',
      attrs: { type: 'number', min: 0 },
    },
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
  /**
   * 주소 - 세 값(우편번호 · 기본주소 · 상세주소)을 한 칸에 보여 주고,
   * 셀을 두 번 누르면 **기본주소와 상세주소**를 그 자리에서 고친다.
   *
   * 우편번호는 여기서 못 고친다. 바꾸려면 우편번호 검색 창(`AddressSearchInput`)이
   * 열려야 하는데, 창이 열리는 순간 셀에서 포커스가 빠져 vxe 가 편집을 닫는다 —
   * 그러면 창이 돌려주는 값이 이미 닫힌 행에 쓰인다. 우편번호는 작업 열의
   * [수정] 폼에서 바꾼다(그 폼에는 검색 단추가 있다).
   */
  {
    field: 'address',
    title: '주소',
    minWidth: 320,
    formatter: ({ row }) => row.address ? `[${row.zipCode || ''}] ${row.address} ${row.addressDetail || ''}` : '',
    // 화면에 보이는 것은 세 값을 합친 글자다. `address` 하나만 훑으면
    // 우편번호나 상세주소로 찾을 수 없다 — 보이는 그대로 훑게 한다.
    params: {
      filterText: (row: any) =>
        [row.zipCode, row.address, row.addressDetail].filter(Boolean).join(' '),
    },
    editRender: {},
    slots: {
      edit: ({ row }: any) =>
        h('div', { class: 'flex w-full items-center gap-1' }, [
          row.zipCode
            ? h(
                'span',
                {
                  class: 'shrink-0 text-xs text-muted-foreground',
                  title: '우편번호는 [수정] 폼에서 바꿉니다',
                },
                `[${row.zipCode}]`,
              )
            : null,
          h(Input, {
            class: 'flex-1',
            placeholder: '주소',
            size: 'small',
            value: row.address,
            'onUpdate:value': (next: string) => {
              row.address = next;
            },
          }),
          h(Input, {
            class: 'w-2/5 shrink-0',
            placeholder: '상세주소',
            size: 'small',
            value: row.addressDetail,
            'onUpdate:value': (next: string) => {
              row.addressDetail = next;
            },
          }),
        ].filter(Boolean)),
    },
  },
  /**
   * 사용처 — 이 회사가 쓰이는 시스템들. 값은 공통코드의 `codeValue` 여러 개다.
   *
   * 이 칸은 여기서 못 고친다. 셀 편집기 하나에 여러 값을 담기 어렵고,
   * [수정] 폼의 다중 선택이 이미 그 일을 한다.
   */
  {
    field: 'usageLocations',
    title: '사용처',
    minWidth: 220,
    /**
     * 값이 **목록**이라 필터도 '들어 있는가' 로 판단해야 한다.
     * 공통 필터줄은 목록을 주면 여러 개 고를 수 있는 칸으로 그리고(OR),
     * 판정은 여기서 준 것을 그대로 쓴다.
     */
    filters: usageOptions.map((o) => ({ label: o.label, value: o.value })),
    filterMethod: ({ option, row }: any) =>
      (row?.usageLocations ?? []).includes(option?.value),
    // 화면에 보이는 것은 코드 이름이다. 이름과 코드값 둘 다로 찾게 한다.
    params: {
      filterText: (row: any) =>
        (row?.usageLocations ?? [])
          .map((code: string) => {
            const found = usageOptions.find((o) => o.value === code);
            return found ? `${found.label} ${code}` : code;
          })
          .join(' '),
    },
    slots: {
      default: ({ row }: any) => {
        const codes: string[] = row?.usageLocations ?? [];
        if (codes.length === 0) {
          // 아무 곳에도 배정하지 않은 회사. 빈 칸과 구분되게 표시한다.
          return h('span', { class: 'text-muted-foreground' }, '-');
        }
        return h(
          'div',
          { class: 'flex flex-wrap items-center gap-1' },
          codes.map((code) => {
            const found = usageOptions.find((o) => o.value === code);
            return h(
              Tag,
              {
                // 코드 목록을 아직 못 받았거나 코드가 지워졌으면 값을 그대로 보여 준다.
                // 그래야 "이름을 못 찾는 값이 남아 있다" 는 것이 눈에 띈다.
                color: found ? 'processing' : 'default',
                key: code,
                title: found ? code : `공통코드에서 이름을 찾을 수 없습니다: ${code}`,
              },
              { default: () => found?.label ?? code },
            );
          }),
        );
      },
    },
  },
  /** 비고 */
  { field: 'remark', title: $t('system.company.remark'), minWidth: 200, editRender: { name: 'VxeInput' } },
  /** 승인일 */
  { 
    field: 'approvalDate', 
    title: '승인일', 
    width: 150, 
    formatter: ({ cellValue }) => {
      if (!cellValue) return '';
      const date = new Date(cellValue);
      if (isNaN(date.getTime())) return '';
      
      const year = date.getFullYear();
      const month = String(date.getMonth() + 1).padStart(2, '0');
      const day = String(date.getDate()).padStart(2, '0');
      return `${year}-${month}-${day}`;
    },
    editRender: { 
      name: 'input',
      attrs: { type: 'date' }
    }
  },
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
    /** 짧은명칭 */
    { fieldName: 'shortName', label: '짧은명칭', component: 'Input', },
    /** 사업자 등록 번호 */
    { fieldName: 'businessNumber', label: $t('system.company.businessNumber'), component: 'Input', },
    /** 대표자 성함 */
    { fieldName: 'representative', label: $t('system.company.representative'), component: 'Input', },
    /**
     * 사용처 — 이 회사가 어느 시스템에서 쓰이는지. 여러 개 고를 수 있고
     * 하나도 안 골라도 된다(그때는 빈 목록으로 저장된다).
     */
    {
      fieldName: 'usageLocations',
      label: '사용처',
      component: markRaw(DictSelect),
      componentProps: {
        allowClear: true,
        dictCode: USAGE_LOCATION_GROUP,
        maxTagCount: 'responsive',
        mode: 'multiple',
        placeholder: '쓰이는 시스템을 고릅니다 (여러 개 가능)',
      },
      /**
       * `col-span-2` 를 주면 안 된다.
       *
       * 이 폼의 감싸개는 `grid-cols-1`(한 줄에 하나)이다. 그 상태에서 한 항목만
       * 두 칸을 차지하게 하면 **없던 두 번째 열이 생겨** 나머지 항목이 좁은 열과
       * 넓은 열로 번갈아 들어간다 — 라벨만 보이고 입력칸이 찌그러진다.
       * 폭이 필요하면 폼 전체의 `wrapperClass` 를 2열로 바꾸고 모든 항목에
       * `col-span-2 md:col-span-1` 을 줘야 한다(메뉴 관리 폼이 그 방식이다).
       */
    },
    /** 우편번호 */
    { 
      fieldName: 'zipCode', 
      label: '우편번호', 
      component: markRaw(AddressSearchInput), 
    },
    /** 주소 */
    { fieldName: 'address', label: '주소', component: 'Input', },
    /** 상세주소 */
    { fieldName: 'addressDetail', label: '상세주소', component: 'Input', },
    /** 정렬 순서 */
    {
      fieldName: 'sortOrder',
      label: '정렬 순서',
      component: 'InputNumber',
      defaultValue: 0,
      componentProps: {
        min: 0,
        step: 1,
        precision: 0,
      },
    },
    /** 상태: 라디오 그룹 사용 (기본값: 활성) */
    { fieldName: 'status', label: $t('system.company.status'), component: 'RadioGroup', defaultValue: 1,
      componentProps: {
        options: [
          { label: $t('common.enabled'), value: 1 },
          { label: $t('common.disabled'), value: 0 },
        ],
      },
    },
    /** 승인일 */
    { fieldName: 'approvalDate', label: '승인일', component: 'DatePicker', componentProps: { valueFormat: 'YYYY-MM-DD' } },
    /** 비고: 멀티라인 텍스트 입력 */
    { fieldName: 'remark', label: $t('system.company.remark'), component: 'Textarea', },
  ],
};
