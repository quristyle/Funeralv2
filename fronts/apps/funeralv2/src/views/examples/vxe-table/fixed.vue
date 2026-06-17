<script lang="ts" setup>
import type { VxeGridProps } from '#/adapter/vxe-table';

import { Page } from '@vben/common-ui';

import { Button } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getExampleTableApi } from '#/api';

interface RowType {
  category: string;
  color: string;
  id: string;
  price: string;
  productName: string;
  releaseDate: string;
}

const gridOptions: VxeGridProps<RowType> = {
  columns: [
    { fixed: 'left', title: '일련번호', type: 'seq', width: 50 },
    { field: 'category', title: '카테고리', width: 300 },
    { field: 'color', title: '색상', width: 300 },
    { field: 'productName', title: '상품명', width: 300 },
    { field: 'price', title: '가격', width: 300 },
    {
      field: 'releaseDate',
      formatter: 'formatDateTime',
      title: '날짜 시간',
      width: 500,
    },
    {
      field: 'action',
      fixed: 'right',
      slots: { default: 'action' },
      title: '관리',
      width: 120,
    },
  ],
  height: 'auto',
  pagerConfig: {},
  proxyConfig: {
    ajax: {
      query: async ({ page }) => {
        return await getExampleTableApi({
          page: page.currentPage,
          pageSize: page.pageSize,
        });
      },
    },
  },
  rowConfig: {
    isHover: true,
  },
};

const [Grid] = useVbenVxeGrid({ gridOptions });
</script>

<template>
  <Page auto-content-height>
    <Grid>
      <template #action>
        <Button type="link">편집</Button>
      </template>
    </Grid>
  </Page>
</template>
