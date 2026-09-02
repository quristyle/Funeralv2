<script lang="ts" setup>
import { Page } from '@vben/common-ui';
import { Button, message } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import GridIconButton from '#/components/GridIconButton.vue';
import { getArchiveItems, downloadArchiveFile } from '#/api/funeral/help';

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'title', title: '자료명', minWidth: 200 },
      {
        field: 'fileName',
        title: '첨부파일명',
        minWidth: 180,
        slots: { default: 'file-link' }
      },
      {
        field: 'fileSize',
        title: '파일 크기',
        width: 120,
        align: 'right',
        formatter: ({ cellValue }: { cellValue: any }) => {
          if (!cellValue) return '-';
          const sizeKb = Number(cellValue) / 1024;
          if (sizeKb > 1024) {
            return `${(sizeKb / 1024).toFixed(2)} MB`;
          }
          return `${sizeKb.toFixed(0)} KB`;
        }
      },
      { field: 'downloadCount', title: '다운로드 수', width: 120, align: 'right' },
      { field: 'createdAt', title: '등록일자', width: 160 },
      {
        field: 'action',
        title: '다운로드',
        width: 150,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getArchiveItems();
        },
      },
    },
  },
});

async function handleDownload(row: any) {
  try {
    message.loading({ content: `${row.fileName} 파일 요청 중...`, key: 'download' });
    await downloadArchiveFile(row.id);
    setTimeout(() => {
      message.success({ content: '다운로드가 완료되었습니다.', key: 'download', duration: 2 });
      gridApi.query(); // 다운로드 수 갱신을 위해 그리드 리로드
    }, 1000);
  } catch (error) {
    message.error('다운로드 요청 실패');
  }
}
</script>

<template>
  <Page auto-content-height>
    <div class="mb-4 bg-card p-4 rounded border flex justify-between items-center">
      <span class="font-semibold text-sm">📁 관내 공용 배포 문서 및 드라이버 자료실</span>
      <GridIconButton
        icon="vxe-icon-repeat"
        title="새로고침"
        @click="gridApi.query()"
      />
    </div>

    <Grid table-title="자료실 및 다운로드 대장 목록">
      <template #file-link="{ row }">
        <span class="text-primary hover:underline cursor-pointer" @click="handleDownload(row)">
          💾 {{ row.fileName }}
        </span>
      </template>

      <template #action="{ row }">
        <Button type="link" size="small" @click="handleDownload(row)">다운로드</Button>
      </template>
    </Grid>
  </Page>
</template>
