import type { SetupVxeTable } from './types';

import { defineComponent, watch } from 'vue';

import { usePreferences } from '@vben/preferences';

import {
  VxeButton,
  VxeCheckbox,
  VxeIcon,
  VxeInput,
  VxeLoading,
  VxeModal,
  VxeNumberInput,
  VxePager,
  VxeRadioGroup,
  VxeSelect,
  VxeTooltip,
  VxeUI,
  VxeUpload,
} from 'vxe-pc-ui';
import enUS from 'vxe-pc-ui/lib/language/en-US'; 
import {
  VxeColgroup,
  VxeColumn,
  VxeGrid,
  VxeTable,
  VxeToolbar,
} from 'vxe-table';
import VxeTablePluginExportXLSX from 'vxe-table-plugin-export-xlsx';
import ExcelJS from 'exceljs';

import { injectPluginsOptions } from '../plugins-context';
import { extendsDefaultFormatter } from './extends'; // 로드 여부

// 로드 여부
let isInit = false;

let tableFormFactory: ((...args: any[]) => any) | undefined;

function normalizeVxeLocale<T extends Record<string, any>>(localeModule: T) {
  return (
    localeModule &&
    typeof localeModule === 'object' &&
    'default' in localeModule
      ? localeModule.default
      : localeModule
  ) as T;
}

export function useTableForm(...args: any[]) {
  const pluginsOptions = injectPluginsOptions();
  const contextFormFactory = pluginsOptions?.form?.useVbenForm;

  const factory = tableFormFactory || contextFormFactory;
  if (!factory) {
    throw new Error(
      'useTableForm is not initialized. Please provide useVbenForm via setupVbenVxeTable() or providePluginsOptions()',
    );
  }

  return factory(...args);
}

// 일부 컴포넌트가 등록되지 않으면 vxe-table에서 오류가 발생할 수 있습니다. 여기서는 실제로 컴포넌트를 사용하지 않으며, 단지 오류를 방지하고 번들 크기를 줄이기 위한 용도입니다.
const createVirtualComponent = (name = '') => {
  return defineComponent({
    name,
  });
};

export function initVxeTable() {
  if (isInit) {
    return;
  }

  // 1. ExcelJS 라이브러리 정규화 및 전역 주입
  const ExcelJS_LIB = (ExcelJS as any).default || ExcelJS;
  if (ExcelJS_LIB) {
    // 모든 형태의 전역 변수 지원
    (window as any).ExcelJS = ExcelJS_LIB;
    
    // vxe-table 전역 객체에 주입 (매우 중요)
    (VxeUI as any).ExcelJS = ExcelJS_LIB;
    if (!(VxeUI as any).addons) (VxeUI as any).addons = {};
    (VxeUI as any).addons.ExcelJS = ExcelJS_LIB;
  }

  // 2. 플러그인 객체 추출
  const plugin = (VxeTablePluginExportXLSX as any).default || VxeTablePluginExportXLSX;
  
  if (plugin) {
    // 3. 플러그인 등록 (ExcelJS 객체를 명시적으로 전달)
    VxeUI.use(plugin, { ExcelJS: ExcelJS_LIB });
    console.log('[vxe-table-init] XLSX(ExcelJS) Plugin registered. Global ExcelJS:', !!(window as any).ExcelJS);
  }

  VxeUI.component(VxeTable);
  VxeUI.component(VxeColumn);
  VxeUI.component(VxeColgroup);
  VxeUI.component(VxeGrid);
  VxeUI.component(VxeToolbar);

  VxeUI.component(VxeButton);
  VxeUI.component(VxeCheckbox);
  VxeUI.component(createVirtualComponent('VxeForm'));
  VxeUI.component(VxeIcon);
  VxeUI.component(VxeInput);
  VxeUI.component(VxeLoading);
  VxeUI.component(VxeModal);
  VxeUI.component(VxeNumberInput);
  VxeUI.component(VxePager);
  VxeUI.component(VxeRadioGroup);
  VxeUI.component(VxeSelect);
  VxeUI.component(VxeTooltip);
  VxeUI.component(VxeUpload);

  VxeUI.setConfig({
    export: {
      types: ['xlsx', 'csv', 'html', 'xml', 'txt'],
    },
  });

  isInit = true;
}

export function setupVbenVxeTable(setupOptions: SetupVxeTable) {
  const { configVxeTable, useVbenForm: useVbenFormFromParam } = setupOptions;

  initVxeTable();

  // 파라미터로 전달된 useVbenForm을 우선 사용하며, 없는 경우 비워두어 context 주입이 적용되도록 합니다.
  if (useVbenFormFromParam) {
    tableFormFactory = useVbenFormFromParam;
  }
  const { isDark, locale } = usePreferences();

  const localMap = {
    'en-US': normalizeVxeLocale(enUS),
  };

  watch(
    [() => isDark.value, () => locale.value],
    ([isDarkValue, localeValue]) => {
      VxeUI.setTheme(isDarkValue ? 'dark' : 'light');
      VxeUI.setI18n(localeValue, localMap[localeValue]);
      VxeUI.setLanguage(localeValue);
    },
    {
      immediate: true,
    },
  );

  extendsDefaultFormatter(VxeUI);

  configVxeTable(VxeUI);
}
