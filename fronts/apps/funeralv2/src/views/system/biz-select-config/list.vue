<script lang="ts" setup>
import type { OnActionClickParams, VxeTableGridOptions } from '#/adapter/vxe-table';
import type { BizSelectConfigApi } from '#/api/system/biz-select-config';

import { Page, useVbenDrawer } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { deleteBizSelectConfig, getBizSelectConfigs } from '#/api/system/biz-select-config';
import { useBizSelectStore } from '#/store/biz-select-config';

import { useColumns } from './data';
import Form from './modules/form.vue';

/**
 * [BizSelect 메타데이터 설정 관리 - 목록 화면]
 */

const bizSelectStore = useBizSelectStore();

const [FormDrawer, formDrawerApi] = useVbenDrawer({ connectedComponent: Form, destroyOnClose: true });

function onEdit(row: BizSelectConfigApi.BizSelectConfig) { 
  formDrawerApi.setData(row).open(); 
}

function onCreate() { 
  formDrawerApi.setData({}).open(); 
}

function onDelete(row: BizSelectConfigApi.BizSelectConfig) {
  deleteBizSelectConfig(row.id)
    .then(async () => {
      message.success(`"${row.bizType}" 설정을 삭제하였습니다.`);
      await bizSelectStore.loadConfigs(true);
      refreshGrid();
    })
    .catch((error) => {
      console.error('[Delete Error]', error);
    });
}

function onActionClick({ code, row }: OnActionClickParams<BizSelectConfigApi.BizSelectConfig>) {
  switch (code) {
    case 'delete': { onDelete(row); break; }
    case 'edit': { onEdit(row); break; }
  }
}

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: useColumns(onActionClick),
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getBizSelectConfigs();
        },
      },
    }
  } as VxeTableGridOptions,
});

function refreshGrid() { gridApi.query(); }
</script>

<template>
  <Page auto-content-height>
    <FormDrawer @success="refreshGrid" />
    <Grid title="BizSelect 설정 관리">
      <template #toolbar-tools>
        <Button type="primary" @click="onCreate">
          <Plus class="size-5" />
          신규 등록
        </Button>
      </template>
    </Grid>
  </Page>
</template>
