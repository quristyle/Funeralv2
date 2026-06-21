<script lang="ts" setup>
import { Page } from '@vben/common-ui';
import { Button, message } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getJqlData } from '#/api/info';

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'code', title: 'JQLME 코드명', minWidth: 150 },
      { field: 'value', title: '송출 설정 값', minWidth: 200 },
      { field: 'description', title: '상세 매뉴얼 설명', minWidth: 250 },
      { field: 'updatedAt', title: '최종 동기화 시각', minWidth: 160 },
      {
        field: 'action',
        title: '동작',
        width: 150,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getJqlData('ME');
        },
      },
    },
  },
});

function handleTestSend(row: any) {
  message.success(`${row.code} 코드로 프로토콜 데이터 테스트 송신 성공`);
}
</script>

<template>
  <Page auto-content-height>
    <div class="mb-4 bg-card p-4 rounded border flex justify-between items-center">
      <div class="text-sm font-semibold">JQLME 프로토콜 기기 연동 데이터 관리 센터</div>
      <Button type="primary" @click="gridApi.query()">전체 코드 동기화</Button>
    </div>
    
    <Grid table-title="JQLME 연동 상태 및 파라미터 매핑 목록">
      <template #action="{ row }">
        <Button type="link" size="small" @click="handleTestSend(row)">테스트 송신</Button>
      </template>
    </Grid>
  </Page>
</template>
