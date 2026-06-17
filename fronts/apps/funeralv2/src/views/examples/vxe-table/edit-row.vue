<script lang="ts" setup>
import type { VxeGridProps } from '#/adapter/vxe-table';

import { Page } from '@vben/common-ui';

import { Button, message } from 'ant-design-vue';

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
    { slots: { default: 'action' }, title: '관리' },
  ],
  editConfig: {
    mode: 'row',
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

const [Grid, gridApi] = useVbenVxeGrid({ gridOptions });

function hasEditStatus(row: RowType) {
  return gridApi.grid?.isEditByRow(row);
}

function editRowEvent(row: RowType) {
  gridApi.grid?.setEditRow(row);
}

async function saveRowEvent(row: RowType) {
  await gridApi.grid?.clearEdit();

  gridApi.setLoading(true);
  setTimeout(() => {
    gridApi.setLoading(false);
    message.success({
      content: `저장에 성공했습니다! category=${row.category}`,
    });
  }, 600);
}

const cancelRowEvent = (_row: RowType) => {
  gridApi.grid?.clearEdit();
};
</script>

<template>
  <Page auto-content-height>
    <Grid>
      <template #action="{ row }">
        <template v-if="hasEditStatus(row)">
          <Button type="link" @click="saveRowEvent(row)">저장</Button>
          <Button type="link" @click="cancelRowEvent(row)">취소</Button>
        </template>
        <template v-else>
          <Button type="link" @click="editRowEvent(row)">편집</Button>
        </template>
      </template>
    </Grid>
  </Page>
</template>
