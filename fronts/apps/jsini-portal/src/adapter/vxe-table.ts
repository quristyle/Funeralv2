import type { VxeTableGridOptions } from '@vben/plugins/vxe-table';
import type { Recordable } from '@vben/types';

import type { ComponentType } from './component';

import { defineComponent, h } from 'vue';

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

import { createGridFeatures, FILTER_HIDDEN_CLASS } from './vxe-grid-features';

/**
 * 조회 응답에서 행 목록을 꺼낸다.
 *
 * vxe 는 응답을 그대로 주지 않고 `{ data, $table, $grid }` 로 감싸서 준다.
 * 조회 함수가 벗긴 배열을 주든(`src/api/envelope.ts` 의 기준) 봉투를 그대로
 * 주든 여기서 흡수한다 — 화면마다 반환 모양을 맞추게 하지 않으려는 것이다.
 */
function gridRows(params: any): any[] {
  const res = params?.data ?? params;
  if (Array.isArray(res)) return res;
  if (Array.isArray(res?.result)) return res.result;
  if (Array.isArray(res?.items)) return res.items;
  if (Array.isArray(res?.data)) return res.data;
  if (Array.isArray(res?.data?.result)) return res.data.result;
  return [];
}

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

        /**
         * 정렬·필터는 **화면에서 거른다.**
         *
         * `remote` 를 적지 않으면 `proxyConfig` 가 켜진 그리드에서 vxe 가
         * 정렬·필터를 원격으로 보고 **서버를 다시 부른다.** 우리 조회는 전량을
         * 받아 오는 방식이라 그럴 필요가 없고, 다시 부르면 화면만 깜빡인다.
         * (계정 관리 화면이 이걸 겪고 혼자 명시해 두었던 것을 전역으로 올렸다.)
         *
         * `showIcon: false` — 깔때기를 감춘다. 필터는 머리글 아래 칸으로 받으므로
         * 남겨 두면 눌렀을 때 '적용 · 초기화' 버튼만 있는 **빈 팝업**이 열린다.
         * 정렬 화살표는 그대로 둔다. (34번 문서 3절)
         */
        sortConfig: {
          multiple: true,
          remote: false,
        },
        filterConfig: {
          remote: false,
          showIcon: false,
        },

        /**
         * 보이는 컬럼 고르기 — **모달로 연다.**
         *
         * 기본값('simple')은 누른 단추에 붙는 작은 팝업이라, 누른 자리를
         * `customStore.btnEl` 로 기억해 두어야 제자리에 뜬다. 그 값을 채워 주는 것은
         * vxe 위쪽 도구줄의 단추인데, 우리는 이 기능을 **아래 도구줄**로 옮겼다
         * (`vxe-grid-features.ts`) — 문서에 없는 내부 상태에 손을 넣지 않으려면
         * 뜨는 자리가 단추와 무관한 방식이어야 한다.
         *
         * `drawer` 는 못 쓴다 — `VxeDrawer` 를 등록하지 않았다(plugins/vxe-table/init.ts).
         * `VxeModal` 은 등록돼 있고 `draggable` 기본값이 참이라 [준수사항 3]도 만족한다.
         */
        customConfig: {
          mode: 'modal',
        },

        /**
         * 페이저는 **쓰지 않는 것이 기본**이다 (2026-09-05).
         *
         * ------------------------------------------------------------
         * 왜 껐나
         * ------------------------------------------------------------
         *
         * 우리 조회는 거의 다 **전량을 한 번에 받는다.** 그런데 vxe 는
         * `proxyConfig` 와 페이저가 함께 켜져 있으면 받은 배열을 **그 쪽에 그릴
         * 행 그대로** 쓰고 스스로 자르지 않는다. 그래서 켜 두면 쪽 번호가
         * 생기는데 눌러도 같은 자료가 나오는 **장식 페이저**가 된다.
         *
         * 게다가 전역에 적지 않으면 꺼지지도 않았다 — vxe 자체 기본값이
         * `grid.pagerConfig.enabled = true` 이고, 그것이 프리셋
         * (`use-vxe-grid.vue` 의 `enabled: false`)보다 **앞서** 병합되기 때문이다.
         * 화면 71곳 중 52곳이 `enabled: false` 를 손으로 적고 있었던 이유가 이것이다.
         *
         * ------------------------------------------------------------
         * 예전에 껐다가 전 화면이 비었던 것은 이미 해결됐다
         * ------------------------------------------------------------
         *
         * vxe 는 페이저가 켜져 있으면 `result` · `page.total` 을 보고, 꺼져 있으면
         * 응답 전체를 목록으로 본다 — 서로 다른 자리를 읽는다. 그때는 그 차이를
         * 화면이 맞춰야 했다.
         *
         * 지금은 아래 `response` 에 셋을 다 지정해 두어(`list` · `result` · `total`)
         * **어느 쪽이든 배열과 봉투를 모두 받는다.** 그래서 이 값을 뒤집어도
         * 조회 함수는 한 줄도 고칠 것이 없다.
         *
         * ------------------------------------------------------------
         * 페이저가 필요한 화면은 켠다
         * ------------------------------------------------------------
         *
         * 화면이 적은 값이 전역을 이긴다.
         *
         * ```ts
         * gridOptions: { pagerConfig: { enabled: true, pageSize: 20 } }
         * ```
         *
         * **켤 거면 서버가 쪽 단위로 잘라 줘야 한다.** `query({ page, sorts })` 를
         * 받아 `{ page: { total }, result }` 를 돌려주는 형태다
         * (`portal/system/push/logs.vue` 가 본이다 — 31,604건).
         * 전량을 받으면서 켜면 위에 적은 장식 페이저가 된다.
         *
         * 이미 켜 둔 화면 넷은 그대로다 — 발송 이력 · 다국어 관리 ·
         * 헬프데스크 요청 관리 · 기상 이벤트.
         */
        pagerConfig: {
          enabled: false,
        },

        formConfig: {
          // vxe-table의 폼 설정을 전역적으로 비활성화하고 formOptions를 사용합니다.
          enabled: false,
        },
        minHeight: 180,
        proxyConfig: {
          autoLoad: true,
          response: {
            /**
             * 페이저가 **꺼져 있을 때** vxe 가 목록을 찾는 자리.
             *
             * 기본값이 `'list'` 라 `res.list` 를 찾는데 우리 API 는 그런 이름을
             * 쓰지 않는다. 그래서 페이저를 끈 화면은 행이 하나도 안 나왔다.
             */
            list: (params: any) => gridRows(params),
            /** 페이저가 **켜져 있을 때** vxe 가 목록을 찾는 자리. */
            result: (params: any) => gridRows(params),
            /**
             * 페이저가 켜져 있을 때의 총건수.
             *
             * 봉투의 `page.total` 이 정본이고, 없으면 받은 건수로 본다
             * (전체를 한 번에 주는 API — 페이징을 vxe 가 하는 경우).
             */
            total: (params: any) => {
              const res = params?.data ?? params;
              const total = Number(res?.page?.total ?? res?.data?.page?.total);
              return Number.isFinite(total) ? total : gridRows(params).length;
            },
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

/**
 * 모바일(<768px) 보정 — 화면들을 일일이 고치지 않고 여기서 한 번에 처리한다 (40번 문서).
 *
 *  · `height: 'auto'` 는 부모가 높이를 주는 데스크톱(page-fill-last) 전제다.
 *    모바일에서는 page-fill-last 가 풀려(styles/index.css) 부모 높이가 없으므로
 *    고정 높이로 바꾼다 — common-code 화면이 먼저 쓴 방식(600px)과 같은 결이다.
 *  · 고정열(fixed)은 375px 에서 데이터 가시 폭을 절반 넘게 잡아먹어 푼다.
 *    열 자체는 남으므로 가로 스크롤로 닿는다.
 *
 * isMobile 은 화면 셋업 시점 값이다 — 화면을 연 채로 창 크기를 바꾸면(회전 등)
 * 다시 들어와야 반영된다. 그리드 옵션을 반응형으로 갈아끼우면 상태(선택·편집)가
 * 날아가서 일부러 이렇게 뒀다.
 */
function adjustGridForMobile(options: any) {
  if (!preferences.app.isMobile || !options?.gridOptions) return options;

  const gridOptions = { ...options.gridOptions };
  if (gridOptions.height === 'auto') {
    gridOptions.height = 500;
  }
  if (Array.isArray(gridOptions.columns)) {
    gridOptions.columns = gridOptions.columns.map((col: any) =>
      col?.fixed ? { ...col, fixed: undefined } : col,
    );
  }
  return { ...options, gridOptions };
}

/**
 * 검색 폼의 **조회 · 초기화**를 아이콘만 남긴 동그란 단추로 바꾼다.
 *
 * 그리드 위쪽 조작 장치는 전부 아이콘으로 통일돼 있는데(도구줄 · `GridIconButton`),
 * 검색 폼 오른쪽 끝의 이 둘만 글자 단추로 남아 줄이 어긋나 보였다.
 *
 * **화면을 고치지 않는다.** 이 둘은 `formOptions` 로만 조절할 수 있고, 그리드를 만드는
 * 길은 이 함수 하나뿐이라 여기서 바탕값을 깔면 전 화면에 한 번에 걸린다.
 * 화면이 일부러 적어 둔 값은 **덮지 않는다**(뒤에 펼친다).
 *
 * `content` 를 비우고 `icon` 을 주는 방식이다 — 이 단추는 antd `Button` 이라
 * (`adapter/component` 의 `PrimaryButton` · `DefaultButton`) `shape` · `icon` 을 받는다.
 * 아래 도구줄처럼 vxe 단추 모양으로 만들 수는 없다. 그 자리는 vben 폼이 그리고,
 * 부품을 갈아끼우려면 상위와 맞춰 둔 `fronts/packages` 를 건드려야 한다.
 *
 * 접기 화살표의 '접기 · 펼치기' 글자는 CSS 로 감춘다(`styles/index.css`) —
 * 그것은 `formOptions` 로 닿지 않는 자리다. 화살표가 도는 것으로 상태를 보인다.
 */
function iconizeSearchFormActions(options: any) {
  const formOptions = options?.formOptions;
  if (!formOptions) return options;

  return {
    ...options,
    formOptions: {
      ...formOptions,
      // 접기 화살표의 글자를 감출 표시. 화면이 준 클래스는 살린다.
      actionWrapperClass: [formOptions.actionWrapperClass, 'jsini-form-actions']
        .filter(Boolean)
        .join(' '),
      resetButtonOptions: {
        content: '',
        icon: h(IconifyIcon, { class: 'size-4', icon: 'lucide:rotate-ccw' }),
        shape: 'circle',
        title: $t('common.reset'),
        ...formOptions.resetButtonOptions,
      },
      submitButtonOptions: {
        content: '',
        icon: h(IconifyIcon, { class: 'size-4', icon: 'lucide:search' }),
        shape: 'circle',
        title: $t('common.query'),
        ...formOptions.submitButtonOptions,
      },
    },
  };
}

/**
 * 화면들이 쓰는 것은 전부 이 함수다 — 예외가 하나도 없다.
 * 그래서 그리드 공통 기능은 화면을 고치지 않고 여기서 건다.
 *
 *   1) `adjustGridForMobile` — 모바일 보정 (40번 문서)
 *   2) `iconizeSearchFormActions` — 검색 폼의 조회 · 초기화를 아이콘으로
 *   3) `createGridFeatures`  — 정렬 · 필터 전용 행 (vxe-grid-features.ts)
 *
 * 필터줄은 그려질 때 **그리드 인스턴스**가 있어야 `setFilter` 를 부를 수 있는데,
 * 컬럼을 손보는 시점에는 아직 그리드가 없다. 그래서 인스턴스를 꺼내는 함수를
 * 넘기고, 만들어진 뒤에 `holder` 에 담아 준다.
 */
export const useVbenVxeGrid = <T extends Record<string, any>>(
  ...rest: Parameters<typeof useGrid<T, ComponentType>>
) => {
  const [options, ...others] = rest;
  const holder: { api?: any } = {};

  const {
    decorateColumns,
    filtersVisible,
    options: prepared,
    renderTools,
  } = createGridFeatures(
    iconizeSearchFormActions(adjustGridForMobile(options)),
    () => holder.api?.grid,
    () => holder.api,
  );

  const result = useGrid<T, ComponentType>(prepared, ...(others as any[]));
  const api = result[1] as any;
  holder.api = api;

  // 컬럼을 나중에 갈아끼우는 화면이 있다. 그 경로로 들어온 컬럼도 손질해야
  // 필터 칸이 사라지지 않는다.
  //   · `setGridOptions` · `setLoading` 등은 전부 `setState` 를 거친다
  //   · 역할-메뉴 탭은 vxe 인스턴스의 `loadColumn` 을 직접 부른다
  const setState = api.setState?.bind(api);
  if (setState) {
    api.setState = (stateOrFn: any) => {
      if (typeof stateOrFn === 'function') {
        return setState((prev: any) => withDecoratedColumns(stateOrFn(prev)));
      }
      return setState(withDecoratedColumns(stateOrFn));
    };
  }

  function withDecoratedColumns(next: any) {
    const columns = next?.gridOptions?.columns;
    if (!Array.isArray(columns)) return next;
    return {
      ...next,
      gridOptions: { ...next.gridOptions, columns: decorateColumns(columns) },
    };
  }

  const mount = api.mount?.bind(api);
  if (mount) {
    api.mount = (instance: any, formApi: any) => {
      patchLoadColumn(instance, decorateColumns);
      return mount(instance, formApi);
    };
  }

  if (!renderTools) return result;

  /**
   * 도구줄을 심기 위해 그리드를 한 겹 감싼다.
   *
   * **DOM 을 더 만들지 않는다.** `inheritAttrs: false` 로 받은 속성을 그대로
   * 안쪽 그리드에 넘기므로 `class="h-full flex-1"` 같은 것이 예전과 같은 자리에
   * 붙는다 — 감싸는 `<div>` 를 두면 화면 70곳의 높이 계산이 어긋난다.
   *
   * 화면이 준 슬롯은 그대로 통과시키고, 도구줄만 두 자리에 더한다.
   * `bottom` 은 화면이 이미 쓰고 있을 수 있으므로 **덮지 않고 뒤에 붙인다.**
   */
  const RawGrid = result[0] as any;
  const Grid = defineComponent({
    inheritAttrs: false,
    name: 'JsiniVxeGrid',
    setup(_props, { attrs, slots }) {
      /**
       * 도구줄을 넣은 슬롯 묶음.
       *
       * **`$stable: true` 가 꼭 필요하다.** 이걸 빼면 Vue 는 이 묶음을
       * '매번 달라지는 슬롯' 으로 보고, 부모가 다시 그려질 때마다 그리드를
       * **통째로 다시 그린다.** 그리드가 다시 그려지면 vxe 가 크기를 다시 재고,
       * 그 결과가 다시 부모를 건드려 몇백 ms 간격으로 화면이 들썩였다
       * (역할 관리 · 회사 사용자 관리에서 실제로 나온 증상).
       *
       * 슬롯 함수는 렌더마다 새로 만들어지지만 하는 일이 같으므로,
       * 자식을 강제로 갱신할 이유가 없다.
       */
      const children: Record<string, any> = {
        $stable: true,
        // 도구줄이 **먼저**다 — 공통 아이콘은 왼쪽 끝에 선다.
        // 화면이 넣은 `bottom` 내용은 그 오른쪽에 이어 붙는다.
        //
        // 페이저가 있는 그리드용 도구줄은 여기가 아니라 `pagerConfig.slots.left`
        // 가 그린다(vxe-grid-features.ts). 둘 중 보이는 것은 하나다.
        bottom: (params: any) =>
          [renderTools('bottom'), slots.bottom?.(params)].filter(Boolean),
      };

      /**
       * 필터줄이 접혀 있으면 표시를 하나 붙인다 — 감추는 일은 CSS 가 한다
       * (`styles/index.css` 의 `.jsini-nofilter`).
       *
       * `gridClass` 로 넘기는 이유: 그것이 vxe 그리드 뿌리에 그대로 붙는
       * 유일한 통로다(`use-vxe-grid.vue` 의 `:class`). 화면이 이미 준 값은
       * 배열로 함께 넘겨 살린다.
       *
       * 이 렌더가 `filtersVisible` 을 읽으므로, 도구줄에서 접고 펼 때
       * 감싸개가 다시 그려지며 표시가 붙었다 떨어진다.
       */
      return () =>
        h(
          RawGrid,
          {
            ...attrs,
            gridClass: [
              (attrs as any).gridClass,
              filtersVisible?.value === false ? FILTER_HIDDEN_CLASS : '',
            ],
          },
          { ...slots, ...children },
        );
    },
  });

  return [Grid, api] as unknown as typeof result;
};

/**
 * vxe 인스턴스의 컬럼 주입 함수를 감싼다.
 *
 * `gridApi.setGridOptions` 를 거치지 않고 vxe 를 직접 부르는 화면이 있어서,
 * 거기로 들어온 컬럼에도 같은 손질을 해 준다. 한 인스턴스를 두 번 감싸지 않도록
 * 표시를 남긴다(그리드는 다시 mount 될 수 있다).
 */
function patchLoadColumn(instance: any, decorateColumns: (c: any) => any) {
  if (!instance || instance.__jsiniColumnsPatched) return;
  instance.__jsiniColumnsPatched = true;

  for (const name of ['loadColumn', 'reloadColumn']) {
    const original = instance[name];
    if (typeof original !== 'function') continue;
    instance[name] = (columns: any, ...args: any[]) =>
      original.call(instance, decorateColumns(columns), ...args);
  }
}

export type OnActionClickParams<T = Recordable<any>> = {
  code: string;
  row: T;
};
export type OnActionClickFn<T = Recordable<any>> = (
  params: OnActionClickParams<T>,
) => void;
export type * from '@vben/plugins/vxe-table';
