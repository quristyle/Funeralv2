<script lang="ts" setup>
import type { VxeGridProps } from '#/adapter/vxe-table';

import { Page } from '@vben/common-ui';

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
    { title: '일련번호', type: 'seq', width: 50 },
    { editRender: { name: 'input' }, field: 'category', title: '카테고리' },
    { editRender: { name: 'input' }, field: 'color', title: '색상' },
    {
      editRender: { name: 'input' },
      field: 'productName',
      title: '상품명',
    },
    { field: 'price', title: '가격' },
    { field: 'releaseDate', formatter: 'formatDateTime', title: '날짜' },
  ],
  editConfig: {
    mode: 'cell',
    trigger: 'click',
  },
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
  showOverflow: true,
};

const [Grid] = useVbenVxeGrid({ gridOptions });
</script>

<template>
  <Page auto-content-height>
    <Grid />
  </Page>
</template>
