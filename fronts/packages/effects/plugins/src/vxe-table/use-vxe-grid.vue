<script lang="ts" setup>
import type {
  VxeGridDefines,
  VxeGridInstance,
  VxeGridListeners,
  VxeGridPropTypes,
  VxeGridProps as VxeTableGridProps,
  VxeToolbarPropTypes,
} from 'vxe-table';

import type { SetupContext } from 'vue';

import type { VbenFormProps } from '@vben-core/form-ui';

import type { ExtendedVxeGridApi, VxeGridProps } from './types';

import {
  computed,
  nextTick,
  onMounted,
  onUnmounted,
  toRaw,
  useSlots,
  useTemplateRef,
  watch,
} from 'vue';

import { usePriorityValues } from '@vben/hooks';
import { EmptyIcon } from '@vben/icons';
import { $t } from '@vben/locales';
import { usePreferences } from '@vben/preferences';
import {
  cloneDeep,
  cn,
  isBoolean,
  isEqual,
  mergeWithArrayOverride,
} from '@vben/utils';

import { VbenHelpTooltip, VbenLoading } from '@vben-core/shadcn-ui';

import { VxeButton } from 'vxe-pc-ui';
import { VxeGrid, VxeUI } from 'vxe-table';

import { extendProxyOptions } from './extends';
import { useTableForm } from './init';

import 'vxe-table/styles/cssvar.scss';
import 'vxe-pc-ui/styles/cssvar.scss';
import './style.css';

interface Props extends VxeGridProps {
  api: ExtendedVxeGridApi;
}

const props = withDefaults(defineProps<Props>(), {});

const FORM_SLOT_PREFIX = 'form-';

const TOOLBAR_ACTIONS = 'toolbar-actions';
const TOOLBAR_TOOLS = 'toolbar-tools';
const TABLE_TITLE = 'table-title';

const gridRef = useTemplateRef<VxeGridInstance>('gridRef');

const state = props.api?.useStore?.();

const {
  gridOptions,
  class: className,
  gridClass,
  gridEvents,
  formOptions,
  tableTitle,
  tableData,
  tableTitleHelp,
  showSearchForm,
  separator,
} = usePriorityValues(props, state);

const { isMobile } = usePreferences();
const isSeparator = computed(() => {
  if (
    !formOptions.value ||
    showSearchForm.value === false ||
    separator.value === false
  ) {
    return false;
  }
  if (separator.value === true || separator.value === undefined) {
    return true;
  }
  return separator.value.show !== false;
});
const separatorBg = computed(() => {
  return !separator.value ||
    isBoolean(separator.value) ||
    !separator.value.backgroundColor
    ? undefined
    : separator.value.backgroundColor;
});
const slots: SetupContext['slots'] = useSlots();

const [Form, formApi] = useTableForm({
  compact: true,
  handleSubmit: async () => {
    const formValues = await formApi.getValues();
    formApi.setLatestSubmissionValues(toRaw(formValues));
    props.api.reload(formValues);
  },
  handleReset: async () => {
    const prevValues = await formApi.getValues();
    await formApi.resetForm();
    const formValues = await formApi.getValues();
    formApi.setLatestSubmissionValues(formValues);
    // 값이 변경된 경우 submitOnChange가 새로고침을 트리거합니다. 따라서 submitOnChange가 false이거나 값이 변경되지 않은 경우에만 수동으로 새로고침합니다.
    if (isEqual(prevValues, formValues) || !formOptions.value?.submitOnChange) {
      props.api.reload(formValues);
    }
  },
  commonConfig: {
    componentProps: {
      class: 'w-full',
    },
  },
  showCollapseButton: true,
  submitButtonOptions: {
    content: computed(() => $t('common.search')),
  },
  wrapperClass: 'grid-cols-1 md:grid-cols-2 lg:grid-cols-3',
});

const showTableTitle = computed(() => {
  return !!slots[TABLE_TITLE]?.() || tableTitle.value;
});

const showToolbar = computed(() => {
  return (
    !!slots[TOOLBAR_ACTIONS]?.() ||
    !!slots[TOOLBAR_TOOLS]?.() ||
    showTableTitle.value
  );
});

const toolbarOptions = computed(() => {
  const slotActions = slots[TOOLBAR_ACTIONS]?.();
  const slotTools = slots[TOOLBAR_TOOLS]?.();
  const searchBtn: VxeToolbarPropTypes.ToolConfig = {
    code: 'search',
    icon: 'vxe-icon-search',
    circle: true,
    status: showSearchForm.value ? 'primary' : undefined,
    title: showSearchForm.value
      ? $t('common.hideSearchPanel')
      : $t('common.showSearchPanel'),
  };
  // 검색 버튼을 사용자가 설정한 toolbarConfig.tools에 병합합니다.
  const toolbarConfig: VxeGridPropTypes.ToolbarConfig = {
    tools: (gridOptions.value?.toolbarConfig?.tools ??
      []) as VxeToolbarPropTypes.ToolConfig[],
  };
  if (gridOptions.value?.toolbarConfig?.search && !!formOptions.value) {
    toolbarConfig.tools = Array.isArray(toolbarConfig.tools)
      ? [...toolbarConfig.tools, searchBtn]
      : [searchBtn];
  }

  if (!showToolbar.value) {
    toolbarConfig.enabled = false;
    return { toolbarConfig };
  }

  // 고정된 툴바 설정을 강제하며, 사용자 정의를 허용하지 않습니다.
  // 설정의 복잡도를 줄이고 향후 유지보수 비용을 절감합니다.
  toolbarConfig.slots = {
    ...(slotActions || showTableTitle.value
      ? { buttons: TOOLBAR_ACTIONS }
      : {}),
    ...(slotTools ? { tools: TOOLBAR_TOOLS } : {}),
  };
  return { toolbarConfig };
});

const options = computed(() => {
  const globalGridConfig = VxeUI?.getConfig()?.grid ?? {};

  // 여러 가지 기본 설정 프리셋(Preset)을 정의합니다.
  const presets: Record<string, VxeTableGridProps> = {
    default: {
      border: true,
      stripe: true,
      height: 'auto',
      showOverflow: true,
      columnConfig: { filter: true , sortable: true},
      keepSource: true,
      keyboardConfig: {
        isTab: true, // Tab 키로 다음 셀로 이동하도록 설정
        isEdit: true, // 키보드로 편집 모드를 제어하도록 설정
      },
      toolbarConfig: {
        custom: true,
        export: true,
        refresh: true,
        zoom: true,
      },
      rowConfig: {
        isCurrent: true,
        isHover: true,
      },
      editConfig: {
        trigger: 'dblclick', // 클릭 시 편집 모드 진입
        mode: 'cell',    // 셀 단위 편집
        showStatus: true,
      },
      exportConfig: {
        filename: 'export',
        types: ['xlsx', 'csv', 'html', 'xml', 'txt'],   // csv, html, xml 가능
        modes: ['current', 'all'], // 현재 페이지 or 전체
        isHeader: true,
        isFooter: true,
      },
      pagerConfig: { enabled:false, pageSize: 15, },
      proxyConfig: {
        autoLoad: true, // 자동 로딩 활성화
        response: {
          result: 'items', // vxe-table 최신 권장 속성
          total: 'total',
        },
     },
    },
    simple: { // 테두리가 없는 심플한 스타일
      border: false,
      stripe: true,
      showOverflow: true,
      columnConfig: { filter: true , sortable: true},
    },
    tree: { // 트리형 그리드 전용 기본 설정
      border: true,
      stripe: true,
      showOverflow: true,
      treeConfig: { transform: true, rowField: 'id', parentField: 'parentId' },
    },
  };

  // 사용자가 전달한 설정에서 preset 값을 읽어오고, 없으면 'default'를 사용합니다.
  const presetKey = (gridOptions.value as any)?.preset || 'default';
  const basePresetOptions = presets[presetKey] || presets.default;

  const mergedOptions: VxeTableGridProps = cloneDeep(
    mergeWithArrayOverride(
      {},
      globalGridConfig,            // 0. 전역 설정 (가장 낮은 우선순위로 변경)
      basePresetOptions,           // 1. 선택된 프리셋 설정 (가장 낮은 우선순위)
       toRaw(toolbarOptions.value),
      toRaw(gridOptions.value),    // 2. 사용자가 전달한 설정 (기본 설정을 덮어씀)     
    ),
  );

  if (mergedOptions.proxyConfig) {
    const { ajax } = mergedOptions.proxyConfig;
    mergedOptions.proxyConfig.enabled = !!ajax;
    // 데이터를 자동으로 로드하지 않고 컴포넌트에서 제어합니다.
    mergedOptions.proxyConfig.autoLoad = false;
  }

  if (mergedOptions.pagerConfig) {
    const mobileLayouts = [
      'PrevJump',
      'PrevPage',
      'Number',
      'NextPage',
      'NextJump',
    ] as any;
    const layouts = [
      'Total',
      'Sizes',
      'Home',
      ...mobileLayouts,
      'End',
    ] as readonly string[];
    mergedOptions.pagerConfig = mergeWithArrayOverride(
      {},
      mergedOptions.pagerConfig,
      {
        pageSize: 20,
        background: true,
        pageSizes: [10, 20, 30, 50, 100, 200],
        className: 'mt-2 w-full',
        layouts: isMobile.value ? mobileLayouts : layouts,
        size: 'mini' as const,
      },
    );
  }
  if (mergedOptions.formConfig) {
    mergedOptions.formConfig.enabled = false;
    if (tableData.value && tableData.value.length > 0) {
      mergedOptions.data = tableData.value;
    }
  }

    // 확장 기능: columnConfig.filter 또는 sortable 플래그가 있는 경우 일괄 적용합니다.
    const isGlobalFilter = (mergedOptions.columnConfig as any)?.filter === true;
    const isGlobalSortable = (mergedOptions.columnConfig as any)?.sortable === true;

    if ((isGlobalFilter || isGlobalSortable) && mergedOptions.columns) {
      mergedOptions.columns = mergedOptions.columns.map((col) => {
        // 순번, 체크박스 등 특수 타입 컬럼과 액션 컬럼(!col.field)은 제외합니다.
        if (
          //['seq', 'checkbox', 'radio', 'expand', 'action', 'id'].includes(col.type as string) ||
          ['action', 'checkbox', 'expand',  'id', 'radio', 'seq'].includes(col.field as string) ||
          !col.field
        ) {
          return col;
        }

        const newCol = { ...col };

        // 정렬 일괄 적용 (개별 컬럼에 명시적으로 설정되지 않은 경우에만)
        if (isGlobalSortable && newCol.sortable === undefined) {
          newCol.sortable = true;
        }

        // 필터 일괄 적용 (개별 컬럼에 명시적으로 설정되지 않은 경우에만)
        if (isGlobalFilter && !newCol.filters) {
          if (newCol.params?.filterList) {
            newCol.filters = [{ label: '', value: '' }]; // 초기 빈 값 (이후 filterVisible 이벤트에서 동적 로드)
            newCol.filterMultiple = true;
          } else {
            newCol.filters = [{ data: '' }];
            newCol.filterRender = { name: 'input', attrs: { placeholder: $t('common.search') } };
            newCol.filterMethod = ({ option, row, column }: any) => {
              if (!option.data) return true;
              const cellValue = String(row[column.field] || '').toLowerCase();
              return cellValue.includes(String(option.data).toLowerCase());
            };
          }
        }

        return newCol;
      });
    }

  return mergedOptions;
});

function onToolbarToolClick(event: VxeGridDefines.ToolbarToolClickEventParams) {
  if (event.code === 'search') {
    onSearchBtnClick();
  }
  (
    gridEvents.value?.toolbarToolClick as VxeGridListeners['toolbarToolClick']
  )?.(event);
}

function onSearchBtnClick() {
  props.api?.toggleSearchForm?.();
}

function onFilterVisible(event: any) {
  const { column, visible, $grid, $table } = event;
  const tableInstance = $grid || $table;
  
  // 필터 패널이 열렸고, 리스트 필터 플래그가 있는 경우
  if (visible && column.params?.filterList && tableInstance) {
    const fullData = tableInstance.getTableData().fullData || [];
    
    // 현재 화면 데이터에서 고유값 추출 (Group By)
    const uniqueValues = [
      ...new Set(fullData.map((row: any) => row[column.field]))
    ].filter((v) => v !== null && v !== undefined && v !== '');

    // 기존에 체크되어 있던 항목 보존
    const checkedValues = new Set(
      (column.filters || []).filter((f: any) => f.checked).map((f: any) => f.value)
    );
    const newFilters = uniqueValues.map((val) => ({
      label: String(val),
      value: val,
      checked: checkedValues.has(val),
    }));

    // 동적으로 추출된 리스트를 필터 옵션으로 주입
    tableInstance.setFilter(column, newFilters.length > 0 ? newFilters : [{ label: $t('common.noData'), value: '' }]);
  }
  
  (gridEvents.value?.filterVisible as any)?.(event);
}

const events = computed(() => {
  return {
    ...gridEvents.value,
    toolbarToolClick: onToolbarToolClick,
    filterVisible: onFilterVisible,
  };
});

const delegatedSlots = computed(() => {
  const resultSlots: string[] = [];

  for (const key of Object.keys(slots)) {
    if (
      !['empty', 'form', 'loading', TOOLBAR_ACTIONS, TOOLBAR_TOOLS].includes(
        key,
      )
    ) {
      resultSlots.push(key);
    }
  }
  return resultSlots;
});

const delegatedFormSlots = computed(() => {
  const resultSlots: string[] = [];

  for (const key of Object.keys(slots)) {
    if (key.startsWith(FORM_SLOT_PREFIX)) {
      resultSlots.push(key);
    }
  }
  return resultSlots.map((key) => key.replace(FORM_SLOT_PREFIX, ''));
});

const showDefaultEmpty = computed(() => {
  // 네이티브 VXE Table 빈 상태 설정이 있는지 확인합니다.
  const hasEmptyText = options.value.emptyText !== undefined;
  const hasEmptyRender = options.value.emptyRender !== undefined;

  // 네이티브 설정이 있는 경우 기본 빈 상태를 표시하지 않습니다.
  return !hasEmptyText && !hasEmptyRender;
});

async function init() {
  await nextTick();
  const globalGridConfig = VxeUI?.getConfig()?.grid ?? {};
  const defaultGridOptions: VxeTableGridProps = mergeWithArrayOverride(
    {},
    toRaw(gridOptions.value),
    toRaw(globalGridConfig),
  );
  // 폼의 기본값 영향을 방지하기 위해 내부적으로 데이터를 능동적으로 로드합니다.
  const autoLoad = defaultGridOptions.proxyConfig?.autoLoad;
  const enableProxyConfig = options.value.proxyConfig?.enabled;
  if (enableProxyConfig && autoLoad) {
    props.api.grid.commitProxy?.(
      'query',
      formOptions.value ? ((await formApi.getValues()) ?? {}) : {},
    );
    // props.api.reload(formApi.form?.values ?? {});
  }

  // 폼은 vben-form으로 대체되므로 formConfig를 지원하지 않으며, 이에 대한 경고를 표시합니다.
  const formConfig = gridOptions.value?.formConfig;
  // 한 페이지에 여러 테이블이 로드될 때 두 번째 이후 테이블 초기화 시 발생하는 경고를 처리합니다.
  // 첫 번째 초기화 후 defaultGridOptions와 gridOptions가 병합되어 State에 캐시되기 때문입니다.
  if (formConfig && formConfig.enabled) {
    console.warn(
      '[Vben Vxe Table]: The formConfig in the grid is not supported, please use the `formOptions` props',
    );
  }
  props.api?.setState?.({ gridOptions: defaultGridOptions });
  // 폼은 vben-form으로 대체되므로 query 관련 이벤트가 파라미터를 받을 수 있도록 보장해야 합니다.
  extendProxyOptions(props.api, defaultGridOptions, () =>
    formApi.getLatestSubmissionValues(),
  );
}

// formOptions는 반응형을 지원합니다.
watch(
  formOptions,
  () => {
    formApi.setState((prev: Record<string, any>) => {
      const finalFormOptions: VbenFormProps = mergeWithArrayOverride(
        {},
        formOptions.value,
        prev,
      );
      return {
        ...finalFormOptions,
        collapseTriggerResize: !!finalFormOptions.showCollapseButton,
      };
    });
  },
  {
    immediate: true,
  },
);

const isCompactForm = computed(() => {
  return formApi.getState()?.compact;
});

onMounted(() => {
  props.api?.mount?.(gridRef.value, formApi);
  init();
});

onUnmounted(() => {
  formApi?.unmount?.();
  props.api?.unmount?.();
});
</script>

<template>
  <div :class="cn('h-full rounded-md bg-card', className)">
    <VxeGrid
      ref="gridRef"
      :class="
        cn(
          'p-2',
          {
            'pt-0': showToolbar && !formOptions,
          },
          gridClass,
        )
      "
      v-bind="options"
      v-on="events"
    >
      <!-- 왼쪽 조작 영역 또는 제목 -->
      <template v-if="showToolbar" #toolbar-actions="slotProps">
        <slot v-if="showTableTitle" name="table-title">
          <div class="flex-center gap-1 text-[1rem] font-bold">
            {{ tableTitle }}
            <VbenHelpTooltip v-if="tableTitleHelp">
              {{ tableTitleHelp }}
            </VbenHelpTooltip>
          </div>
        </slot>
        <slot name="toolbar-actions" v-bind="slotProps"> </slot>
      </template>

      <!-- 기본 슬롯 상속 -->
      <template
        v-for="slotName in delegatedSlots"
        :key="slotName"
        #[slotName]="slotProps"
      >
        <slot :name="slotName" v-bind="slotProps"></slot>
      </template>
      <template #toolbar-tools="slotProps">
        <slot name="toolbar-tools" v-bind="slotProps"></slot>
        <VxeButton
          icon="vxe-icon-search"
          circle
          class="ml-2"
          v-if="gridOptions?.toolbarConfig?.search && !!formOptions"
          :status="showSearchForm ? 'primary' : undefined"
          :title="$t('common.search')"
          @click="onSearchBtnClick"
        />
      </template>

      <!-- 폼 -->
      <template #form>
        <div
          v-if="formOptions"
          v-show="showSearchForm !== false"
          :class="
            cn(
              'relative rounded-sm py-3',
              isCompactForm
                ? isSeparator
                  ? 'pb-8'
                  : 'pb-4'
                : isSeparator
                  ? 'pb-4'
                  : 'pb-0',
            )
          "
        >
          <slot name="form">
            <Form>
              <template
                v-for="slotName in delegatedFormSlots"
                :key="slotName"
                #[slotName]="slotProps"
              >
                <slot
                  :name="`${FORM_SLOT_PREFIX}${slotName}`"
                  v-bind="slotProps"
                ></slot>
              </template>
              <template #reset-before="slotProps">
                <slot name="reset-before" v-bind="slotProps"></slot>
              </template>
              <template #submit-before="slotProps">
                <slot name="submit-before" v-bind="slotProps"></slot>
              </template>
              <template #expand-before="slotProps">
                <slot name="expand-before" v-bind="slotProps"></slot>
              </template>
              <template #expand-after="slotProps">
                <slot name="expand-after" v-bind="slotProps"></slot>
              </template>
            </Form>
          </slot>
          <div
            v-if="isSeparator"
            :style="{
              ...(separatorBg ? { backgroundColor: separatorBg } : undefined),
            }"
            class="absolute bottom-1 -left-2 z-100 h-2 w-[calc(100%+1rem)] overflow-hidden bg-background-deep md:bottom-2 md:h-3"
          ></div>
        </div>
      </template>
      <!-- 로딩 -->
      <template #loading>
        <slot name="loading">
          <VbenLoading :spinning="true" />
        </slot>
      </template>
      <!-- 통합 상태 표시 -->
      <template v-if="showDefaultEmpty" #empty>
        <slot name="empty">
          <EmptyIcon class="mx-auto" />
          <div class="mt-2">{{ $t('common.noData') }}</div>
        </slot>
      </template>
    </VxeGrid>
  </div>
</template>
