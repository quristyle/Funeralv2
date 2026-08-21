<script lang="ts" setup>
import type { VxeGridListeners, VxeGridProps } from '#/adapter/vxe-table';

import { Page } from '@vben/common-ui';

import { Button, message } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';

import DocButton from '../doc-button.vue';
import { MOCK_TABLE_DATA } from './table-data';

interface RowType {
  address: string;
  age: number;
  id: number;
  name: string;
  nickname: string;
  role: string;
}

const gridOptions: VxeGridProps<RowType> = {
  columns: [
    { title: '번호', type: 'seq', width: 50 },
    { field: 'name', title: 'Name' },
    { field: 'age', sortable: true, title: 'Age' },
    { field: 'nickname', title: 'Nickname' },
    { field: 'role', title: 'Role' },
    { field: 'address', showOverflow: true, title: 'Address' },
  ],
  data: MOCK_TABLE_DATA,
  pagerConfig: {
    enabled: false,
  },
  sortConfig: {
    multiple: true,
  },
};

const gridEvents: VxeGridListeners<RowType> = {
  cellClick: ({ row }) => {
    message.info(`cell-click: ${row.name}`);
  },
};

const [Grid, gridApi] = useVbenVxeGrid<RowType>({
  // 폼 컴포넌트의 타입을 확인하려면 주석을 해제하세요.
  // formOptions: {
  //   schema: [
  //     {
  //       component: 'Switch',
  //       fieldName: 'name',
  //     },
  //   ],
  // },
  gridEvents,
  gridOptions,
});

// 현재 테이블 인스턴스의 타입을 확인하려면 주석을 해제하세요.
// gridApi.grid

const showBorder = gridApi.useStore((state) => state.gridOptions?.border);
const showStripe = gridApi.useStore((state) => state.gridOptions?.stripe);

function changeBorder() {
  gridApi.setGridOptions({
    border: !showBorder.value,
  });
}

function changeStripe() {
  gridApi.setGridOptions({
    stripe: !showStripe.value,
  });
}

function changeLoading() {
  gridApi.setLoading(true);
  setTimeout(() => {
    gridApi.setLoading(false);
  }, 2000);
}
</script>

<template>
  <Page
    description="테이블 컴포넌트는 데이터 표시 및 상호 작용 인터페이스를 신속하게 개발하는 데 자주 사용되며, 예시 데이터는 정적 데이터입니다. 이 컴포넌트는 vxe-table을 간단하게 2차 캡슐화한 것으로, 대부분의 속성과 메서드가 vxe-table과 일치합니다."
    title="테이블 기본 예시"
  >
    <template #extra>
      <DocButton path="/components/common-ui/vben-vxe-table" />
    </template>
    <Grid table-title="기본 목록" table-title-help="도움말">
      <!-- <template #toolbar-actions>
        <Button class="mr-2" type="primary">왼쪽 슬롯</Button>
      </template> -->
      <template #toolbar-tools>
        <Button class="mr-2" type="primary" @click="changeBorder">
          테두리 {{ showBorder ? '숨기기' : '표시' }}
        </Button>
        <Button class="mr-2" type="primary" @click="changeLoading">
          로딩 표시
        </Button>
        <Button type="primary" @click="changeStripe">
          스트라이프 {{ showStripe ? '숨기기' : '표시' }}
        </Button>
      </template>
    </Grid>
  </Page>
</template>
