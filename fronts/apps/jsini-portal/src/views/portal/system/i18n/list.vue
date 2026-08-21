<script lang="ts" setup>
import type { OnActionClickParams, VxeTableGridOptions, } from '#/adapter/vxe-table';
import type { VxeGridDefines } from 'vxe-table';

import { Page, useVbenDrawer } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { $t } from '@vben/locales';
import { Button, message, Modal } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { deleteI18nResource, updateI18nResource, getAllI18nList, getI18nPaged, type SystemI18nApi } from '#/api/system/i18n';

import { useColumns } from './data';
import Form from './modules/form.vue';

const [FormDrawer, formDrawerApi] = useVbenDrawer({
  connectedComponent: Form,
  destroyOnClose: true,
});

const [Grid, gridApi] = useVbenVxeGrid({
  // 검색 폼 명시적 활성화
  showSearchForm: true,
  formOptions: {
    // 화면 크기에 따른 반응형 그리드 설정 (한 줄에 최대 4개 표시)
    wrapperClass: 'grid-cols-1 md:grid-cols-2 lg:grid-cols-4',
    // 검색 폼 설정
    schema: [
      {
        componentProps: {
          options: [
            { label: $t('ui.i18n.koKR'), value: 'ko-KR' },
            { label: $t('ui.i18n.enUS'), value: 'en-US' },
          ],
          allowClear: true,
        },
        component: 'Select', fieldName: 'locale', label: $t('ui.i18n.locale'),
      },
      { component: 'Input', fieldName: 'category', label: $t('ui.i18n.category'), },
      { component: 'Input', fieldName: 'key', label: $t('ui.i18n.key'), },
      { component: 'Input', fieldName: 'value', label: $t('ui.i18n.value'), },
    ],
    // 검색 버튼 클릭 시 실행
    submitButtonOptions: { text: $t('common.query'), },
    // 초기화 버튼 클릭 시 실행
    resetButtonOptions: { text: $t('common.reset'), },
  },
  gridOptions: {
    columns: useColumns(onActionClick),
    height: 'auto',
    keepSource: true,
    pagerConfig: { enabled: true },
    proxyConfig: {
      ajax: {
        query: async ({ page }, formValues) => {
          const params = {
            page: page.currentPage || 1,
            pageSize: page.pageSize || 20,
            ...formValues,
          };

          try {
            return await getI18nPaged(params);
          } catch (error) {
            console.error('I18n fetch error:', error);
            return { items: [], total: 0 };
          }
        },
      },
    },
    rowConfig: { keyField: 'id', },
  } as VxeTableGridOptions,
  gridEvents: {
    // 셀 편집 종료 후 이벤트 처리
    editClosed: async ({ row, column, }: VxeGridDefines.EditClosedEventParams<SystemI18nApi.I18nResource>) => {
      if (column.field === 'value') {
        try {
          const updateData = {
            key: row.key,
            locale: row.locale,
            value: row.value,
            category: row.category,
          };
          await updateI18nResource(row.id, updateData);
          message.success($t('ui.actionMessage.operationSuccess'));
        } catch (error) {
          message.error($t('ui.actionMessage.operationFailed'));
          onRefresh();
        }
      }
    },
  },
});

function onActionClick({ code, row }: OnActionClickParams) {
  const item = row as SystemI18nApi.I18nResource;
  switch (code) {
    case 'delete': { onDelete(item); break; }
    case 'edit': { onEdit(item); break; }
  }
}

function onRefresh() { gridApi.reload(); }

function onEdit(row: SystemI18nApi.I18nResource) {
  formDrawerApi.setData(row).open();
}

function onCreate() { formDrawerApi.setData({}).open(); }

function onDelete(row: SystemI18nApi.I18nResource) {
  Modal.confirm({
    title: $t('ui.actionMessage.deleteConfirm', [row.key]),
    onOk: async () => {
      try {
        await deleteI18nResource(row.id);
        message.success($t('ui.actionMessage.deleteSuccess', [row.key]));
        onRefresh();
      } catch (error) {
        console.error(error);
      }
    },
  });
}
</script>

<template>
  <Page auto-content-height>
    <FormDrawer @success="onRefresh" />
    <Grid>
      <template #toolbar-tools>
        <Button type="primary" @click="onCreate">
          <Plus class="size-5" />
          {{ $t('ui.actionTitle.create', ['I18n']) }}
        </Button>
      </template>
    </Grid>
  </Page>
</template>
