import type { VxeTableGridOptions } from '@vben/plugins/vxe-table';
import type { Recordable } from '@vben/types';

import type { ComponentType } from './component';

import { h } from 'vue';

import { IconifyIcon } from '@vben/icons';
import {
  setupVbenVxeTable,
  useVbenVxeGrid as useGrid,
} from '@vben/plugins/vxe-table';
import { preferences } from '@vben/preferences';
import { get, isFunction, isString } from '@vben/utils';

import { objectOmit } from '@vueuse/core';
import { Button, Image, Popconfirm, Switch, Tag } from 'ant-design-vue';

import { $t, $te } from '#/locales';

setupVbenVxeTable({
  // vxe-table의 다국어 처리를 위해 전역 i18n 시스템의 $t 함수를 주입합니다.
  i18n: (key: string, args?: any) => {
    return $t(key, args);
  },
  configVxeTable: (vxeUI) => {


    // 1. VxeUI 전역 번역 콜백 등록
    vxeUI.setI18n((key: string, args?: any) => {
      return $t(key, args);
    });


    // 2. vxe-table이 이 콜백을 사용하도록 현재 언어를 강제로 설정합니다.
    // 함수가 존재하는 경우에만 안전하게 호출합니다.
    if (typeof vxeUI.setLanguage === 'function') {
      // 우리는 ko · en 으로 관리하고, vxe 는 자기 규격(ko-KR · en-US)을 쓴다.
      const VXE_LANG: Record<string, string> = { en: 'en-US', ko: 'ko-KR' };
      const vxeLang = VXE_LANG[preferences.app.locale] ?? 'en-US';
      vxeUI.setLanguage(vxeLang);
    }


    // 3. 전역 설정에도 i18n 콜백을 명시적으로 포함시킵니다.
    vxeUI.setConfig({
      i18n: (key: string, args?: any) => { 
     //   console.log(`[Vxe I18n Callback] Key: ${key}`, args);


     //   console.log(`preferences.app.locale: `, preferences.app.locale);


//if( preferences.app.locale === 'ko'){

        return $t(key, args);
      },
      grid: {
        align: 'center',
        border: false,
        columnConfig: {
          resizable: true,
        },

        formConfig: {
          // vxe-table의 폼 설정을 전역적으로 비활성화하고 formOptions를 사용합니다.
          enabled: false,
        },
        minHeight: 180,
        proxyConfig: {
          autoLoad: true,
          response: {
          //  result: 'items',
          //result: (res: any) => res
          //  result: '',
          //  total: 'total',
          //  list: '',
          },
          showActiveMsg: true,
          showResponseMsg: false,
        },
        round: true,
        showOverflow: true,
        size: 'small',
      } as VxeTableGridOptions,
    });

    /**
     * 핫 리로드 시 vxeTable에서 발생할 수 있는 오류를 해결합니다.
     */
    vxeUI.renderer.forEach((_item, key) => {
      if (key.startsWith('Cell')) {
        vxeUI.renderer.delete(key);
      }
    });

    // 테이블 설정 항목에서 cellRender: { name: 'CellImage' }를 사용할 수 있습니다.
    vxeUI.renderer.add('CellImage', {
      renderTableDefault(renderOpts, params) {
        const { props } = renderOpts;
        const { column, row } = params;
        return h(Image, { src: row[column.field], ...props });
      },
    });

    // 테이블 설정 항목에서 cellRender: { name: 'CellLink' }를 사용할 수 있습니다.
    vxeUI.renderer.add('CellLink', {
      renderTableDefault(renderOpts) {
        const { props } = renderOpts;
        return h(
          Button,
          { size: 'small', type: 'link' },
          { default: () => props?.text },
        );
      },
    });

    // 셀 렌더링: Tag
    vxeUI.renderer.add('CellTag', {
      renderTableDefault({ options, props }, { column, row }) {
        const value = get(row, column.field);
        const tagOptions = options ?? [
          { color: 'success', label: $t('common.enabled'), value: 1 },
          { color: 'error', label: $t('common.disabled'), value: 0 },
        ];
        const tagItem = tagOptions.find((item) => item.value === value);
        return h(
          Tag,
          {
            ...props,
            ...objectOmit(tagItem ?? {}, ['label']),
          },
          { default: () => tagItem?.label ?? value },
        );
      },
    });

    vxeUI.renderer.add('CellSwitch', {
      renderTableDefault({ attrs, props }, { column, row }) {
        const loadingKey = `__loading_${column.field}`;
        const finallyProps = {
          checkedChildren: $t('common.enabled'),
          checkedValue: 1,
          unCheckedChildren: $t('common.disabled'),
          unCheckedValue: 0,
          ...props,
          checked: row[column.field],
          loading: row[loadingKey] ?? false,
          'onUpdate:checked': onChange,
        };
        async function onChange(newVal: any) {
          row[loadingKey] = true;
          try {
            const result = await attrs?.beforeChange?.(newVal, row);
            if (result !== false) {
              row[column.field] = newVal;
            }
          } finally {
            row[loadingKey] = false;
          }
        }
        return h(Switch, finallyProps);
      },
    });

    /**
     * 테이블의 작업 버튼 렌더러 등록
     */
    vxeUI.renderer.add('CellOperation', {
      renderTableDefault({ attrs, options, props }, { column, row }) {
        const defaultProps = { size: 'small', type: 'link', ...props };
        let align: string;
        switch (column.align) {
          case 'center': {
            align = 'center';
            break;
          }
          case 'left': {
            align = 'start';
            break;
          }
          default: {
            align = 'end';
            break;
          }
        }
        const presets: Recordable<Recordable<any>> = {
          delete: {
            danger: true,
            text: $t('common.delete'),
          },
          edit: {
            text: $t('common.edit'),
          },
        };
        const operations: Array<Recordable<any>> = (
          options || ['edit', 'delete']
        )
          .map((opt) => {
            if (isString(opt)) {
              return presets[opt]
                ? { code: opt, ...presets[opt], ...defaultProps }
                : {
                    code: opt,
                    text: $te(`common.${opt}`) ? $t(`common.${opt}`) : opt,
                    ...defaultProps,
                  };
            } else {
              return { ...defaultProps, ...presets[opt.code], ...opt };
            }
          })
          .map((opt) => {
            const optBtn: Recordable<any> = {};
            Object.keys(opt).forEach((key) => {
              optBtn[key] = isFunction(opt[key]) ? opt[key](row) : opt[key];
            });
            return optBtn;
          })
          .filter((opt) => opt.show !== false);

        function renderBtn(opt: Recordable<any>, listen = true) {
          return h(
            Button,
            {
              ...props,
              ...opt,
              icon: undefined,
              onClick: listen
                ? () =>
                    attrs?.onClick?.({
                      code: opt.code,
                      row,
                    })
                : undefined,
            },
            {
              default: () => {
                const content = [];
                if (opt.icon) {
                  content.push(
                    h(IconifyIcon, { class: 'size-5', icon: opt.icon }),
                  );
                }
                content.push(opt.text);
                return content;
              },
            },
          );
        }

        function renderConfirm(opt: Recordable<any>) {
          let viewportWrapper: HTMLElement | null = null;
          return h(
            Popconfirm,
            {
              /**
               * Popconfirm이 고정 열(fixed column)에서 사용될 때, 고정 열을 팝업 컨테이너로 사용하면 좁은 열 너비 때문에 팝업이 제대로 표시되지 않을 수 있습니다. 
               * 테이블 본체 영역을 컨테이너로 사용하면 고정 열의 레이어 순위가 높아 팝업을 가릴 수 있습니다. 
               * body나 테이블 뷰포트 영역을 컨테이너로 사용하면 테이블 스크롤 시 팝업이 따라오지 못할 수 있습니다. 
               * 위와 같은 상황들을 고려하여, 팝업이 표시될 때 테이블 스크롤바 조작을 금지하는 것이 절충안입니다. 
               * 이렇게 하면 팝업 가림 문제를 해결하면서도 팝업이 뷰포트 영역을 벗어나지 않게 할 수 있습니다.
               */
              getPopupContainer(el) {
                viewportWrapper = el.closest('.vxe-table--viewport-wrapper');
                return document.body;
              },
              placement: 'topLeft',
              title: $t('ui.actionTitle.delete', [attrs?.nameTitle || '']),
              ...props,
              ...opt,
              icon: undefined,
              onOpenChange: (open: boolean) => {
                // 팝업이 열릴 때 테이블 스크롤 금지
                if (open) {
                  viewportWrapper?.style.setProperty('pointer-events', 'none');
                } else {
                  viewportWrapper?.style.removeProperty('pointer-events');
                }
              },
              onConfirm: () => {
                attrs?.onClick?.({
                  code: opt.code,
                  row,
                });
              },
            },
            {
              default: () => renderBtn({ ...opt }, false),
              description: () =>
                h(
                  'div',
                  { class: 'truncate' },
                  $t('ui.actionMessage.deleteConfirm', [
                    row[attrs?.nameField || 'name'],
                  ]),
                ),
            },
          );
        }

        const btns = operations.map((opt) =>
          opt.code === 'delete' ? renderConfirm(opt) : renderBtn(opt),
        );
        return h(
          'div',
          {
            class: 'flex table-operations',
            style: { justifyContent: align },
          },
          btns,
        );
      },
    });

    // 여기서 vxe-table의 전역 설정을 자유롭게 확장할 수 있습니다 (예: 사용자 정의 포맷팅).
    // vxeUI.formats.add
  },
});

export const useVbenVxeGrid = <T extends Record<string, any>>(
  ...rest: Parameters<typeof useGrid<T, ComponentType>>
) => useGrid<T, ComponentType>(...rest);

export type OnActionClickParams<T = Recordable<any>> = {
  code: string;
  row: T;
};
export type OnActionClickFn<T = Recordable<any>> = (
  params: OnActionClickParams<T>,
) => void;
export type * from '@vben/plugins/vxe-table';
